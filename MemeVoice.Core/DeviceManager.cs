// src/MemeVoice.Core/DeviceManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.CoreAudioApi;

namespace MemeVoice.Core;

public sealed record AudioDeviceInfo(string Id, string Name);

public sealed class DeviceManager
{
    private const string VbCableNameFragment = "CABLE Input";

    public IReadOnlyList<AudioDeviceInfo> GetInputDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .Select(d => new AudioDeviceInfo(d.ID, d.FriendlyName))
            .ToList();
    }

    public IReadOnlyList<AudioDeviceInfo> GetOutputDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .Select(d => new AudioDeviceInfo(d.ID, d.FriendlyName))
            .ToList();
    }

    public bool IsVbCableInstalled()
    {
        return GetOutputDevices().Any(d => d.Name.Contains(VbCableNameFragment, StringComparison.OrdinalIgnoreCase));
    }
}
