#nullable disable
using System;
using System.IO;
using System.Threading.Tasks;

namespace ArcanumLib.Audio.NLayer.Decoder;

internal class MpegStreamReader
{
	private class ReadBuffer
	{
		public byte[] Data;

		public long BaseOffset;

		public int End;

		public int DiscardCount;

		private object _localLock = new object();

		public ReadBuffer(int initialSize)
		{
			initialSize = 2 << (int)Math.Log(initialSize, 2.0);
			Data = new byte[initialSize];
		}

		public int Read(MpegStreamReader reader, long offset, byte[] buffer, int index, int count)
		{
			lock (_localLock)
			{
				int srcOffset = EnsureFilled(reader, offset, ref count);
				Buffer.BlockCopy(Data, srcOffset, buffer, index, count);
				return count;
			}
		}

		public int ReadByte(MpegStreamReader reader, long offset)
		{
			lock (_localLock)
			{
				int count = 1;
				int num = EnsureFilled(reader, offset, ref count);
				if (count == 1)
				{
					return Data[num];
				}
			}
			return -1;
		}

		private int EnsureFilled(MpegStreamReader reader, long offset, ref int count)
		{
			int num = (int)(offset - BaseOffset);
			int num2 = num + count;
			if (num < 0 || num2 > End)
			{
				int num3 = 0;
				int num4 = 0;
				int num5 = 0;
				long num6 = 0L;
				if (num < 0)
				{
					if (!reader._source.CanSeek)
					{
						throw new InvalidOperationException("Cannot seek backwards on a forward-only stream!");
					}
					if (End > 0 && (num + Data.Length > 0 || (Data.Length * 2 <= 16384 && num + Data.Length * 2 > 0)))
					{
						num2 = End;
					}
					num6 = offset;
					if (num2 < 0)
					{
						Truncate();
						BaseOffset = offset;
						num = 0;
						num2 = count;
						num4 = count;
					}
					else
					{
						num5 = -num2;
						num4 = -num;
					}
				}
				else if (num2 < Data.Length)
				{
					num4 = num2 - End;
					num3 = End;
					num6 = BaseOffset + num3;
				}
				else if (num2 - DiscardCount < Data.Length)
				{
					num5 = DiscardCount;
					num3 = End;
					num4 = num2 - num3;
					num6 = BaseOffset + num3;
				}
				else if (Data.Length * 2 <= 16384)
				{
					num5 = DiscardCount;
					num3 = End;
					num4 = num2 - End;
					num6 = BaseOffset + num3;
				}
				else
				{
					Truncate();
					BaseOffset = offset;
					num6 = offset;
					num = 0;
					num2 = count;
					num4 = count;
				}
				if (num2 - num5 > Data.Length || num3 + num4 - num5 > Data.Length)
				{
					int num7;
					for (num7 = Data.Length * 2; num7 < num2 - num5; num7 *= 2)
					{
					}
					byte[] array = new byte[num7];
					if (num5 < 0)
					{
						Buffer.BlockCopy(Data, 0, array, -num5, End + num5);
						DiscardCount = 0;
					}
					else
					{
						Buffer.BlockCopy(Data, num5, array, 0, End - num5);
						DiscardCount -= num5;
					}
					Data = array;
				}
				else if (num5 != 0)
				{
					if (num5 > 0)
					{
						Buffer.BlockCopy(Data, num5, Data, 0, End - num5);
						DiscardCount -= num5;
					}
					else
					{
						int num8 = 0;
						int num9 = Data.Length - 1;
						int num10 = Data.Length - 1 - num5;
						while (num8 < num5)
						{
							Data[num10] = Data[num9];
							num8++;
							num9--;
							num10--;
						}
						DiscardCount = 0;
					}
				}
				BaseOffset += num5;
				num3 -= num5;
				num -= num5;
				num2 -= num5;
				End -= num5;
				lock (reader._readLock)
				{
					if (num4 > 0 && reader._source.Position != num6 && num6 < reader._eofOffset)
					{
						if (reader._canSeek)
						{
							try
							{
								reader._source.Position = num6;
							}
							catch (EndOfStreamException)
							{
								reader._eofOffset = reader._source.Length;
								num4 = 0;
							}
						}
						else
						{
							long num11 = num6 - reader._source.Position;
							while (--num11 >= 0)
							{
								if (reader._source.ReadByte() == -1)
								{
									reader._eofOffset = reader._source.Position;
									num4 = 0;
									break;
								}
							}
						}
					}
					while (num4 > 0 && num6 < reader._eofOffset)
					{
						int num12 = reader._source.Read(Data, num3, num4);
						if (num12 == 0)
						{
							break;
						}
						num3 += num12;
						num6 += num12;
						num4 -= num12;
					}
					if (num3 > End)
					{
						End = num3;
					}
					if (End < num2)
					{
						count = Math.Max(0, End - num);
					}
					else if (End < Data.Length)
					{
						int num13 = reader._source.Read(Data, End, Data.Length - End);
						End += num13;
					}
				}
			}
			return num;
		}

		public void DiscardThrough(long offset)
		{
			lock (_localLock)
			{
				int val = (int)(offset - BaseOffset);
				DiscardCount = Math.Max(val, DiscardCount);
				if (DiscardCount >= Data.Length)
				{
					CommitDiscard();
				}
			}
		}

		private void Truncate()
		{
			End = 0;
			DiscardCount = 0;
		}

		private void CommitDiscard()
		{
			if (DiscardCount >= Data.Length || DiscardCount >= End)
			{
				BaseOffset += DiscardCount;
				End = 0;
			}
			else
			{
				Buffer.BlockCopy(Data, DiscardCount, Data, 0, End - DiscardCount);
				BaseOffset += DiscardCount;
				End -= DiscardCount;
			}
			DiscardCount = 0;
		}
	}

	private ID3Frame _id3Frame;

	private ID3Frame _id3v1Frame;

	private RiffHeaderFrame _riffHeaderFrame;

	private VBRInfo _vbrInfo;

	private MpegFrame _first;

	private MpegFrame _current;

	private MpegFrame _last;

	private MpegFrame _lastFree;

	private long _readOffset;

	private long _eofOffset;

	private Stream _source;

	private bool _canSeek;

	private bool _endFound;

	private bool _mixedFrameSize;

	private object _readLock = new object();

	private object _frameLock = new object();

	private ReadBuffer _readBuf = new ReadBuffer(2048);

	internal bool CanSeek => _canSeek;

	internal long SampleCount
	{
		get
		{
			if (_vbrInfo != null)
			{
				return _vbrInfo.VBRStreamSampleCount;
			}
			if (!_canSeek)
			{
				return -1L;
			}
			ReadToEnd();
			return _last.SampleCount + _last.SampleOffset;
		}
	}

	internal int SampleRate
	{
		get
		{
			if (_vbrInfo != null)
			{
				return _vbrInfo.SampleRate;
			}
			return _first.SampleRate;
		}
	}

	internal int Channels
	{
		get
		{
			if (_vbrInfo != null)
			{
				return _vbrInfo.Channels;
			}
			return _first.Channels;
		}
	}

	internal int FirstFrameSampleCount
	{
		get
		{
			if (_first == null)
			{
				return 0;
			}
			return _first.SampleCount;
		}
	}

	internal MpegStreamReader(Stream source)
	{
		_source = source;
		_canSeek = source.CanSeek;
		_readOffset = 0L;
		_eofOffset = long.MaxValue;
		FrameBase frameBase = FindNextFrame();
		while (frameBase != null && !(frameBase is MpegFrame))
		{
			frameBase = FindNextFrame();
		}
		if (frameBase == null)
		{
			throw new InvalidDataException("Not a valid MPEG file!");
		}
		frameBase = FindNextFrame();
		if (frameBase == null || !(frameBase is MpegFrame))
		{
			throw new InvalidDataException("Not a valid MPEG file!");
		}
		_current = _first;
	}

	private FrameBase FindNextFrame()
	{
		if (_endFound)
		{
			return null;
		}
		MpegFrame lastFree = _lastFree;
		long num = _readOffset;
		lock (_frameLock)
		{
			byte[] array = new byte[4];
			try
			{
				if (Read(_readOffset, array, 0, 4) == 4)
				{
					do
					{
						uint syncMark = (uint)((array[0] << 24) | (array[1] << 16) | (array[2] << 8) | array[3]);
						num = _readOffset;
						if (_id3Frame == null)
						{
							ID3Frame iD3Frame = ID3Frame.TrySync(syncMark);
							if (iD3Frame != null && iD3Frame.Validate(_readOffset, this))
							{
								if (!_canSeek)
								{
									iD3Frame.SaveBuffer();
								}
								_readOffset += iD3Frame.Length;
								DiscardThrough(_readOffset, minimalRead: true);
								return _id3Frame = iD3Frame;
							}
						}
						if (_first == null && _riffHeaderFrame == null)
						{
							RiffHeaderFrame riffHeaderFrame = RiffHeaderFrame.TrySync(syncMark);
							if (riffHeaderFrame != null && riffHeaderFrame.Validate(_readOffset, this))
							{
								_readOffset += riffHeaderFrame.Length;
								DiscardThrough(_readOffset, minimalRead: true);
								return _riffHeaderFrame = riffHeaderFrame;
							}
						}
						MpegFrame mpegFrame = MpegFrame.TrySync(syncMark);
						if (mpegFrame != null && mpegFrame.Validate(_readOffset, this) && (lastFree == null || (mpegFrame.Layer == lastFree.Layer && mpegFrame.Version == lastFree.Version && mpegFrame.SampleRate == lastFree.SampleRate && mpegFrame.BitRateIndex <= 0)))
						{
							if (!_canSeek)
							{
								mpegFrame.SaveBuffer();
								DiscardThrough(_readOffset + mpegFrame.FrameLength, minimalRead: true);
							}
							_readOffset += mpegFrame.FrameLength;
							if (_first == null)
							{
								if (_vbrInfo == null && (_vbrInfo = mpegFrame.ParseVBR()) != null)
								{
									return FindNextFrame();
								}
								mpegFrame.Number = 0;
								_first = (_last = mpegFrame);
							}
							else
							{
								if (mpegFrame.SampleCount != _first.SampleCount)
								{
									_mixedFrameSize = true;
								}
								mpegFrame.SampleOffset = _last.SampleCount + _last.SampleOffset;
								mpegFrame.Number = _last.Number + 1;
								_last = (_last.Next = mpegFrame);
							}
							if (mpegFrame.BitRateIndex == 0)
							{
								_lastFree = mpegFrame;
							}
							return mpegFrame;
						}
						if (_last != null)
						{
							ID3Frame iD3Frame2 = ID3Frame.TrySync(syncMark);
							if (iD3Frame2 != null && iD3Frame2.Validate(_readOffset, this))
							{
								if (!_canSeek)
								{
									iD3Frame2.SaveBuffer();
								}
								if (iD3Frame2.Version == 1)
								{
									_id3v1Frame = iD3Frame2;
								}
								else
								{
									_id3Frame.Merge(iD3Frame2);
								}
								_readOffset += iD3Frame2.Length;
								DiscardThrough(_readOffset, minimalRead: true);
								return iD3Frame2;
							}
						}
						_readOffset++;
						if (_first == null || !_canSeek)
						{
							DiscardThrough(_readOffset, minimalRead: true);
						}
						Buffer.BlockCopy(array, 1, array, 0, 3);
					}
					while (Read(_readOffset + 3, array, 3, 1) == 1);
				}
				num += 4;
				_endFound = true;
				return null;
			}
			finally
			{
				if (lastFree != null)
				{
					lastFree.Length = (int)(num - lastFree.Offset);
					if (!_canSeek)
					{
						throw new InvalidOperationException("Free frames cannot be read properly from forward-only streams!");
					}
					if (_lastFree == lastFree)
					{
						_lastFree = null;
					}
				}
			}
		}
	}

	internal int Read(long offset, byte[] buffer, int index, int count)
	{
		if (offset < 0)
		{
			throw new ArgumentOutOfRangeException("offset");
		}
		if (index < 0 || index + count > buffer.Length)
		{
			throw new ArgumentOutOfRangeException("index");
		}
		return _readBuf.Read(this, offset, buffer, index, count);
	}

	internal int ReadByte(long offset)
	{
		if (offset < 0)
		{
			throw new ArgumentOutOfRangeException("offset");
		}
		return _readBuf.ReadByte(this, offset);
	}

	internal void DiscardThrough(long offset, bool minimalRead)
	{
		_readBuf.DiscardThrough(offset);
	}

	internal void ReadToEnd()
	{
		try
		{
			int num = 40000;
			if (_id3Frame != null)
			{
				num += _id3Frame.Length;
			}
			while (!_endFound)
			{
				FindNextFrame();
				while (!_canSeek && FrameBase.TotalAllocation >= num)
				{
					Task.Delay(500).Wait();
				}
			}
		}
		catch (ObjectDisposedException)
		{
		}
	}

	internal long SeekTo(long sampleNumber)
	{
		if (!_canSeek)
		{
			throw new InvalidOperationException("Cannot seek!");
		}
		int num = (int)(sampleNumber / _first.SampleCount);
		MpegFrame mpegFrame = _first;
		if (_current != null && _current.Number <= num && _current.SampleOffset <= sampleNumber)
		{
			mpegFrame = _current;
			num -= mpegFrame.Number;
		}
		while (!_mixedFrameSize && --num >= 0 && mpegFrame != null)
		{
			if (mpegFrame == _last && !_endFound)
			{
				do
				{
					FindNextFrame();
				}
				while (mpegFrame == _last && !_endFound);
			}
			if (_mixedFrameSize)
			{
				break;
			}
			mpegFrame = mpegFrame.Next;
		}
		while (mpegFrame != null && mpegFrame.SampleOffset + mpegFrame.SampleCount < sampleNumber)
		{
			if (mpegFrame == _last && !_endFound)
			{
				do
				{
					FindNextFrame();
				}
				while (mpegFrame == _last && !_endFound);
			}
			mpegFrame = mpegFrame.Next;
		}
		if (mpegFrame == null)
		{
			return -1L;
		}
		return (_current = mpegFrame).SampleOffset;
	}

	internal MpegFrame NextFrame()
	{
		MpegFrame current = _current;
		if (current != null)
		{
			if (_canSeek)
			{
				current.SaveBuffer();
				DiscardThrough(current.Offset + current.FrameLength, minimalRead: false);
			}
			if (current == _last && !_endFound)
			{
				do
				{
					FindNextFrame();
				}
				while (current == _last && !_endFound);
			}
			_current = current.Next;
			if (!_canSeek)
			{
				lock (_frameLock)
				{
					MpegFrame first = _first;
					_first = first.Next;
					first.Next = null;
				}
			}
		}
		return current;
	}

	internal MpegFrame GetCurrentFrame()
	{
		return _current;
	}
}
