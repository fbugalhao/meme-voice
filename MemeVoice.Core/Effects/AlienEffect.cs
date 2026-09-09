namespace MemeVoice.Core.Effects;

public sealed class AlienEffect : IVoiceEffect
{
    private readonly LfoOscillator _lfo = new(frequency: 6.0);
    private const float Depth = 0.6f; // how strongly the LFO swings the gain

    public void Process(float[] buffer, int sampleRate)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            float lfoValue = _lfo.NextValue(sampleRate); // -1..1
            float gain = 1f - Depth * (0.5f * (lfoValue + 1f)); // maps to (1-Depth)..1
            buffer[i] *= gain;
        }
    }
}
