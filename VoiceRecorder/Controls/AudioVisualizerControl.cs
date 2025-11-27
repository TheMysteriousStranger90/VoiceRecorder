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
    private readonly object _dataLock = new();
    private readonly float[] _audioData = new float[BarCount];

    private const double MinimumBarHeight = 0.08;
    private const double AmplificationFactor = 15.0;
    private const double SmoothingFactor = 0.65;
    private const double DecaySpeed = 0.08;

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
        _barHeights = new List<double>(BarCount);
        for (int i = 0; i < BarCount; i++)
        {
            _barHeights.Add(0);
        }

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(40)
        };
        _timer.Tick += Timer_Tick;
        _timer.Start();
    }

    public void UpdateAudioData(float[] samples)
    {
        if (samples == null || samples.Length == 0) return;

        lock (_dataLock)
        {
            int samplesPerBar = samples.Length / BarCount;
            if (samplesPerBar < 1) samplesPerBar = 1;

            for (int i = 0; i < BarCount; i++)
            {
                float sum = 0;
                float maxSample = 0;
                int startIndex = i * samplesPerBar;
                int endIndex = Math.Min(startIndex + samplesPerBar, samples.Length);

                for (int j = startIndex; j < endIndex; j++)
                {
                    float absSample = Math.Abs(samples[j]);
                    sum += absSample;
                    if (absSample > maxSample)
                        maxSample = absSample;
                }

                float average = sum / (endIndex - startIndex);
                _audioData[i] = (average * 0.7f) + (maxSample * 0.3f);
            }
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        UpdateBars();
        InvalidateVisual();
    }

    private void UpdateBars()
    {
        lock (_dataLock)
        {
            for (int i = 0; i < BarCount; i++)
            {
                if (IsActive)
                {
                    double rawValue = _audioData[i] * AmplificationFactor;
                    double targetHeight = rawValue > 0.001
                        ? Math.Min(1.0, Math.Log10(1 + rawValue * 9) / Math.Log10(10))
                        : MinimumBarHeight;

                    _barHeights[i] = _barHeights[i] * SmoothingFactor + targetHeight * (1 - SmoothingFactor);

                    if (_barHeights[i] < MinimumBarHeight)
                        _barHeights[i] = MinimumBarHeight;
                }
                else
                {
                    _barHeights[i] = Math.Max(0, _barHeights[i] - DecaySpeed);
                }
            }
        }
    }

    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var rect = new Rect(Bounds.Size);
        context.FillRectangle(Brushes.Transparent, rect);

        float barWidth = (float)Bounds.Width / BarCount;
        float spacing = 1f;

        for (int i = 0; i < BarCount; i++)
        {
            var barHeight = _barHeights[i] * Bounds.Height;
            var barRect = new Rect(
                i * barWidth,
                Bounds.Height - barHeight,
                Math.Max(1, barWidth - spacing),
                barHeight);

            var gradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                GradientStops =
                [
                    new GradientStop(Colors.Purple, 0),
                    new GradientStop(Colors.Magenta, 0.5),
                    new GradientStop(Color.FromRgb(255, 100, 255), 1.0)
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
