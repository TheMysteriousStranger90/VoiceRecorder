using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;

namespace VoiceRecorder.Controls;

public class AudioVisualizerControl : TemplatedControl
{
    private readonly List<double> _barHeights;
    private readonly DispatcherTimer _timer;
    private const int BarCount = 32;
    private readonly Random _random;

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<AudioVisualizerControl, bool>(
            nameof(IsActive),
            defaultValue: false);

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<AudioVisualizerControl, CornerRadius>(
            nameof(CornerRadius),
            new CornerRadius(0));

    public CornerRadius CornerRadius
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
        _random = new Random();
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

    private void Timer_Tick(object sender, EventArgs e)
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
                    _barHeights[i] + (_random.NextDouble() * 0.2 - 0.1)));
            }
            else
            {
                _barHeights[i] = Math.Max(0, _barHeights[i] - 0.05);
            }
        }
    }

    public override void Render(DrawingContext context)
    {
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
                GradientStops = new GradientStops
                {
                    new GradientStop(Colors.Purple, 0),
                    new GradientStop(Colors.Magenta, 1)
                }
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