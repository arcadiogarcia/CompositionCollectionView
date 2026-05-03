# A guided tour of CompositionCollectionView

**Audience.** You already know `Windows.UI.Composition` (or
`Microsoft.UI.Composition` — same model, different namespace): visuals,
property sets, expression animations, key-frame animations, interaction
trackers. You've written `CreateExpressionAnimation("p.Offset.X * 2")` by
hand. You know that animations run on the compositor thread and that reading
animated property getters from the UI thread returns stale values.

This doc walks the layered design that produces a smooth,
gesture-aware, transition-animating collection view on top of that base.
Each layer adds *exactly one* capability the previous layer was missing,
and we explain the missing capability before the layer that fills it.

---

## Layer 0 — what inbox composition gives you, and what it doesn't

You can already do all of this with the inbox API:

* Create a `Visual`, set its `Offset` / `Scale` / `Opacity` /
  `RotationAngle` / `Orientation` from C#.
* Drive any of those with a `KeyFrameAnimation` for time-based motion.
* Drive any of those with an `ExpressionAnimation` whose string body
  references other composition objects via `SetReferenceParameter`. The
  GPU evaluates the expression every frame, so values are always live on
  the rendering side.
* Wire an `InteractionTracker` to a `VisualInteractionSource` for
  inertia-aware gesture input, and reference its `Position` / `Scale`
  inside expressions so visuals respond directly to fingers without a
  managed thread tick.

What's missing — and what every subsequent layer in CCV addresses — falls
into three buckets:

1. **No type safety in expressions.** `"p.Offset.X * 2"` is a string. Typos
   are runtime errors. Swizzling a `Vector3` to a `Vector2` doesn't fail
   at compile time. There's no IntelliSense over the function library
   (`Lerp`, `Slerp`, `Clamp`, `Matrix4x4FromTranslation`, …).
2. **No way to read live values back on the UI thread.** Composition is
   one-way: C# describes intent, GPU executes. The managed property
   getters return whatever the UI thread last wrote, not the animated
   value. There is no `Visual.GetCurrentAnimatedOffset()` API.
3. **No collection abstraction.** If you want N visuals laid out in a
   responsive grid, animating between layouts, with per-item gestures,
   element identity preserved across data updates, you build all of it
   yourself.

CCV's source tree is layered to address those three in order.

---

## Layer 1 — Toolkit `ExpressionBuilder` (addresses bucket 1)

The Windows Community Toolkit's `Microsoft.Toolkit.Uwp.UI.Animations.Expressions`
namespace gives you a typed expression node API:

```csharp
ScalarNode  x      = visual.GetReference().Offset.X;
Vector3Node center = ExpressionFunctions.Vector3(0, 0, 0);
Vector3Node spin   = ExpressionFunctions.Vector3(0, x * 360, 0);
visual2.StartAnimation("RotationAxis", spin);
```

What this layer adds over raw composition:

* **Sealed typed nodes** for every composition value type: `ScalarNode`,
  `Vector2Node`, `Vector3Node`, `Vector4Node`, `QuaternionNode`,
  `ColorNode`, `Matrix3x2Node`, `Matrix4x4Node`, `BooleanNode`. Each
  carries a `NodeType` enum tag and a `Children` array, so the structure
  is a compile-checked AST.
* **Operator overloads** so `a + b * c` builds an AST node instead of a
  string fragment. Cross-type operators (`scalar * vector3` →
  `Vector3Node`) are pre-declared, so the type system catches dimension
  mismatches.
* **`ExpressionFunctions`** — typed wrappers for the entire composition
  function library (`Lerp`, `Clamp`, `Slerp`, `Matrix4x4CreateFromAxisAngle`,
  trigonometry, …).
* **`ReferenceNode`** subclasses for every composition object you can
  reference: `VisualReferenceNode`, `PropertySetReferenceNode`,
  `InteractionTrackerReferenceNode`, light/brush nodes, the
  manipulation/pointer-position stand-ins.
* **`CompositionExtensions.GetReference()`** so any `Visual` /
  `CompositionPropertySet` / `InteractionTracker` becomes a typed
  reference node you can compose with.
* **Codegen via `ToExpressionString()`**. The `StartAnimation` extension
  takes a typed node, walks the AST to produce the underlying expression
  string, and calls `SetReferenceParameter` for every reference baked
  into the tree.

End result: the GPU still receives the same expression string it always
did, but you wrote it in C# with full type checking. Bucket 1 solved.
Bucket 2 (UI-thread readback) is **not** addressed — every `XxxNode` only
knows how to emit a string; nothing in the AST returns a typed value.

---

## Layer 2 — `ExpressionsFork` adds `Evaluate()` (addresses bucket 2, partly)

The fork in `src/CompositionCollectionView/ExpressionsFork/` is
upstream's source vendored verbatim with one structural addition: every
typed node grows an `Evaluate()` method that returns its current value
on the UI thread by walking the AST in pure managed code.

```csharp
public float Evaluate()       // ScalarNode
public Vector3 Evaluate()     // Vector3Node
public Quaternion Evaluate()  // QuaternionNode
…  // every typed node
```

The walk mirrors `ToExpressionString()` exactly — same node types, same
recursion shape — but instead of producing a string, it produces a value:

* `ConstantValue` → return the boxed literal.
* `ConstantParameter` → return the parameter's stored value.
* `ReferenceProperty` → read the property off the underlying `Visual` /
  `PropertySet` / `Tracker` via the standard managed getter.
* `Function` / arithmetic → recurse into children, apply the operation
  in C#.
* `Conditional`, swizzle, matrix construction, all the
  `ExpressionFunctions` helpers → mirrored implementations.

This is the **pure-tree** baseline. It works perfectly for any expression
whose leaves are not currently being animated by the compositor. The
moment a leaf is `visual.Offset.X` and a `ScalarKeyFrameAnimation` is
running on it, the property getter returns the stale value the UI thread
last wrote, and `Evaluate()` returns that stale value too — the walk
faithfully reproduces composition semantics, but composition semantics
are themselves UI-thread-stale.

That's the residual problem layer 4 addresses. First, layer 3 builds
infrastructure that exposes the limitation.

### Why this had to be a fork

`Evaluate()` is structural: it needs a typed return on every sealed
node, an internal recursive walk that shares the AST shape with
`ToExpressionString()`, and direct access to `NodeType` /
`ParamName` / `Children`. None of that is layerable as extension methods
on sealed types in another assembly. The fork has to be in-tree.

The namespace differs from upstream by suffix
(`Microsoft.Toolkit.Uwp.UI.Animations.ExpressionsFork`) so a consumer
can reference both side-by-side without collision. The public API
surface is a strict superset — every upstream sample compiles after a
single `using` change.

---

## Layer 3 — `AnimatableXxxCompositionNode` wrappers

We now have typed expression trees, both `ToExpressionString()` and
`Evaluate()`. To use them as the layout primitive of a collection view,
we need *named animatable cells* — single floats / vectors that:

1. Can be referenced inside expression trees (so a layout's position
   expression can incorporate a per-element entry/hover/press value).
2. Can be animated independently (KFA, expression, or direct write).
3. Can be evaluated on the UI thread when a layout transition needs to
   snapshot "current value" before lerping.

The composition platform has no such primitive — you're expected to make
your own with a `CompositionPropertySet` (typed only by name string) or
a hidden `Visual` whose `Offset.X` doubles as a single-float storage
cell.

CCV picks the hidden-`Visual` route and packages it as
`AnimatableScalarCompositionNode` (and siblings for `Vector3`,
`Quaternion`, `Matrix4x4`):

```csharp
class AnimatableScalarCompositionNode
{
    public float Value { get; set; }                 // direct read/write
    public void  Animate(CompositionAnimation a);    // opaque animation
    public void  Animate(ScalarNode tree);           // expression that's also evaluable
    public ScalarNode Reference { get; }             // for embedding in expressions
}
```

What this gives you over a raw `CompositionPropertySet`:

* **Strongly typed.** No string property names; the wrapper *is* the
  cell.
* **Two-way `Value`.** The setter writes the underlying visual; the
  getter has a fallback chain (described in layer 4) that tries to
  return the live value rather than the stale one.
* **Evaluable when animated by an expression.** If you call
  `Animate(ScalarNode tree)`, the wrapper remembers the tree and
  `Value` returns `tree.Evaluate()` — bypassing the stale property read
  for the common case of "animation is itself a typed expression."
* **`AnimatableCompositionNodeSet`** indexed by string name, attached
  per-element, so a layout can look up `element.AnimatableNodes
  .GetOrCreateScalarNode("hover", 0)` and either drive it or reference
  it in expressions.

Crucial gap: `Animate(CompositionAnimation)` for a `ScalarKeyFrameAnimation`
has no recoverable structure — there's no `ScalarNode` tree to evaluate
— so `Value` falls through to reading the underlying `Visual.Offset.X`
managed getter, which returns a stale value the moment the KFA starts
ticking. That's bucket 2 of the original list, and it's exactly what
layer 4 fixes.

---

## Layer 4 — `LiveValueProvider` and live-replay closures (closes bucket 2)

### The escape hatch

Every typed node in the fork now carries:

```csharp
internal Func<T>? LiveValueProvider { get; set; }
```

`Evaluate()` checks it first and short-circuits before the normal
`NodeType` walk:

```csharp
public float Evaluate()
{
    if (LiveValueProvider is not null) return LiveValueProvider();
    switch (NodeType) { /* …unchanged tree walk… */ }
}
```

`ToExpressionString()` ignores it entirely. Composition codegen and GPU
rendering are byte-identical to before. The escape hatch only fires when
managed code calls `Evaluate()`.

### Why on the leaf node, not the wrapper

`Evaluate()` is recursive on the AST itself. By the time recursion
reaches a leaf inside `wobble + (spin - wobble) * mode`, all knowledge
of which wrapper produced that leaf is gone. The intercept has to live
on the leaf.

### How `AnimatableXxxCompositionNode` uses it

`Animate` gains an overload that takes a UI-thread "live value" closure
alongside the opaque composition animation:

```csharp
public void Animate(CompositionAnimation animation, Func<float> liveValueProvider);
```

The closure is stored. `Value` consults a three-tier fallback:

1. closure (set by the `(animation, liveValueProvider)` overload),
2. expression tree (set by `Animate(ScalarNode)`),
3. `ComposerValue` — the stale `Visual.Offset.X` getter, used only when
   nothing is animating.

`Reference` then attaches `() => this.Value` to the leaf node it
returns, so embedding trees automatically see the live value through
the bypass.

### How callers produce the closure

For a `ScalarKeyFrameAnimation` whose math you can mirror, write a
closure that reproduces the same value as a function of "now":

```csharp
// Forever-iterating linear KFA from start to start+360 over N seconds:
var sw = Stopwatch.StartNew();
var startValue = node.Value;
node.Animate(kfa, () =>
    startValue + (float)((sw.Elapsed.TotalSeconds * 360.0 / N) % 360.0));
```

For a one-shot eased KFA, evaluate the easing in C#
(Newton-Raphson on cubic-bezier matches the GPU output to float
precision):

```csharp
node.Animate(kfa, KfaReplay.EasedScalar(from, to, durationMs, p1x, p1y, p2x, p2y));
```

Both representations agree: composition runs the animation on the GPU,
the closure mirrors it on the UI thread, `Evaluate()` returns
sub-millisecond-accurate values.

### How `TrackedInteractionNodes` uses it

`InteractionTracker` exposes no UI-thread "current animated value"
property either, but it does fire `IInteractionTrackerOwner.ValuesChanged`
once per input frame on the UI thread. `TrackedInteractionNodes` caches
the latest `Position` / `Scale` / limits in the handler and exposes
factory methods that produce reference nodes wired to the cache:

```csharp
var tracked = tracker.CreateTrackedNodes();
// in your IInteractionTrackerOwner.ValuesChanged: tracked.OnValuesChanged();

Vector3Node pos = tracked.PositionReference();   // for embedding
ScalarNode  s   = tracked.ScalarReference(TrackerScalarChannel.Scale);
```

Both `pos` and `s` resolve on the composition side to the real tracker
properties (rendering is unchanged), and on the UI thread to the cached
values. Same `Evaluate()` bypass, different freshness source.

### What this still doesn't solve

* **Animations / trackers driven by code we don't own** — `ScrollViewer`'s
  internal tracker, XAML implicit animations, `ConnectedAnimation`. We
  can't subscribe to events we weren't given access to. A truly general
  fix would require the OS to expose `GetCurrentAnimatedValue`.
* **Closure-vs-GPU drift** — the closure must mirror the same easing
  the GPU is using. The bundled `KfaReplay` covers linear loops and
  cubic-bezier eased lerps. Step / back / elastic / spring need their
  own implementations.

---

## Layer 5 — `CompositionCollectionView` and `CompositionCollectionLayout`

With expression nodes that compile down to GPU expressions, evaluate
correctly on the UI thread, and survive being driven by KFAs and
trackers, we have the full set of primitives needed to express
"smoothly-animating, gesture-aware collection layout" without any
per-frame managed work.

### `CompositionCollectionView`

A WinUI 3 control. Hosts a single `Panel` (`RootPanel`) that contains
the actual element framework-elements, and a single
`CompositionPropertySet` of layout-wide animatable values. The control
is mostly a dumb host: it wires `SizeChanged` to the active layout, owns
the `RootPanelVisual` for global transforms, and forwards `UpdateSource`
to whichever `CompositionCollectionLayout` is currently attached.

### `CompositionCollectionLayout<TId, TItem>` — the layout abstraction

Subclass it to define how N items map to N visuals. The hot path is
five virtual methods, each returning an *expression node* rather than a
value:

```csharp
public virtual Vector3Node    GetElementPositionNode(ElementReference<TId, TItem> e);
public virtual ScalarNode     GetElementScaleNode   (ElementReference<TId, TItem> e);
public virtual ScalarNode     GetElementOpacityNode (ElementReference<TId, TItem> e);
public virtual QuaternionNode GetElementOrientationNode(ElementReference<TId, TItem> e);
protected virtual ElementTransition? GetElementTransitionEasingFunction(…);
```

Each `Get…Node` returns a typed expression tree that references whatever
mix of layout-wide property-set scalars (`Properties`) and per-element
animatable cells (`element.AnimatableNodes`) the layout cares about.
The base layer wires those expressions onto each element's
`Visual.Offset` / `Scale` / `Opacity` / `Orientation` once at element
configuration time. After that the GPU evaluates them every frame; no
managed code participates in per-frame layout.

### `ElementReference<TId, TItem>`

Stable per-element handle: the source key (`Id`), the model
(`Model`), the framework element (`Container`), the underlying `Visual`,
a per-element `BindableCompositionPropertySet` and an
`AnimatableCompositionNodeSet`. CCV's `UpdateSource` is a *diff*, not a
re-bind — surviving items keep the same `ElementReference`, which is
what lets per-element animatable cells (entry, hover, press) outlive
data updates.

### `Get…Value` mirrors

```csharp
public virtual Vector3 GetElementPositionValue(…);
public virtual float   GetElementScaleValue   (…);
public virtual float   GetElementOpacityValue (…);
```

The base implementation calls `Get…Node().Evaluate()`. This is exactly
where layer 2's `Evaluate()` and layer 4's `LiveValueProvider` earn
their keep: `TransitionTo(newLayout)` snapshots each element's current
position/scale/opacity by calling these methods on the *outgoing*
layout, then feeds the snapshot into a lerp expression on the
*incoming* layout. Without UI-thread evaluation, you couldn't
crossfade between two expression-driven layouts at all.

Many concrete layouts override `Get…Value` directly to avoid evaluating
trees that contain references to per-element nodes whose snapshot value
is known statically (e.g. "assume entry is finished before transition
starts"). Both paths work; the override is a perf optimization.

### Behaviors

`CompositionCollectionLayoutBehavior<TId, TItem>` lets you attach
reusable, lifecycle-aware add-ons to a layout: gesture wiring, periodic
work, debug overlays, anything that wants `OnActivated` /
`OnDeactivated` / `OnElementsUpdated` hooks. The two built-in
behaviors —
[InteractionTrackerBehavior](src/CompositionCollectionView/Behaviors/InteractionTrackerBehavior.cs)
and
[ElementInteractionTrackerBehavior](src/CompositionCollectionView/Behaviors/ElementInteractionTrackerBehavior.cs)
— wire layout-level and per-element interaction trackers respectively,
and expose their `Position` / `Scale` as expression-embeddable nodes
(now via `TrackedInteractionNodes`, so consuming layouts can also
`Evaluate()` them).

### Transitions

`TransitionTo(newLayout, duration, easing)` is the headline animation
API. Implementation:

1. Snapshot every surviving element's current
   `(Position, Scale, Opacity, Orientation)` by calling the *outgoing*
   layout's `Get…Value` overloads.
2. Compute every surviving element's target
   `(Position, Scale, Opacity, Orientation)` from the *incoming*
   layout's `Get…Node` expressions.
3. Bind a single layout-wide blend scalar (0 → 1 over `duration` with
   `easing`) and re-bind each element's transform expression to
   `Lerp(snapshot, target, blend)`.
4. After `duration`, swap the active layout and bind the bare target
   expressions (no more lerp).

Steps 1 and 2 are the snapshot-then-target pattern that makes UI-thread
`Evaluate()` mandatory. Without layer 2, every transition would start
elements at `Vector3.Zero` (the value the managed getter returns after
StartAnimation has run). Without layer 4, transitions kicked off while
a layout-wide KFA or tracker is in motion would see stale values from
those animated leaves.

---

## How the layers cooperate, end to end

A single line of consumer code like

```csharp
// Layout author, inside GetElementPositionNode:
var hover = element.AnimatableNodes.GetOrCreateScalarNode("hover", 0).Reference;
return ExpressionFunctions.Vector3(col * 120, row * 80 - hover * 8, 0);
```

…touches every layer:

* **Layer 1** — `Vector3` / `ScalarNode` / `*` operator / `Vector3` factory.
* **Layer 2** — the returned `Vector3Node` is `Evaluate()`-able, so the
  base `GetElementPositionValue` works without an override.
* **Layer 3** — the wrapper exposes `hover` as a strongly-typed
  animatable cell, plus its `Reference` for embedding.
* **Layer 4** — `Reference` attached `() => this.Value` to the leaf, so
  if you later `Animate(KFA, replay)` the hover, both GPU rendering and
  UI-thread snapshots stay accurate.
* **Layer 5** — the layout's base `ConfigureElement` binds the returned
  expression onto `element.Visual.Offset` once, and `TransitionTo`
  knows how to snapshot the live value when blending into another
  layout.

Each layer fills exactly one gap in the layer below it; nothing further
up could exist without the layer beneath it; and nothing in the lower
layers ever has to know about the higher ones. The composition platform
hasn't been bypassed at any point — every animation still runs on the
GPU. CCV just teaches the managed side enough about composition's
semantics that the same node tree can describe both what to render and
how to read back what's being rendered.
