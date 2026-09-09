using System;

namespace MemeVoice.Core.Effects;

public sealed class BiquadFilter
{
    private double _b0, _b1, _b2, _a1, _a2;
    private double _x1, _x2, _y1, _y2;

    public void ConfigureBandpass(double centerFreq, double q, int sampleRate)
    {
        double omega = 2 * Math.PI * centerFreq / sampleRate;
        double alpha = Math.Sin(omega) / (2 * q);
        double cosOmega = Math.Cos(omega);

        double a0 = 1 + alpha;
        _b0 = alpha / a0;
        _b1 = 0;
        _b2 = -alpha / a0;
        _a1 = (-2 * cosOmega) / a0;
        _a2 = (1 - alpha) / a0;

        _x1 = _x2 = _y1 = _y2 = 0;
    }

    /// <summary>
    /// RBJ Audio EQ Cookbook highpass filter. Attenuates frequencies below
    /// <paramref name="cutoffFreq"/>, passes frequencies above it.
    /// </summary>
    public void ConfigureHighpass(double cutoffFreq, double q, int sampleRate)
    {
        double omega = 2 * Math.PI * cutoffFreq / sampleRate;
        double alpha = Math.Sin(omega) / (2 * q);
        double cosOmega = Math.Cos(omega);

        double a0 = 1 + alpha;
        _b0 = ((1 + cosOmega) / 2) / a0;
        _b1 = (-(1 + cosOmega)) / a0;
        _b2 = ((1 + cosOmega) / 2) / a0;
        _a1 = (-2 * cosOmega) / a0;
        _a2 = (1 - alpha) / a0;

        _x1 = _x2 = _y1 = _y2 = 0;
    }

    /// <summary>
    /// RBJ Audio EQ Cookbook lowpass filter. Passes frequencies below
    /// <paramref name="cutoffFreq"/>, attenuates frequencies above it.
    /// </summary>
    public void ConfigureLowpass(double cutoffFreq, double q, int sampleRate)
    {
        double omega = 2 * Math.PI * cutoffFreq / sampleRate;
        double alpha = Math.Sin(omega) / (2 * q);
        double cosOmega = Math.Cos(omega);

        double a0 = 1 + alpha;
        _b0 = ((1 - cosOmega) / 2) / a0;
        _b1 = (1 - cosOmega) / a0;
        _b2 = ((1 - cosOmega) / 2) / a0;
        _a1 = (-2 * cosOmega) / a0;
        _a2 = (1 - alpha) / a0;

        _x1 = _x2 = _y1 = _y2 = 0;
    }

    public float ProcessSample(float input)
    {
        double output = _b0 * input + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;

        _x2 = _x1;
        _x1 = input;
        _y2 = _y1;
        _y1 = output;

        return (float)output;
    }
}
