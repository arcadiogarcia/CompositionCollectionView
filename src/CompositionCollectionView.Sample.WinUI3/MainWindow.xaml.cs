using System.Linq;
using System.Numerics;
using CommunityToolkit.Labs.WinUI;
using Microsoft.Toolkit.Uwp.UI.Animations.ExpressionsFork;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CompositionCollectionView.Sample.WinUI3;

public sealed partial class MainWindow : Window
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

    public MainWindow()
    {
        this.InitializeComponent();
        SetupCardFanSample();
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
                FontWeight = FontWeights.Bold,
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
                case "Wave" when current is not WaveLayout:
                    current.TransitionTo(_ => new WaveLayout(current));
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
        private const string OneHzNode = nameof(OneHzNode);
        private const float CenterX = 350f;
        private const float CenterY = 175f;
        private const float RadiusX = 260f;
        private const float RadiusY = 110f;
        private const float OrbitSpeed = 0.05f;

        public OrbitLayout(Func<uint, FrameworkElement> factory) : base(factory) { }
        public OrbitLayout(CompositionCollectionLayout<uint, object?> source) : base(source) { }

        protected override void OnActivated()
        {
            var node = AnimatableNodes.GetOrCreateScalarNode(OneHzNode, 0);
            int iterations = 1000;
            var anim = Compositor.CreateScalarKeyFrameAnimation();
            anim.Duration = TimeSpan.FromMilliseconds(1000 * iterations);
            anim.InsertKeyFrame(0, 0);
            anim.InsertKeyFrame(1, 2f * Pi * iterations, Compositor.CreateLinearEasingFunction());
            anim.IterationBehavior = AnimationIterationBehavior.Forever;
            node.Animate(anim);
        }

        public override Vector3Node GetElementPositionNode(ElementReference<uint, object?> element)
        {
            var hzRef = AnimatableNodes.GetOrCreateScalarNode(OneHzNode, 0).Reference;
            float baseAngle = element.Id * (2f * Pi / CardCount) - Pi / 2f;
            var angle = hzRef * OrbitSpeed + baseAngle;
            var x = (ScalarNode)(CenterX - 40f) + ExpressionFunctions.Cos(angle) * RadiusX;
            var y = (ScalarNode)(CenterY - 60f) + ExpressionFunctions.Sin(angle) * RadiusY;
            return ExpressionFunctions.Vector3(x, y, 0);
        }

        public override ScalarNode GetElementScaleNode(ElementReference<uint, object?> element)
        {
            var hzRef = AnimatableNodes.GetOrCreateScalarNode(OneHzNode, 0).Reference;
            float baseAngle = element.Id * (2f * Pi / CardCount) - Pi / 2f;
            var angle = hzRef * OrbitSpeed + baseAngle;
            var depth = (ExpressionFunctions.Sin(angle) + 1f) / 2f;
            return 0.5f + 0.5f * depth;
        }

        public override ScalarNode GetElementOpacityNode(ElementReference<uint, object?> element)
        {
            var hzRef = AnimatableNodes.GetOrCreateScalarNode(OneHzNode, 0).Reference;
            float baseAngle = element.Id * (2f * Pi / CardCount) - Pi / 2f;
            var angle = hzRef * OrbitSpeed + baseAngle;
            var depth = (ExpressionFunctions.Sin(angle) + 1f) / 2f;
            return 0.4f + 0.6f * depth;
        }

        public override QuaternionNode GetElementOrientationNode(ElementReference<uint, object?> element)
            => Quaternion.Identity;

        protected override ElementTransition GetElementTransitionEasingFunction(ElementReference<uint, object?> element) =>
            new(400, Compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0f), new Vector2(0f, 1f)));

        protected override void ConfigureElement(ElementReference<uint, object?> element)
        {
            element.Container.SetValue(Canvas.ZIndexProperty, 50);
        }
    }

    /// <summary>
    /// Cards spread horizontally with Y positions continuously driven by a
    /// compositor-thread sine wave (AnimatableNode). Each card has a phase
    /// offset so the result is a travelling wave — no UI-thread ticking.
    /// Inspired by the phone-bobbing OneHzNode pattern in CardGame.
    /// </summary>
    private class WaveLayout : CompositionCollectionLayout<uint, object?>
    {
        private const string OneHzNode = nameof(OneHzNode);
        private const float Spacing = 55f;
        private const float BaseY = 150f;
        private const float Amplitude = 50f;
        private const float Speed = 1f;
        private const float PhaseStep = 0.55f;

        public WaveLayout(Func<uint, FrameworkElement> factory) : base(factory) { }
        public WaveLayout(CompositionCollectionLayout<uint, object?> source) : base(source) { }

        protected override void OnActivated()
        {
            var node = AnimatableNodes.GetOrCreateScalarNode(OneHzNode, 0);
            int iterations = 1000;
            var anim = Compositor.CreateScalarKeyFrameAnimation();
            anim.Duration = TimeSpan.FromMilliseconds(1000 * iterations);
            anim.InsertKeyFrame(0, 0);
            anim.InsertKeyFrame(1, 2f * Pi * iterations, Compositor.CreateLinearEasingFunction());
            anim.IterationBehavior = AnimationIterationBehavior.Forever;
            node.Animate(anim);
        }

        public override Vector3Node GetElementPositionNode(ElementReference<uint, object?> element)
        {
            var hzRef = AnimatableNodes.GetOrCreateScalarNode(OneHzNode, 0).Reference;
            float phase = element.Id * PhaseStep;
            var x = (ScalarNode)(element.Id * Spacing + 10f);
            var y = (ScalarNode)BaseY + ExpressionFunctions.Sin(hzRef * Speed + phase) * Amplitude;
            return ExpressionFunctions.Vector3(x, y, 0);
        }

        public override ScalarNode GetElementScaleNode(ElementReference<uint, object?> element) => 0.9f;

        public override QuaternionNode GetElementOrientationNode(ElementReference<uint, object?> element)
        {
            var hzRef = AnimatableNodes.GetOrCreateScalarNode(OneHzNode, 0).Reference;
            float phase = element.Id * PhaseStep;
            var tilt = ExpressionFunctions.Cos(hzRef * Speed + phase) * 0.15f;
            return ExpressionFunctions.Quaternion(0, 0, tilt, 1);
        }

        protected override ElementTransition GetElementTransitionEasingFunction(ElementReference<uint, object?> element) =>
            new(400, Compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0f), new Vector2(0f, 1f)));

        protected override void ConfigureElement(ElementReference<uint, object?> element)
        {
            element.Container.SetValue(Canvas.ZIndexProperty, (int)element.Id);
        }
    }

    #endregion
}
