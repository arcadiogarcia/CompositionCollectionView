// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
using System.Numerics;
using static CommunityToolkit.Labs.WinUI.AnimationConstants;


namespace CommunityToolkit.Labs.WinUI;

/// <summary>
/// A wrapper around a hidden composition <see cref="Visual"/> whose <c>Offset.X</c>
/// is used as a single-float animatable storage cell. Exposes a <see cref="ScalarNode"/>
/// reference for embedding in expression animations on the composition side, while
/// also offering a <see cref="Value"/> getter that returns the live UI-thread value
/// when possible.
///
/// <para><b>Why the bypass exists.</b> Composition animations run server-side and do
/// not write their values back to the UI-thread <c>Visual.Offset</c> getter. Reading
/// <c>Visual.Offset.X</c> while a KeyFrameAnimation is running returns whatever was
/// last set from C# — typically the value-at-StartAnimation-time. The class works
/// around this by keeping a UI-thread-evaluable representation of the current
/// animation, so consumers (and any expression tree that references this node) can
/// observe the same value the compositor is rendering.</para>
///
/// <para><b>How to drive it.</b>
/// <list type="bullet">
///   <item><see cref="Animate(ScalarNode)"/> — the animation is itself a
///   C#-evaluable expression; <see cref="Value"/> calls <c>Evaluate()</c> on it.</item>
///   <item><see cref="Animate(CompositionAnimation, Func{float})"/> — for
///   KeyFrameAnimations and other opaque composition animations, the caller passes
///   a closure that replays the animation in C#. Both representations agree;
///   composition gets the fast path on the GPU, the UI thread gets a live value
///   from the closure.</item>
///   <item><see cref="Animate(CompositionAnimation)"/> — legacy. No live value;
///   <see cref="Value"/> falls back to the (possibly stale) composer value.</item>
/// </list></para>
/// </summary>
public class AnimatableScalarCompositionNode : IDisposable
{
    private Visual _underlyingVisual;
    private bool disposedValue;
    private ScalarNode? _currentAnimationNode = null;
    private Func<float>? _liveValueProvider = null;

    public float Value
    {
        get
        {
            // Preferred: a caller-supplied UI-thread replay closure (set when
            // Animate(CompositionAnimation, Func<float>) was used). It wins because
            // it's the most general — it works for KFAs whose math we couldn't
            // otherwise recover.
            if (_liveValueProvider is not null)
            {
                return _liveValueProvider();
            }

            if (_currentAnimationNode is not null)
            {
                // When the node value is being driven by a ongoing scalarnode animation, reading the property might return a stale value,
                // so we instead default to evaluating the original expression to get the most accurate value
                return _currentAnimationNode.Evaluate();
            }
            else
            {
                return ComposerValue;
            }
        }
        set
        {
            _underlyingVisual.Offset = new Vector3(value, 0, 0);
            _currentAnimationNode = null;
            _liveValueProvider = null;
        }
    }

    public float ComposerValue => _underlyingVisual.Offset.X;

    public AnimatableScalarCompositionNode(Compositor compositor)
    {
        _underlyingVisual = compositor.CreateShapeVisual();
    }

    /// <summary>
    /// Start a composition animation with no UI-thread mirror. <see cref="Value"/>
    /// will return whatever was last set from C# (typically stale during the run).
    /// Prefer the <see cref="Animate(CompositionAnimation, Func{float})"/> overload
    /// for KeyFrameAnimations whose value you also need to read live.
    /// </summary>
    public void Animate(CompositionAnimation animation)
    {
        _currentAnimationNode = null;
        _liveValueProvider = null;
        _underlyingVisual.StartAnimation(Offset.X, animation);
    }

    /// <summary>
    /// Start an expression animation whose <see cref="ScalarNode"/> tree is itself
    /// C#-evaluable. <see cref="Value"/> will return <c>animation.Evaluate()</c>,
    /// transitively bypassing any stale composition reads inside the tree.
    /// </summary>
    public void Animate(ScalarNode animation)
    {
        _currentAnimationNode = animation;
        _liveValueProvider = null;
        _underlyingVisual.StartAnimation(Offset.X, animation);
    }

    /// <summary>
    /// Start a composition animation alongside a UI-thread closure that reproduces
    /// the same value as a function of "now". The closure is invoked on demand by
    /// <see cref="Value"/> (and therefore by <see cref="ScalarNode.Evaluate"/> on
    /// the <see cref="Reference"/> node), so no per-frame ticking is required —
    /// each call recomputes fresh.
    ///
    /// <para>This is the canonical way to use a <see cref="ScalarKeyFrameAnimation"/>
    /// (or any other opaque <see cref="CompositionAnimation"/>) while still having
    /// a live UI-thread value. The two representations must agree: composition
    /// runs the animation on the GPU; the closure mirrors it in C#.</para>
    ///
    /// <para>Typical pattern for a continuous spin:
    /// <code>
    /// var stopwatch = Stopwatch.StartNew();
    /// var startValue = node.Value;
    /// node.Animate(kfa, () => startValue + (float)(stopwatch.Elapsed.TotalSeconds * degPerSec) % 360f);
    /// </code></para>
    /// </summary>
    public void Animate(CompositionAnimation animation, Func<float> liveValueProvider)
    {
        _currentAnimationNode = null;
        _liveValueProvider = liveValueProvider;
        _underlyingVisual.StartAnimation(Offset.X, animation);
    }

    /// <summary>
    /// A <see cref="ScalarNode"/> reference suitable for embedding in larger
    /// expression trees. The composition side resolves to <c>Visual.Offset.X</c>
    /// of the hidden backing visual; the UI-thread <see cref="ScalarNode.Evaluate"/>
    /// path is intercepted via <see cref="ScalarNode.LiveValueProvider"/> so it
    /// returns this wrapper's live <see cref="Value"/>, even when the node is
    /// nested inside arithmetic or vector constructors.
    /// </summary>
    public ScalarNode Reference
    {
        get
        {
            var node = _underlyingVisual.GetReference().Offset.X;
            // Intercept Evaluate so embedding trees see the live Value, not the
            // stale composer-cached Visual.Offset.X. ToExpressionString is unaffected
            // (it doesn't consult LiveValueProvider), so composition rendering is
            // identical to before.
            node.LiveValueProvider = () => this.Value;
            return node;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
                _underlyingVisual.Dispose();
                _currentAnimationNode?.Dispose();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~AnimatableScalarCompositionNode()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
