namespace MemeVoice.Core.Effects;

public sealed class EchoEffect : IVoiceEffect
{
    private DelayLine? _delayLine;
    private int _configuredSampleRate = -1;
    private const double DelaySeconds = 0.18;
    private const float Feedback = 0.35f;
    private const float WetMix = 0.5f;

    public void Process(float[] buffer, int sampleRate)
    {
        if (_configuredSampleRate != sampleRate)
        {
            _delayLine = new DelayLine((int)(sampleRate * DelaySeconds));
            _configuredSampleRate = sampleRate;
        }

        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = _delayLine!.Process(buffer[i], Feedback, WetMix);
        }
    }
}
