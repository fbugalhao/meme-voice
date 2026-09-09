namespace MemeVoice.Core.Effects;

public sealed class TelephoneEffect : IVoiceEffect
{
    // Butterworth Q (1/sqrt(2)) gives a maximally-flat passband with no resonant peaking,
    // so cascading these two stages approximates a real 300-3400Hz telephone band instead
    // of a narrow resonant bump.
    private const double ButterworthQ = 0.7071067811865476;

    private readonly BiquadFilter _highpass = new();
    private readonly BiquadFilter _lowpass = new();
    private int _configuredSampleRate = -1;

    public void Process(float[] buffer, int sampleRate)
    {
        if (_configuredSampleRate != sampleRate)
        {
            // Classic telephone band: attenuate below ~300Hz and above ~3400Hz.
            _highpass.ConfigureHighpass(cutoffFreq: 300, ButterworthQ, sampleRate);
            _lowpass.ConfigureLowpass(cutoffFreq: 3400, ButterworthQ, sampleRate);
            _configuredSampleRate = sampleRate;
        }

        for (int i = 0; i < buffer.Length; i++)
        {
            float sample = _highpass.ProcessSample(buffer[i]);
            sample = _lowpass.ProcessSample(sample);
            buffer[i] = sample;
        }
    }
}
