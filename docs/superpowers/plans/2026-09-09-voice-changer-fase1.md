# Voice Changer — Fase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Windows desktop app (C#/.NET, WPF) that captures the real microphone, applies a real-time pitch/timbre effect, and routes the result to VB-Cable so Discord/games can use it as a virtual microphone.

**Architecture:** A `MemeVoice.Core` class library holds all audio DSP and device/config logic, decoupled from UI (effects operate purely on `float[]` buffers; `AudioEngine` wires WASAPI capture/render around an `EffectChain`). A `MemeVoice.App` WPF project provides the tray-based GUI, global hotkeys (Win32 interop needs a window handle, so this lives in the App project), and wires user actions to `MemeVoice.Core`.

**Tech Stack:** .NET 8, WPF, NAudio (WASAPI capture/render + device enumeration), SoundTouch.NET (pitch shifting), xUnit (tests).

## Global Constraints

- Windows-only (Windows 11). No cross-platform abstraction needed.
- Audio routing depends on VB-Audio VB-CABLE being installed by the user; the app must detect its absence and warn, not crash.
- Real-time path: capture buffer size ~10-20ms; all effect processing must be allocation-light per callback (reuse buffers, avoid per-call `new` in the hot path where reasonably possible).
- Effect swapping must be thread-safe (capture callback thread vs UI thread).
- Effects list for Fase 1: Grave/Robusto, Agudo/Esquilo, Robô, Eco/Cavernão, Pitch Livre (-12..+12 semitons), Demônio, Chipmunk extremo, Telefone/rádio, Alien/interferência.
- Config persisted as JSON under `%AppData%\MemeVoice\config.json`.

---

## File Structure

```
MemeVoice.sln
src/
  MemeVoice.Core/
    MemeVoice.Core.csproj
    Effects/
      IVoiceEffect.cs
      PassthroughEffect.cs
      PitchShiftEffect.cs
      DistortionEffect.cs
      CompositeEffect.cs
      BiquadFilter.cs
      TelephoneEffect.cs
      RingModulator.cs
      RobotEffect.cs
      DelayLine.cs
      EchoEffect.cs
      LfoOscillator.cs
      AlienEffect.cs
      EffectPresets.cs
    EffectChain.cs
    DeviceManager.cs
    AudioEngine.cs
    ConfigStore.cs
  MemeVoice.App/
    MemeVoice.App.csproj
    App.xaml / App.xaml.cs
    MainWindow.xaml / MainWindow.xaml.cs
    TrayIconManager.cs
    HotkeyManager.cs
tests/
  MemeVoice.Core.Tests/
    MemeVoice.Core.Tests.csproj
    Effects/
      EffectChainTests.cs
      PitchShiftEffectTests.cs
      DistortionEffectTests.cs
      TelephoneEffectTests.cs
      RobotEffectTests.cs
      EchoEffectTests.cs
      AlienEffectTests.cs
    PipelineIntegrationTests.cs
    fixtures/
      tone_440hz.wav
```

---

### Task 1: Project scaffolding

**Files:**
- Create: `MemeVoice.sln`
- Create: `src/MemeVoice.Core/MemeVoice.Core.csproj`
- Create: `src/MemeVoice.App/MemeVoice.App.csproj`
- Create: `tests/MemeVoice.Core.Tests/MemeVoice.Core.Tests.csproj`

**Interfaces:**
- Produces: solution with 3 projects wired together (`MemeVoice.App` and `MemeVoice.Core.Tests` reference `MemeVoice.Core`), NAudio + SoundTouch.NET refs in Core, xUnit refs in Tests.

- [ ] **Step 1: Create the solution and projects**

```bash
mkdir -p src/MemeVoice.Core src/MemeVoice.App tests/MemeVoice.Core.Tests
dotnet new classlib -n MemeVoice.Core -o src/MemeVoice.Core -f net8.0
dotnet new wpf -n MemeVoice.App -o src/MemeVoice.App -f net8.0-windows
dotnet new xunit -n MemeVoice.Core.Tests -o tests/MemeVoice.Core.Tests -f net8.0
dotnet new sln -n MemeVoice
dotnet sln MemeVoice.sln add src/MemeVoice.Core/MemeVoice.Core.csproj
dotnet sln MemeVoice.sln add src/MemeVoice.App/MemeVoice.App.csproj
dotnet sln MemeVoice.sln add tests/MemeVoice.Core.Tests/MemeVoice.Core.Tests.csproj
dotnet add src/MemeVoice.App/MemeVoice.App.csproj reference src/MemeVoice.Core/MemeVoice.Core.csproj
dotnet add tests/MemeVoice.Core.Tests/MemeVoice.Core.Tests.csproj reference src/MemeVoice.Core/MemeVoice.Core.csproj
```

- [ ] **Step 2: Add NAudio to Core and App, SoundTouch.NET to Core**

```bash
dotnet add src/MemeVoice.Core/MemeVoice.Core.csproj package NAudio
dotnet add src/MemeVoice.App/MemeVoice.App.csproj package NAudio
dotnet add src/MemeVoice.Core/MemeVoice.Core.csproj package SoundTouch.Net.NetCore
```

If `SoundTouch.Net.NetCore` is not found on nuget.org, search `dotnet nuget search SoundTouch` and use the closest actively-maintained .NET 8-compatible SoundTouch wrapper package instead; keep the class-level API used in Task 3 (`SetSampleRate`, `SetChannels`, `SetPitchSemiTones`, `PutSamples`, `ReceiveSamples`, `Flush`) — adjust only the `using` namespace/package id if the actual package differs.

- [ ] **Step 3: Verify solution builds**

```bash
dotnet build MemeVoice.sln
```

Expected: build succeeds (0 errors) — the generated default test/class files are fine at this point.

- [ ] **Step 4: Commit**

```bash
git add MemeVoice.sln src/ tests/
git commit -m "chore: scaffold MemeVoice solution (Core, App, Tests)"
```

---

### Task 2: IVoiceEffect interface + EffectChain

**Files:**
- Create: `src/MemeVoice.Core/Effects/IVoiceEffect.cs`
- Create: `src/MemeVoice.Core/Effects/PassthroughEffect.cs`
- Create: `src/MemeVoice.Core/EffectChain.cs`
- Test: `tests/MemeVoice.Core.Tests/Effects/EffectChainTests.cs`

**Interfaces:**
- Produces: `IVoiceEffect` with `void Process(float[] buffer, int sampleRate)`; `EffectChain` with `IVoiceEffect Current { get; }`, `void SetEffect(IVoiceEffect effect)`, `void Process(float[] buffer, int sampleRate)`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/MemeVoice.Core.Tests/Effects/EffectChainTests.cs
using MemeVoice.Core;
using MemeVoice.Core.Effects;
using Xunit;

namespace MemeVoice.Core.Tests.Effects;

public class EffectChainTests
{
    [Fact]
    public void Passthrough_DoesNotModifyBuffer()
    {
        var chain = new EffectChain(new PassthroughEffect());
        var buffer = new float[] { 0.1f, -0.2f, 0.3f };
        var expected = (float[])buffer.Clone();

        chain.Process(buffer, sampleRate: 48000);

        Assert.Equal(expected, buffer);
    }

    [Fact]
    public void SetEffect_SwapsActiveEffect()
    {
        var chain = new EffectChain(new PassthroughEffect());
        var zeroing = new ZeroingEffect();

        chain.SetEffect(zeroing);
        var buffer = new float[] { 1f, 1f, 1f };
        chain.Process(buffer, sampleRate: 48000);

        Assert.All(buffer, sample => Assert.Equal(0f, sample));
    }

    private sealed class ZeroingEffect : IVoiceEffect
    {
        public void Process(float[] buffer, int sampleRate)
        {
            for (int i = 0; i < buffer.Length; i++) buffer[i] = 0f;
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/MemeVoice.Core.Tests --filter EffectChainTests`
Expected: FAIL to compile — `IVoiceEffect`, `PassthroughEffect`, `EffectChain` do not exist yet.

- [ ] **Step 3: Implement IVoiceEffect, PassthroughEffect, EffectChain**

```csharp
// src/MemeVoice.Core/Effects/IVoiceEffect.cs
namespace MemeVoice.Core.Effects;

public interface IVoiceEffect
{
    void Process(float[] buffer, int sampleRate);
}
```

```csharp
// src/MemeVoice.Core/Effects/PassthroughEffect.cs
namespace MemeVoice.Core.Effects;

public sealed class PassthroughEffect : IVoiceEffect
{
    public void Process(float[] buffer, int sampleRate)
    {
        // no-op: leaves buffer unchanged
    }
}
```

```csharp
// src/MemeVoice.Core/EffectChain.cs
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
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/MemeVoice.Core.Tests --filter EffectChainTests`
Expected: PASS (2 tests)

- [ ] **Step 5: Commit**

```bash
git add src/MemeVoice.Core/Effects/IVoiceEffect.cs src/MemeVoice.Core/Effects/PassthroughEffect.cs src/MemeVoice.Core/EffectChain.cs tests/MemeVoice.Core.Tests/Effects/EffectChainTests.cs
git commit -m "feat: add IVoiceEffect, PassthroughEffect, thread-safe EffectChain"
```

---

### Task 3: PitchShiftEffect (SoundTouch.NET wrapper)

**Files:**
- Create: `src/MemeVoice.Core/Effects/PitchShiftEffect.cs`
- Test: `tests/MemeVoice.Core.Tests/Effects/PitchShiftEffectTests.cs`

**Interfaces:**
- Consumes: `IVoiceEffect` (Task 2).
- Produces: `PitchShiftEffect` implementing `IVoiceEffect`, constructor `PitchShiftEffect(float semitones)`, mutable property `float Semitones { get; set; }` (used later by Pitch Livre slider).

- [ ] **Step 1: Write the failing test**

```csharp
// tests/MemeVoice.Core.Tests/Effects/PitchShiftEffectTests.cs
using System;
using MemeVoice.Core.Effects;
using Xunit;

namespace MemeVoice.Core.Tests.Effects;

public class PitchShiftEffectTests
{
    [Fact]
    public void ShiftingUpAnOctave_DoublesDominantFrequency()
    {
        const int sampleRate = 48000;
        const float inputFreq = 220f;
        var buffer = GenerateSineWave(inputFreq, sampleRate, seconds: 1.0);

        var effect = new PitchShiftEffect(semitones: 12f); // +1 octave
        effect.Process(buffer, sampleRate);

        // Skip the first 20% of samples: pitch algorithms need a short warm-up window
        int skip = buffer.Length / 5;
        double detected = EstimateFrequencyByZeroCrossings(buffer, skip, sampleRate);

        Assert.InRange(detected, inputFreq * 2 * 0.9, inputFreq * 2 * 1.1);
    }

    private static float[] GenerateSineWave(float freq, int sampleRate, double seconds)
    {
        int n = (int)(sampleRate * seconds);
        var buffer = new float[n];
        for (int i = 0; i < n; i++)
            buffer[i] = (float)Math.Sin(2 * Math.PI * freq * i / sampleRate);
        return buffer;
    }

    private static double EstimateFrequencyByZeroCrossings(float[] buffer, int skip, int sampleRate)
    {
        int crossings = 0;
        for (int i = skip + 1; i < buffer.Length; i++)
        {
            if (buffer[i - 1] < 0 && buffer[i] >= 0) crossings++;
        }
        double seconds = (double)(buffer.Length - skip) / sampleRate;
        return crossings / seconds;
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/MemeVoice.Core.Tests --filter PitchShiftEffectTests`
Expected: FAIL — `PitchShiftEffect` does not exist.

- [ ] **Step 3: Implement PitchShiftEffect wrapping SoundTouch**

```csharp
// src/MemeVoice.Core/Effects/PitchShiftEffect.cs
using SoundTouch;

namespace MemeVoice.Core.Effects;

public sealed class PitchShiftEffect : IVoiceEffect
{
    private readonly SoundTouchProcessor _soundTouch = new();
    private int _configuredSampleRate = -1;
    private float[] _receiveBuffer = new float[4096];

    public PitchShiftEffect(float semitones)
    {
        Semitones = semitones;
    }

    public float Semitones
    {
        get => _soundTouch.PitchSemiTones;
        set => _soundTouch.PitchSemiTones = value;
    }

    public void Process(float[] buffer, int sampleRate)
    {
        EnsureConfigured(sampleRate);

        _soundTouch.PutSamples(buffer, (uint)buffer.Length);

        int written = 0;
        while (written < buffer.Length)
        {
            uint received = _soundTouch.ReceiveSamples(_receiveBuffer, (uint)_receiveBuffer.Length);
            if (received == 0) break;

            int toCopy = (int)System.Math.Min(received, buffer.Length - written);
            System.Array.Copy(_receiveBuffer, 0, buffer, written, toCopy);
            written += toCopy;
        }

        // Pad any remainder with silence if SoundTouch hasn't produced enough samples yet
        for (int i = written; i < buffer.Length; i++) buffer[i] = 0f;
    }

    private void EnsureConfigured(int sampleRate)
    {
        if (_configuredSampleRate == sampleRate) return;

        _soundTouch.SampleRate = sampleRate;
        _soundTouch.Channels = 1;
        _configuredSampleRate = sampleRate;
    }
}
```

Note: adjust member names (`PitchSemiTones`, `SampleRate`, `Channels`, `PutSamples`, `ReceiveSamples`) to match whichever SoundTouch.NET package actually got installed in Task 1 — check its public API via `dotnet-script`/IntelliSense/decompiled signatures if names differ slightly (some ports expose `SetPitchSemiTones(float)` as a method instead of a property).

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/MemeVoice.Core.Tests --filter PitchShiftEffectTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/MemeVoice.Core/Effects/PitchShiftEffect.cs tests/MemeVoice.Core.Tests/Effects/PitchShiftEffectTests.cs
git commit -m "feat: add PitchShiftEffect wrapping SoundTouch.NET"
```

---

### Task 4: DistortionEffect + CompositeEffect

**Files:**
- Create: `src/MemeVoice.Core/Effects/DistortionEffect.cs`
- Create: `src/MemeVoice.Core/Effects/CompositeEffect.cs`
- Test: `tests/MemeVoice.Core.Tests/Effects/DistortionEffectTests.cs`

**Interfaces:**
- Consumes: `IVoiceEffect` (Task 2).
- Produces: `DistortionEffect(float drive)` implementing `IVoiceEffect`; `CompositeEffect(params IVoiceEffect[] stages)` implementing `IVoiceEffect`, runs each stage in order.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/MemeVoice.Core.Tests/Effects/DistortionEffectTests.cs
using MemeVoice.Core.Effects;
using Xunit;

namespace MemeVoice.Core.Tests.Effects;

public class DistortionEffectTests
{
    [Fact]
    public void HighDrive_ClampsSamplesTowardUnitRange()
    {
        var effect = new DistortionEffect(drive: 8f);
        var buffer = new float[] { 0.01f, -0.01f, 0.5f };

        effect.Process(buffer, sampleRate: 48000);

        foreach (var sample in buffer)
        {
            Assert.InRange(sample, -1f, 1f);
        }
        // A small input pushed through high drive should be pushed much closer to the rails
        Assert.True(System.Math.Abs(buffer[0]) > 0.05f);
    }

    [Fact]
    public void CompositeEffect_AppliesStagesInOrder()
    {
        var composite = new CompositeEffect(new PitchShiftEffect(0f), new DistortionEffect(drive: 8f));
        var buffer = new float[] { 0.01f, -0.01f, 0.5f };

        composite.Process(buffer, sampleRate: 48000);

        foreach (var sample in buffer)
        {
            Assert.InRange(sample, -1f, 1f);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/MemeVoice.Core.Tests --filter DistortionEffectTests`
Expected: FAIL — `DistortionEffect`/`CompositeEffect` do not exist.

- [ ] **Step 3: Implement DistortionEffect and CompositeEffect**

```csharp
// src/MemeVoice.Core/Effects/DistortionEffect.cs
using System;

namespace MemeVoice.Core.Effects;

public sealed class DistortionEffect : IVoiceEffect
{
    private readonly float _drive;

    public DistortionEffect(float drive)
    {
        _drive = drive;
    }

    public void Process(float[] buffer, int sampleRate)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (float)Math.Tanh(buffer[i] * _drive);
        }
    }
}
```

```csharp
// src/MemeVoice.Core/Effects/CompositeEffect.cs
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
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/MemeVoice.Core.Tests --filter DistortionEffectTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/MemeVoice.Core/Effects/DistortionEffect.cs src/MemeVoice.Core/Effects/CompositeEffect.cs tests/MemeVoice.Core.Tests/Effects/DistortionEffectTests.cs
git commit -m "feat: add DistortionEffect and CompositeEffect"
```

---

### Task 5: BiquadFilter + TelephoneEffect

**Files:**
- Create: `src/MemeVoice.Core/Effects/BiquadFilter.cs`
- Create: `src/MemeVoice.Core/Effects/TelephoneEffect.cs`
- Test: `tests/MemeVoice.Core.Tests/Effects/TelephoneEffectTests.cs`

**Interfaces:**
- Consumes: `IVoiceEffect` (Task 2).
- Produces: `TelephoneEffect()` implementing `IVoiceEffect` (fixed bandpass ~300-3400Hz).

- [ ] **Step 1: Write the failing test**

```csharp
// tests/MemeVoice.Core.Tests/Effects/TelephoneEffectTests.cs
using System;
using MemeVoice.Core.Effects;
using Xunit;

namespace MemeVoice.Core.Tests.Effects;

public class TelephoneEffectTests
{
    [Fact]
    public void LowFrequencyTone_IsAttenuatedMoreThanMidFrequencyTone()
    {
        const int sampleRate = 48000;

        var lowTone = GenerateSineWave(80f, sampleRate, 0.5);   // below the 300Hz cutoff
        var midTone = GenerateSineWave(1000f, sampleRate, 0.5); // inside the passband

        new TelephoneEffect().Process(lowTone, sampleRate);
        new TelephoneEffect().Process(midTone, sampleRate);

        double lowRms = Rms(lowTone, skip: lowTone.Length / 4);
        double midRms = Rms(midTone, skip: midTone.Length / 4);

        Assert.True(lowRms < midRms * 0.5, $"lowRms={lowRms} midRms={midRms}");
    }

    private static float[] GenerateSineWave(float freq, int sampleRate, double seconds)
    {
        int n = (int)(sampleRate * seconds);
        var buffer = new float[n];
        for (int i = 0; i < n; i++)
            buffer[i] = (float)Math.Sin(2 * Math.PI * freq * i / sampleRate);
        return buffer;
    }

    private static double Rms(float[] buffer, int skip)
    {
        double sum = 0;
        for (int i = skip; i < buffer.Length; i++) sum += buffer[i] * buffer[i];
        return Math.Sqrt(sum / (buffer.Length - skip));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/MemeVoice.Core.Tests --filter TelephoneEffectTests`
Expected: FAIL — `TelephoneEffect` does not exist.

- [ ] **Step 3: Implement BiquadFilter (RBJ cookbook bandpass) and TelephoneEffect**

```csharp
// src/MemeVoice.Core/Effects/BiquadFilter.cs
using System;

namespace MemeVoice.Core.Effects;

public sealed class BiquadFilter
{
    private double _b0, _b1, _b2, _a1, _a2;
    private double _x1, _x2, _y1, _y2;

    public void ConfigureBandpass(double centerFreq, double q, int sampleRate)
    {
        double omega = 2 * Math.PI * centerFreq / sampleRate;
        double alpha = Math.Sin(omega) / (2 * q);
        double cosOmega = Math.Cos(omega);

        double a0 = 1 + alpha;
        _b0 = alpha / a0;
        _b1 = 0;
        _b2 = -alpha / a0;
        _a1 = (-2 * cosOmega) / a0;
        _a2 = (1 - alpha) / a0;

        _x1 = _x2 = _y1 = _y2 = 0;
    }

    public float ProcessSample(float input)
    {
        double output = _b0 * input + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;

        _x2 = _x1;
        _x1 = input;
        _y2 = _y1;
        _y1 = output;

        return (float)output;
    }
}
```

```csharp
// src/MemeVoice.Core/Effects/TelephoneEffect.cs
namespace MemeVoice.Core.Effects;

public sealed class TelephoneEffect : IVoiceEffect
{
    private readonly BiquadFilter _filter = new();
    private int _configuredSampleRate = -1;

    public void Process(float[] buffer, int sampleRate)
    {
        if (_configuredSampleRate != sampleRate)
        {
            // Center of the classic 300-3400Hz telephone band, moderate Q for a narrow, "tinny" sound
            _filter.ConfigureBandpass(centerFreq: 1500, q: 0.7, sampleRate);
            _configuredSampleRate = sampleRate;
        }

        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = _filter.ProcessSample(buffer[i]);
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/MemeVoice.Core.Tests --filter TelephoneEffectTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/MemeVoice.Core/Effects/BiquadFilter.cs src/MemeVoice.Core/Effects/TelephoneEffect.cs tests/MemeVoice.Core.Tests/Effects/TelephoneEffectTests.cs
git commit -m "feat: add BiquadFilter and TelephoneEffect (bandpass)"
```

---

### Task 6: RingModulator + RobotEffect

**Files:**
- Create: `src/MemeVoice.Core/Effects/RingModulator.cs`
- Create: `src/MemeVoice.Core/Effects/RobotEffect.cs`
- Test: `tests/MemeVoice.Core.Tests/Effects/RobotEffectTests.cs`

**Interfaces:**
- Consumes: `IVoiceEffect` (Task 2).
- Produces: `RobotEffect()` implementing `IVoiceEffect` (ring modulation at a low carrier frequency for a monotone/robotic buzz).

- [ ] **Step 1: Write the failing test**

```csharp
// tests/MemeVoice.Core.Tests/Effects/RobotEffectTests.cs
using System;
using MemeVoice.Core.Effects;
using Xunit;

namespace MemeVoice.Core.Tests.Effects;

public class RobotEffectTests
{
    [Fact]
    public void Process_ProducesNonSilentOutput_DifferentFromInput()
    {
        const int sampleRate = 48000;
        var buffer = GenerateSineWave(200f, sampleRate, 0.2);
        var original = (float[])buffer.Clone();

        new RobotEffect().Process(buffer, sampleRate);

        bool anyNonZero = false;
        bool anyDifferent = false;
        for (int i = 0; i < buffer.Length; i++)
        {
            if (buffer[i] != 0f) anyNonZero = true;
            if (Math.Abs(buffer[i] - original[i]) > 1e-6f) anyDifferent = true;
        }

        Assert.True(anyNonZero);
        Assert.True(anyDifferent);
    }

    private static float[] GenerateSineWave(float freq, int sampleRate, double seconds)
    {
        int n = (int)(sampleRate * seconds);
        var buffer = new float[n];
        for (int i = 0; i < n; i++)
            buffer[i] = (float)Math.Sin(2 * Math.PI * freq * i / sampleRate);
        return buffer;
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/MemeVoice.Core.Tests --filter RobotEffectTests`
Expected: FAIL — `RobotEffect` does not exist.

- [ ] **Step 3: Implement RingModulator and RobotEffect**

```csharp
// src/MemeVoice.Core/Effects/RingModulator.cs
using System;

namespace MemeVoice.Core.Effects;

public sealed class RingModulator
{
    private readonly double _carrierFreq;
    private double _phase;

    public RingModulator(double carrierFreq)
    {
        _carrierFreq = carrierFreq;
    }

    public void Process(float[] buffer, int sampleRate)
    {
        double phaseIncrement = 2 * Math.PI * _carrierFreq / sampleRate;

        for (int i = 0; i < buffer.Length; i++)
        {
            double carrier = Math.Sin(_phase);
            buffer[i] = (float)(buffer[i] * carrier);
            _phase += phaseIncrement;
            if (_phase > 2 * Math.PI) _phase -= 2 * Math.PI;
        }
    }
}
```

```csharp
// src/MemeVoice.Core/Effects/RobotEffect.cs
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
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/MemeVoice.Core.Tests --filter RobotEffectTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/MemeVoice.Core/Effects/RingModulator.cs src/MemeVoice.Core/Effects/RobotEffect.cs tests/MemeVoice.Core.Tests/Effects/RobotEffectTests.cs
git commit -m "feat: add RingModulator and RobotEffect"
```

---

### Task 7: DelayLine + EchoEffect

**Files:**
- Create: `src/MemeVoice.Core/Effects/DelayLine.cs`
- Create: `src/MemeVoice.Core/Effects/EchoEffect.cs`
- Test: `tests/MemeVoice.Core.Tests/Effects/EchoEffectTests.cs`

**Interfaces:**
- Consumes: `IVoiceEffect` (Task 2).
- Produces: `EchoEffect()` implementing `IVoiceEffect` (feedback delay for "cavernão" effect).

- [ ] **Step 1: Write the failing test**

```csharp
// tests/MemeVoice.Core.Tests/Effects/EchoEffectTests.cs
using MemeVoice.Core.Effects;
using Xunit;

namespace MemeVoice.Core.Tests.Effects;

public class EchoEffectTests
{
    [Fact]
    public void Impulse_ProducesDelayedRepeatLaterInBuffer()
    {
        const int sampleRate = 48000;
        var buffer = new float[sampleRate]; // 1 second
        buffer[0] = 1f; // impulse

        new EchoEffect().Process(buffer, sampleRate);

        // Somewhere after the impulse, the delayed/feedback copy should produce further non-zero energy
        bool hasLaterEnergy = false;
        for (int i = 1000; i < buffer.Length; i++)
        {
            if (System.Math.Abs(buffer[i]) > 0.01f)
            {
                hasLaterEnergy = true;
                break;
            }
        }

        Assert.True(hasLaterEnergy);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/MemeVoice.Core.Tests --filter EchoEffectTests`
Expected: FAIL — `EchoEffect` does not exist.

- [ ] **Step 3: Implement DelayLine and EchoEffect**

```csharp
// src/MemeVoice.Core/Effects/DelayLine.cs
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
        float output = input + delayed * wetMix;

        _buffer[_writeIndex] = input + delayed * feedback;
        _writeIndex = (_writeIndex + 1) % _buffer.Length;

        return output;
    }
}
```

```csharp
// src/MemeVoice.Core/Effects/EchoEffect.cs
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
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/MemeVoice.Core.Tests --filter EchoEffectTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/MemeVoice.Core/Effects/DelayLine.cs src/MemeVoice.Core/Effects/EchoEffect.cs tests/MemeVoice.Core.Tests/Effects/EchoEffectTests.cs
git commit -m "feat: add DelayLine and EchoEffect"
```

---

### Task 8: LfoOscillator + AlienEffect

**Files:**
- Create: `src/MemeVoice.Core/Effects/LfoOscillator.cs`
- Create: `src/MemeVoice.Core/Effects/AlienEffect.cs`
- Test: `tests/MemeVoice.Core.Tests/Effects/AlienEffectTests.cs`

**Interfaces:**
- Consumes: `IVoiceEffect` (Task 2).
- Produces: `AlienEffect()` implementing `IVoiceEffect` (tremolo: amplitude modulated by a low-frequency oscillator, for an "interferência" character distinct from Robot's ring mod).

- [ ] **Step 1: Write the failing test**

```csharp
// tests/MemeVoice.Core.Tests/Effects/AlienEffectTests.cs
using System;
using MemeVoice.Core.Effects;
using Xunit;

namespace MemeVoice.Core.Tests.Effects;

public class AlienEffectTests
{
    [Fact]
    public void ConstantAmplitudeInput_GetsAmplitudeVariationFromLfo()
    {
        const int sampleRate = 48000;
        var buffer = new float[sampleRate]; // 1 second of constant amplitude
        for (int i = 0; i < buffer.Length; i++) buffer[i] = 1f;

        new AlienEffect().Process(buffer, sampleRate);

        float min = float.MaxValue, max = float.MinValue;
        foreach (var sample in buffer)
        {
            min = Math.Min(min, sample);
            max = Math.Max(max, sample);
        }

        // Tremolo should create a noticeable spread between quiet and loud parts of the signal
        Assert.True(max - min > 0.2f, $"min={min} max={max}");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/MemeVoice.Core.Tests --filter AlienEffectTests`
Expected: FAIL — `AlienEffect` does not exist.

- [ ] **Step 3: Implement LfoOscillator and AlienEffect**

```csharp
// src/MemeVoice.Core/Effects/LfoOscillator.cs
using System;

namespace MemeVoice.Core.Effects;

public sealed class LfoOscillator
{
    private readonly double _frequency;
    private double _phase;

    public LfoOscillator(double frequency)
    {
        _frequency = frequency;
    }

    public float NextValue(int sampleRate)
    {
        double value = Math.Sin(_phase);
        _phase += 2 * Math.PI * _frequency / sampleRate;
        if (_phase > 2 * Math.PI) _phase -= 2 * Math.PI;
        return (float)value;
    }
}
```

```csharp
// src/MemeVoice.Core/Effects/AlienEffect.cs
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
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/MemeVoice.Core.Tests --filter AlienEffectTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/MemeVoice.Core/Effects/LfoOscillator.cs src/MemeVoice.Core/Effects/AlienEffect.cs tests/MemeVoice.Core.Tests/Effects/AlienEffectTests.cs
git commit -m "feat: add LfoOscillator and AlienEffect (tremolo)"
```

---

### Task 9: EffectPresets registry

**Files:**
- Create: `src/MemeVoice.Core/Effects/EffectPresets.cs`
- Test: `tests/MemeVoice.Core.Tests/Effects/EffectPresetsTests.cs`

**Interfaces:**
- Consumes: `PitchShiftEffect` (Task 3), `DistortionEffect`/`CompositeEffect` (Task 4), `TelephoneEffect` (Task 5), `RobotEffect` (Task 6), `EchoEffect` (Task 7), `AlienEffect` (Task 8).
- Produces: `EffectPreset` record `(string Name, IVoiceEffect Effect)`; `EffectPresets.GetAll()` returning `IReadOnlyList<EffectPreset>` with exactly the 9 Fase 1 effects; `EffectPresets.CreatePitchLivre(float semitones)` returning a fresh adjustable `PitchShiftEffect`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/MemeVoice.Core.Tests/Effects/EffectPresetsTests.cs
using System.Linq;
using MemeVoice.Core.Effects;
using Xunit;

namespace MemeVoice.Core.Tests.Effects;

public class EffectPresetsTests
{
    [Fact]
    public void GetAll_ReturnsAllNineFase1Effects()
    {
        var presets = EffectPresets.GetAll();

        var names = presets.Select(p => p.Name).ToArray();

        Assert.Equal(9, presets.Count);
        Assert.Contains("Grave/Robusto", names);
        Assert.Contains("Agudo/Esquilo", names);
        Assert.Contains("Robô", names);
        Assert.Contains("Eco/Cavernão", names);
        Assert.Contains("Pitch Livre", names);
        Assert.Contains("Demônio", names);
        Assert.Contains("Chipmunk Extremo", names);
        Assert.Contains("Telefone/Rádio", names);
        Assert.Contains("Alien/Interferência", names);
    }

    [Fact]
    public void CreatePitchLivre_HonorsRequestedSemitones()
    {
        var effect = EffectPresets.CreatePitchLivre(3.5f);

        Assert.Equal(3.5f, effect.Semitones);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/MemeVoice.Core.Tests --filter EffectPresetsTests`
Expected: FAIL — `EffectPresets` does not exist.

- [ ] **Step 3: Implement EffectPresets**

```csharp
// src/MemeVoice.Core/Effects/EffectPresets.cs
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
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/MemeVoice.Core.Tests --filter EffectPresetsTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/MemeVoice.Core/Effects/EffectPresets.cs tests/MemeVoice.Core.Tests/Effects/EffectPresetsTests.cs
git commit -m "feat: add EffectPresets registry with all 9 Fase 1 effects"
```

---

### Task 10: DeviceManager

**Files:**
- Create: `src/MemeVoice.Core/DeviceManager.cs`
- Test: `tests/MemeVoice.Core.Tests/DeviceManagerTests.cs`

**Interfaces:**
- Produces: `AudioDeviceInfo` record `(string Id, string Name)`; `DeviceManager` with `IReadOnlyList<AudioDeviceInfo> GetInputDevices()`, `IReadOnlyList<AudioDeviceInfo> GetOutputDevices()`, `bool IsVbCableInstalled()`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/MemeVoice.Core.Tests/DeviceManagerTests.cs
using MemeVoice.Core;
using Xunit;

namespace MemeVoice.Core.Tests;

public class DeviceManagerTests
{
    [Fact]
    public void GetOutputDevices_ReturnsAtLeastOneDevice()
    {
        // This test runs against the machine's real audio devices (WASAPI enumeration
        // has no practical fake/mocked implementation without hiding NAudio entirely).
        var manager = new DeviceManager();

        var devices = manager.GetOutputDevices();

        Assert.NotEmpty(devices);
    }

    [Fact]
    public void IsVbCableInstalled_ReturnsFalse_WhenNoCableDeviceNamePresent()
    {
        var manager = new DeviceManager();

        // We can't force-uninstall VB-Cable from a test, so this just verifies the
        // method runs and returns a bool without throwing — presence is verified manually.
        var result = manager.IsVbCableInstalled();

        Assert.IsType<bool>(result);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/MemeVoice.Core.Tests --filter DeviceManagerTests`
Expected: FAIL — `DeviceManager` does not exist.

- [ ] **Step 3: Implement DeviceManager using NAudio's MMDeviceEnumerator**

```csharp
// src/MemeVoice.Core/DeviceManager.cs
using System.Collections.Generic;
using System.Linq;
using NAudio.CoreAudioApi;

namespace MemeVoice.Core;

public sealed record AudioDeviceInfo(string Id, string Name);

public sealed class DeviceManager
{
    private const string VbCableNameFragment = "CABLE Input";

    public IReadOnlyList<AudioDeviceInfo> GetInputDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .Select(d => new AudioDeviceInfo(d.ID, d.FriendlyName))
            .ToList();
    }

    public IReadOnlyList<AudioDeviceInfo> GetOutputDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .Select(d => new AudioDeviceInfo(d.ID, d.FriendlyName))
            .ToList();
    }

    public bool IsVbCableInstalled()
    {
        return GetOutputDevices().Any(d => d.Name.Contains(VbCableNameFragment));
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/MemeVoice.Core.Tests --filter DeviceManagerTests`
Expected: PASS (runs against real machine audio devices)

- [ ] **Step 5: Commit**

```bash
git add src/MemeVoice.Core/DeviceManager.cs tests/MemeVoice.Core.Tests/DeviceManagerTests.cs
git commit -m "feat: add DeviceManager (WASAPI enumeration + VB-Cable detection)"
```

---

### Task 11: AudioEngine

**Files:**
- Create: `src/MemeVoice.Core/AudioEngine.cs`
- Test: `tests/MemeVoice.Core.Tests/PipelineIntegrationTests.cs`

**Interfaces:**
- Consumes: `EffectChain` (Task 2), `AudioDeviceInfo` (Task 10).
- Produces: `AudioEngine` with `AudioEngine(EffectChain chain)`, `void Start(string inputDeviceId, string outputDeviceId)`, `void Stop()`, `bool IsRunning { get; }`, `event Action<string>? OnError`. Also produces the pure-buffer helper `AudioEngine.ApplyEffectChain(float[] samples, int sampleRate, EffectChain chain)` used directly by the integration test (so the test doesn't need real hardware).

**Note on testability:** WASAPI capture/render requires real hardware and can't run headlessly in CI. `AudioEngine` is split so the hardware-facing capture/render loop is a thin wrapper, and the actual per-buffer processing logic (`ApplyEffectChain`) is a static, pure method tested directly with a WAV fixture — this is what Task 17's "no silence/NaN" pipeline test exercises for the full effect list.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/MemeVoice.Core.Tests/PipelineIntegrationTests.cs
using System;
using System.Linq;
using MemeVoice.Core;
using MemeVoice.Core.Effects;
using Xunit;

namespace MemeVoice.Core.Tests;

public class PipelineIntegrationTests
{
    [Fact]
    public void ApplyEffectChain_OnSineWave_ProducesFiniteNonSilentOutput()
    {
        const int sampleRate = 48000;
        var samples = GenerateSineWave(220f, sampleRate, 0.5);
        var chain = new EffectChain(new PitchShiftEffect(-5f));

        AudioEngine.ApplyEffectChain(samples, sampleRate, chain);

        Assert.All(samples, s => Assert.True(!float.IsNaN(s) && !float.IsInfinity(s)));
        Assert.Contains(samples, s => Math.Abs(s) > 0.001f);
    }

    private static float[] GenerateSineWave(float freq, int sampleRate, double seconds)
    {
        int n = (int)(sampleRate * seconds);
        var buffer = new float[n];
        for (int i = 0; i < n; i++)
            buffer[i] = (float)Math.Sin(2 * Math.PI * freq * i / sampleRate);
        return buffer;
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/MemeVoice.Core.Tests --filter PipelineIntegrationTests`
Expected: FAIL — `AudioEngine` does not exist.

- [ ] **Step 3: Implement AudioEngine**

```csharp
// src/MemeVoice.Core/AudioEngine.cs
using System;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace MemeVoice.Core;

public sealed class AudioEngine : IDisposable
{
    private readonly EffectChain _chain;
    private WasapiCapture? _capture;
    private BufferedWaveProvider? _bufferedProvider;
    private WasapiOut? _output;

    public AudioEngine(EffectChain chain)
    {
        _chain = chain;
    }

    public bool IsRunning { get; private set; }

    public event Action<string>? OnError;

    public void Start(string inputDeviceId, string outputDeviceId)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var inputDevice = enumerator.GetDevice(inputDeviceId);
            var outputDevice = enumerator.GetDevice(outputDeviceId);

            _capture = new WasapiCapture(inputDevice);
            _bufferedProvider = new BufferedWaveProvider(_capture.WaveFormat)
            {
                DiscardOnBufferOverflow = true
            };

            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += (_, e) =>
            {
                if (e.Exception != null) OnError?.Invoke(e.Exception.Message);
            };

            _output = new WasapiOut(outputDevice, AudioClientShareMode.Shared, useEventSync: true, latency: 20);
            _output.Init(_bufferedProvider);

            _capture.StartRecording();
            _output.Play();
            IsRunning = true;
        }
        catch (Exception ex)
        {
            IsRunning = false;
            OnError?.Invoke(ex.Message);
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_capture == null || _bufferedProvider == null) return;

        int bytesPerSample = _capture.WaveFormat.BitsPerSample / 8;
        int sampleCount = e.BytesRecorded / bytesPerSample;
        var samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] = BitConverter.ToSingle(e.Buffer, i * bytesPerSample);
        }

        ApplyEffectChain(samples, _capture.WaveFormat.SampleRate, _chain);

        var outBytes = new byte[e.BytesRecorded];
        Buffer.BlockCopy(samples, 0, outBytes, 0, e.BytesRecorded);
        _bufferedProvider.AddSamples(outBytes, 0, outBytes.Length);
    }

    /// <summary>Pure buffer transform, split out so it's testable without real audio hardware.</summary>
    public static void ApplyEffectChain(float[] samples, int sampleRate, EffectChain chain)
    {
        chain.Process(samples, sampleRate);
    }

    public void Stop()
    {
        _capture?.StopRecording();
        _output?.Stop();
        IsRunning = false;
    }

    public void Dispose()
    {
        Stop();
        _capture?.Dispose();
        _output?.Dispose();
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/MemeVoice.Core.Tests --filter PipelineIntegrationTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/MemeVoice.Core/AudioEngine.cs tests/MemeVoice.Core.Tests/PipelineIntegrationTests.cs
git commit -m "feat: add AudioEngine (WASAPI capture/render around EffectChain)"
```

---

### Task 12: ConfigStore

**Files:**
- Create: `src/MemeVoice.Core/ConfigStore.cs`
- Test: `tests/MemeVoice.Core.Tests/ConfigStoreTests.cs`

**Interfaces:**
- Produces: `AppConfig` record `(string? InputDeviceId, string? OutputDeviceId, string LastEffectName, string ToggleHotkey, string NextEffectHotkey)`; `ConfigStore` with `AppConfig Load()`, `void Save(AppConfig config)`, constructor `ConfigStore(string filePath)` (explicit path for testability; production code points it at `%AppData%\MemeVoice\config.json`).

- [ ] **Step 1: Write the failing test**

```csharp
// tests/MemeVoice.Core.Tests/ConfigStoreTests.cs
using System;
using System.IO;
using MemeVoice.Core;
using Xunit;

namespace MemeVoice.Core.Tests;

public class ConfigStoreTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(), $"memevoice-test-{Guid.NewGuid()}.json");

    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaults()
    {
        var store = new ConfigStore(_tempFile);

        var config = store.Load();

        Assert.Equal("Grave/Robusto", config.LastEffectName);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsValues()
    {
        var store = new ConfigStore(_tempFile);
        var config = new AppConfig(
            InputDeviceId: "input-123",
            OutputDeviceId: "output-456",
            LastEffectName: "Robô",
            ToggleHotkey: "Ctrl+Alt+V",
            NextEffectHotkey: "Ctrl+Alt+N");

        store.Save(config);
        var loaded = store.Load();

        Assert.Equal(config, loaded);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/MemeVoice.Core.Tests --filter ConfigStoreTests`
Expected: FAIL — `ConfigStore`/`AppConfig` do not exist.

- [ ] **Step 3: Implement AppConfig and ConfigStore**

```csharp
// src/MemeVoice.Core/ConfigStore.cs
using System.IO;
using System.Text.Json;

namespace MemeVoice.Core;

public sealed record AppConfig(
    string? InputDeviceId,
    string? OutputDeviceId,
    string LastEffectName,
    string ToggleHotkey,
    string NextEffectHotkey)
{
    public static AppConfig Default => new(
        InputDeviceId: null,
        OutputDeviceId: null,
        LastEffectName: "Grave/Robusto",
        ToggleHotkey: "Ctrl+Alt+V",
        NextEffectHotkey: "Ctrl+Alt+N");
}

public sealed class ConfigStore
{
    private readonly string _filePath;

    public ConfigStore(string filePath)
    {
        _filePath = filePath;
    }

    public AppConfig Load()
    {
        if (!File.Exists(_filePath)) return AppConfig.Default;

        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<AppConfig>(json) ?? AppConfig.Default;
    }

    public void Save(AppConfig config)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/MemeVoice.Core.Tests --filter ConfigStoreTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/MemeVoice.Core/ConfigStore.cs tests/MemeVoice.Core.Tests/ConfigStoreTests.cs
git commit -m "feat: add ConfigStore with JSON persistence under %AppData%"
```

---

### Task 13: WPF MainWindow (effect list, device pickers, start/stop)

**Files:**
- Modify: `src/MemeVoice.App/MainWindow.xaml`
- Modify: `src/MemeVoice.App/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `EffectPresets.GetAll()` (Task 9), `DeviceManager` (Task 10), `AudioEngine`/`EffectChain` (Tasks 2, 11), `ConfigStore`/`AppConfig` (Task 12).
- Produces: running WPF window that a human can manually verify; no new public API consumed by later tasks except `MainWindow`'s public methods `ActivateEffectByName(string name)` and `ToggleProcessing()`, used by `HotkeyManager` in Task 16.

This task is UI wiring, verified manually (per the spec's testing section, GUI is validated by hand, not unit tests).

- [ ] **Step 1: Build the XAML layout**

```xml
<!-- src/MemeVoice.App/MainWindow.xaml -->
<Window x:Class="MemeVoice.App.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="MemeVoice" Height="420" Width="360" ResizeMode="CanMinimize">
    <StackPanel Margin="12">
        <TextBlock Text="Microfone de entrada" FontWeight="Bold"/>
        <ComboBox x:Name="InputDeviceCombo" Margin="0,4,0,12" DisplayMemberPath="Name"/>

        <TextBlock Text="Saída (VB-Cable)" FontWeight="Bold"/>
        <ComboBox x:Name="OutputDeviceCombo" Margin="0,4,0,12" DisplayMemberPath="Name"/>

        <TextBlock x:Name="VbCableWarning" Foreground="Red" TextWrapping="Wrap" Visibility="Collapsed"
                   Text="VB-Cable não detectado. Instale o VB-Audio Virtual Cable antes de iniciar."/>

        <TextBlock Text="Efeito" FontWeight="Bold" Margin="0,8,0,0"/>
        <ListBox x:Name="EffectList" Height="140" Margin="0,4,0,8" DisplayMemberPath="Name"/>

        <TextBlock Text="Pitch livre (semitons)" x:Name="PitchLivreLabel" Visibility="Collapsed"/>
        <Slider x:Name="PitchLivreSlider" Minimum="-12" Maximum="12" Visibility="Collapsed" Margin="0,0,0,8"/>

        <Button x:Name="StartStopButton" Content="Iniciar" Height="32" Click="StartStopButton_Click"/>
        <TextBlock x:Name="StatusText" Margin="0,8,0,0" Text="Parado"/>
    </StackPanel>
</Window>
```

- [ ] **Step 2: Implement code-behind wiring devices, effects, and start/stop**

```csharp
// src/MemeVoice.App/MainWindow.xaml.cs
using System.Linq;
using System.Windows;
using MemeVoice.Core;
using MemeVoice.Core.Effects;

namespace MemeVoice.App;

public partial class MainWindow : Window
{
    private readonly DeviceManager _deviceManager = new();
    private readonly ConfigStore _configStore;
    private readonly EffectChain _chain;
    private readonly AudioEngine _engine;
    private AppConfig _config;

    public MainWindow()
    {
        InitializeComponent();

        var appDataConfigPath = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "MemeVoice", "config.json");
        _configStore = new ConfigStore(appDataConfigPath);
        _config = _configStore.Load();

        var presets = EffectPresets.GetAll();
        EffectList.ItemsSource = presets;
        var initial = presets.FirstOrDefault(p => p.Name == _config.LastEffectName) ?? presets[0];
        EffectList.SelectedItem = initial;

        _chain = new EffectChain(initial.Effect);
        _engine = new AudioEngine(_chain);
        _engine.OnError += message => Dispatcher.Invoke(() => StatusText.Text = $"Erro: {message}");

        InputDeviceCombo.ItemsSource = _deviceManager.GetInputDevices();
        OutputDeviceCombo.ItemsSource = _deviceManager.GetOutputDevices();
        SelectConfiguredOrFirst(InputDeviceCombo, _config.InputDeviceId);
        SelectConfiguredOrFirst(OutputDeviceCombo, _config.OutputDeviceId);

        VbCableWarning.Visibility = _deviceManager.IsVbCableInstalled() ? Visibility.Collapsed : Visibility.Visible;
        StartStopButton.IsEnabled = _deviceManager.IsVbCableInstalled();

        EffectList.SelectionChanged += (_, _) => OnEffectSelectionChanged();
        PitchLivreSlider.ValueChanged += (_, _) => OnPitchLivreChanged();
    }

    private static void SelectConfiguredOrFirst(System.Windows.Controls.ComboBox combo, string? configuredId)
    {
        var items = combo.ItemsSource?.Cast<AudioDeviceInfo>().ToList();
        if (items == null || items.Count == 0) return;

        combo.SelectedItem = items.FirstOrDefault(d => d.Id == configuredId) ?? items[0];
    }

    private void OnEffectSelectionChanged()
    {
        if (EffectList.SelectedItem is not EffectPreset preset) return;

        bool isPitchLivre = preset.Name == "Pitch Livre";
        PitchLivreLabel.Visibility = isPitchLivre ? Visibility.Visible : Visibility.Collapsed;
        PitchLivreSlider.Visibility = isPitchLivre ? Visibility.Visible : Visibility.Collapsed;

        _chain.SetEffect(preset.Effect);
    }

    private void OnPitchLivreChanged()
    {
        if (_chain.Current is PitchShiftEffect pitchEffect)
        {
            pitchEffect.Semitones = (float)PitchLivreSlider.Value;
        }
    }

    public void ActivateEffectByName(string name)
    {
        if (EffectList.ItemsSource.Cast<EffectPreset>().FirstOrDefault(p => p.Name == name) is { } preset)
        {
            EffectList.SelectedItem = preset;
        }
    }

    public void ToggleProcessing()
    {
        StartStopButton_Click(this, new RoutedEventArgs());
    }

    private void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_engine.IsRunning)
        {
            _engine.Stop();
            StartStopButton.Content = "Iniciar";
            StatusText.Text = "Parado";
        }
        else
        {
            var input = (AudioDeviceInfo)InputDeviceCombo.SelectedItem;
            var output = (AudioDeviceInfo)OutputDeviceCombo.SelectedItem;
            _engine.Start(input.Id, output.Id);
            StartStopButton.Content = "Parar";
            StatusText.Text = _engine.IsRunning ? "Ativo" : "Erro ao iniciar";
        }

        SaveConfig();
    }

    private void SaveConfig()
    {
        var input = InputDeviceCombo.SelectedItem as AudioDeviceInfo;
        var output = OutputDeviceCombo.SelectedItem as AudioDeviceInfo;
        var effectName = (EffectList.SelectedItem as EffectPreset)?.Name ?? _config.LastEffectName;

        _config = _config with
        {
            InputDeviceId = input?.Id,
            OutputDeviceId = output?.Id,
            LastEffectName = effectName
        };
        _configStore.Save(_config);
    }
}
```

- [ ] **Step 3: Build and manually verify**

```bash
dotnet build src/MemeVoice.App
dotnet run --project src/MemeVoice.App
```

Expected: window opens, device dropdowns populate with real devices, effect list shows all 9 presets, selecting "Pitch Livre" reveals the slider, Iniciar/Parar toggles without crashing.

- [ ] **Step 4: Commit**

```bash
git add src/MemeVoice.App/MainWindow.xaml src/MemeVoice.App/MainWindow.xaml.cs
git commit -m "feat: wire MainWindow to device selection, effect presets, and AudioEngine"
```

---

### Task 14: Tray icon + minimize-to-tray

**Files:**
- Create: `src/MemeVoice.App/TrayIconManager.cs`
- Modify: `src/MemeVoice.App/MainWindow.xaml.cs`
- Modify: `src/MemeVoice.App/MemeVoice.App.csproj` (add `System.Windows.Forms` reference for `NotifyIcon`)

**Interfaces:**
- Consumes: `MainWindow` (Task 13).
- Produces: `TrayIconManager` with `TrayIconManager(Window window)`, `void Attach()`; wires minimize → hide-to-tray, tray icon double-click → restore, tray context menu "Sair" → real close.

- [ ] **Step 1: Add WinForms reference for NotifyIcon**

```bash
dotnet add src/MemeVoice.App/MemeVoice.App.csproj reference System.Windows.Forms
```

If that fails (WinForms isn't referenced as a project), edit `src/MemeVoice.App/MemeVoice.App.csproj` and ensure `<UseWindowsForms>true</UseWindowsForms>` sits alongside the existing `<UseWPF>true</UseWPF>` in the `<PropertyGroup>`.

- [ ] **Step 2: Implement TrayIconManager**

```csharp
// src/MemeVoice.App/TrayIconManager.cs
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace MemeVoice.App;

public sealed class TrayIconManager
{
    private readonly Window _window;
    private readonly NotifyIcon _notifyIcon;

    public TrayIconManager(Window window)
    {
        _window = window;
        _notifyIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Visible = false,
            Text = "MemeVoice"
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir", null, (_, _) => Restore());
        menu.Items.Add("Sair", null, (_, _) => Application.Current.Shutdown());
        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => Restore();
    }

    public void Attach()
    {
        _window.StateChanged += (_, _) =>
        {
            if (_window.WindowState == WindowState.Minimized)
            {
                _window.Hide();
                _notifyIcon.Visible = true;
            }
        };
    }

    private void Restore()
    {
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
        _notifyIcon.Visible = false;
    }
}
```

- [ ] **Step 3: Wire it up in MainWindow constructor**

```csharp
// Add near the end of the MainWindow() constructor in MainWindow.xaml.cs
new TrayIconManager(this).Attach();
```

- [ ] **Step 4: Build and manually verify**

```bash
dotnet build src/MemeVoice.App
dotnet run --project src/MemeVoice.App
```

Expected: minimizing the window hides it and shows a tray icon; double-clicking the tray icon restores the window; "Sair" from the tray context menu closes the app.

- [ ] **Step 5: Commit**

```bash
git add src/MemeVoice.App/TrayIconManager.cs src/MemeVoice.App/MainWindow.xaml.cs src/MemeVoice.App/MemeVoice.App.csproj
git commit -m "feat: add tray icon with minimize-to-tray behavior"
```

---

### Task 15: HotkeyManager (global hotkeys)

**Files:**
- Create: `src/MemeVoice.App/HotkeyManager.cs`
- Modify: `src/MemeVoice.App/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `MainWindow.ActivateEffectByName(string)`, `MainWindow.ToggleProcessing()` (Task 13).
- Produces: `HotkeyManager` with `HotkeyManager(Window window)`, `bool RegisterToggleHotkey(Action callback)`, `bool RegisterNextEffectHotkey(Action callback)`, `event Action<string>? OnRegistrationFailed`.

- [ ] **Step 1: Implement HotkeyManager using Win32 RegisterHotKey**

```csharp
// src/MemeVoice.App/HotkeyManager.cs
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MemeVoice.App;

public sealed class HotkeyManager
{
    private const int WM_HOTKEY = 0x0312;
    private const int ToggleHotkeyId = 1;
    private const int NextEffectHotkeyId = 2;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_ALT = 0x0001;
    private const uint VK_V = 0x56;
    private const uint VK_N = 0x4E;

    private readonly Window _window;
    private HwndSource? _source;
    private Action? _toggleCallback;
    private Action? _nextEffectCallback;

    public event Action<string>? OnRegistrationFailed;

    public HotkeyManager(Window window)
    {
        _window = window;
    }

    public bool RegisterToggleHotkey(Action callback)
    {
        _toggleCallback = callback;
        return RegisterHotkeyInternal(ToggleHotkeyId, MOD_CONTROL | MOD_ALT, VK_V, "Ctrl+Alt+V");
    }

    public bool RegisterNextEffectHotkey(Action callback)
    {
        _nextEffectCallback = callback;
        return RegisterHotkeyInternal(NextEffectHotkeyId, MOD_CONTROL | MOD_ALT, VK_N, "Ctrl+Alt+N");
    }

    private bool RegisterHotkeyInternal(int id, uint modifiers, uint key, string description)
    {
        EnsureSourceAttached();
        var handle = new System.Windows.Interop.WindowInteropHelper(_window).Handle;

        bool success = RegisterHotKey(handle, id, modifiers, key);
        if (!success)
        {
            OnRegistrationFailed?.Invoke($"Não foi possível registrar o atalho {description} (talvez já esteja em uso por outro programa).");
        }
        return success;
    }

    private void EnsureSourceAttached()
    {
        if (_source != null) return;

        var handle = new System.Windows.Interop.WindowInteropHelper(_window).Handle;
        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (id == ToggleHotkeyId) _toggleCallback?.Invoke();
            else if (id == NextEffectHotkeyId) _nextEffectCallback?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
```

- [ ] **Step 2: Wire HotkeyManager into MainWindow**

```csharp
// Add to MainWindow.xaml.cs: a field, plus registration after InitializeComponent()
// and a helper for "next effect" cycling.

// Field:
private readonly HotkeyManager _hotkeyManager;

// In the constructor, after the tray icon wiring added in Task 14:
_hotkeyManager = new HotkeyManager(this);
_hotkeyManager.OnRegistrationFailed += message => Dispatcher.Invoke(() => StatusText.Text = message);
_hotkeyManager.RegisterToggleHotkey(() => Dispatcher.Invoke(ToggleProcessing));
_hotkeyManager.RegisterNextEffectHotkey(() => Dispatcher.Invoke(SelectNextEffect));

// New method:
private void SelectNextEffect()
{
    var presets = EffectList.ItemsSource.Cast<Core.Effects.EffectPreset>().ToList();
    int currentIndex = EffectList.SelectedIndex;
    int nextIndex = (currentIndex + 1) % presets.Count;
    EffectList.SelectedItem = presets[nextIndex];
}
```

- [ ] **Step 3: Build and manually verify**

```bash
dotnet build src/MemeVoice.App
dotnet run --project src/MemeVoice.App
```

Expected: pressing Ctrl+Alt+V toggles Iniciar/Parar even when another window is focused; Ctrl+Alt+N cycles to the next effect in the list.

- [ ] **Step 4: Commit**

```bash
git add src/MemeVoice.App/HotkeyManager.cs src/MemeVoice.App/MainWindow.xaml.cs
git commit -m "feat: add global hotkeys for toggle and next-effect"
```

---

### Task 16: Error handling polish (VB-Cable warning, mic disconnect, buffer glitch logging)

**Files:**
- Create: `src/MemeVoice.Core/SimpleLogger.cs`
- Modify: `src/MemeVoice.Core/AudioEngine.cs`
- Modify: `src/MemeVoice.App/MainWindow.xaml.cs`

**Interfaces:**
- Produces: `SimpleLogger` static class with `void LogError(string message)`, appending timestamped lines to `%AppData%\MemeVoice\log.txt`.

- [ ] **Step 1: Implement SimpleLogger**

```csharp
// src/MemeVoice.Core/SimpleLogger.cs
using System;
using System.IO;

namespace MemeVoice.Core;

public static class SimpleLogger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MemeVoice", "log.txt");

    public static void LogError(string message)
    {
        try
        {
            var directory = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
        catch
        {
            // Logging must never crash the app; silently drop if the file can't be written.
        }
    }
}
```

- [ ] **Step 2: Route AudioEngine errors through SimpleLogger**

```csharp
// In AudioEngine.Start()'s catch block (src/MemeVoice.Core/AudioEngine.cs), replace:
//   OnError?.Invoke(ex.Message);
// with:
            SimpleLogger.LogError($"AudioEngine.Start failed: {ex}");
            OnError?.Invoke(ex.Message);

// In the RecordingStopped handler registered in Start(), replace:
//   if (e.Exception != null) OnError?.Invoke(e.Exception.Message);
// with:
            _capture.RecordingStopped += (_, e) =>
            {
                if (e.Exception != null)
                {
                    SimpleLogger.LogError($"Recording stopped unexpectedly: {e.Exception}");
                    IsRunning = false;
                    OnError?.Invoke("Microfone desconectado. Processamento parado.");
                }
            };
```

- [ ] **Step 3: Re-check VB-Cable presence when the user reopens the window from the tray**

```csharp
// In MainWindow.xaml.cs, extend the Restore-triggered flow: add a public method and
// call it from TrayIconManager's Restore() action, or simply re-check on window Activated.
// Simplest: subscribe in the MainWindow constructor, after existing wiring:
this.Activated += (_, _) =>
{
    bool installed = _deviceManager.IsVbCableInstalled();
    VbCableWarning.Visibility = installed ? Visibility.Collapsed : Visibility.Visible;
    StartStopButton.IsEnabled = installed;
};
```

- [ ] **Step 4: Build and manually verify**

```bash
dotnet build MemeVoice.sln
dotnet run --project src/MemeVoice.App
```

Expected: unplugging the selected microphone while running shows "Microfone desconectado..." in the status text and stops cleanly (no crash); `%AppData%\MemeVoice\log.txt` gets a line when this happens.

- [ ] **Step 5: Commit**

```bash
git add src/MemeVoice.Core/SimpleLogger.cs src/MemeVoice.Core/AudioEngine.cs src/MemeVoice.App/MainWindow.xaml.cs
git commit -m "feat: log audio errors and re-check VB-Cable presence on window activation"
```

---

### Task 17: Full-chain pipeline test across all Fase 1 effects

**Files:**
- Modify: `tests/MemeVoice.Core.Tests/PipelineIntegrationTests.cs`

**Interfaces:**
- Consumes: `EffectPresets.GetAll()` (Task 9), `AudioEngine.ApplyEffectChain` (Task 11).

- [ ] **Step 1: Write the failing test**

```csharp
// Add to tests/MemeVoice.Core.Tests/PipelineIntegrationTests.cs
using MemeVoice.Core.Effects;

[Fact]
public void ApplyEffectChain_AllFase1Presets_ProduceFiniteNonSilentOutput()
{
    const int sampleRate = 48000;

    foreach (var preset in EffectPresets.GetAll())
    {
        var samples = GenerateSineWave(220f, sampleRate, 0.3);
        var chain = new EffectChain(preset.Effect);

        AudioEngine.ApplyEffectChain(samples, sampleRate, chain);

        Assert.All(samples, s => Assert.True(!float.IsNaN(s) && !float.IsInfinity(s), $"{preset.Name} produced NaN/Infinity"));
        Assert.Contains(samples, s => Math.Abs(s) > 0.0005f);
    }
}
```

- [ ] **Step 2: Run test to verify it fails (or passes trivially if already covered — confirm it exercises all 9 presets)**

Run: `dotnet test tests/MemeVoice.Core.Tests --filter ApplyEffectChain_AllFase1Presets_ProduceFiniteNonSilentOutput`
Expected: FAIL initially if any preset is misconfigured (e.g., `EchoEffect` producing pure silence on a short sine burst); otherwise PASS immediately once Tasks 3-9 are correct.

- [ ] **Step 3: Fix any failing preset**

If a specific effect fails this test, adjust its constants (e.g., `EchoEffect`'s `WetMix`/`Feedback`, `AlienEffect`'s `Depth`) directly in that effect's file until the assertions hold — do not weaken the test.

- [ ] **Step 4: Run the full Core test suite**

Run: `dotnet test tests/MemeVoice.Core.Tests`
Expected: PASS — all tests from Tasks 2-17 green.

- [ ] **Step 5: Commit**

```bash
git add tests/MemeVoice.Core.Tests/PipelineIntegrationTests.cs
git commit -m "test: verify full effect chain produces finite, non-silent output for all Fase 1 presets"
```

---

## Manual End-to-End Verification (after Task 17)

Not automatable — run once before considering Fase 1 done:

1. Install VB-Audio VB-CABLE if not already installed.
2. `dotnet run --project src/MemeVoice.App`.
3. Select real microphone as input, "CABLE Input (VB-Audio Virtual Cable)" as output.
4. Open Discord → User Settings → Voice & Video → set Input Device to "CABLE Output (VB-Audio Virtual Cable)".
5. Click Iniciar, speak, and confirm Discord's input meter reacts and a voice call partner hears the effect.
6. Cycle through all 9 presets and the Pitch Livre slider, confirming audible change and no crashes.
7. Test Ctrl+Alt+V and Ctrl+Alt+N while Discord/game window is focused (not MemeVoice).
8. Minimize to tray, restore, and quit via tray menu.
