using System;

namespace MemeVoice.Core.Effects;

public sealed class RingModulator
{
    private readonly double _carrierFreq;
    private double _phase;

    public RingModulator(double carrierFreq)
    {
        _carrierFreq = carrierFreq;
    }

    public void Process(float[] buffer, int sampleRate)
    {
        double phaseIncrement = 2 * Math.PI * _carrierFreq / sampleRate;

        for (int i = 0; i < buffer.Length; i++)
        {
            double carrier = Math.Sin(_phase);
            buffer[i] = (float)(buffer[i] * carrier);
            _phase += phaseIncrement;
            if (_phase > 2 * Math.PI) _phase -= 2 * Math.PI;
        }
    }
}
