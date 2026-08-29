using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;

namespace AuraVault.App.Aura;

/// <summary>
/// The animated "aura" background: a few soft radial colour blobs drifting on sine paths.
/// Pure Avalonia rendering, one control, GPU-composited. The render-thread SKSL mesh-gradient
/// is a later optimisation (see docs/plan.md, Sektor 5 risks).
/// </summary>
public sealed class AuraLayer : Control
{
    public static readonly StyledProperty<double> IntensityProperty =
        AvaloniaProperty.Register<AuraLayer, double>(nameof(Intensity), 0.7);

    public static readonly StyledProperty<bool> AnimatedProperty =
        AvaloniaProperty.Register<AuraLayer, bool>(nameof(Animated), true);

    public static readonly StyledProperty<Color> AccentProperty =
        AvaloniaProperty.Register<AuraLayer, Color>(nameof(Accent), Color.Parse("#7C5CFF"));

    public static readonly StyledProperty<Color> Accent2Property =
        AvaloniaProperty.Register<AuraLayer, Color>(nameof(Accent2), Color.Parse("#33D6C4"));

    public static readonly StyledProperty<Color> BaseColorProperty =
        AvaloniaProperty.Register<AuraLayer, Color>(nameof(BaseColor), Color.Parse("#0B0B10"));

    private readonly (double X, double Y, double R, double Phase, int Which)[] _orbs =
    [
        (0.22, 0.28, 0.55, 0.0, 0),
        (0.78, 0.20, 0.50, 2.1, 1),
        (0.65, 0.82, 0.62, 4.0, 2),
        (0.15, 0.85, 0.42, 5.4, 0),
    ];

    private double _time;
    private TimeSpan _last;
    private bool _subscribed;

    public AuraLayer()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
    }

    public double Intensity { get => GetValue(IntensityProperty); set => SetValue(IntensityProperty, value); }

    public bool Animated { get => GetValue(AnimatedProperty); set => SetValue(AnimatedProperty, value); }

    public Color Accent { get => GetValue(AccentProperty); set => SetValue(AccentProperty, value); }

    public Color Accent2 { get => GetValue(Accent2Property); set => SetValue(Accent2Property, value); }

    public Color BaseColor { get => GetValue(BaseColorProperty); set => SetValue(BaseColorProperty, value); }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Subscribe();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _subscribed = false;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AnimatedProperty || change.Property == IntensityProperty)
        {
            if (Animated && Intensity > 0)
            {
                Subscribe();
            }

            InvalidateVisual();
        }
        else if (change.Property == AccentProperty || change.Property == Accent2Property || change.Property == BaseColorProperty)
        {
            InvalidateVisual();
        }
    }

    private void Subscribe()
    {
        if (_subscribed || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        _subscribed = true;
        _last = TimeSpan.Zero;
        top.RequestAnimationFrame(Tick);
    }

    private void Tick(TimeSpan now)
    {
        if (!_subscribed || TopLevel.GetTopLevel(this) is not { } top)
        {
            _subscribed = false;
            return;
        }

        double dt = _last == TimeSpan.Zero ? 0 : (now - _last).TotalSeconds;
        _last = now;

        bool animating = Animated && Intensity > 0 && !MotionScale.ReducedMotion && IsEffectivelyVisible;
        if (animating)
        {
            _time += dt * 0.15 * (MotionScale.Speed <= 0 ? 1 : MotionScale.Speed);
            InvalidateVisual();
        }

        // Keep the subscription alive; cheap when not animating.
        top.RequestAnimationFrame(Tick);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(new ImmutableSolidColorBrush(BaseColor), bounds);

        double intensity = Math.Clamp(Intensity, 0, 1);
        if (intensity <= 0.001 || bounds.Width < 1 || bounds.Height < 1)
        {
            return;
        }

        double w = bounds.Width, h = bounds.Height;
        double diag = Math.Sqrt((w * w) + (h * h));

        foreach (var orb in _orbs)
        {
            double driftX = MotionScale.ReducedMotion ? 0 : Math.Sin(_time + orb.Phase) * 0.06;
            double driftY = MotionScale.ReducedMotion ? 0 : Math.Cos((_time * 0.8) + orb.Phase) * 0.05;
            var center = new Point((orb.X + driftX) * w, (orb.Y + driftY) * h);
            double radius = orb.R * diag * 0.5;

            Color color = orb.Which switch
            {
                1 => Accent2,
                2 => Mix(Accent, Accent2, 0.5),
                _ => Accent,
            };

            byte peak = (byte)(70 * intensity);
            var brush = new RadialGradientBrush
            {
                GradientOrigin = new RelativePoint(center, RelativeUnit.Absolute),
                Center = new RelativePoint(center, RelativeUnit.Absolute),
                RadiusX = new RelativeScalar(radius, RelativeUnit.Absolute),
                RadiusY = new RelativeScalar(radius, RelativeUnit.Absolute),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(peak, color.R, color.G, color.B), 0),
                    new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1),
                },
            };

            context.FillRectangle(brush, bounds);
        }
    }

    private static Color Mix(Color a, Color b, double t) => Color.FromArgb(
        255,
        (byte)((a.R * (1 - t)) + (b.R * t)),
        (byte)((a.G * (1 - t)) + (b.G * t)),
        (byte)((a.B * (1 - t)) + (b.B * t)));
}
