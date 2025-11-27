namespace VoiceRecorder.Models;

public class AudioDataEventArgs : EventArgs
{
    public float[] Samples { get; }

    public AudioDataEventArgs(float[] samples)
    {
        Samples = samples;
    }
}
