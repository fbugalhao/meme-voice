using System.Collections.Generic;

namespace MemeVoice.Core.Effects;

public sealed record EffectPreset(string Name, IVoiceEffect Effect);

public static class EffectPresets
{
    public static IReadOnlyList<EffectPreset> GetAll()
    {
        return new List<EffectPreset>
        {
            new("Grave/Robusto", new PitchShiftEffect(-5f)),
            new("Agudo/Esquilo", new PitchShiftEffect(7f)),
            new("Robô", new RobotEffect()),
            new("Eco/Cavernão", new EchoEffect()),
            new("Pitch Livre", CreatePitchLivre(0f)),
            new("Demônio", new CompositeEffect(new PitchShiftEffect(-8f), new DistortionEffect(3f))),
            new("Chipmunk Extremo", new PitchShiftEffect(12f)),
            new("Telefone/Rádio", new TelephoneEffect()),
            new("Alien/Interferência", new AlienEffect()),
        };
    }

    public static PitchShiftEffect CreatePitchLivre(float semitones)
    {
        return new PitchShiftEffect(semitones);
    }
}
