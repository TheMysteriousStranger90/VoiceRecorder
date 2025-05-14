using CSCore;
using CSCore.Streams.Effects;
using VoiceRecorder.Filters.Interfaces;

namespace VoiceRecorder.Filters;

public class DistortionFilter : IAudioFilter
{
    public IWaveSource ApplyFilter(IWaveSource source)
    {
        var distortionEffect = new DmoDistortionEffect(source);

        distortionEffect.Gain = -10;
        distortionEffect.PostEQCenterFrequency = 200f;
        distortionEffect.PostEQBandwidth = 1500f;

        return distortionEffect;
    }
}