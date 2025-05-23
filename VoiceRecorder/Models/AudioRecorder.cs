﻿using System;
using System.Diagnostics;
using CSCore;
using CSCore.Codecs.WAV;
using CSCore.CoreAudioAPI;
using CSCore.SoundIn;
using CSCore.Streams;
using VoiceRecorder.Exceptions;
using VoiceRecorder.Filters.Interfaces;

namespace VoiceRecorder.Models;

public sealed class AudioRecorder : IDisposable
{
    private WasapiCapture _capture;
    private WaveWriter _writer;
    private bool _disposed = false;
    private SoundInSource _soundInSource;

    public IWaveSource CaptureSource => _soundInSource;

    public void StartRecording(string outputFilePath, MMDevice device, IAudioFilter filter)
    {
        try
        {
            _capture = new WasapiCapture(true, AudioClientShareMode.Shared, 100);
            _capture.Device = device;
            _capture.Initialize();

            _soundInSource = new SoundInSource(_capture) { FillWithZeros = false };

            IWaveSource filteredSource;
            if (filter != null)
            {
                filteredSource = filter.ApplyFilter((IWaveSource)_soundInSource);
            }
            else
            {
                filteredSource = _soundInSource;
            }

            _writer = new WaveWriter(outputFilePath, filteredSource.WaveFormat);

            byte[] buffer = new byte[filteredSource.WaveFormat.BytesPerSecond / 2];
            _capture.DataAvailable += (s, e) =>
            {
                int read;
                while ((read = filteredSource.Read(buffer, 0, buffer.Length)) > 0)
                {
                    _writer.Write(buffer, 0, read);
                }
            };

            _capture.Start();
        }
        catch (CoreAudioAPIException ex) when (ex.ErrorCode == unchecked((int)0x80070005)) // Access Denied
        {
            throw new UnauthorizedAccessException("Microphone access is denied. Please check your privacy settings.", ex);
        }
        catch (Exception ex)
        {
            throw new AudioRecorderException("Failed to start recording", ex);
        }
    }
    
    public void StopRecording()
    {
        try
        {
            _capture.Stop();
            _writer.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    public void UpdateSource(IWaveSource newSource)
    {
        _capture.Stop();

        _soundInSource = newSource as SoundInSource;

        if (_soundInSource != null)
        {
            _capture.Start();
        }
        else
        {
            Console.WriteLine("newSource is not a SoundInSource");
        }
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                if (_capture != null)
                {
                    _capture.Dispose();
                    _capture = null;
                }

                if (_writer != null)
                {
                    _writer.Dispose();
                    _writer = null;
                }
            }

            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}