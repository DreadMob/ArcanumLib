#nullable disable
namespace ArcanumLib.Audio.NLayer.Decoder;

internal class LayerIIDecoder : LayerIIDecoderBase
{
	private static readonly int[][] _rateLookupTable = new int[5][]
	{
		new int[27]
		{
			3, 3, 3, 2, 2, 2, 2, 2, 2, 2,
			2, 1, 1, 1, 1, 1, 1, 1, 1, 1,
			1, 1, 1, 0, 0, 0, 0
		},
		new int[30]
		{
			3, 3, 3, 2, 2, 2, 2, 2, 2, 2,
			2, 1, 1, 1, 1, 1, 1, 1, 1, 1,
			1, 1, 1, 0, 0, 0, 0, 0, 0, 0
		},
		new int[8] { 4, 4, 5, 5, 5, 5, 5, 5 },
		new int[12]
		{
			4, 4, 5, 5, 5, 5, 5, 5, 5, 5,
			5, 5
		},
		new int[30]
		{
			6, 6, 6, 6, 5, 5, 5, 5, 5, 5,
			5, 7, 7, 7, 7, 7, 7, 7, 7, 7,
			7, 7, 7, 7, 7, 7, 7, 7, 7, 7
		}
	};

	private static readonly int[][] _allocLookupTable = new int[8][]
	{
		new int[5] { 2, 0, -5, -7, 16 },
		new int[9] { 3, 0, -5, -7, 3, -10, 4, 5, 16 },
		new int[17]
		{
			4, 0, -5, -7, 3, -10, 4, 5, 6, 7,
			8, 9, 10, 11, 12, 13, 16
		},
		new int[17]
		{
			4, 0, -5, 3, 4, 5, 6, 7, 8, 9,
			10, 11, 12, 13, 14, 15, 16
		},
		new int[17]
		{
			4, 0, -5, -7, -10, 4, 5, 6, 7, 8,
			9, 10, 11, 12, 13, 14, 15
		},
		new int[9] { 3, 0, -5, -7, -10, 4, 5, 6, 9 },
		new int[17]
		{
			4, 0, -5, -7, 3, -10, 4, 5, 6, 7,
			8, 9, 10, 11, 12, 13, 14
		},
		new int[5] { 2, 0, -5, -7, 3 }
	};

	internal static bool GetCRC(MpegFrame frame, ref uint crc)
	{
		return LayerIIDecoderBase.GetCRC(frame, SelectTable(frame), _allocLookupTable, readScfsiBits: true, ref crc);
	}

	private static int[] SelectTable(IMpegFrame frame)
	{
		int num = frame.BitRate / ((frame.ChannelMode == MpegChannelMode.Mono) ? 1 : 2) / 1000;
		if (frame.Version == MpegVersion.Version1)
		{
			if ((num >= 56 && num <= 80) || (frame.SampleRate == 48000 && num >= 56))
			{
				return _rateLookupTable[0];
			}
			if (frame.SampleRate != 48000 && num >= 96)
			{
				return _rateLookupTable[1];
			}
			if (frame.SampleRate != 32000 && num <= 48)
			{
				return _rateLookupTable[2];
			}
			return _rateLookupTable[3];
		}
		return _rateLookupTable[4];
	}

	internal LayerIIDecoder()
		: base(_allocLookupTable, 3)
	{
	}

	protected override int[] GetRateTable(IMpegFrame frame)
	{
		return SelectTable(frame);
	}

	protected override void ReadScaleFactorSelection(IMpegFrame frame, int[][] scfsi, int channels)
	{
		for (int i = 0; i < 30; i++)
		{
			for (int j = 0; j < channels; j++)
			{
				if (scfsi[j][i] == 2)
				{
					scfsi[j][i] = frame.ReadBits(2);
				}
			}
		}
	}
}
