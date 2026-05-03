# CompositionCollectionView — Design Notes

A running record of the architectural choices in this fork (and the original
upstream library), why they exist, and what they enable. New entries go at the
top. Use this when adding a feature: if the feature crosses one of the
boundaries below, link or amend the relevant entry.

---

## 4. UI-thread `Evaluate()` liveness for animated nodes (2026-04)

### Problem

`ScalarNode.Evaluate()` (and the other typed `Evaluate()` methods) walks the
expression tree on the UI thread. When the tree contains a leaf that points at
a composition property being driven by a running animation — most commonly a
`Visual.Offset.X` cell driven by a `ScalarKeyFrameAnimation`, or any
`InteractionTracker` channel — the leaf reads the *managed* property getter,
which returns whatever the UI thread last wrote. Composition does not flow
animated values back to that getter, so the read is stale (typically `0`).

The class library sits on top of this: `AnimatableScalarCompositionNode.Reference`
returns a `ScalarNode` rooted at `Visual.Offset.X`, and downstream consumers
embed it in trees they expect to evaluate. The staleness silently corrupts any
UI-thread snapshot of the rendered value (painter-sort, hit-test, custom
projection, etc.).

### Solution: `LiveValueProvider` escape hatch on every typed node

Each typed node (`ScalarNode`, `Vector2Node`, `Vector3Node`, `Vector4Node`,
`QuaternionNode`, `ColorNode`, `Matrix4x4Node`) carries an optional
`internal Func<T>? LiveValueProvider` property. `Evaluate()` checks it first
and short-circuits before the normal `NodeType` switch. `ToExpressionString()`
ignores it entirely, so composition-side codegen (and therefore GPU rendering)
is unchanged.

The provider is `internal` because the only legitimate setters are sibling
classes in the assembly: the `AnimatableXxxCompositionNode` family attaches
`() => this.Value` to the node returned by `Reference`, and the
`TrackedInteractionNodes` cache attaches its closure to nodes it produces.

### Why on the leaf node, not the wrapper

`Evaluate()` is recursive on the tree, not on the wrapper that built it. Once a
leaf ends up inside `wobble + (spin - wobble) * mode`, the only knowledge it
carries is its own `NodeType`. The intercept must live on the leaf itself.

### How `AnimatableXxxCompositionNode` uses it

Three additions to each wrapper:

1. New private `Func<T>? _liveValueProvider` field, mutually exclusive with the
   pre-existing `_currentAnimationNode` and the `ComposerValue` fallback.
2. `Value` getter consults the live provider first, then the expression node,
   then `ComposerValue`. All entry points that swap the animation null both
   fields cleanly.
3. New `Animate(CompositionAnimation, Func<T> liveValueProvider)` overload —
   for `ScalarKeyFrameAnimation` and other opaque composition animations whose
   math the caller can mirror in C#. The closure is invoked on demand by `Value`
   (no per-frame ticking).
4. `Reference` now attaches `() => this.Value` to the produced leaf node. The
   indirection through `this.Value` (rather than capturing the closure
   directly) ensures references stay correct across subsequent `Animate` calls
   that swap the underlying source.

### How `TrackedInteractionNodes` uses it

`InteractionTracker` doesn't expose any UI-thread "live current value" API, but
it *does* fire `IInteractionTrackerOwner.ValuesChanged` once per input frame
on the UI thread. `TrackedInteractionNodes` caches the latest `Position`,
`Scale`, and limit values in the handler and exposes `PositionReference()` /
`ScalarReference(channel)` factories that produce nodes wired to the cache.

This is the same shape as the KFA case — the only difference is *where the
freshness comes from*. The KFA path manufactures freshness via a stopwatch +
math closure; the tracker path borrows it from the system's existing event.
The intercept in `Evaluate()` is identical.

### What this still does NOT solve

* **Trackers/animations driven by code we don't own** (e.g. a `ScrollViewer`'s
  internal interaction tracker, XAML implicit animations, `ConnectedAnimation`).
  We can't subscribe to events we weren't given access to. Truly fixing this
  would require Composition to expose `GetCurrentAnimatedValue` from the OS.
* **KFA replay drift** — `KfaReplay`-style closures must mirror the same
  easing the GPU is using. If the easing isn't modelled in C# (e.g. `Step`,
  `Back`, `Elastic`), the closure value diverges from the rendered value. Add
  a paired implementation when the case arises.
* **Closure must be alive** — `TrackedInteractionNodes` holds the cache. If
  the wrapper is GC'd while reference nodes it produced are still held by an
  expression tree, the closure will throw on `Evaluate`. Consumers must keep
  the wrapper alive for at least the lifetime of the references.

### Files touched

* `ExpressionsFork/Expressions/ExpressionNodes/{Scalar,Vector2,Vector3,Vector4,Quaternion,Color,Matrix4x4}Node.cs` —
  `LiveValueProvider` field + `Evaluate()` intercept.
* `AnimatableNodes/AnimatableScalarCompositionNode.cs` — new field, three-tier
  `Value`, new `Animate(animation, liveValueProvider)` overload, `Reference`
  attaches provider.
* `AnimatableNodes/AnimatableVector3CompositionNode.cs` — same.
* `AnimatableNodes/AnimatableQuaternionCompositionNode.cs` — same.
* `AnimatableNodes/AnimatableMatrix4x4CompositionNode.cs` — same.
* `AnimatableNodes/TrackedInteractionNodes.cs` — new file. Cache + reference
  factories for `InteractionTracker`.

### When to extend this further

* **New composition value source** (e.g. you write a custom
  `CompositionObject`-backed cell): produce a wrapper that exposes a
  `Reference` getter and attaches a provider that returns the UI-thread-known
  value. No fork-side change required — `LiveValueProvider` is already on
  every node type.
* **New tracker channel** (e.g. you want `ScaleVelocity`): add an entry to
  `TrackerScalarChannel`, a case in `ScalarReference`, a corresponding cache
  field, and refresh it in `OnValuesChanged`. ~6 lines.
* **Vector2 / Vector4 / Color storage cells**: `Visual` itself has no
  `Vector4`/`Color` properties suitable as cells. Use a
  `CompositionPropertySet` instead — same pattern, the property-set reference
  produces a node and you attach the provider yourself.

---

## 3. WinAppSdk fork project (`src/CompositionCollectionView.WinAppSdk`) (pre-existing)

### Why it exists

The original library targets UWP (`uap10.0.19041`); the
`CompositionCollectionView.WinAppSdk` wrapper csproj re-targets the same source
files at `net9.0/net10.0-windows10.0.19041.0` so they consume `Microsoft.UI.*`
(WinAppSdk) instead of `Windows.UI.*` (UWP). It uses MSBuild `<Compile Include>`
with `Link=` to share the source rather than duplicating it.

### Why a separate project, not multi-targeting

Multi-targeting in a single csproj causes conflicts in the XAML compiler
(`Themes/Generic.xaml` and `Page` items try to compile twice with different
toolchains) and breaks the WCT package's PriGen step on certain SDK versions.
A second csproj sidesteps both by giving each target its own obj/output tree.

### Versioning

The WinAppSdk project's `Microsoft.WindowsAppSDK` version must match (or be
ahead of) the consuming app's version. Earlier releases were on 1.7.x; this is
now 1.8.260317003 because 1.7's `MrtCore.PriGen.targets` fails on .NET SDK
10.0.201 with MSB4062.

---

## 2. ExpressionsFork (`src/CompositionCollectionView/ExpressionsFork`) (pre-existing)

### Why we have it

The Windows Community Toolkit's `ExpressionBuilder`
(`Microsoft.Toolkit.Uwp.UI.Animations.Expressions`) gives you a strongly-typed
node API — `ScalarNode`, `Vector3Node`, `ExpressionFunctions.Vector3(...)`,
operator overloads — that compiles down to composition expression strings via
`ToExpressionString()` / `GetValue()`. Composition then runs the expression on
the GPU. The whole pipeline is one-way: C# describes intent, the compositor
executes, no value ever flows back.

CCV needs the values to flow back. Layouts compute element transforms as node
trees (`GetElementPositionNode` returns a `Vector3Node`), but several internal
operations — `TransitionTo` snapshotting current element positions for the
crossfade lerp, hit-testing, painter-sort for 3D layouts, any C#-side mirror
of an animated value — need to ask "what does this expression evaluate to
*right now*?" The upstream library has no answer to that question; every
typed node only knows how to emit a composition string.

So the original reason for the fork was to add a typed `Evaluate()` method to
every node — a pure-managed walk of the same tree the compositor consumes,
producing the equivalent value on the UI thread. With `Evaluate()` in place,
layout authors can use the *same* node tree on both sides of the boundary:
hand it to composition for rendering, hand it to C# for snapshotting. The rest
of CCV (most notably `GetElementPositionValue` overrides, `AnimatableXxxCompositionNode.Value`,
the transition machinery) all rests on that capability.

### Why a fork rather than a NuGet dep

Two reasons:

1. **`Evaluate()` is a structural extension.** It needs a typed return on
   every node, a recursive walk that mirrors `ToExpressionString()`, and
   internal hooks into how `ReferenceProperty` reads back values from
   `Visual` / `CompositionPropertySet` / `InteractionTracker`. That can't be
   layered as extension methods on a sealed type tree — it has to live in the
   same assembly.
2. **Subsequent patches.** The `LiveValueProvider` mechanism (entry 4) extends
   `Evaluate()` semantics on every typed node. Maintaining that as a downstream
   patch on a NuGet would be impossible; vendoring keeps it in tree.

### Namespace deviation

Files use the namespace `Microsoft.Toolkit.Uwp.UI.Animations.ExpressionsFork`
(note the `.ExpressionsFork` suffix) so they can't collide with the original
`Microsoft.Toolkit.Uwp.UI.Animations.Expressions` if a consumer somehow ends up
with both. Otherwise the public API surface is a strict superset of upstream —
operator overloads, function library, reference node hierarchy are all
identical, so porting a Toolkit sample only requires changing the `using`
line.

### What stays sync'd vs. what intentionally diverges

* **Sync'd**: type signatures, operator overloads, function library
  (`ExpressionFunctions`), reference node hierarchy. Anything pulled from
  upstream samples should "just work."
* **Diverges** (additions, not changes): every typed node has `Evaluate()` and
  `LiveValueProvider`; some `internal` setters exist that aren't in the
  upstream surface. Composition codegen output is byte-identical to upstream.

---

## 1. Composition-driven layout (the whole point) (pre-existing)

CCV exists because `ItemsControl` / virtualizing panels can't smoothly animate
element position/scale/opacity per-frame at 60fps without re-laying-out and
re-measuring on every tick. Composition expression animations run on the
compositor thread independent of layout, so an element's position is *defined*
as `f(time, layout state)` and the GPU evaluates it. The library's job is to
give layout authors a typed surface for writing those expressions
(`GetElementPositionNode` etc.) and a behavior model for orchestrating
transitions, while keeping element identity stable across `UpdateSource` diffs.

Key consequences that ripple through the rest of the design:

* Layouts return `Vector3Node` / `ScalarNode`, not `Vector3` / `float`.
* Per-element state lives in `ElementReference.AnimatableNodes` (a per-element
  registry of `AnimatableScalarCompositionNode`s), not in C# fields, so
  expressions can reference it.
* `UpdateSource` is a diff (not a re-bind), so survivors keep their nodes.
* `TransitionTo` interpolates between two layouts' position/scale/opacity
  expressions on a shared blend scalar — managed code only kicks off the
  blend, the compositor does the lerp.

This is also why entry 4 (`LiveValueProvider`) matters: the moment you want to
*read* one of these animated values back on the UI thread (for hit-testing,
painter-sort, snapshot transitions), you've stepped outside the composition
fast path and need a way to learn the live value without a round-trip.
