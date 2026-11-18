#nullable disable
using System;
using System.IO;

namespace ArcanumLib.Audio.NLayer.Decoder;

internal class BitReservoir
{
	private byte[] _buf = new byte[8192];

	private int _start;

	private int _end = -1;

	private int _bitsLeft;

	private long _bitsRead;

	public int BitsAvailable
	{
		get
		{
			if (_bitsLeft > 0)
			{
				return (_end + _buf.Length - _start) % _buf.Length * 8 + _bitsLeft;
			}
			return 0;
		}
	}

	public long BitsRead => _bitsRead;

	private static int GetSlots(IMpegFrame frame)
	{
		int num = frame.FrameLength - 4;
		if (frame.HasCrc)
		{
			num -= 2;
		}
		if (frame.Version == MpegVersion.Version1 && frame.ChannelMode != MpegChannelMode.Mono)
		{
			return num - 32;
		}
		if (frame.Version > MpegVersion.Version1 && frame.ChannelMode == MpegChannelMode.Mono)
		{
			return num - 9;
		}
		return num - 17;
	}

	public bool AddBits(IMpegFrame frame, int overlap)
	{
		int before = _end;
		int num = GetSlots(frame);
		while (--num >= 0)
		{
			int num2 = frame.ReadBits(8);
			if (num2 == -1)
			{
				throw new InvalidDataException("Frame did not have enough bytes!");
			}
			_buf[++_end] = (byte)num2;
			if (_end == _buf.Length - 1)
			{
				_end = -1;
			}
		}
		_bitsLeft = 8;
		int len = _buf.Length;
		int available = (before + 1 - _start + len) % len;
		if (available >= overlap)
		{
			_start = (before + 1 - overlap + len) % len;
			return true;
		}
		if (available > len / 2)
		{
			_start = (before + 1 - 1024 + len) % len;
		}
		return false;
	}

	public int GetBits(int count)
	{
		int result = TryPeekBits(count, out var readCount);
		if (readCount < count)
		{
			throw new InvalidDataException("Reservoir did not have enough bytes!");
		}
		SkipBits(count);
		return result;
	}

	public int Get1Bit()
	{
		if (_bitsLeft == 0)
		{
			throw new InvalidDataException("Reservoir did not have enough bytes!");
		}
		_bitsLeft--;
		_bitsRead++;
		int result = (_buf[_start] >> _bitsLeft) & 1;
		if (_bitsLeft == 0 && (_start = (_start + 1) % _buf.Length) != _end + 1)
		{
			_bitsLeft = 8;
		}
		return result;
	}

	public int TryPeekBits(int count, out int readCount)
	{
		if (count < 0 || count > 32)
		{
			throw new ArgumentOutOfRangeException("count", "Must return between 0 and 32 bits!");
		}
		if (_bitsLeft == 0 || count == 0)
		{
			readCount = 0;
			return 0;
		}
		int num = _buf[_start];
		if (count < _bitsLeft)
		{
			num >>= _bitsLeft - count;
			num &= (1 << count) - 1;
			readCount = count;
			return num;
		}
		num &= (1 << _bitsLeft) - 1;
		count -= _bitsLeft;
		readCount = _bitsLeft;
		int num2 = _start;
		while (count > 0 && (num2 = (num2 + 1) % _buf.Length) != _end + 1)
		{
			int num3 = Math.Min(count, 8);
			num <<= num3;
			num |= _buf[num2] >> (8 - num3) % 8;
			count -= num3;
			readCount += num3;
		}
		return num;
	}

	public void SkipBits(int count)
	{
		if (count <= 0)
		{
			return;
		}
		if (count >= BitsAvailable)
		{
			_start = (_end + 1) % _buf.Length;
			_bitsLeft = 0;
			_bitsRead += count;
			return;
		}
		int num = 8 - _bitsLeft + count;
		_start = (num / 8 + _start) % _buf.Length;
		_bitsLeft = 8 - num % 8;
		_bitsRead += count;
	}

	public void RewindBits(int count)
	{
		_bitsLeft += count;
		_bitsRead -= count;
		while (_bitsLeft > 8)
		{
			_start--;
			_bitsLeft -= 8;
		}
		while (_start < 0)
		{
			_start += _buf.Length;
		}
	}

	public void FlushBits()
	{
		if (_bitsLeft < 8)
		{
			SkipBits(_bitsLeft);
		}
	}

	public void Reset()
	{
		_start = 0;
		_end = -1;
		_bitsLeft = 0;
	}
}
