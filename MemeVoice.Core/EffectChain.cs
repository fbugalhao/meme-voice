using MemeVoice.Core.Effects;

namespace MemeVoice.Core;

public sealed class EffectChain
{
    private volatile IVoiceEffect _current;

    public EffectChain(IVoiceEffect initial)
    {
        _current = initial;
    }

    public IVoiceEffect Current => _current;

    public void SetEffect(IVoiceEffect effect)
    {
        _current = effect; // reference assignment is atomic; safe swap from UI thread
    }

    public void Process(float[] buffer, int sampleRate)
    {
        _current.Process(buffer, sampleRate);
    }
}
