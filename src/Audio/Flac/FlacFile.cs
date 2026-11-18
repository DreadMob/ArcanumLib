#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

namespace ArcanumLib.Audio.Flac;

/// <summary>
/// Pure-C# streaming FLAC decoder (RFC 9639) producing interleaved signed 16-bit PCM.
/// Supports 8/16/24/32-bit PCM, 1-2 channels (multi-channel down-selects L/R),
/// CONSTANT / VERBATIM / FIXED / LPC subframes, Rice &amp; Rice2 residuals,
/// and seeking via SEEKTABLE or frame-sync binary search.
/// </summary>
public sealed class FlacFile : IDisposable
{
    private readonly Stream _input;
    private readonly BitReader _br;
    private readonly long _firstFrameOffset;

    public int SampleRate { get; }
    public int RawChannels { get; }
    public int Channels { get; }
    public int BitsPerSample { get; }
    public long TotalSamples { get; }
    public bool CanSeek => _input.CanSeek;

    public TimeSpan Duration => SampleRate > 0 && TotalSamples > 0
        ? TimeSpan.FromSeconds((double)TotalSamples / SampleRate)
        : TimeSpan.Zero;

    public TimeSpan Time
    {
        get => SampleRate > 0 ? TimeSpan.FromSeconds((double)_currentSample / SampleRate) : TimeSpan.Zero;
        set => Seek(value.TotalSeconds);
    }

    private readonly int _minBlockSize;
    private readonly int _maxBlockSize;
    private readonly SeekPoint[] _seekPoints;

    private long _currentSample;
    private bool _eof;

    private readonly int[][] _channelBuffers;
    private byte[] _pcmOut = Array.Empty<byte>();
    private int _pcmOfs;
    private int _pcmLen;

    public FlacFile(Stream input)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _br = new BitReader(input);

        Span<byte> magic = stackalloc byte[4];
        if (!_br.ReadExactBytes(magic) || magic[0] != 'f' || magic[1] != 'L' || magic[2] != 'a' || magic[3] != 'C')
            throw new InvalidDataException("not a FLAC stream");

        bool hasStreamInfo = false;
        int minBlock = 0, maxBlock = 0, sr = 0, ch = 0, bps = 0;
        long totalSamples = 0;
        var seekList = new List<SeekPoint>();

        bool last = false;
        while (!last)
        {
            uint hdr = _br.ReadBits(32);
            last = (hdr >> 31) != 0;
            int type = (int)((hdr >> 24) & 0x7F);
            int len = (int)(hdr & 0xFFFFFF);

            if (type == 0)
            {
                if (len < 34) throw new InvalidDataException("invalid FLAC STREAMINFO");
                minBlock = (int)_br.ReadBits(16);
                maxBlock = (int)_br.ReadBits(16);
                _br.SkipBits(48); // minFrameSize + maxFrameSize
                sr = (int)_br.ReadBits(20);
                ch = (int)_br.ReadBits(3) + 1;
                bps = (int)_br.ReadBits(5) + 1;
                totalSamples = ((long)_br.ReadBits(4) << 32) | _br.ReadBits(32);
                _br.SkipBytes(16); // MD5
                if (len > 34) _br.SkipBytes(len - 34);
                hasStreamInfo = true;
            }
            else if (type == 3)
            {
                int count = len / 18;
                for (int i = 0; i < count; i++)
                {
                    ulong sample = _br.ReadUInt64();
                    ulong offset = _br.ReadUInt64();
                    ushort frameSamples = (ushort)_br.ReadBits(16);
                    if (sample != ulong.MaxValue)
                        seekList.Add(new SeekPoint((long)sample, (long)offset, frameSamples));
                }
                int rem = len - count * 18;
                if (rem > 0) _br.SkipBytes(rem);
            }
            else
            {
                _br.SkipBytes(len);
            }
        }

        if (!hasStreamInfo || sr <= 0 || ch <= 0 || maxBlock <= 0)
            throw new InvalidDataException("missing or invalid FLAC STREAMINFO");

        _minBlockSize = minBlock;
        _maxBlockSize = maxBlock;
        SampleRate = sr;
        RawChannels = ch;
        Channels = Math.Min(ch, 2);
        BitsPerSample = bps;
        TotalSamples = totalSamples;
        _seekPoints = seekList.ToArray();
        _firstFrameOffset = _br.StreamBytePosition;

        _channelBuffers = new int[ch][];
        for (int i = 0; i < ch; i++)
            _channelBuffers[i] = new int[Math.Max(maxBlock, 65536)];
    }

    public int ReadPcm16(byte[] buffer, int offset, int length)
    {
        int written = 0;
        while (written < length)
        {
            if (_pcmOfs < _pcmLen)
            {
                int copy = Math.Min(length - written, _pcmLen - _pcmOfs);
                Buffer.BlockCopy(_pcmOut, _pcmOfs, buffer, offset + written, copy);
                _pcmOfs += copy;
                written += copy;
                continue;
            }
            if (_eof || !DecodeNextFrame())
                break;
        }
        return written;
    }

    public void Seek(double seconds)
    {
        if (!_input.CanSeek || SampleRate <= 0 || seconds < 0) return;

        long target = (long)(seconds * SampleRate);
        if (TotalSamples > 0 && target >= TotalSamples)
            target = Math.Max(0, TotalSamples - SampleRate / 2);

        _pcmOfs = 0;
        _pcmLen = 0;
        _eof = false;

        if (_seekPoints.Length > 0)
        {
            int idx = 0;
            for (int i = 0; i < _seekPoints.Length; i++)
            {
                if (_seekPoints[i].SampleNumber <= target) idx = i;
                else break;
            }
            _br.SeekTo(_firstFrameOffset + _seekPoints[idx].StreamOffset);
            _currentSample = _seekPoints[idx].SampleNumber;
        }
        else if (TotalSamples > 0)
        {
            long dataLen = Math.Max(0, _input.Length - _firstFrameOffset);
            long approx = _firstFrameOffset + (long)((double)target / TotalSamples * dataLen);
            // Back up slightly to land before a frame sync
            approx = Math.Max(_firstFrameOffset, approx - 8192);
            _br.SeekTo(approx);
            if (!SyncToNextFrame())
            {
                _br.SeekTo(_firstFrameOffset);
                _currentSample = 0;
                return;
            }
        }
        else
        {
            _br.SeekTo(_firstFrameOffset);
            _currentSample = 0;
        }

        // Advance frame by frame until reaching target sample
        while (_currentSample < target && DecodeNextFrame())
        {
            long frameEnd = _currentSample;
            int frameSamples = _pcmLen / (2 * Channels);
            long frameStart = frameEnd - frameSamples;
            if (frameEnd > target && target >= frameStart)
            {
                int skipSamples = (int)(target - frameStart);
                _pcmOfs = skipSamples * 2 * Channels;
                break;
            }
            _pcmOfs = _pcmLen;
        }
    }

    private bool SyncToNextFrame()
    {
        int prev = _br.ReadByte();
        while (prev >= 0)
        {
            int cur = _br.ReadByte();
            if (cur < 0) return false;
            if (prev == 0xFF && (cur & 0xFE) == 0xF8)
            {
                _br.RewindBytes(2);
                return true;
            }
            prev = cur;
        }
        return false;
    }

    private bool DecodeNextFrame()
    {
        _br.AlignToByte();

        // Find sync code 0xFFF8 or 0xFFF9
        int b0 = _br.ReadByte();
        if (b0 < 0) { _eof = true; return false; }
        int b1 = _br.ReadByte();
        if (b1 < 0) { _eof = true; return false; }

        while (b0 != 0xFF || (b1 & 0xFE) != 0xF8)
        {
            b0 = b1;
            b1 = _br.ReadByte();
            if (b1 < 0) { _eof = true; return false; }
        }

        bool variableBlock = (b1 & 1) != 0;
        int b2 = _br.ReadByte();
        int b3 = _br.ReadByte();
        if (b2 < 0 || b3 < 0) { _eof = true; return false; }

        int bsCode = (b2 >> 4) & 0xF;
        int srCode = b2 & 0xF;
        int chAssign = (b3 >> 4) & 0xF;
        int ssCode = (b3 >> 1) & 0x7;

        long codedNum = _br.ReadUtf8UInt64();
        if (codedNum < 0) { _eof = true; return false; }

        int blockSize = bsCode switch
        {
            1 => 192,
            >= 2 and <= 5 => 576 << (bsCode - 2),
            6 => _br.ReadByteChecked() + 1,
            7 => (int)_br.ReadBits(16) + 1,
            >= 8 and <= 15 => 256 << (bsCode - 8),
            _ => throw new InvalidDataException("reserved FLAC block size")
        };

        int sampleRate = srCode switch
        {
            0 => SampleRate,
            1 => 88200,
            2 => 176400,
            3 => 192000,
            4 => 8000,
            5 => 16000,
            6 => 22050,
            7 => 24000,
            8 => 32000,
            9 => 44100,
            10 => 48000,
            11 => 96000,
            12 => _br.ReadByteChecked() * 1000,
            13 => (int)_br.ReadBits(16),
            14 => (int)_br.ReadBits(16) * 10,
            _ => throw new InvalidDataException("invalid FLAC sample rate")
        };

        int bps = ssCode switch
        {
            0 => BitsPerSample,
            1 => 8,
            2 => 12,
            4 => 16,
            5 => 20,
            6 => 24,
            7 => 32,
            _ => throw new InvalidDataException("reserved FLAC sample size")
        };

        _br.SkipBytes(1); // CRC-8

        int frameChannels = chAssign switch
        {
            >= 0 and <= 7 => chAssign + 1,
            >= 8 and <= 10 => 2,
            _ => throw new InvalidDataException("reserved FLAC channel assignment")
        };

        for (int ch = 0; ch < frameChannels; ch++)
        {
            int subBps = bps;
            if ((chAssign == 8 && ch == 1) || (chAssign == 9 && ch == 0) || (chAssign == 10 && ch == 1))
                subBps++;
            DecodeSubframe(_channelBuffers[ch], blockSize, subBps);
        }

        _br.AlignToByte();
        _br.SkipBytes(2); // CRC-16

        // Stereo decorrelation
        if (chAssign == 8)
        {
            int[] l = _channelBuffers[0], r = _channelBuffers[1];
            for (int i = 0; i < blockSize; i++) r[i] = l[i] - r[i];
        }
        else if (chAssign == 9)
        {
            int[] l = _channelBuffers[0], r = _channelBuffers[1];
            for (int i = 0; i < blockSize; i++) l[i] = l[i] + r[i];
        }
        else if (chAssign == 10)
        {
            int[] m = _channelBuffers[0], s = _channelBuffers[1];
            for (int i = 0; i < blockSize; i++)
            {
                int mid = (m[i] << 1) | (s[i] & 1);
                int side = s[i];
                m[i] = (mid + side) >> 1;
                s[i] = (mid - side) >> 1;
            }
        }

        int neededBytes = blockSize * Channels * 2;
        if (_pcmOut.Length < neededBytes) _pcmOut = new byte[neededBytes];

        int shift = bps - 16;
        int outPos = 0;
        for (int i = 0; i < blockSize; i++)
        {
            for (int ch = 0; ch < Channels; ch++)
            {
                int v = _channelBuffers[ch][i];
                int s16 = shift == 0 ? v : (shift > 0 ? v >> shift : v << -shift);
                if (s16 > short.MaxValue) s16 = short.MaxValue;
                else if (s16 < short.MinValue) s16 = short.MinValue;
                _pcmOut[outPos++] = (byte)s16;
                _pcmOut[outPos++] = (byte)(s16 >> 8);
            }
        }

        _pcmOfs = 0;
        _pcmLen = neededBytes;
        _currentSample = variableBlock
            ? codedNum + blockSize
            : codedNum * Math.Max(1, _minBlockSize) + blockSize;
        return true;
    }

    private void DecodeSubframe(int[] dst, int blockSize, int bps)
    {
        if (_br.ReadBits(1) != 0) throw new InvalidDataException("invalid FLAC subframe padding");
        int type = (int)_br.ReadBits(6);
        int wasted = (int)_br.ReadBits(1);
        if (wasted != 0)
        {
            wasted = 1;
            while (_br.ReadBits(1) == 0) wasted++;
            bps -= wasted;
        }

        if (type == 0) // CONSTANT
        {
            int val = _br.ReadSignedBits(bps);
            for (int i = 0; i < blockSize; i++) dst[i] = val;
        }
        else if (type == 1) // VERBATIM
        {
            for (int i = 0; i < blockSize; i++) dst[i] = _br.ReadSignedBits(bps);
        }
        else if ((type & 0x38) == 0x08) // FIXED
        {
            int order = type & 0x07;
            if (order > 4) throw new InvalidDataException("invalid FLAC FIXED order");
            for (int i = 0; i < order; i++) dst[i] = _br.ReadSignedBits(bps);
            DecodeResidual(dst, blockSize, order);
            RestoreFixed(dst, blockSize, order);
        }
        else if ((type & 0x20) != 0) // LPC
        {
            int order = (type & 0x1F) + 1;
            for (int i = 0; i < order; i++) dst[i] = _br.ReadSignedBits(bps);
            int precision = (int)_br.ReadBits(4) + 1;
            if (precision == 16) throw new InvalidDataException("invalid FLAC LPC precision");
            int shift = _br.ReadSignedBits(5);
            if (shift < 0) throw new InvalidDataException("negative FLAC LPC shift");

            Span<int> coefs = stackalloc int[32];
            for (int i = 0; i < order; i++) coefs[i] = _br.ReadSignedBits(precision);

            DecodeResidual(dst, blockSize, order);
            for (int i = order; i < blockSize; i++)
            {
                long sum = 0;
                for (int j = 0; j < order; j++)
                    sum += (long)coefs[j] * dst[i - j - 1];
                dst[i] += (int)(sum >> shift);
            }
        }
        else
        {
            throw new InvalidDataException("unsupported FLAC subframe type");
        }

        if (wasted > 0)
        {
            for (int i = 0; i < blockSize; i++) dst[i] <<= wasted;
        }
    }

    private static void RestoreFixed(int[] s, int n, int order)
    {
        switch (order)
        {
            case 0: break;
            case 1:
                for (int i = 1; i < n; i++) s[i] += s[i - 1];
                break;
            case 2:
                for (int i = 2; i < n; i++) s[i] += (s[i - 1] << 1) - s[i - 2];
                break;
            case 3:
                for (int i = 3; i < n; i++) s[i] += 3 * (s[i - 1] - s[i - 2]) + s[i - 3];
                break;
            case 4:
                for (int i = 4; i < n; i++) s[i] += 4 * (s[i - 1] + s[i - 3]) - 6 * s[i - 2] - s[i - 4];
                break;
        }
    }

    private void DecodeResidual(int[] dst, int blockSize, int predictorOrder)
    {
        int method = (int)_br.ReadBits(2);
        if (method > 1) throw new InvalidDataException("reserved FLAC residual coding method");
        int paramBits = method == 0 ? 4 : 5;
        int escapeCode = (1 << paramBits) - 1;
        int partitionOrder = (int)_br.ReadBits(4);
        int partitions = 1 << partitionOrder;
        int baseSamples = blockSize >> partitionOrder;

        int pos = predictorOrder;
        for (int p = 0; p < partitions; p++)
        {
            int count = p == 0 ? baseSamples - predictorOrder : baseSamples;
            int riceParam = (int)_br.ReadBits(paramBits);
            if (riceParam == escapeCode)
            {
                int rawBps = (int)_br.ReadBits(5);
                if (rawBps == 0)
                {
                    for (int i = 0; i < count; i++) dst[pos++] = 0;
                }
                else
                {
                    for (int i = 0; i < count; i++) dst[pos++] = _br.ReadSignedBits(rawBps);
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    uint q = _br.ReadUnary();
                    uint rem = riceParam > 0 ? _br.ReadBits(riceParam) : 0;
                    uint u = (q << riceParam) | rem;
                    dst[pos++] = (int)((u >> 1) ^ (uint)-(int)(u & 1));
                }
            }
        }
    }

    public void Dispose() => _input.Dispose();

    private readonly record struct SeekPoint(long SampleNumber, long StreamOffset, ushort FrameSamples);

    private sealed class BitReader
    {
        private readonly Stream _stream;
        private readonly byte[] _buf = new byte[16384];
        private int _bufPos;
        private int _bufLen;
        private long _streamReadPos;
        private ulong _bitCache;
        private int _bitCount;

        public long StreamBytePosition => _streamReadPos - (_bufLen - _bufPos) - (_bitCount / 8);

        public BitReader(Stream stream) => _stream = stream;

        public void SeekTo(long bytePos)
        {
            _stream.Position = bytePos;
            _streamReadPos = bytePos;
            _bufPos = 0;
            _bufLen = 0;
            _bitCache = 0;
            _bitCount = 0;
        }

        public void AlignToByte()
        {
            int drop = _bitCount & 7;
            if (drop > 0) _bitCount -= drop;
        }

        public bool ReadExactBytes(Span<byte> dst)
        {
            for (int i = 0; i < dst.Length; i++)
            {
                int b = ReadByte();
                if (b < 0) return false;
                dst[i] = (byte)b;
            }
            return true;
        }

        public void SkipBytes(int count)
        {
            AlignToByte();
            while (_bitCount >= 8 && count > 0)
            {
                _bitCount -= 8;
                count--;
            }
            int inBuf = _bufLen - _bufPos;
            if (count <= inBuf) { _bufPos += count; return; }
            count -= inBuf;
            _bufPos = 0;
            _bufLen = 0;
            if (_stream.CanSeek)
            {
                _stream.Seek(count, SeekOrigin.Current);
                _streamReadPos += count;
                return;
            }
            while (count > 0)
            {
                int n = _stream.Read(_buf, 0, Math.Min(count, _buf.Length));
                if (n <= 0) throw new EndOfStreamException();
                _streamReadPos += n;
                count -= n;
            }
        }

        public void SkipBits(int bits)
        {
            while (bits > 0)
            {
                int chunk = Math.Min(bits, 32);
                ReadBits(chunk);
                bits -= chunk;
            }
        }

        public int ReadByteChecked()
        {
            int b = ReadByte();
            if (b < 0) throw new EndOfStreamException();
            return b;
        }

        public int ReadByte()
        {
            if (_bitCount >= 8)
            {
                _bitCount -= 8;
                return (int)((_bitCache >> _bitCount) & 0xFF);
            }
            if (_bitCount > 0) AlignToByte();
            if (_bufPos >= _bufLen && !FillBuffer()) return -1;
            return _buf[_bufPos++];
        }

        public void RewindBytes(int count)
        {
            _bitCache = 0;
            _bitCount = 0;
            if (_bufPos >= count)
            {
                _bufPos -= count;
                return;
            }
            long target = StreamBytePosition - count;
            SeekTo(Math.Max(0, target));
        }

        private bool FillBuffer()
        {
            _bufPos = 0;
            _bufLen = _stream.Read(_buf, 0, _buf.Length);
            _streamReadPos += _bufLen;
            return _bufLen > 0;
        }

        private void EnsureBits(int n)
        {
            while (_bitCount < n)
            {
                if (_bufPos >= _bufLen && !FillBuffer())
                    throw new EndOfStreamException();
                _bitCache = (_bitCache << 8) | _buf[_bufPos++];
                _bitCount += 8;
            }
        }

        public uint ReadBits(int n)
        {
            if (n == 0) return 0;
            EnsureBits(n);
            _bitCount -= n;
            return (uint)((_bitCache >> _bitCount) & ((1UL << n) - 1));
        }

        public int ReadSignedBits(int n)
        {
            uint v = ReadBits(n);
            int shift = 32 - n;
            return ((int)v << shift) >> shift;
        }

        public ulong ReadUInt64() => ((ulong)ReadBits(32) << 32) | ReadBits(32);

        public uint ReadUnary()
        {
            uint q = 0;
            while (true)
            {
                if (_bitCount == 0) EnsureBits(8);
                int shift = _bitCount - 1;
                if (((_bitCache >> shift) & 1) != 0)
                {
                    _bitCount--;
                    return q;
                }
                _bitCount--;
                q++;
            }
        }

        public long ReadUtf8UInt64()
        {
            int b0 = ReadByte();
            if (b0 < 0) return -1;
            if ((b0 & 0x80) == 0) return b0;
            int extra = 0;
            int mask = 0x40;
            while ((b0 & mask) != 0) { extra++; mask >>= 1; }
            if (extra > 6) throw new InvalidDataException("invalid FLAC UTF-8 integer");
            long val = b0 & (mask - 1);
            for (int i = 0; i < extra; i++)
            {
                int b = ReadByte();
                if (b < 0 || (b & 0xC0) != 0x80) throw new InvalidDataException("invalid FLAC UTF-8 continuation");
                val = (val << 6) | (uint)(b & 0x3F);
            }
            return val;
        }
    }
}
