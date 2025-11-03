using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using System.Security.Cryptography;

namespace VoiceRecorder.Controls;

public class AudioVisualizerControl : TemplatedControl
{
    private readonly List<double> _barHeights;
    private readonly DispatcherTimer _timer;
    private const int BarCount = 32;
    private readonly RandomNumberGenerator _random;

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<AudioVisualizerControl, bool>(
            nameof(IsActive),
            defaultValue: false);

    public new static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<AudioVisualizerControl, CornerRadius>(
            nameof(CornerRadius),
            new CornerRadius(0));

    public new CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public AudioVisualizerControl()
    {
        _random = RandomNumberGenerator.Create();
        _barHeights = new List<double>(BarCount);
        for (int i = 0; i < BarCount; i++)
        {
            _barHeights.Add(0);
        }

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _timer.Tick += Timer_Tick;
        _timer.Start();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        UpdateBars();
        InvalidateVisual();
    }

    private void UpdateBars()
    {
        for (int i = 0; i < BarCount; i++)
        {
            if (IsActive)
            {
                _barHeights[i] = Math.Max(0.1, Math.Min(1.0,
                    _barHeights[i] + (GetSecureRandomDouble() * 0.2 - 0.1)));
            }
            else
            {
                _barHeights[i] = Math.Max(0, _barHeights[i] - 0.05);
            }
        }
    }

    private double GetSecureRandomDouble()
    {
        var bytes = new byte[8];
        _random.GetBytes(bytes);
        ulong uint64 = BitConverter.ToUInt64(bytes, 0) / (1UL << 11);
        return uint64 / (double)(1UL << 53);
    }

    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var rect = new Rect(Bounds.Size);
        context.FillRectangle(Brushes.Transparent, rect);

        float barWidth = (float)Bounds.Width / BarCount;

        for (int i = 0; i < BarCount; i++)
        {
            var barHeight = _barHeights[i] * Bounds.Height;
            var barRect = new Rect(
                i * barWidth,
                Bounds.Height - barHeight,
                barWidth - 1,
                barHeight);

            var gradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                GradientStops =
                [
                    new GradientStop(Colors.Purple, 0),
                    new GradientStop(Colors.Magenta, 1)
                ]
            };

            context.FillRectangle(gradient, barRect);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer.Stop();
    }
}
