using System;

namespace MemeVoice.Core.Effects;

public sealed class DistortionEffect : IVoiceEffect
{
    private readonly float _drive;

    public DistortionEffect(float drive)
    {
        _drive = drive;
    }

    public void Process(float[] buffer, int sampleRate)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (float)Math.Tanh(buffer[i] * _drive);
        }
    }
}
