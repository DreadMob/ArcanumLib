#nullable disable
namespace ArcanumLib.Audio.NLayer.Decoder;

internal class ID3Frame : FrameBase
{
	private int _version;

	internal int Version
	{
		get
		{
			if (_version == 0)
			{
				return 1;
			}
			return _version;
		}
	}

	internal static ID3Frame TrySync(uint syncMark)
	{
		if ((syncMark & 0xFFFFFF00u) == 1229206272)
		{
			return new ID3Frame
			{
				_version = 2
			};
		}
		if ((syncMark & 0xFFFFFF00u) == 1413564160)
		{
			if ((syncMark & 0xFF) == 43)
			{
				return new ID3Frame
				{
					_version = 1
				};
			}
			return new ID3Frame
			{
				_version = 0
			};
		}
		return null;
	}

	private ID3Frame()
	{
	}

	protected override int Validate()
	{
		switch (_version)
		{
		case 2:
		{
			byte[] array = new byte[7];
			if (Read(3, array) == 7)
			{
				byte b;
				switch (array[0])
				{
				case 3:
					b = 31;
					break;
				case 4:
					b = 15;
					break;
				default:
					return -1;
				}
				int num = (array[3] << 21) | (array[4] << 14) | (array[5] << 7) | array[6];
				if (((array[2] & b) | (array[3] & 0x80) | (array[4] & 0x80) | (array[5] & 0x80) | (array[6] & 0x80)) == 0 && array[1] != byte.MaxValue)
				{
					return num + 10;
				}
			}
			break;
		}
		case 1:
			return 355;
		case 0:
			return 128;
		}
		return -1;
	}

	internal override void Parse()
	{
		switch (_version)
		{
		case 2:
			ParseV2();
			break;
		case 1:
			ParseV1Enh();
			break;
		case 0:
			ParseV1(3);
			break;
		}
	}

	private void ParseV1(int offset)
	{
	}

	private void ParseV1Enh()
	{
		ParseV1(230);
	}

	private void ParseV2()
	{
	}

	internal void Merge(ID3Frame newFrame)
	{
	}
}
