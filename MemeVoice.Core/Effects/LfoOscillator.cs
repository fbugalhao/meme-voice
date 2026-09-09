using System;

namespace MemeVoice.Core.Effects;

public sealed class LfoOscillator
{
    private readonly double _frequency;
    private double _phase;

    public LfoOscillator(double frequency)
    {
        _frequency = frequency;
    }

    public float NextValue(int sampleRate)
    {
        double value = Math.Sin(_phase);
        _phase += 2 * Math.PI * _frequency / sampleRate;
        if (_phase > 2 * Math.PI) _phase -= 2 * Math.PI;
        return (float)value;
    }
}
