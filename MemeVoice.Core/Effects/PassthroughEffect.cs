namespace MemeVoice.Core.Effects;

public sealed class PassthroughEffect : IVoiceEffect
{
    public void Process(float[] buffer, int sampleRate)
    {
        // no-op: leaves buffer unchanged
    }
}
