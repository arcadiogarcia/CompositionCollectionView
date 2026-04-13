namespace CompositionCollectionView.Sample.Uwp;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        this.InitializeComponent();
        SetupBasicSample();
        SetupSwitchLayoutsSample();
    }

    #region Basic Sample

    private readonly Dictionary<uint, object?> _basicElements = new()
    {
        { 0, null },
        { 1, null },
        { 2, null },
        { 3, null },
        { 4, null }
    };

    private void SetupBasicSample()
    {
        var layout = new BasicLayout((id) =>
            new Rectangle()
            {
                Width = 100,
                Height = 100,
                Fill = new SolidColorBrush(Colors.CornflowerBlue)
            });
        basicView.SetLayout(layout);
        basicView.UpdateSource(_basicElements);

        addButton.Click += (sender, e) =>
        {
            _basicElements.Add((uint)_basicElements.Count, null);
            basicView.UpdateSource(_basicElements);
        };
    }

    private class BasicLayout : CompositionCollectionLayout<uint, object?>
    {
        public BasicLayout(Func<uint, FrameworkElement> elementFactory) : base(elementFactory) { }

        public override Vector3Node GetElementPositionNode(ElementReference<uint, object?> element)
            => ExpressionFunctions.Vector3(element.Id * 120, 0, 0);

        public override ScalarNode GetElementScaleNode(ElementReference<uint, object?> element) => 1;
    }

    #endregion

    #region Switch Layouts Sample

    private void SetupSwitchLayoutsSample()
    {
        var elements = new Dictionary<uint, object?>()
        {
            { 0, null },
            { 1, null },
            { 2, null },
            { 3, null },
            { 4, null }
        };

        var layout = new LinearLayout((id) =>
            new Rectangle()
            {
                Width = 100,
                Height = 100,
                Fill = new SolidColorBrush(Colors.CornflowerBlue),
                Stroke = new SolidColorBrush(Colors.Gray),
                StrokeThickness = 1
            });
        switchView.SetLayout(layout);
        switchView.UpdateSource(elements);

        layoutToggle.Toggled += (sender, e) =>
        {
            if (sender is ToggleSwitch toggle)
            {
                if (toggle.IsOn && switchView.Layout<uint, object?>() is LinearLayout currentLinear)
                {
                    currentLinear.TransitionTo(_ => new StackedLayout(currentLinear));
                }
                else if (!toggle.IsOn && switchView.Layout<uint, object?>() is StackedLayout currentStacked)
                {
                    currentStacked.TransitionTo(_ => new LinearLayout(currentStacked));
                }
            }
        };
    }

    private class LinearLayout : CompositionCollectionLayout<uint, object?>
    {
        public LinearLayout(Func<uint, FrameworkElement> elementFactory) : base(elementFactory) { }
        public LinearLayout(CompositionCollectionLayout<uint, object?> source) : base(source) { }

        public override Vector3Node GetElementPositionNode(ElementReference<uint, object?> element)
            => ExpressionFunctions.Vector3(element.Id * 120, 0, 0);

        public override ScalarNode GetElementScaleNode(ElementReference<uint, object?> element) => 1;

        protected override ElementTransition GetElementTransitionEasingFunction(ElementReference<uint, object?> element) =>
            new(300, Window.Current.Compositor.CreateCubicBezierEasingFunction(
                new Vector2(0.25f, 0.1f), new Vector2(0.25f, 1f)));
    }

    private class StackedLayout : CompositionCollectionLayout<uint, object?>
    {
        public StackedLayout(Func<uint, FrameworkElement> elementFactory) : base(elementFactory) { }
        public StackedLayout(CompositionCollectionLayout<uint, object?> source) : base(source) { }

        public override Vector3Node GetElementPositionNode(ElementReference<uint, object?> element)
            => ExpressionFunctions.Vector3(element.Id * 10, element.Id * 10, 0);

        public override ScalarNode GetElementScaleNode(ElementReference<uint, object?> element)
            => (float)Math.Pow(0.95f, element.Id);

        protected override ElementTransition GetElementTransitionEasingFunction(ElementReference<uint, object?> element) =>
            new(300, Window.Current.Compositor.CreateCubicBezierEasingFunction(
                new Vector2(0.25f, 0.1f), new Vector2(0.25f, 1f)));

        protected override void ConfigureElement(ElementReference<uint, object?> element)
        {
            element.Container.SetValue(Canvas.ZIndexProperty, -(int)element.Id);
        }
    }

    #endregion
}
