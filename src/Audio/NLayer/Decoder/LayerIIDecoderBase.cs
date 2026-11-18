#nullable disable
using System;

namespace ArcanumLib.Audio.NLayer.Decoder;

internal abstract class LayerIIDecoderBase : LayerDecoderBase
{
	protected const int SSLIMIT = 12;

	private static readonly float[] _groupedC = new float[5] { 0f, 0f, 1.3333334f, 1.6f, 1.7777778f };

	private static readonly float[] _groupedD = new float[5] { 0f, 0f, -0.5f, -0.5f, -0.5f };

	private static readonly float[] _C = new float[17]
	{
		0f, 0f, 1.3333334f, 1.1428572f, 1.0666667f, 1.032258f, 1.0158731f, 1.007874f, 1.0039216f, 1.0019569f,
		1.0009775f, 1.0004885f, 1.0002443f, 1.0001221f, 1.000061f, 1.0000305f, 1.0000153f
	};

	private static readonly float[] _D = new float[17]
	{
		0f,
		0f,
		-0.5f,
		-0.75f,
		-0.875f,
		-0.9375f,
		-31f / 32f,
		-63f / 64f,
		-127f / 128f,
		-255f / 256f,
		-0.9980469f,
		-0.99902344f,
		-0.9995117f,
		-0.99975586f,
		-0.9998779f,
		-0.99993896f,
		-0.9999695f
	};

	private static readonly float[] _denormalMultiplier = new float[64]
	{
		2f,
		1.587401f,
		1.2599211f,
		1f,
		0.7937005f,
		0.62996054f,
		0.5f,
		0.39685026f,
		0.31498027f,
		0.25f,
		0.19842513f,
		0.15749013f,
		0.125f,
		0.099212565f,
		0.07874507f,
		0.0625f,
		0.049606282f,
		0.039372534f,
		1f / 32f,
		0.024803141f,
		0.019686267f,
		1f / 64f,
		0.012401571f,
		0.009843133f,
		1f / 128f,
		0.0062007853f,
		0.0049215667f,
		1f / 256f,
		0.0031003926f,
		0.0024607833f,
		2f / 1024f,
		0.0015501963f,
		0.0012303917f,
		1f / 1024f,
		0.00077509816f,
		0.00061519584f,
		2f / 4096f,
		0.00038754908f,
		0.00030759792f,
		1f / 4096f,
		0.00019377454f,
		0.00015379896f,
		1f / 8192f,
		9.688727E-05f,
		7.689948E-05f,
		1f / 16384f,
		4.8443635E-05f,
		3.844974E-05f,
		1f / 32768f,
		2.4221818E-05f,
		1.922487E-05f,
		1f / 65536f,
		1.2110909E-05f,
		9.612435E-06f,
		8f / 1048576f,
		6.0554544E-06f,
		4.8062175E-06f,
		4f / 1048576f,
		3.0277272E-06f,
		2.4031087E-06f,
		2f / 1048576f,
		1.5138636E-06f,
		1.2015544E-06f,
		1f / 1048576f
	};

	private int _channels;

	private int _jsbound;

	private int _granuleCount;

	private int[][] _allocLookupTable;

	private int[][] _scfsi;

	private int[][] _samples;

	private int[][][] _scalefac;

	private float[] _polyPhaseBuf;

	private int[][] _allocation;

	protected static bool GetCRC(MpegFrame frame, int[] rateTable, int[][] allocLookupTable, bool readScfsiBits, ref uint crc)
	{
		int num = 0;
		int num2 = rateTable.Length;
		int num3 = num2;
		if (frame.ChannelMode == MpegChannelMode.JointStereo)
		{
			num3 = frame.ChannelModeExtension * 4 + 4;
		}
		int num4 = ((frame.ChannelMode == MpegChannelMode.Mono) ? 1 : 2);
		int i;
		for (i = 0; i < num3; i++)
		{
			int num5 = allocLookupTable[rateTable[i]][0];
			for (int j = 0; j < num4; j++)
			{
				int num6 = frame.ReadBits(num5);
				if (num6 > 0)
				{
					num += 2;
				}
				MpegFrame.UpdateCRC(num6, num5, ref crc);
			}
		}
		for (; i < num2; i++)
		{
			int num7 = allocLookupTable[rateTable[i]][0];
			int num8 = frame.ReadBits(num7);
			if (num8 > 0)
			{
				num += num4 * 2;
			}
			MpegFrame.UpdateCRC(num8, num7, ref crc);
		}
		if (readScfsiBits)
		{
			while (num >= 2)
			{
				MpegFrame.UpdateCRC(frame.ReadBits(2), 2, ref crc);
				num -= 2;
			}
		}
		return true;
	}

	protected LayerIIDecoderBase(int[][] allocLookupTable, int granuleCount)
	{
		_allocLookupTable = allocLookupTable;
		_granuleCount = granuleCount;
		_allocation = new int[2][]
		{
			new int[32],
			new int[32]
		};
		_scfsi = new int[2][]
		{
			new int[32],
			new int[32]
		};
		_samples = new int[2][]
		{
			new int[384 * _granuleCount],
			new int[384 * _granuleCount]
		};
		_scalefac = new int[2][][]
		{
			new int[3][],
			new int[3][]
		};
		for (int i = 0; i < 3; i++)
		{
			_scalefac[0][i] = new int[32];
			_scalefac[1][i] = new int[32];
		}
		_polyPhaseBuf = new float[32];
	}

	internal override int DecodeFrame(IMpegFrame frame, float[] ch0, float[] ch1)
	{
		InitFrame(frame);
		int[] rateTable = GetRateTable(frame);
		ReadAllocation(frame, rateTable);
		for (int i = 0; i < _scfsi[0].Length; i++)
		{
			_scfsi[0][i] = ((_allocation[0][i] != 0) ? 2 : (-1));
			_scfsi[1][i] = ((_allocation[1][i] != 0) ? 2 : (-1));
		}
		ReadScaleFactorSelection(frame, _scfsi, _channels);
		ReadScaleFactors(frame);
		ReadSamples(frame);
		return DecodeSamples(ch0, ch1);
	}

	private void InitFrame(IMpegFrame frame)
	{
		switch (frame.ChannelMode)
		{
		case MpegChannelMode.Mono:
			_channels = 1;
			_jsbound = 32;
			break;
		case MpegChannelMode.JointStereo:
			_channels = 2;
			_jsbound = frame.ChannelModeExtension * 4 + 4;
			break;
		default:
			_channels = 2;
			_jsbound = 32;
			break;
		}
	}

	protected abstract int[] GetRateTable(IMpegFrame frame);

	private void ReadAllocation(IMpegFrame frame, int[] rateTable)
	{
		int num = rateTable.Length;
		if (_jsbound > num)
		{
			_jsbound = num;
		}
		Array.Clear(_allocation[0], 0, 32);
		Array.Clear(_allocation[1], 0, 32);
		int i;
		for (i = 0; i < _jsbound; i++)
		{
			int[] array = _allocLookupTable[rateTable[i]];
			int bitCount = array[0];
			for (int j = 0; j < _channels; j++)
			{
				_allocation[j][i] = array[frame.ReadBits(bitCount) + 1];
			}
		}
		for (; i < num; i++)
		{
			int[] array2 = _allocLookupTable[rateTable[i]];
			_allocation[0][i] = (_allocation[1][i] = array2[frame.ReadBits(array2[0]) + 1]);
		}
	}

	protected abstract void ReadScaleFactorSelection(IMpegFrame frame, int[][] scfsi, int channels);

	private void ReadScaleFactors(IMpegFrame frame)
	{
		for (int i = 0; i < 32; i++)
		{
			for (int j = 0; j < _channels; j++)
			{
				switch (_scfsi[j][i])
				{
				case 0:
					_scalefac[j][0][i] = frame.ReadBits(6);
					_scalefac[j][1][i] = frame.ReadBits(6);
					_scalefac[j][2][i] = frame.ReadBits(6);
					break;
				case 1:
					_scalefac[j][0][i] = (_scalefac[j][1][i] = frame.ReadBits(6));
					_scalefac[j][2][i] = frame.ReadBits(6);
					break;
				case 2:
					_scalefac[j][0][i] = (_scalefac[j][1][i] = (_scalefac[j][2][i] = frame.ReadBits(6)));
					break;
				case 3:
					_scalefac[j][0][i] = frame.ReadBits(6);
					_scalefac[j][1][i] = (_scalefac[j][2][i] = frame.ReadBits(6));
					break;
				default:
					_scalefac[j][0][i] = 63;
					_scalefac[j][1][i] = 63;
					_scalefac[j][2][i] = 63;
					break;
				}
			}
		}
	}

	private void ReadSamples(IMpegFrame frame)
	{
		int num = 0;
		int num2 = 0;
		while (num < 12)
		{
			int num3 = 0;
			while (num3 < 32)
			{
				for (int i = 0; i < _channels; i++)
				{
					if (i == 0 || num3 < _jsbound)
					{
						int num4 = _allocation[i][num3];
						if (num4 != 0)
						{
							if (num4 < 0)
							{
								int num5 = frame.ReadBits(-num4);
								int num6 = (1 << -num4 / 2 + -num4 % 2 - 1) + 1;
								_samples[i][num2] = num5 % num6;
								num5 /= num6;
								_samples[i][num2 + 32] = num5 % num6;
								_samples[i][num2 + 64] = num5 / num6;
							}
							else
							{
								for (int j = 0; j < _granuleCount; j++)
								{
									_samples[i][num2 + 32 * j] = frame.ReadBits(num4);
								}
							}
						}
						else
						{
							for (int k = 0; k < _granuleCount; k++)
							{
								_samples[i][num2 + 32 * k] = 0;
							}
						}
					}
					else
					{
						for (int l = 0; l < _granuleCount; l++)
						{
							_samples[1][num2 + 32 * l] = _samples[0][num2 + 32 * l];
						}
					}
				}
				num3++;
				num2++;
			}
			num++;
			num2 += 32 * (_granuleCount - 1);
		}
	}

	private int DecodeSamples(float[] ch0, float[] ch1)
	{
		float[][] array = new float[2][];
		int num = 0;
		int num2 = _channels - 1;
		if (_channels == 1 || StereoMode == StereoMode.LeftOnly)
		{
			array[0] = ch0;
			num2 = 0;
		}
		else if (StereoMode == StereoMode.RightOnly)
		{
			array[1] = ch0;
			num = 1;
		}
		else
		{
			array[0] = ch0;
			array[1] = ch1;
		}
		int num3 = 0;
		for (int i = num; i <= num2; i++)
		{
			num3 = 0;
			for (int j = 0; j < _granuleCount; j++)
			{
				for (int k = 0; k < 12; k++)
				{
					int num4 = 0;
					while (num4 < 32)
					{
						int num5 = _allocation[i][num4];
						if (num5 != 0)
						{
							float[] array2;
							float[] array3;
							if (num5 < 0)
							{
								num5 = -num5 / 2 + -num5 % 2 - 1;
								array2 = _groupedC;
								array3 = _groupedD;
							}
							else
							{
								array2 = _C;
								array3 = _D;
							}
							_polyPhaseBuf[num4] = array2[num5] * ((float)(_samples[i][num3] << 16 - num5) / 32768f + array3[num5]) * _denormalMultiplier[_scalefac[i][j][num4]];
						}
						else
						{
							_polyPhaseBuf[num4] = 0f;
						}
						num4++;
						num3++;
					}
					InversePolyPhase(i, _polyPhaseBuf);
					Array.Copy(_polyPhaseBuf, 0, array[i], num3 - 32, 32);
				}
			}
		}
		if (_channels == 2 && StereoMode == StereoMode.DownmixToMono)
		{
			for (int l = 0; l < num3; l++)
			{
				ch0[l] = (ch0[l] + ch1[l]) / 2f;
			}
		}
		return num3;
	}
}
