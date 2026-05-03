// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
using System;
using System.Numerics;
using Microsoft.Toolkit.Uwp.UI.Animations.ExpressionsFork;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.Interactions;

namespace CommunityToolkit.Labs.WinUI;

/// <summary>
/// Per-property selectors for an <see cref="InteractionTracker"/>. Used by
/// <see cref="TrackedInteractionNodes"/> to pick which tracker channel a given
/// node mirrors.
/// </summary>
public enum TrackerScalarChannel
{
    PositionX,
    PositionY,
    PositionZ,
    Scale,
    MinPosition_X,
    MinPosition_Y,
    MinPosition_Z,
    MaxPosition_X,
    MaxPosition_Y,
    MaxPosition_Z,
}

/// <summary>
/// UI-thread cache of selected <see cref="InteractionTracker"/> channels, plus
/// helpers to produce <see cref="ScalarNode"/> and <see cref="Vector3Node"/>
/// references whose <see cref="ScalarNode.LiveValueProvider"/> is wired to the
/// cache. Embed those references in any expression tree and call
/// <see cref="ExpressionNode.Evaluate"/> from the UI thread to get a frame-fresh
/// value, even though composition's tracker properties are not normally readable
/// live from C#.
///
/// <para><b>How it stays fresh.</b> Subscribe the cache to your
/// <see cref="IInteractionTrackerOwner"/> (or the input source's
/// <c>ValuesChanged</c>-equivalent event) and call <see cref="OnValuesChanged"/>
/// from the handler. The cache stores the latest <c>Position</c>, <c>Scale</c>,
/// and limit values. Reads are O(1) field accesses.</para>
///
/// <para><b>What this does NOT do.</b> It does not subscribe automatically.
/// InteractionTracker's owner contract requires you to implement
/// <see cref="IInteractionTrackerOwner"/> yourself; consumers wire
/// <c>ValuesChanged</c> through to <see cref="OnValuesChanged"/>. We don't own
/// the owner because most callers already have one with custom inertia /
/// ideation logic.</para>
/// </summary>
public sealed class TrackedInteractionNodes
{
    private readonly InteractionTracker _tracker;
    private Vector3 _position;
    private float _scale;
    private Vector3 _minPosition;
    private Vector3 _maxPosition;

    public TrackedInteractionNodes(InteractionTracker tracker)
    {
        _tracker = tracker;
        // Seed the cache so the first Evaluate before any ValuesChanged still
        // returns the tracker's current snapshot (typically the resting state).
        _position = tracker.Position;
        _scale = tracker.Scale;
        _minPosition = tracker.MinPosition;
        _maxPosition = tracker.MaxPosition;
    }

    /// <summary>
    /// Refresh the cache from the tracker. Call from your
    /// <see cref="IInteractionTrackerOwner.ValuesChanged"/> implementation
    /// (and from the inertia / customAnimationStateEntered callbacks if you
    /// want intra-inertia reads to be exact). Cheap — five field copies.
    /// </summary>
    public void OnValuesChanged()
    {
        _position    = _tracker.Position;
        _scale       = _tracker.Scale;
        _minPosition = _tracker.MinPosition;
        _maxPosition = _tracker.MaxPosition;
    }

    /// <summary>Latest cached <see cref="InteractionTracker.Position"/>.</summary>
    public Vector3 Position => _position;

    /// <summary>Latest cached <see cref="InteractionTracker.Scale"/>.</summary>
    public float Scale => _scale;

    /// <summary>
    /// Build a <see cref="Vector3Node"/> that, on the composition side, references
    /// <c>tracker.Position</c>; on the UI thread, returns the cached value via
    /// <see cref="Vector3Node.LiveValueProvider"/>. Embed in any expression tree.
    /// </summary>
    public Vector3Node PositionReference()
    {
        var node = _tracker.GetReference().Position;
        node.LiveValueProvider = () => _position;
        return node;
    }

    /// <summary>
    /// Build a <see cref="ScalarNode"/> mirroring one channel of the tracker.
    /// Composition-side resolves to the matching <c>tracker.X</c> property;
    /// UI-thread <c>Evaluate</c> short-circuits to the cached value.
    /// </summary>
    public ScalarNode ScalarReference(TrackerScalarChannel channel)
    {
        var trackerRef = _tracker.GetReference();
        ScalarNode node;
        Func<float> selector;
        switch (channel)
        {
            case TrackerScalarChannel.PositionX:    node = trackerRef.Position.X;     selector = () => _position.X;    break;
            case TrackerScalarChannel.PositionY:    node = trackerRef.Position.Y;     selector = () => _position.Y;    break;
            case TrackerScalarChannel.PositionZ:    node = trackerRef.Position.Z;     selector = () => _position.Z;    break;
            case TrackerScalarChannel.Scale:        node = trackerRef.Scale;          selector = () => _scale;          break;
            case TrackerScalarChannel.MinPosition_X: node = trackerRef.MinPosition.X; selector = () => _minPosition.X; break;
            case TrackerScalarChannel.MinPosition_Y: node = trackerRef.MinPosition.Y; selector = () => _minPosition.Y; break;
            case TrackerScalarChannel.MinPosition_Z: node = trackerRef.MinPosition.Z; selector = () => _minPosition.Z; break;
            case TrackerScalarChannel.MaxPosition_X: node = trackerRef.MaxPosition.X; selector = () => _maxPosition.X; break;
            case TrackerScalarChannel.MaxPosition_Y: node = trackerRef.MaxPosition.Y; selector = () => _maxPosition.Y; break;
            case TrackerScalarChannel.MaxPosition_Z: node = trackerRef.MaxPosition.Z; selector = () => _maxPosition.Z; break;
            default: throw new ArgumentOutOfRangeException(nameof(channel));
        }
        node.LiveValueProvider = selector;
        return node;
    }
}

/// <summary>
/// Convenience extensions to obtain a <see cref="TrackedInteractionNodes"/> from a tracker.
/// </summary>
public static class TrackedInteractionExtensions
{
    /// <summary>
    /// Allocate a node-cache wrapper for this tracker. The returned object must be
    /// kept alive (held by the consumer) for the lifetime of any reference nodes
    /// it produced; otherwise GC'd closures will throw on Evaluate.
    /// </summary>
    public static TrackedInteractionNodes CreateTrackedNodes(this InteractionTracker tracker)
        => new TrackedInteractionNodes(tracker);
}
