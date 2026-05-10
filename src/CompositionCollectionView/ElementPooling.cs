// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#nullable enable

namespace CommunityToolkit.Labs.WinUI;

public enum ElementPoolingMode
{
    Detached,
    Retained,
}

public enum ElementRemovalReason
{
    SourceUpdate,
    ExplicitClear,
    PoolEviction,
    ControlUnloaded,
}

public readonly record struct ElementPoolStats(
    int Active,
    int Retained,
    int Created,
    int Reused,
    int Retired,
    int Destroyed,
    int Evicted);

public sealed class ElementPoolContext<TId, TItem> where TId : notnull
{
    internal ElementPoolContext(
        Panel rootPanel,
        Visual rootPanelVisual,
        IReadOnlyCollection<ElementReference<TId, TItem>> activeElements)
    {
        RootPanel = rootPanel;
        RootPanelVisual = rootPanelVisual;
        ActiveElements = activeElements;
    }

    public Panel RootPanel { get; }
    public Visual RootPanelVisual { get; }
    public Compositor Compositor => RootPanelVisual.Compositor;
    public IReadOnlyCollection<ElementReference<TId, TItem>> ActiveElements { get; }
}

public interface IElementPoolPolicy<TId, TItem> where TId : notnull
{
    ElementPoolingMode Mode { get; }
    ElementPoolStats Stats { get; }

    bool TryAcquire(
        TId id,
        TItem model,
        ElementPoolContext<TId, TItem> context,
        out FrameworkElement? container);

    bool TryRetire(
        ElementReference<TId, TItem> element,
        ElementRemovalReason reason,
        ElementPoolContext<TId, TItem> context);

    void Restore(
        ElementReference<TId, TItem> element,
        TItem model,
        ElementPoolContext<TId, TItem> context);

    void Destroy(
        ElementReference<TId, TItem> element,
        ElementRemovalReason reason,
        ElementPoolContext<TId, TItem> context);

    void Trim(ElementPoolContext<TId, TItem> context);

    void Clear(ElementRemovalReason reason, ElementPoolContext<TId, TItem> context);
}

internal interface IElementPoolPolicyHost
{
    void SetElementPoolPolicyObject(object? policy);
    void ClearElementPoolObject(ElementRemovalReason reason);
}

public static class CompositionCollectionDiagnostics
{
    public static Action<string>? Log { get; set; }

    internal static void Write(string message) => Log?.Invoke(message);
}
