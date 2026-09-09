namespace MemeVoice.Core.Effects;

public interface IVoiceEffect
{
    void Process(float[] buffer, int sampleRate);
}
