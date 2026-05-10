// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#nullable enable


// The Templated Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234235
namespace CommunityToolkit.Labs.WinUI;


public sealed class CompositionCollectionView : Control
{
    private Canvas? _contentPanel;
    private ILayout? _layout;
    private Action? _pendingSourceUpdate;
    private object? _elementPoolPolicy;
    public CompositionCollectionLayout<TId, TItem>? Layout<TId, TItem>() where TId : notnull => _layout as CompositionCollectionLayout<TId, TItem>;

    public delegate void LayoutChangedHandler(CompositionCollectionView sender, ILayout newLayout, bool isAnimated);
    public event LayoutChangedHandler? LayoutChanged;

    public CompositionCollectionView()
    {
        this.DefaultStyleKey = typeof(CompositionCollectionView);
        Unloaded += OnUnloaded;
    }

    public void SetElementPoolPolicy<TId, TItem>(IElementPoolPolicy<TId, TItem>? policy) where TId : notnull
    {
        if (_layout is IElementPoolPolicyHost host)
        {
            host.ClearElementPoolObject(ElementRemovalReason.ExplicitClear);
        }

        _elementPoolPolicy = policy;
        ApplyElementPoolPolicy(_layout);
    }

    public void ClearElementPool()
    {
        if (_layout is IElementPoolPolicyHost host)
        {
            host.ClearElementPoolObject(ElementRemovalReason.ExplicitClear);
        }
    }

    public void SetLayout(ILayout layout)
    {
        _layout = layout;
        ApplyElementPoolPolicy(_layout);
        _layout.LayoutReplaced += OnLayoutReplaced;

        if (_contentPanel is not null)
        {
            _layout.Activate(_contentPanel);
        }
    }

    public async Task UpdateSource<TId, TItem>(IDictionary<TId, TItem> source, bool animate = true) where TId : notnull
    {
        if (_contentPanel is not null && _layout as CompositionCollectionLayout<TId, TItem> is CompositionCollectionLayout<TId, TItem> layout)
        {
            await layout.UpdateSource(source, animate);
        }
        else
        {
            _pendingSourceUpdate = () => (_layout as CompositionCollectionLayout<TId, TItem>)?.UpdateSource(source, animate);
        }
    }

    private void OnLayoutReplaced(ILayout sender, ILayout newLayout, bool isAnimated)
    {
        if (_layout is not null)
        {
            _layout.LayoutReplaced -= OnLayoutReplaced;
        }

        _layout = newLayout;
        ApplyElementPoolPolicy(_layout);
        _layout.LayoutReplaced += OnLayoutReplaced;
        LayoutChanged?.Invoke(this, _layout, isAnimated);
    }

    private void ApplyElementPoolPolicy(ILayout? layout)
    {
        if (layout is IElementPoolPolicyHost host)
        {
            host.SetElementPoolPolicyObject(_elementPoolPolicy);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_layout is IElementPoolPolicyHost host)
        {
            host.ClearElementPoolObject(ElementRemovalReason.ControlUnloaded);
        }
    }

    protected override void OnApplyTemplate()
    {
        if (GetTemplateChild("contentPanel") is Canvas contentPanel)
        {
            _contentPanel = contentPanel;
            if (_layout is not null)
            {
                _layout.Activate(_contentPanel);
            }
            if (_pendingSourceUpdate is not null)
            {
                _pendingSourceUpdate?.Invoke();
                _pendingSourceUpdate = null;
            }
        }
    }
}
