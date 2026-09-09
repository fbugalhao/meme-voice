using System;
using NAudio.Wave;
using SoundTouch.Net.NAudioSupport;

namespace MemeVoice.Core.Effects;

public sealed class PitchShiftEffect : IVoiceEffect
{
    // Written by whichever thread owns the effect (e.g. UI thread for a "Pitch Livre" slider),
    // read only by the audio thread at the top of Process(). volatile gives us a safe publish
    // without needing a lock: Process() always applies the latest value to the SoundTouch
    // processor itself, so the third-party SoundTouchWaveProvider is only ever mutated from the
    // audio thread.
    private volatile float _pendingSemitones;
    private QueuedWaveProvider? _source;
    private SoundTouchWaveProvider? _processor;
    private int _configuredSampleRate = -1;

    // Reusable hot-path buffers for Process(), resized only when the caller's buffer size
    // actually changes (mirrors the pattern AudioEngine.OnDataAvailable uses), so steady-state
    // streaming does no per-callback allocation.
    private byte[] _inputBytes = Array.Empty<byte>();
    private byte[] _outputBytes = Array.Empty<byte>();

    public PitchShiftEffect(float semitones)
    {
        _pendingSemitones = semitones;
    }

    public float Semitones
    {
        get => _pendingSemitones;
        set => _pendingSemitones = value;
    }

    public void Process(float[] buffer, int sampleRate)
    {
        EnsureConfigured(sampleRate);

        // Apply any pitch change requested from another thread. This runs exclusively on the
        // audio thread that calls Process(), so it's the only place that ever writes to
        // _processor.PitchSemiTones.
        if (_processor!.PitchSemiTones != _pendingSemitones)
        {
            _processor.PitchSemiTones = _pendingSemitones;
        }

        int byteLength = buffer.Length * sizeof(float);
        if (_inputBytes.Length != byteLength)
        {
            _inputBytes = new byte[byteLength];
            _outputBytes = new byte[byteLength];
        }

        Buffer.BlockCopy(buffer, 0, _inputBytes, 0, byteLength);
        _source!.Enqueue(_inputBytes, byteLength);

        // Measured warm-up behavior (final whole-branch review): SoundTouch's internal latency
        // means the first ~80-100ms of output after a cold start or a pitch-preset switch can come
        // back as silence (QueuedWaveProvider.Read pads short reads with silence so this call never
        // blocks). That warm-up window is bounded and does not recur or drift during a sustained
        // session -- see PitchShiftEffectTests.RepeatedSmallBufferCalls_NoNaNOrCrash for the
        // regression test guarding against a crash/NaN/hang regression in this streaming path.
        int read = 0;
        while (read < byteLength)
        {
            int n = _processor!.Read(_outputBytes, read, byteLength - read);
            if (n <= 0) break;
            read += n;
        }

        Buffer.BlockCopy(_outputBytes, 0, buffer, 0, byteLength);
    }

    private void EnsureConfigured(int sampleRate)
    {
        if (_configuredSampleRate == sampleRate) return;

        var format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        _source = new QueuedWaveProvider(format);
        _processor = new SoundTouchWaveProvider(_source) { PitchSemiTones = _pendingSemitones };
        _configuredSampleRate = sampleRate;
    }
}
