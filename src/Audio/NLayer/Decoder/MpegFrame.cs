#nullable disable
using System;
using System.IO;

namespace ArcanumLib.Audio.NLayer.Decoder;

internal class MpegFrame : FrameBase, IMpegFrame
{
	private static readonly int[][][] _bitRateTable = new int[2][][]
	{
		new int[3][]
		{
			new int[15]
			{
				0, 32, 64, 96, 128, 160, 192, 224, 256, 288,
				320, 352, 384, 416, 448
			},
			new int[15]
			{
				0, 32, 48, 56, 64, 80, 96, 112, 128, 160,
				192, 224, 256, 320, 384
			},
			new int[15]
			{
				0, 32, 40, 48, 56, 64, 80, 96, 112, 128,
				160, 192, 224, 256, 320
			}
		},
		new int[3][]
		{
			new int[15]
			{
				0, 32, 48, 56, 64, 80, 96, 112, 128, 144,
				160, 176, 192, 224, 256
			},
			new int[15]
			{
				0, 8, 16, 24, 32, 40, 48, 56, 64, 80,
				96, 112, 128, 144, 160
			},
			new int[15]
			{
				0, 8, 16, 24, 32, 40, 48, 56, 64, 80,
				96, 112, 128, 144, 160
			}
		}
	};

	internal MpegFrame Next;

	internal int Number;

	private int _syncBits;

	private int _readOffset;

	private int _bitsRead;

	private ulong _bitBucket;

	private long _offset;

	private bool _isMuted;

	public int FrameLength => Length;

	public MpegVersion Version => ((_syncBits >> 19) & 3) switch
	{
		0 => MpegVersion.Version25, 
		2 => MpegVersion.Version2, 
		3 => MpegVersion.Version1, 
		_ => MpegVersion.Unknown, 
	};

	public MpegLayer Layer => (MpegLayer)((4 - ((_syncBits >> 17) & 3)) % 4);

	public bool HasCrc => (_syncBits & 0x10000) == 0;

	public int BitRate
	{
		get
		{
			if (BitRateIndex > 0)
			{
				return _bitRateTable[(int)Version / 10 - 1][(int)(Layer - 1)][BitRateIndex] * 1000;
			}
			return (FrameLength * 8 * SampleRate / SampleCount + 499 + 500) / 1000 * 1000;
		}
	}

	public int BitRateIndex => (_syncBits >> 12) & 0xF;

	public int SampleRate
	{
		get
		{
			int num = SampleRateIndex switch
			{
				0 => 44100, 
				1 => 48000, 
				2 => 32000, 
				_ => 0, 
			};
			if (Version > MpegVersion.Version1)
			{
				num = ((Version != MpegVersion.Version25) ? (num / 2) : (num / 4));
			}
			return num;
		}
	}

	public int SampleRateIndex => (_syncBits >> 10) & 3;

	private int Padding => (_syncBits >> 9) & 1;

	public MpegChannelMode ChannelMode => (MpegChannelMode)((_syncBits >> 6) & 3);

	public int ChannelModeExtension => (_syncBits >> 4) & 3;

	internal int Channels
	{
		get
		{
			if (ChannelMode != MpegChannelMode.Mono)
			{
				return 2;
			}
			return 1;
		}
	}

	public bool IsCopyrighted => (_syncBits & 8) == 8;

	internal bool IsOriginal => (_syncBits & 4) == 4;

	internal int EmphasisMode => _syncBits & 3;

	public bool IsCorrupted => _isMuted;

	public int SampleCount
	{
		get
		{
			if (Layer == MpegLayer.LayerI)
			{
				return 384;
			}
			if (Layer == MpegLayer.LayerIII && Version > MpegVersion.Version1)
			{
				return 576;
			}
			return 1152;
		}
	}

	internal long SampleOffset
	{
		get
		{
			return _offset;
		}
		set
		{
			_offset = value;
		}
	}

	internal static MpegFrame TrySync(uint syncMark)
	{
		if ((syncMark & 0xFFE00000u) == 4292870144u && (syncMark & 0x180000) != 524288 && (syncMark & 0x60000) != 0 && (syncMark & 0xF000) != 61440 && (syncMark & 0xC00) != 3072)
		{
			switch ((syncMark >> 4) & 0xF)
			{
			case 0u:
			case 4u:
			case 5u:
			case 6u:
			case 7u:
			case 8u:
			case 12u:
				return new MpegFrame
				{
					_syncBits = (int)syncMark
				};
			}
		}
		return null;
	}

	private MpegFrame()
	{
	}

	protected override int Validate()
	{
		if (Layer == MpegLayer.LayerII)
		{
			switch (BitRate)
			{
			case 32000:
			case 48000:
			case 56000:
			case 80000:
				if (ChannelMode != MpegChannelMode.Mono)
				{
					return -1;
				}
				break;
			case 224000:
			case 256000:
			case 320000:
			case 384000:
				if (ChannelMode == MpegChannelMode.Mono)
				{
					return -1;
				}
				break;
			}
		}
		int result;
		if (BitRateIndex <= 0)
		{
			result = _readOffset + GetSideDataSize() + Padding;
		}
		else
		{
			result = ((Layer != MpegLayer.LayerI) ? (144 * BitRate / SampleRate + Padding) : ((12 * BitRate / SampleRate + Padding) * 4));
		}
		if (HasCrc)
		{
			_readOffset = 4 + (HasCrc ? 2 : 0);
			if (!ValidateCRC())
			{
				_isMuted = true;
				return 6;
			}
		}
		Reset();
		return result;
	}

	internal int GetSideDataSize()
	{
		switch (Layer)
		{
		case MpegLayer.LayerI:
			if (ChannelMode == MpegChannelMode.Mono)
			{
				return 16;
			}
			if (ChannelMode == MpegChannelMode.Stereo || ChannelMode == MpegChannelMode.DualChannel)
			{
				return 32;
			}
			switch (ChannelModeExtension)
			{
			case 0:
				return 18;
			case 1:
				return 20;
			case 2:
				return 22;
			case 3:
				return 24;
			}
			break;
		case MpegLayer.LayerII:
			return 0;
		case MpegLayer.LayerIII:
			if (ChannelMode == MpegChannelMode.Mono && Version >= MpegVersion.Version2)
			{
				return 9;
			}
			if (ChannelMode != MpegChannelMode.Mono && Version < MpegVersion.Version2)
			{
				return 32;
			}
			return 17;
		}
		return 0;
	}

	private bool ValidateCRC()
	{
		uint crc = 65535u;
		UpdateCRC(_syncBits, 16, ref crc);
		bool flag = false;
		switch (Layer)
		{
		case MpegLayer.LayerI:
			flag = LayerIDecoder.GetCRC(this, ref crc);
			break;
		case MpegLayer.LayerII:
			flag = LayerIIDecoder.GetCRC(this, ref crc);
			break;
		case MpegLayer.LayerIII:
			flag = LayerIIIDecoder.GetCRC(this, ref crc);
			break;
		}
		if (flag)
		{
			return ((ReadByte(4) << 8) | ReadByte(5)) == crc;
		}
		return true;
	}

	internal static void UpdateCRC(int data, int length, ref uint crc)
	{
		uint num = (uint)(1 << length);
		while ((num >>= 1) != 0)
		{
			uint num2 = crc & 0x8000;
			crc <<= 1;
			if ((num2 == 0) ^ ((data & num) == 0))
			{
				crc ^= 32773u;
			}
		}
		crc &= 65535u;
	}

	internal VBRInfo ParseVBR()
	{
		byte[] array = new byte[4];
		int num;
		if (Version == MpegVersion.Version1 && ChannelMode != MpegChannelMode.Mono)
		{
			num = 36;
		}
		else
		{
			num = ((Version <= MpegVersion.Version1 || ChannelMode != MpegChannelMode.Mono) ? 21 : 13);
		}
		if (Read(num, array) != 4)
		{
			return null;
		}
		if ((array[0] == 88 && array[1] == 105 && array[2] == 110 && array[3] == 103) || (array[0] == 73 && array[1] == 110 && array[2] == 102 && array[3] == 111))
		{
			return ParseXing(num + 4);
		}
		if (Read(36, array) != 4)
		{
			return null;
		}
		if (array[0] == 86 && array[1] == 66 && array[2] == 82 && array[3] == 73)
		{
			return ParseVBRI();
		}
		return null;
	}

	private VBRInfo ParseXing(int offset)
	{
		VBRInfo vBRInfo = new VBRInfo();
		vBRInfo.Channels = Channels;
		vBRInfo.SampleRate = SampleRate;
		vBRInfo.SampleCount = SampleCount;
		byte[] array = new byte[100];
		if (Read(offset, array, 0, 4) != 4)
		{
			return null;
		}
		offset += 4;
		int num = (array[0] << 24) | (array[1] << 16) | (array[2] << 8) | array[3];
		if ((num & 1) != 0)
		{
			if (Read(offset, array, 0, 4) != 4)
			{
				return null;
			}
			offset += 4;
			vBRInfo.VBRFrames = (array[0] << 24) | (array[1] << 16) | (array[2] << 8) | array[3];
		}
		if ((num & 2) != 0)
		{
			if (Read(offset, array, 0, 4) != 4)
			{
				return null;
			}
			offset += 4;
			vBRInfo.VBRBytes = (array[0] << 24) | (array[1] << 16) | (array[2] << 8) | array[3];
		}
		if ((num & 4) != 0)
		{
			if (Read(offset, array) != 100)
			{
				return null;
			}
			offset += 100;
		}
		if ((num & 8) != 0)
		{
			if (Read(offset, array, 0, 4) != 4)
			{
				return null;
			}
			offset += 4;
			vBRInfo.VBRQuality = (array[0] << 24) | (array[1] << 16) | (array[2] << 8) | array[3];
		}
		return vBRInfo;
	}

	private VBRInfo ParseVBRI()
	{
		VBRInfo vBRInfo = new VBRInfo();
		vBRInfo.Channels = Channels;
		vBRInfo.SampleRate = SampleRate;
		vBRInfo.SampleCount = SampleCount;
		byte[] array = new byte[26];
		if (Read(36, array) != 26)
		{
			return null;
		}
		_ = array[4];
		_ = array[5];
		vBRInfo.VBRDelay = (array[6] << 8) | array[7];
		vBRInfo.VBRQuality = (array[8] << 8) | array[9];
		vBRInfo.VBRBytes = (array[10] << 24) | (array[11] << 16) | (array[12] << 8) | array[13];
		vBRInfo.VBRFrames = (array[14] << 24) | (array[15] << 16) | (array[16] << 8) | array[17];
		int num = (array[18] << 8) | array[19];
		_ = array[20];
		_ = array[21];
		int num2 = (array[22] << 8) | array[23];
		_ = array[24];
		_ = array[25];
		int num3 = num * num2;
		byte[] buffer = new byte[num3];
		if (Read(62, buffer) != num3)
		{
			return null;
		}
		return vBRInfo;
	}

	public void Reset()
	{
		_readOffset = 4 + (HasCrc ? 2 : 0);
		_bitBucket = 0uL;
		_bitsRead = 0;
	}

	public int ReadBits(int bitCount)
	{
		if (bitCount < 1 || bitCount > 32)
		{
			throw new ArgumentOutOfRangeException("bitCount");
		}
		if (_isMuted)
		{
			return 0;
		}
		while (_bitsRead < bitCount)
		{
			int num = ReadByte(_readOffset);
			if (num == -1)
			{
				throw new EndOfStreamException();
			}
			_readOffset++;
			_bitBucket <<= 8;
			_bitBucket |= (byte)(num & 0xFF);
			_bitsRead += 8;
		}
		int result = (int)((long)(_bitBucket >> _bitsRead - bitCount) & ((1L << bitCount) - 1));
		_bitsRead -= bitCount;
		return result;
	}
}
