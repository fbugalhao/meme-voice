// src/MemeVoice.Core/AudioEngine.cs
using System;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace MemeVoice.Core;

public sealed class AudioEngine : IDisposable
{
    /// <summary>Target capture/render latency, matching NAudio's default useEventSync buffer granularity.</summary>
    private const int CaptureBufferMilliseconds = 20;

    /// <summary>
    /// KSDATAFORMAT_SUBTYPE_IEEE_FLOAT. Many real WASAPI endpoints report their shared-mode mix
    /// format as WAVE_FORMAT_EXTENSIBLE even when the actual sample payload is IEEE float; the
    /// true type is only discoverable via this SubFormat GUID.
    /// </summary>
    private static readonly Guid KsDataFormatSubtypeIeeeFloat = new("00000003-0000-0010-8000-00AA00389B71");

    private readonly EffectChain _chain;
    private WasapiCapture? _capture;
    private BufferedWaveProvider? _bufferedProvider;
    private WasapiOut? _output;

    // Reusable hot-path buffers for OnDataAvailable, resized only when the incoming buffer's
    // size actually changes (WASAPI's negotiated buffer size is stable for the life of a
    // capture session, so in steady state no allocation happens per callback).
    private float[] _sampleBuffer = Array.Empty<float>();
    private byte[] _outputByteBuffer = Array.Empty<byte>();

    // Discard-rate-limiting state for the buffer-overflow log line (see OnDataAvailable). Only
    // ever touched from the capture callback thread, so no synchronization is needed.
    private long _discardedSampleCount;
    private DateTime _lastDiscardLogUtc = DateTime.MinValue;

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

            _capture = new WasapiCapture(inputDevice, useEventSync: true, audioBufferMillisecondsLength: CaptureBufferMilliseconds);

            if (!IsSupportedFloatFormat(_capture.WaveFormat))
            {
                var actualEncoding = _capture.WaveFormat.Encoding;
                var actualBits = _capture.WaveFormat.BitsPerSample;
                var actualChannels = _capture.WaveFormat.Channels;
                _capture.Dispose();
                _capture = null;
                IsRunning = false;
                SimpleLogger.LogError($"AudioEngine.Start rejected capture format: {actualEncoding} {actualBits}-bit, {actualChannels} channel(s).");
                OnError?.Invoke(
                    $"Unsupported capture format: {actualEncoding} {actualBits}-bit, {actualChannels} channel(s). " +
                    "AudioEngine requires 32-bit IEEE float mono capture.");
                return;
            }

            _bufferedProvider = new BufferedWaveProvider(_capture.WaveFormat)
            {
                DiscardOnBufferOverflow = true
            };

            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += OnRecordingStopped;

            _output = new WasapiOut(outputDevice, AudioClientShareMode.Shared, useEventSync: true, latency: 20);
            _output.Init(_bufferedProvider);

            _capture.StartRecording();
            _output.Play();
            IsRunning = true;
        }
        catch (Exception ex)
        {
            IsRunning = false;
            SimpleLogger.LogError($"AudioEngine.Start failed: {ex}");
            OnError?.Invoke(ex.Message);
        }
    }

    /// <summary>
    /// True if <paramref name="format"/> is 32-bit IEEE float mono, whether reported directly as
    /// WAVE_FORMAT_IEEE_FLOAT or wrapped in WAVE_FORMAT_EXTENSIBLE (a common WASAPI quirk where
    /// the container tag is Extensible but SubFormat identifies the real sample type). Every
    /// downstream effect (PitchShiftEffect, RingModulator/LfoOscillator, EchoEffect) assumes a
    /// single channel, so a stereo (or other multi-channel) capture device must be rejected here
    /// rather than silently corrupted.
    /// </summary>
    private static bool IsSupportedFloatFormat(WaveFormat format)
    {
        if (format.Channels != 1)
        {
            return false;
        }

        if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
        {
            return true;
        }

        if (format.Encoding == WaveFormatEncoding.Extensible
            && format is WaveFormatExtensible extensible
            && extensible.SubFormat == KsDataFormatSubtypeIeeeFloat
            && extensible.BitsPerSample == 32)
        {
            return true;
        }

        return false;
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            SimpleLogger.LogError($"Recording stopped unexpectedly: {e.Exception}");
            IsRunning = false;
            OnError?.Invoke("Microfone desconectado. Processamento parado.");
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_capture == null || _bufferedProvider == null) return;

        int bytesPerSample = _capture.WaveFormat.BitsPerSample / 8;
        int sampleCount = e.BytesRecorded / bytesPerSample;

        // ApplyEffectChain's contract processes the whole array it's given (chain.Process uses
        // samples.Length), so the reused buffer must be exactly sampleCount long, not just "big
        // enough". WASAPI's negotiated buffer size is stable for the life of a capture session, so
        // in steady state sampleCount doesn't change and this reallocates once (at most a few times
        // while the format settles) rather than on every callback.
        if (_sampleBuffer.Length != sampleCount)
        {
            _sampleBuffer = new float[sampleCount];
        }

        if (_outputByteBuffer.Length != e.BytesRecorded)
        {
            _outputByteBuffer = new byte[e.BytesRecorded];
        }

        for (int i = 0; i < sampleCount; i++)
        {
            _sampleBuffer[i] = BitConverter.ToSingle(e.Buffer, i * bytesPerSample);
        }

        ApplyEffectChain(_sampleBuffer, _capture.WaveFormat.SampleRate, _chain);

        Buffer.BlockCopy(_sampleBuffer, 0, _outputByteBuffer, 0, e.BytesRecorded);

        // BufferedWaveProvider is configured with DiscardOnBufferOverflow = true (see Start()), so
        // a buffer that's already full silently drops these incoming bytes instead of throwing.
        // That's the right behavior for a real-time thread (never block/throw here), but it must
        // not be silent: the design spec requires an underrun/glitch to be logged for diagnosis.
        // Detect an imminent discard *before* calling AddSamples (once the provider is full,
        // there's no post-hoc way to tell whether this specific call's bytes made it in), and
        // rate-limit the actual log write to once per second so the logging itself can't become a
        // new real-time-thread hazard.
        int bufferedBytes = _bufferedProvider.BufferedBytes;
        int bufferLength = _bufferedProvider.BufferLength;
        if (bufferedBytes + e.BytesRecorded > bufferLength)
        {
            _discardedSampleCount++;
            var now = DateTime.UtcNow;
            if (now - _lastDiscardLogUtc >= TimeSpan.FromSeconds(1))
            {
                _lastDiscardLogUtc = now;
                SimpleLogger.LogError(
                    $"AudioEngine: output buffer overflow, discarding audio (count since last log: {_discardedSampleCount}).");
                _discardedSampleCount = 0;
            }
        }

        _bufferedProvider.AddSamples(_outputByteBuffer, 0, e.BytesRecorded);
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

        if (_capture != null)
        {
            _capture.DataAvailable -= OnDataAvailable;
            _capture.RecordingStopped -= OnRecordingStopped;
            _capture.Dispose();
            _capture = null;
        }

        _output?.Dispose();
        _output = null;
    }

    public void Dispose()
    {
        Stop();
    }
}
