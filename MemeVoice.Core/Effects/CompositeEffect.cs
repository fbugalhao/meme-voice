namespace MemeVoice.Core.Effects;

public sealed class CompositeEffect : IVoiceEffect
{
    private readonly IVoiceEffect[] _stages;

    public CompositeEffect(params IVoiceEffect[] stages)
    {
        _stages = stages;
    }

    public void Process(float[] buffer, int sampleRate)
    {
        foreach (var stage in _stages)
        {
            stage.Process(buffer, sampleRate);
        }
    }
}
