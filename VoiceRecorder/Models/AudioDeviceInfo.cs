using System.Diagnostics.CodeAnalysis;
using CSCore.CoreAudioAPI;

namespace VoiceRecorder.Models;

[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "Model used across assemblies")]
public sealed class AudioDeviceInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public DeviceState State { get; init; }
    public bool IsDefault { get; init; }
    public int SampleRate { get; init; }
    public int Channels { get; init; }

    public string DisplayName => IsDefault ? $"{Name} (Default)" : Name;

    public string FormatInfo => $"{SampleRate}Hz, {Channels}ch";

    public override string ToString() => DisplayName;
}
