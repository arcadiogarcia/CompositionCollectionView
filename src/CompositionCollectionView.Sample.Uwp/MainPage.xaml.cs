namespace CompositionCollectionView.Sample.Uwp;

public sealed partial class MainPage : Page
{
    private static readonly Color[] CardColors =
    {
        Color.FromArgb(255, 231, 76, 60),
        Color.FromArgb(255, 46, 204, 113),
        Color.FromArgb(255, 52, 152, 219),
        Color.FromArgb(255, 155, 89, 182),
        Color.FromArgb(255, 241, 196, 15),
        Color.FromArgb(255, 230, 126, 34),
        Color.FromArgb(255, 26, 188, 156),
        Color.FromArgb(255, 236, 100, 75),
        Color.FromArgb(255, 52, 73, 94),
        Color.FromArgb(255, 142, 68, 173),
        Color.FromArgb(255, 39, 174, 96),
        Color.FromArgb(255, 41, 128, 185),
    };

    private const int CardCount = 12;
    private const float Pi = 3.14159265f;
    private const float Deg2Rad = Pi / 180f;

    public MainPage()
    {
        this.InitializeComponent();
        SetupCardFanSample();
        SetupDynamicGallerySample();
    }

    private static FrameworkElement CreateCard(uint id)
    {
        var color = CardColors[id % CardColors.Length];
        return new Border
        {
            Width = 80,
            Height = 120,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(color),
            BorderBrush = new SolidColorBrush(Colors.White),
            BorderThickness = new Thickness(2),
            Child = new TextBlock
            {
                Text = (id + 1).ToString(),
                FontSize = 24,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Colors.White)
            }
        };
    }

    #region Card Fan & Layout Transitions

    private void SetupCardFanSample()
    {
        var source = new Dictionary<uint, object?>();
        for (uint i = 0; i < CardCount; i++)
            source[i] = null;

        var layout = new FanLayout(CreateCard);
        cardFanView.SetLayout(layout);
        cardFanView.UpdateSource(source);

        layoutSelector.SelectionChanged += (s, e) =>
        {
            var current = cardFanView.Layout<uint, object?>();
            if (current == null) return;

            var selected = (layoutSelector.SelectedItem as ComboBoxItem)?.Content?.ToString();
            switch (selected)
            {
                case "Fan" when current is not FanLayout:
                    current.TransitionTo(_ => new FanLayout(current));
                    break;
                case "Grid" when current is not CardGridLayout:
                    current.TransitionTo(_ => new CardGridLayout(current));
                    break;
                case "Spiral" when current is not SpiralLayout:
                    current.TransitionTo(_ => new SpiralLayout(current));
                    break;
                case "Orbit" when current is not OrbitLayout:
                    current.TransitionTo(_ => new OrbitLayout(current));
                    break;
            }
        };
    }

    private class FanLayout : CompositionCollectionLayout<uint, object?>
    {
        private const float PivotX = 350f;
        private const float PivotY = 500f;
        private const float Radius = 380f;
        private const float TotalSpread = 60f;

        public FanLayout(Func<uint, FrameworkElement> factory) : base(factory) { }
        public FanLayout(CompositionCollectionLayout<uint, object?> source) : base(source) { }

        public override Vector3Node GetElementPositionNode(ElementReference<uint, object?> element)
        {
            float t = CardCount > 1 ? (float)element.Id / (CardCount - 1) - 0.5f : 0f;
            float angleDeg = 90f + t * TotalSpread;
            float angleRad = angleDeg * Deg2Rad;
            float x = PivotX + Radius * (float)Math.Cos(angleRad) - 40f;
            float y = PivotY - Radius * (float)Math.Sin(angleRad) - 60f;
            return new Vector3(x, y, 0);
        }

        public override ScalarNode GetElementScaleNode(ElementReference<uint, object?> element)
        {
            float t = CardCount > 1 ? (float)element.Id / (CardCount - 1) : 0.5f;
            return 1f - 0.15f * Math.Abs(t - 0.5f) * 2f;
        }

        public override QuaternionNode GetElementOrientationNode(ElementReference<uint, object?> element)
        {
            float t = CardCount > 1 ? (float)element.Id / (CardCount - 1) - 0.5f : 0f;
            float rotationRad = -t * TotalSpread * Deg2Rad;
            return Quaternion.CreateFromAxisAngle(Vector3.UnitZ, rotationRad);
        }

        protected override ElementTransition GetElementTransitionEasingFunction(ElementReference<uint, object?> element) =>
            new(400, Compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0f), new Vector2(0f, 1f)));

        protected override void ConfigureElement(ElementReference<uint, object?> element)
        {
            float t = CardCount > 1 ? (float)element.Id / (CardCount - 1) : 0.5f;
            int zIndex = (int)(100 - Math.Abs(t - 0.5f) * 200);
            element.Container.SetValue(Canvas.ZIndexProperty, zIndex);
        }
    }

    private class CardGridLayout : CompositionCollectionLayout<uint, object?>
    {
        private const int Columns = 4;
        private const float CellW = 100f;
        private const float CellH = 115f;
        private const float StartX = 168f;
        private const float StartY = 10f;

        public CardGridLayout(Func<uint, FrameworkElement> factory) : base(factory) { }
        public CardGridLayout(CompositionCollectionLayout<uint, object?> source) : base(source) { }

        public override Vector3Node GetElementPositionNode(ElementReference<uint, object?> element)
        {
            int col = (int)(element.Id % Columns);
            int row = (int)(element.Id / Columns);
            return new Vector3(StartX + col * CellW, StartY + row * CellH, 0);
        }

        public override ScalarNode GetElementScaleNode(ElementReference<uint, object?> element) => 0.85f;

        public override QuaternionNode GetElementOrientationNode(ElementReference<uint, object?> element)
            => Quaternion.Identity;

        protected override ElementTransition GetElementTransitionEasingFunction(ElementReference<uint, object?> element) =>
            new(400, Compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0f), new Vector2(0f, 1f)));

        protected override void ConfigureElement(ElementReference<uint, object?> element)
        {
            element.Container.SetValue(Canvas.ZIndexProperty, 0);
        }
    }

    private class SpiralLayout : CompositionCollectionLayout<uint, object?>
    {
        private const float CenterX = 350f;
        private const float CenterY = 175f;
        private const float BaseRadius = 40f;
        private const float RadiusGrowth = 22f;
        private const float AngleStep = 50f;

        public SpiralLayout(Func<uint, FrameworkElement> factory) : base(factory) { }
        public SpiralLayout(CompositionCollectionLayout<uint, object?> source) : base(source) { }

        public override Vector3Node GetElementPositionNode(ElementReference<uint, object?> element)
        {
            float angleRad = element.Id * AngleStep * Deg2Rad;
            float radius = BaseRadius + element.Id * RadiusGrowth;
            float x = CenterX + radius * (float)Math.Cos(angleRad) - 40f;
            float y = CenterY + radius * (float)Math.Sin(angleRad) - 60f;
            return new Vector3(x, y, 0);
        }

        public override ScalarNode GetElementScaleNode(ElementReference<uint, object?> element)
            => 0.6f + 0.4f * (1f - element.Id / (float)CardCount);

        public override QuaternionNode GetElementOrientationNode(ElementReference<uint, object?> element)
        {
            float rotationRad = element.Id * AngleStep * Deg2Rad;
            return Quaternion.CreateFromAxisAngle(Vector3.UnitZ, rotationRad);
        }

        protected override ElementTransition GetElementTransitionEasingFunction(ElementReference<uint, object?> element) =>
            new(400, Compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0f), new Vector2(0f, 1f)));

        protected override void ConfigureElement(ElementReference<uint, object?> element)
        {
            element.Container.SetValue(Canvas.ZIndexProperty, (int)element.Id);
        }
    }

    private class OrbitLayout : CompositionCollectionLayout<uint, object?>
    {
        private const float CenterX = 350f;
        private const float CenterY = 175f;
        private const float RadiusX = 260f;
        private const float RadiusY = 110f;

        public OrbitLayout(Func<uint, FrameworkElement> factory) : base(factory) { }
        public OrbitLayout(CompositionCollectionLayout<uint, object?> source) : base(source) { }

        public override Vector3Node GetElementPositionNode(ElementReference<uint, object?> element)
        {
            float angle = element.Id * (2f * Pi / CardCount) - Pi / 2f;
            float x = CenterX + RadiusX * (float)Math.Cos(angle) - 40f;
            float y = CenterY + RadiusY * (float)Math.Sin(angle) - 60f;
            return new Vector3(x, y, 0);
        }

        public override ScalarNode GetElementScaleNode(ElementReference<uint, object?> element)
        {
            float angle = element.Id * (2f * Pi / CardCount) - Pi / 2f;
            float depth = ((float)Math.Sin(angle) + 1f) / 2f;
            return 0.5f + 0.5f * depth;
        }

        public override ScalarNode GetElementOpacityNode(ElementReference<uint, object?> element)
        {
            float angle = element.Id * (2f * Pi / CardCount) - Pi / 2f;
            float depth = ((float)Math.Sin(angle) + 1f) / 2f;
            return 0.4f + 0.6f * depth;
        }

        public override QuaternionNode GetElementOrientationNode(ElementReference<uint, object?> element)
            => Quaternion.Identity;

        protected override ElementTransition GetElementTransitionEasingFunction(ElementReference<uint, object?> element) =>
            new(400, Compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0f), new Vector2(0f, 1f)));

        protected override void ConfigureElement(ElementReference<uint, object?> element)
        {
            float angle = element.Id * (2f * Pi / CardCount) - Pi / 2f;
            float depth = ((float)Math.Sin(angle) + 1f) / 2f;
            element.Container.SetValue(Canvas.ZIndexProperty, (int)(depth * 100));
        }
    }

    #endregion

    #region Dynamic Gallery

    private int _galleryCount = 8;
    private readonly Dictionary<uint, object?> _galleryElements = new();

    private void SetupDynamicGallerySample()
    {
        for (uint i = 0; i < _galleryCount; i++)
            _galleryElements[i] = null;

        var layout = new GalleryGridLayout(CreateGalleryItem);
        galleryView.SetLayout(layout);
        galleryView.UpdateSource(_galleryElements);
        UpdateGalleryCount();

        addGalleryButton.Click += (s, e) =>
        {
            _galleryElements[(uint)_galleryCount++] = null;
            galleryView.UpdateSource(_galleryElements);
            UpdateGalleryCount();
        };

        removeGalleryButton.Click += (s, e) =>
        {
            if (_galleryCount > 0)
            {
                _galleryElements.Remove((uint)(--_galleryCount));
                galleryView.UpdateSource(_galleryElements);
                UpdateGalleryCount();
            }
        };
    }

    private void UpdateGalleryCount()
    {
        galleryCountText.Text = $"{_galleryCount} items";
    }

    private static FrameworkElement CreateGalleryItem(uint id)
    {
        var color = CardColors[id % CardColors.Length];
        return new Border
        {
            Width = 80,
            Height = 80,
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(color),
            Child = new TextBlock
            {
                Text = (id + 1).ToString(),
                FontSize = 18,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Colors.White)
            }
        };
    }

    private class GalleryGridLayout : CompositionCollectionLayout<uint, object?>
    {
        private const int Columns = 6;
        private const float CellSize = 100f;
        private const float Padding = 10f;

        public GalleryGridLayout(Func<uint, FrameworkElement> factory) : base(factory) { }

        public override Vector3Node GetElementPositionNode(ElementReference<uint, object?> element)
        {
            int col = (int)(element.Id % Columns);
            int row = (int)(element.Id / Columns);
            return new Vector3(Padding + col * CellSize, Padding + row * CellSize, 0);
        }

        public override ScalarNode GetElementScaleNode(ElementReference<uint, object?> element) => 1f;

        protected override ElementTransition GetElementTransitionEasingFunction(ElementReference<uint, object?> element) =>
            new(300, Compositor.CreateCubicBezierEasingFunction(new Vector2(0.25f, 0.1f), new Vector2(0.25f, 1f)));
    }

    #endregion
}
