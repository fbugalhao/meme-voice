namespace MemeVoice.Core.Effects;

public sealed class RobotEffect : IVoiceEffect
{
    // Low carrier frequency gives the classic monotone "robot buzz" character
    private readonly RingModulator _modulator = new(carrierFreq: 30);

    public void Process(float[] buffer, int sampleRate)
    {
        _modulator.Process(buffer, sampleRate);
    }
}
