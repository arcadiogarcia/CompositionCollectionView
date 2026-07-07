# CompositionCollectionView

A composition-driven collection view control for UWP and WinUI 3 / Windows App SDK, with fully customizable layouts and behaviors powered by `Windows.UI.Composition`.

## Features

- **Composition-powered animations** — all element positioning, scaling, rotation, and opacity are driven by composition expression animations for smooth 60fps performance
- **Custom layouts** — extend `CompositionCollectionLayout<TId, TItem>` to define how elements are positioned, scaled, and rotated
- **Animated layout transitions** — smoothly animate between different layouts with `TransitionTo()`
- **Behaviors** — attach reusable behaviors like `InteractionTrackerBehavior` for gesture-driven interactions
- **Interaction tracker gestures** — define composable touch/pointer gestures with visual previews
- **Multi-platform** — ships as a single NuGet package targeting both UWP (`uap10.0.19041`) and WinUI 3 (`net9.0-windows10.0.19041.0`)

## Quick Start

```xml
<labs:CompositionCollectionView x:Name="collectionView"
                                Width="600" Height="200" />
```

```csharp
// Define a simple horizontal layout
public class HorizontalLayout : CompositionCollectionLayout<uint, MyItem>
{
    public HorizontalLayout(Func<uint, FrameworkElement> factory) : base(factory) { }

    public override Vector3Node GetElementPositionNode(ElementReference<uint, MyItem> element)
        => ExpressionFunctions.Vector3(element.Id * 120, 0, 0);
}

// Create and use the layout
var layout = new HorizontalLayout(id => new MyItemControl());
collectionView.SetLayout(layout);
await collectionView.UpdateSource(myItems);
```

## Solution Structure

```
CompositionCollectionView/
├── src/CompositionCollectionView/          # Control library (UWP + WinUI 3)
├── samples/
│   ├── CompositionCollectionView.Sample.Uwp/       # UWP sample app
│   └── CompositionCollectionView.Sample.WinUI3/    # WinUI 3 sample app
```

## Building

Build with Visual Studio 2022+ or MSBuild:

```powershell
msbuild CompositionCollectionView.sln /p:Platform=x64
```

## Architecture

### CompositionCollectionLayout

The core abstract class you extend to define layouts. Override these methods:

| Method | Purpose |
|---|---|
| `GetElementPositionNode()` | Return an expression node for element position |
| `GetElementScaleNode()` | Return an expression node for element scale |
| `GetElementOpacityNode()` | Return an expression node for element opacity |
| `GetElementOrientationNode()` | Return an expression node for element orientation |
| `GetElementTransitionEasingFunction()` | Define transition animation easing |

### Behaviors

Attach `CompositionCollectionLayoutBehavior<TId, TItem>` instances to layouts for reusable functionality:

- **`InteractionTrackerBehavior`** — wraps `VisualInteractionSource` + `InteractionTracker` for gesture handling
- **`ElementInteractionTrackerBehavior`** — per-element interaction trackers
- **`InteractionTrackerGesture`** — composable gesture definitions with visual previews

### ExpressionsFork

An internal fork of the Windows Community Toolkit's expression animation helpers, with added `Evaluate()` method support.

## CompositionExpressions (standalone expression-DSL packages)

The typed expression-animation DSL (`ExpressionsFork` — `ScalarNode`, `Matrix4x4Node`, `ExpressionFunctions`, `.Evaluate()`, plus the animatable-node wrappers) is also published on its own, so you can compose `CompositionObject` expression animations without taking a dependency on the collection-view control.

Which package you need depends on **two independent axes** — the target-framework era and the composition surface your app renders on:

|                                | System `Windows.UI.Composition`                        | Lifted `Microsoft.UI.Composition`              |
| ------------------------------ | ------------------------------------------------------ | ---------------------------------------------- |
| **UWP** (`uap10.0`)            | `arcadiog.CompositionExpressions` *(uap asset)*        | —                                              |
| **.NET 5+** (`net10.0-windows`) | **`arcadiog.CompositionExpressions.SystemComp`**       | `arcadiog.CompositionExpressions` *(net asset)* |

- **`arcadiog.CompositionExpressions`** — the mainstream package. Ships a UWP asset (system composition, for UWP apps) and a `net10.0-windows` asset that binds to the **lifted** `Microsoft.UI.Composition` and depends on the Windows App SDK. Use it from WinUI 3 / Windows App SDK apps.

- **`arcadiog.CompositionExpressions.SystemComp`** — for **.NET 5+** hosts that render on the **system** compositor (`Windows.UI.Composition` on a `DesktopWindowTarget`), e.g. a transparent per-pixel-alpha window created outside the XAML/WinUI stack. This is the one asset the mainstream package can't provide: its UWP asset trips `NETSDK1149` when consumed from .NET 5+, and its lifted `net10.0-windows` asset is the wrong composition surface and drags in the Windows App SDK.

  It compiles the same DSL source, but with a **distinct assembly name** (`CompositionExpressions.SystemComp`) so a single app can reference **both** flavors at once — e.g. lifted composition for its main WinUI window and system composition for a headless/transparent companion window — without a `CompositionExpressions.dll` output collision.

## License

MIT
