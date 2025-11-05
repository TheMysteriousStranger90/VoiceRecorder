using CSCore;
using CSCore.Streams.Effects;
using VoiceRecorder.Filters.Interfaces;

namespace VoiceRecorder.Filters;

internal sealed class DistortionFilter : IAudioFilter
{
    public IWaveSource ApplyFilter(IWaveSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var distortionEffect = new DmoDistortionEffect(source)
        {
            Gain = -15,
            PostEQCenterFrequency = 300f,
            PostEQBandwidth = 2000f
        };

        return distortionEffect;
    }
}
