#nullable disable
using System;
using System.Threading;

namespace ArcanumLib.Audio.NLayer.Decoder;

internal abstract class FrameBase
{
	private static int _totalAllocation;

	private MpegStreamReader _reader;

	private byte[] _savedBuffer;

	internal static int TotalAllocation => Interlocked.CompareExchange(ref _totalAllocation, 0, 0);

	internal long Offset { get; private set; }

	internal int Length { get; set; }

	internal bool Validate(long offset, MpegStreamReader reader)
	{
		Offset = offset;
		_reader = reader;
		int num = Validate();
		if (num > 0)
		{
			Length = num;
			return true;
		}
		return false;
	}

	protected int Read(int offset, byte[] buffer)
	{
		return Read(offset, buffer, 0, buffer.Length);
	}

	protected int Read(int offset, byte[] buffer, int index, int count)
	{
		if (_savedBuffer != null)
		{
			if (index < 0 || index + count > buffer.Length)
			{
				return 0;
			}
			if (offset < 0 || offset >= _savedBuffer.Length)
			{
				return 0;
			}
			if (offset + count > _savedBuffer.Length)
			{
				count = _savedBuffer.Length - index;
			}
			Array.Copy(_savedBuffer, offset, buffer, index, count);
			return count;
		}
		return _reader.Read(Offset + offset, buffer, index, count);
	}

	protected int ReadByte(int offset)
	{
		if (_savedBuffer != null)
		{
			if (offset < 0)
			{
				throw new ArgumentOutOfRangeException();
			}
			if (offset >= _savedBuffer.Length)
			{
				return -1;
			}
			return _savedBuffer[offset];
		}
		return _reader.ReadByte(Offset + offset);
	}

	protected abstract int Validate();

	internal void SaveBuffer()
	{
		_savedBuffer = new byte[Length];
		_reader.Read(Offset, _savedBuffer, 0, Length);
		Interlocked.Add(ref _totalAllocation, Length);
	}

	internal void ClearBuffer()
	{
		Interlocked.Add(ref _totalAllocation, -Length);
		_savedBuffer = null;
	}

	internal virtual void Parse()
	{
	}
}
