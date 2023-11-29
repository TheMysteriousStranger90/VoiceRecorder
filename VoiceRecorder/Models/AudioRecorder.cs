using CSCore;
using CSCore.Codecs.WAV;
using CSCore.SoundIn;
using System;
using CSCore.CoreAudioAPI;
using CSCore.Streams;
using VoiceRecorder.Filters.Interfaces;

public class AudioRecorder : IDisposable
{
    private WasapiCapture capture;
    private WaveWriter writer;
    private bool disposed = false;
    private SoundInSource soundInSource;

    public IWaveSource CaptureSource => soundInSource;

    public void StartRecording(string outputFilePath, MMDevice device, IAudioFilter filter)
    {
        try
        {
            capture = new WasapiCapture();
            capture.Device = device;
            capture.Initialize();

            soundInSource = new SoundInSource(capture) { FillWithZeros = false };

            IWaveSource filteredSource;
            if (filter != null)
            {
                filteredSource = filter.ApplyFilter((IWaveSource)soundInSource);
            }
            else
            {
                filteredSource = soundInSource;
            }

            writer = new WaveWriter(outputFilePath, filteredSource.WaveFormat);

            byte[] buffer = new byte[filteredSource.WaveFormat.BytesPerSecond / 2];
            capture.DataAvailable += (s, e) =>
            {
                int read;
                while ((read = filteredSource.Read(buffer, 0, buffer.Length)) > 0)
                {
                    writer.Write(buffer, 0, read);
                }
            };

            capture.Start();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    public void StopRecording()
    {
        try
        {
            capture.Stop();
            writer.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    public void UpdateSource(IWaveSource newSource)
    {
        capture.Stop();

        soundInSource = newSource as SoundInSource;

        if (soundInSource != null)
        {
            capture.Start();
        }
        else
        {
            Console.WriteLine("newSource is not a SoundInSource");
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                if (capture != null)
                {
                    capture.Dispose();
                    capture = null;
                }

                if (writer != null)
                {
                    writer.Dispose();
                    writer = null;
                }
            }

            disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}