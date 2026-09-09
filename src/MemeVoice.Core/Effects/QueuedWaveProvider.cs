using System;
using NAudio.Wave;

namespace MemeVoice.Core.Effects;

/// <summary>
/// A byte ring buffer exposed as an <see cref="IWaveProvider"/>. Enqueue/Read operate in bulk via
/// <see cref="Buffer.BlockCopy"/> rather than per-byte, so steady-state streaming (buffer size
/// stable call to call, as it is on the WASAPI capture callback) does no per-byte work and no
/// per-call allocation once the ring has grown to its working size.
/// </summary>
internal sealed class QueuedWaveProvider : IWaveProvider
{
    private byte[] _buffer;
    private int _head; // index of the oldest unread byte
    private int _count; // number of unread bytes currently stored

    public WaveFormat WaveFormat { get; }

    public QueuedWaveProvider(WaveFormat waveFormat)
    {
        WaveFormat = waveFormat;
        _buffer = new byte[4096];
    }

    public void Enqueue(byte[] data, int length)
    {
        EnsureCapacity(_count + length);

        int tail = (_head + _count) % _buffer.Length;
        int firstPart = Math.Min(length, _buffer.Length - tail);
        Buffer.BlockCopy(data, 0, _buffer, tail, firstPart);
        if (firstPart < length)
        {
            Buffer.BlockCopy(data, firstPart, _buffer, 0, length - firstPart);
        }

        _count += length;
    }

    // Measured warm-up behavior (final whole-branch review): when the ring has fewer bytes than
    // requested -- expected only during the SoundTouch processor's initial warm-up latency
    // (bounded ~80-100ms) after a cold start or a pitch-preset switch, not during steady-state
    // streaming -- this pads the remainder with silence so SoundTouchWaveProvider's upstream read
    // never blocks or throws. Verified via a throwaway probe (not part of the permanent suite)
    // that SoundTouchWaveProvider tolerates short/zero upstream reads without crashing, stalling,
    // or emitting NaN. See PitchShiftEffectTests.RepeatedSmallBufferCalls_NoNaNOrCrash for the
    // regression test guarding this streaming path against a crash/NaN/hang regression.
    public int Read(byte[] buffer, int offset, int count)
    {
        int available = Math.Min(count, _count);
        if (available > 0)
        {
            int firstPart = Math.Min(available, _buffer.Length - _head);
            Buffer.BlockCopy(_buffer, _head, buffer, offset, firstPart);
            if (firstPart < available)
            {
                Buffer.BlockCopy(_buffer, 0, buffer, offset + firstPart, available - firstPart);
            }

            _head = (_head + available) % _buffer.Length;
            _count -= available;
        }

        // Pad remainder with silence so SoundTouchWaveProvider never underflows/blocks.
        for (int i = available; i < count; i++)
        {
            buffer[offset + i] = 0;
        }

        return count;
    }

    private void EnsureCapacity(int requiredCapacity)
    {
        if (requiredCapacity <= _buffer.Length) return;

        int newCapacity = _buffer.Length;
        while (newCapacity < requiredCapacity)
        {
            newCapacity *= 2;
        }

        var newBuffer = new byte[newCapacity];
        if (_count > 0)
        {
            int firstPart = Math.Min(_count, _buffer.Length - _head);
            Buffer.BlockCopy(_buffer, _head, newBuffer, 0, firstPart);
            if (firstPart < _count)
            {
                Buffer.BlockCopy(_buffer, 0, newBuffer, firstPart, _count - firstPart);
            }
        }

        _buffer = newBuffer;
        _head = 0;
    }
}
