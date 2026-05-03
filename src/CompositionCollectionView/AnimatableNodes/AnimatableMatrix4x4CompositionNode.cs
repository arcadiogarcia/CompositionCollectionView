// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
using System.Numerics;
using static CommunityToolkit.Labs.WinUI.AnimationConstants;

namespace CommunityToolkit.Labs.WinUI;
public class AnimatableMatrix4x4CompositionNode : IDisposable
{
    private Visual _underlyingVisual;
    private bool disposedValue;
    private Matrix4x4Node? _currentAnimationNode = null;
    private Func<Matrix4x4>? _liveValueProvider = null;

    public Matrix4x4 Value
    {
        get
        {
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
            _underlyingVisual.TransformMatrix = value;
            _currentAnimationNode = null;
            _liveValueProvider = null;
        }
    }

    public Matrix4x4 ComposerValue => _underlyingVisual.TransformMatrix;

    public AnimatableMatrix4x4CompositionNode(Compositor compositor)
    {
        _underlyingVisual = compositor.CreateShapeVisual();
    }

    public void Animate(CompositionAnimation animation)
    {
        _currentAnimationNode = null;
        _liveValueProvider = null;
        _underlyingVisual.StartAnimation(TransformMatrix, animation);
    }

    public void Animate(Matrix4x4Node animation)
    {
        _currentAnimationNode = animation;
        _liveValueProvider = null;
        _underlyingVisual.StartAnimation(TransformMatrix, animation);
    }

    /// <summary>
    /// Start a composition animation alongside a UI-thread closure that reproduces the
    /// same value as a function of "now". See <see cref="AnimatableScalarCompositionNode.Animate(CompositionAnimation, Func{float})"/>
    /// for the full design discussion.
    /// </summary>
    public void Animate(CompositionAnimation animation, Func<Matrix4x4> liveValueProvider)
    {
        _currentAnimationNode = null;
        _liveValueProvider = liveValueProvider;
        _underlyingVisual.StartAnimation(TransformMatrix, animation);
    }

    public Matrix4x4Node Reference
    {
        get
        {
            var node = _underlyingVisual.GetReference().TransformMatrix;
            // Intercept Evaluate so embedding trees see the live Value, not the
            // stale composer-cached Visual.TransformMatrix. See ScalarNode.LiveValueProvider.
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
