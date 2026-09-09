namespace MemeVoice.Core.Effects;

public sealed class DelayLine
{
    private float[] _buffer;
    private int _writeIndex;

    public DelayLine(int delaySamples)
    {
        _buffer = new float[System.Math.Max(1, delaySamples)];
        _writeIndex = 0;
    }

    public float Process(float input, float feedback, float wetMix)
    {
        float delayed = _buffer[_writeIndex];

        // Normalize the wet/dry sum by (1 + wetMix) to bring it near unity gain for typical
        // signal levels, then pass through Math.Tanh as a hard safety bound. Normalization alone
        // isn't sufficient: the feedback path's own steady-state gain is 1 / (1 - feedback), which
        // is independent of wetMix, so a sustained loud input (or a tone at the comb filter's
        // resonant frequency) can still exceed the normalized sum. Math.Tanh is near-identity for
        // the normalized sum's normal operating range (no audible compression under typical use)
        // but guarantees |output| < 1 even at worst-case steady-state gain (final whole-branch
        // review, Finding 4 -- unnormalized worst case was ~1.77 for feedback=0.35, wetMix=0.5).
        float normalized = (input + delayed * wetMix) / (1f + wetMix);
        float output = (float)System.Math.Tanh(normalized);

        _buffer[_writeIndex] = input + delayed * feedback;
        _writeIndex = (_writeIndex + 1) % _buffer.Length;

        return output;
    }
}
