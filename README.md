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

## License

MIT
