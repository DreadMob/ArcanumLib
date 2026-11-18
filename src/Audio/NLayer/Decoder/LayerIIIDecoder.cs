#nullable disable
using System;
using System.Collections.Generic;

namespace ArcanumLib.Audio.NLayer.Decoder;

internal sealed class LayerIIIDecoder : LayerDecoderBase
{
	private class HybridMDCT
	{
		private const float PI = (float)Math.PI;

		private static float[][] _swin;

		private static float[] icos72_table;

		private List<float[]> _prevBlock;

		private List<float[]> _nextBlock;

		private float[] _imdctTemp = new float[18];

		private float[] _imdctResult = new float[36];

		private const float sqrt32 = 0.8660254f;

		static HybridMDCT()
		{
			icos72_table = new float[35]
			{
				0.50047636f, 0.5019099f, 0.5043145f, 0.5077133f, 0.51213974f, 0.5176381f, 0.5242646f, 0.5320889f, 0.5411961f, 0.55168897f,
				0.56369096f, 0.57735026f, 0.59284455f, 0.61038727f, 0.6302362f, 0.65270364f, 0.67817086f, 0.70710677f, 0.7400936f, 0.7778619f,
				0.8213398f, 0.8717234f, 0.9305795f, 1f, 1.0828403f, 1.1831008f, 1.306563f, 1.4619021f, 1.6627548f, 1.9318516f,
				2.3101132f, 2.8793852f, 3.830649f, 5.7368565f, 11.462792f
			};
			_swin = new float[4][]
			{
				new float[36],
				new float[36],
				new float[36],
				new float[36]
			};
			for (int i = 0; i < 36; i++)
			{
				_swin[0][i] = (float)Math.Sin(0.0872664675116539 * ((double)i + 0.5));
			}
			for (int i = 0; i < 18; i++)
			{
				_swin[1][i] = (float)Math.Sin(0.0872664675116539 * ((double)i + 0.5));
			}
			for (int i = 18; i < 24; i++)
			{
				_swin[1][i] = 1f;
			}
			for (int i = 24; i < 30; i++)
			{
				_swin[1][i] = (float)Math.Sin(0.2617993950843811 * ((double)i + 0.5 - 18.0));
			}
			for (int i = 30; i < 36; i++)
			{
				_swin[1][i] = 0f;
			}
			for (int i = 0; i < 6; i++)
			{
				_swin[3][i] = 0f;
			}
			for (int i = 6; i < 12; i++)
			{
				_swin[3][i] = (float)Math.Sin(0.2617993950843811 * ((double)i + 0.5 - 6.0));
			}
			for (int i = 12; i < 18; i++)
			{
				_swin[3][i] = 1f;
			}
			for (int i = 18; i < 36; i++)
			{
				_swin[3][i] = (float)Math.Sin(0.0872664675116539 * ((double)i + 0.5));
			}
			for (int i = 0; i < 12; i++)
			{
				_swin[2][i] = (float)Math.Sin(0.2617993950843811 * ((double)i + 0.5));
			}
			for (int i = 12; i < 36; i++)
			{
				_swin[2][i] = 0f;
			}
		}

		internal HybridMDCT()
		{
			_prevBlock = new List<float[]>();
			_nextBlock = new List<float[]>();
		}

		internal void Reset()
		{
			_prevBlock.Clear();
			_nextBlock.Clear();
		}

		private void GetPrevBlock(int channel, out float[] prevBlock, out float[] nextBlock)
		{
			while (_prevBlock.Count <= channel)
			{
				_prevBlock.Add(new float[576]);
			}
			while (_nextBlock.Count <= channel)
			{
				_nextBlock.Add(new float[576]);
			}
			prevBlock = _prevBlock[channel];
			nextBlock = _nextBlock[channel];
			_nextBlock[channel] = prevBlock;
			_prevBlock[channel] = nextBlock;
		}

		internal void Apply(float[] fsIn, int channel, int blockType, bool doMixed)
		{
			GetPrevBlock(channel, out var prevBlock, out var nextBlock);
			int sbStart = 0;
			if (doMixed)
			{
				LongImpl(fsIn, 0, 2, nextBlock, 0);
				sbStart = 2;
			}
			if (blockType == 2)
			{
				ShortImpl(fsIn, sbStart, nextBlock);
			}
			else
			{
				LongImpl(fsIn, sbStart, 32, nextBlock, blockType);
			}
			for (int i = 0; i < 576; i++)
			{
				fsIn[i] += prevBlock[i];
			}
		}

		private void LongImpl(float[] fsIn, int sbStart, int sbLimit, float[] nextblck, int blockType)
		{
			int i = sbStart;
			int num = sbStart * 18;
			for (; i < sbLimit; i++)
			{
				Array.Copy(fsIn, num, _imdctTemp, 0, 18);
				LongIMDCT(_imdctTemp, _imdctResult);
				float[] array = _swin[blockType];
				int j;
				for (j = 0; j < 18; j++)
				{
					fsIn[num++] = _imdctResult[j] * array[j];
				}
				num -= 18;
				for (; j < 36; j++)
				{
					nextblck[num++] = _imdctResult[j] * array[j];
				}
			}
		}

		private static void LongIMDCT(float[] invec, float[] outvec)
		{
			float[] array = new float[17];
			float[] array2 = new float[18];
			float[] array3 = new float[9];
			float[] array4 = new float[9];
			float[] array5 = new float[9];
			float[] array6 = new float[9];
			int i;
			for (i = 0; i < 17; i++)
			{
				array[i] = invec[i] + invec[i + 1];
			}
			array3[0] = invec[0];
			array4[0] = array[0];
			int num = 0;
			i = 1;
			while (i < 9)
			{
				array3[i] = array[num + 1];
				array4[i] = array[num] + array[num + 2];
				i++;
				num += 2;
			}
			imdct_9pt(array3, array5);
			imdct_9pt(array4, array6);
			for (i = 0; i < 9; i++)
			{
				array6[i] *= ICOS36_A(i);
				array2[i] = (array5[i] + array6[i]) * ICOS72_A(i);
			}
			for (; i < 18; i++)
			{
				array2[i] = (array5[17 - i] - array6[17 - i]) * ICOS72_A(i);
			}
			outvec[0] = array2[9];
			outvec[1] = array2[10];
			outvec[2] = array2[11];
			outvec[3] = array2[12];
			outvec[4] = array2[13];
			outvec[5] = array2[14];
			outvec[6] = array2[15];
			outvec[7] = array2[16];
			outvec[8] = array2[17];
			outvec[9] = 0f - array2[17];
			outvec[10] = 0f - array2[16];
			outvec[11] = 0f - array2[15];
			outvec[12] = 0f - array2[14];
			outvec[13] = 0f - array2[13];
			outvec[14] = 0f - array2[12];
			outvec[15] = 0f - array2[11];
			outvec[16] = 0f - array2[10];
			outvec[17] = 0f - array2[9];
			outvec[35] = (outvec[18] = 0f - array2[8]);
			outvec[34] = (outvec[19] = 0f - array2[7]);
			outvec[33] = (outvec[20] = 0f - array2[6]);
			outvec[32] = (outvec[21] = 0f - array2[5]);
			outvec[31] = (outvec[22] = 0f - array2[4]);
			outvec[30] = (outvec[23] = 0f - array2[3]);
			outvec[29] = (outvec[24] = 0f - array2[2]);
			outvec[28] = (outvec[25] = 0f - array2[1]);
			outvec[27] = (outvec[26] = 0f - array2[0]);
		}

		private static float ICOS72_A(int i)
		{
			return icos72_table[2 * i];
		}

		private static float ICOS36_A(int i)
		{
			return icos72_table[4 * i + 1];
		}

		private static void imdct_9pt(float[] invec, float[] outvec)
		{
			float[] array = new float[5];
			float[] array2 = new float[4];
			float num = invec[6] / 2f + invec[0];
			float num2 = invec[0] - invec[6];
			float num3 = invec[2] - invec[4] - invec[8];
			array[0] = num + invec[2] * 0.9396926f + invec[4] * 0.76604444f + invec[8] * 0.17364818f;
			array[1] = num3 / 2f + num2;
			array[2] = num - invec[2] * 0.17364818f - invec[4] * 0.9396926f + invec[8] * 0.76604444f;
			array[3] = num - invec[2] * 0.76604444f + invec[4] * 0.17364818f - invec[8] * 0.9396926f;
			array[4] = num2 - num3;
			float num4 = invec[1] + invec[3];
			float num5 = invec[3] + invec[5];
			num = (invec[5] + invec[7]) * 0.5f + invec[1];
			array2[0] = num + num4 * 0.9396926f + num5 * 0.76604444f;
			array2[1] = (invec[1] - invec[5]) * 1.5f - invec[7];
			array2[2] = num - num4 * 0.17364818f - num5 * 0.9396926f;
			array2[3] = num - num4 * 0.76604444f + num5 * 0.17364818f;
			array2[0] += invec[7] * 0.17364818f;
			array2[1] -= invec[7] * 0.5f;
			array2[2] += invec[7] * 0.76604444f;
			array2[3] -= invec[7] * 0.9396926f;
			array2[0] *= 0.5077133f;
			array2[1] *= 0.57735026f;
			array2[2] *= 0.7778619f;
			array2[3] *= 1.4619021f;
			for (int i = 0; i < 4; i++)
			{
				outvec[i] = array[i] + array2[i];
			}
			outvec[4] = array[4];
			for (int i = 5; i < 9; i++)
			{
				outvec[i] = array[8 - i] - array2[8 - i];
			}
		}

		private void ShortImpl(float[] fsIn, int sbStart, float[] nextblck)
		{
			_ = _swin[2];
			int num = sbStart;
			int num2 = sbStart * 18;
			while (num < 32)
			{
				int i = 0;
				int num3 = 0;
				for (; i < 3; i++)
				{
					int num4 = num2 + i;
					for (int j = 0; j < 6; j++)
					{
						_imdctTemp[num3 + j] = fsIn[num4];
						num4 += 3;
					}
					num3 += 6;
				}
				Array.Clear(fsIn, num2, 6);
				ShortIMDCT(_imdctTemp, 0, _imdctResult);
				Array.Copy(_imdctResult, 0, fsIn, num2 + 6, 12);
				ShortIMDCT(_imdctTemp, 6, _imdctResult);
				for (int k = 0; k < 6; k++)
				{
					fsIn[num2 + k + 12] += _imdctResult[k];
				}
				Array.Copy(_imdctResult, 6, nextblck, num2, 6);
				ShortIMDCT(_imdctTemp, 12, _imdctResult);
				for (int l = 0; l < 6; l++)
				{
					nextblck[num2 + l] += _imdctResult[l];
				}
				Array.Copy(_imdctResult, 6, nextblck, num2 + 6, 6);
				Array.Clear(nextblck, num2 + 12, 6);
				num++;
				num2 += 18;
			}
		}

		private static void ShortIMDCT(float[] invec, int inIdx, float[] outvec)
		{
			float[] array = new float[6];
			float[] array2 = new float[6];
			float[] array3 = new float[3];
			float[] array4 = new float[3];
			int num = inIdx;
			for (int i = 1; i < 6; i++)
			{
				array[i] = invec[num];
				array[i] += invec[++num];
			}
			float num2 = array[4] / 2f + invec[inIdx];
			float num3 = array[2] * 0.8660254f;
			array3[0] = num2 + num3;
			array3[1] = invec[inIdx] - array[4];
			array3[2] = num2 - num3;
			float num4 = array[3] + array[5];
			num2 = num4 / 2f + array[1];
			num3 = (array[1] + array[3]) * 0.8660254f;
			array4[0] = num2 + num3;
			array4[1] = array[1] - num4;
			array4[2] = num2 - num3;
			array4[0] *= 0.5176381f;
			array4[1] *= 0.70710677f;
			array4[2] *= 1.9318516f;
			array2[0] = (array3[0] + array4[0]) * 0.5043145f;
			array2[1] = (array3[1] + array4[1]) * 0.5411961f;
			array2[2] = (array3[2] + array4[2]) * 0.6302362f;
			array2[3] = (array3[2] - array4[2]) * 0.82133985f;
			array2[4] = (array3[1] - array4[1]) * 1.306563f;
			array2[5] = (array3[0] - array4[0]) * 3.830649f;
			outvec[0] = array2[3] * _swin[2][0];
			outvec[1] = array2[4] * _swin[2][1];
			outvec[2] = array2[5] * _swin[2][2];
			outvec[3] = (0f - array2[5]) * _swin[2][3];
			outvec[4] = (0f - array2[4]) * _swin[2][4];
			outvec[5] = (0f - array2[3]) * _swin[2][5];
			outvec[6] = (0f - array2[2]) * _swin[2][6];
			outvec[7] = (0f - array2[1]) * _swin[2][7];
			outvec[8] = (0f - array2[0]) * _swin[2][8];
			outvec[9] = (0f - array2[0]) * _swin[2][9];
			outvec[10] = (0f - array2[1]) * _swin[2][10];
			outvec[11] = (0f - array2[2]) * _swin[2][11];
		}
	}

	private const int SSLIMIT = 18;

	private HybridMDCT _hybrid = new HybridMDCT();

	private BitReservoir _bitRes = new BitReservoir();

	private int _channels;

	private int _privBits;

	private int _mainDataBegin;

	private int[][] _scfsi = new int[2][]
	{
		new int[4],
		new int[4]
	};

	private int[][] _part23Length = new int[2][]
	{
		new int[2],
		new int[2]
	};

	private int[][] _bigValues = new int[2][]
	{
		new int[2],
		new int[2]
	};

	private float[][] _globalGain = new float[2][]
	{
		new float[2],
		new float[2]
	};

	private int[][] _scalefacCompress = new int[2][]
	{
		new int[2],
		new int[2]
	};

	private bool[][] _blockSplitFlag = new bool[2][]
	{
		new bool[2],
		new bool[2]
	};

	private bool[][] _mixedBlockFlag = new bool[2][]
	{
		new bool[2],
		new bool[2]
	};

	private int[][] _blockType = new int[2][]
	{
		new int[2],
		new int[2]
	};

	private int[][][] _tableSelect;

	private float[][][] _subblockGain;

	private int[][] _regionAddress1 = new int[2][]
	{
		new int[2],
		new int[2]
	};

	private int[][] _regionAddress2 = new int[2][]
	{
		new int[2],
		new int[2]
	};

	private int[][] _preflag = new int[2][]
	{
		new int[2],
		new int[2]
	};

	private float[][] _scalefacScale = new float[2][]
	{
		new float[2],
		new float[2]
	};

	private int[][] _count1TableSelect = new int[2][]
	{
		new int[2],
		new int[2]
	};

	private static float[] GAIN_TAB = new float[256]
	{
		1.5700924E-16f,
		1.8671652E-16f,
		2.220446E-16f,
		2.6405702E-16f,
		3.1401849E-16f,
		3.7343303E-16f,
		4.440892E-16f,
		5.2811403E-16f,
		6.2803697E-16f,
		7.4686606E-16f,
		8.881784E-16f,
		1.0562281E-15f,
		1.2560739E-15f,
		1.4937321E-15f,
		1.7763568E-15f,
		2.1124561E-15f,
		2.5121479E-15f,
		2.9874642E-15f,
		3.5527137E-15f,
		4.2249122E-15f,
		5.0242958E-15f,
		5.9749285E-15f,
		7.1054274E-15f,
		8.4498245E-15f,
		1.00485916E-14f,
		1.1949857E-14f,
		1.4210855E-14f,
		1.6899649E-14f,
		2.0097183E-14f,
		2.3899714E-14f,
		2.842171E-14f,
		3.3799298E-14f,
		4.0194366E-14f,
		4.7799428E-14f,
		5.684342E-14f,
		6.7598596E-14f,
		8.038873E-14f,
		9.5598856E-14f,
		1.1368684E-13f,
		1.3519719E-13f,
		1.6077747E-13f,
		1.9119771E-13f,
		2.2737368E-13f,
		2.7039438E-13f,
		3.2155493E-13f,
		3.8239542E-13f,
		4.5474735E-13f,
		5.4078877E-13f,
		6.4310986E-13f,
		7.6479085E-13f,
		9.094947E-13f,
		1.0815775E-12f,
		1.2862197E-12f,
		1.5295817E-12f,
		1.8189894E-12f,
		2.163155E-12f,
		2.5724394E-12f,
		3.0591634E-12f,
		3.637979E-12f,
		4.32631E-12f,
		5.144879E-12f,
		6.1183268E-12f,
		7.275958E-12f,
		8.65262E-12f,
		1.0289758E-11f,
		1.22366535E-11f,
		1.4551915E-11f,
		1.730524E-11f,
		2.0579516E-11f,
		2.4473307E-11f,
		2.910383E-11f,
		3.461048E-11f,
		4.115903E-11f,
		4.8946614E-11f,
		5.820766E-11f,
		6.922096E-11f,
		8.231806E-11f,
		9.789323E-11f,
		1.1641532E-10f,
		1.3844192E-10f,
		1.6463612E-10f,
		1.9578646E-10f,
		2.3283064E-10f,
		2.7688385E-10f,
		3.2927225E-10f,
		3.915729E-10f,
		4.656613E-10f,
		5.537677E-10f,
		6.585445E-10f,
		7.831458E-10f,
		9.313226E-10f,
		1.1075354E-09f,
		1.317089E-09f,
		1.5662917E-09f,
		1.8626451E-09f,
		2.2150708E-09f,
		2.634178E-09f,
		3.1325833E-09f,
		3.7252903E-09f,
		4.4301416E-09f,
		5.268356E-09f,
		6.2651666E-09f,
		7.450581E-09f,
		8.860283E-09f,
		1.0536712E-08f,
		1.2530333E-08f,
		1.4901161E-08f,
		1.7720566E-08f,
		2.1073424E-08f,
		2.5060666E-08f,
		2.9802322E-08f,
		3.5441133E-08f,
		4.2146848E-08f,
		5.0121333E-08f,
		5.9604645E-08f,
		7.0882265E-08f,
		8.4293696E-08f,
		1.00242666E-07f,
		1.1920929E-07f,
		1.4176453E-07f,
		1.6858739E-07f,
		2.0048533E-07f,
		2.3841858E-07f,
		2.8352906E-07f,
		3.3717478E-07f,
		4.0097066E-07f,
		4.7683716E-07f,
		5.670581E-07f,
		6.7434956E-07f,
		8.019413E-07f,
		1f / 1048576f,
		1.1341162E-06f,
		1.3486991E-06f,
		1.6038827E-06f,
		2f / 1048576f,
		2.2682325E-06f,
		2.6973983E-06f,
		3.2077653E-06f,
		4f / 1048576f,
		4.536465E-06f,
		5.3947965E-06f,
		6.4155306E-06f,
		8f / 1048576f,
		9.07293E-06f,
		1.0789593E-05f,
		1.2831061E-05f,
		1f / 65536f,
		1.814586E-05f,
		2.1579186E-05f,
		2.5662122E-05f,
		1f / 32768f,
		3.629172E-05f,
		4.3158372E-05f,
		5.1324245E-05f,
		1f / 16384f,
		7.258344E-05f,
		8.6316744E-05f,
		0.00010264849f,
		1f / 8192f,
		0.00014516688f,
		0.00017263349f,
		0.00020529698f,
		1f / 4096f,
		0.00029033376f,
		0.00034526698f,
		0.00041059396f,
		2f / 4096f,
		0.0005806675f,
		0.00069053395f,
		0.0008211879f,
		1f / 1024f,
		0.001161335f,
		0.0013810679f,
		0.0016423758f,
		2f / 1024f,
		0.00232267f,
		0.0027621358f,
		0.0032847517f,
		1f / 256f,
		0.00464534f,
		0.0055242716f,
		0.0065695033f,
		1f / 128f,
		0.00929068f,
		0.011048543f,
		0.013139007f,
		1f / 64f,
		0.01858136f,
		0.022097087f,
		0.026278013f,
		1f / 32f,
		0.03716272f,
		0.044194173f,
		0.052556027f,
		0.0625f,
		0.07432544f,
		0.088388346f,
		0.10511205f,
		0.125f,
		0.14865088f,
		0.17677669f,
		0.2102241f,
		0.25f,
		0.29730177f,
		0.35355338f,
		0.4204482f,
		0.5f,
		0.59460354f,
		0.70710677f,
		0.8408964f,
		1f,
		1.1892071f,
		1.4142135f,
		1.6817929f,
		2f,
		2.3784142f,
		2.828427f,
		3.3635857f,
		4f,
		4.7568283f,
		5.656854f,
		6.7271714f,
		8f,
		9.513657f,
		11.313708f,
		13.454343f,
		16f,
		19.027313f,
		22.627417f,
		26.908686f,
		32f,
		38.054626f,
		45.254833f,
		53.81737f,
		64f,
		76.10925f,
		90.50967f,
		107.63474f,
		128f,
		152.2185f,
		181.01933f,
		215.26949f,
		256f,
		304.437f,
		362.03867f,
		430.53897f,
		512f,
		608.874f,
		724.07733f,
		861.07794f,
		1024f,
		1217.748f,
		1448.1547f,
		1722.1559f,
		2048f,
		2435.496f
	};

	private int[] _sfBandIndexL;

	private int[] _sfBandIndexS;

	private byte[] _cbLookupL = new byte[576];

	private byte[] _cbLookupS = new byte[576];

	private byte[] _cbwLookupS = new byte[576];

	private int _cbLookupSR;

	private static readonly int[][] _sfBandIndexLTable = new int[9][]
	{
		new int[23]
		{
			0, 4, 8, 12, 16, 20, 24, 30, 36, 44,
			52, 62, 74, 90, 110, 134, 162, 196, 238, 288,
			342, 418, 576
		},
		new int[23]
		{
			0, 4, 8, 12, 16, 20, 24, 30, 36, 42,
			50, 60, 72, 88, 106, 128, 156, 190, 230, 276,
			330, 384, 576
		},
		new int[23]
		{
			0, 4, 8, 12, 16, 20, 24, 30, 36, 44,
			54, 66, 82, 102, 126, 156, 194, 240, 296, 364,
			448, 550, 576
		},
		new int[23]
		{
			0, 4, 8, 12, 16, 20, 24, 30, 36, 44,
			52, 62, 74, 90, 110, 134, 162, 196, 238, 288,
			342, 418, 576
		},
		new int[23]
		{
			0, 4, 8, 12, 16, 20, 24, 30, 36, 42,
			50, 60, 72, 88, 106, 128, 156, 190, 230, 276,
			330, 384, 576
		},
		new int[23]
		{
			0, 4, 8, 12, 16, 20, 24, 30, 36, 44,
			54, 66, 82, 102, 126, 156, 194, 240, 296, 364,
			448, 550, 576
		},
		new int[23]
		{
			0, 6, 12, 18, 24, 30, 36, 44, 54, 66,
			80, 96, 116, 140, 168, 200, 238, 284, 336, 396,
			464, 522, 576
		},
		new int[23]
		{
			0, 6, 12, 18, 24, 30, 36, 44, 54, 66,
			80, 96, 116, 140, 168, 200, 238, 284, 336, 396,
			464, 522, 576
		},
		new int[23]
		{
			0, 12, 24, 36, 48, 60, 72, 88, 108, 132,
			160, 192, 232, 280, 336, 400, 476, 566, 568, 570,
			572, 574, 576
		}
	};

	private static readonly int[][] _sfBandIndexSTable = new int[9][]
	{
		new int[14]
		{
			0, 4, 8, 12, 16, 22, 30, 40, 52, 66,
			84, 106, 136, 192
		},
		new int[14]
		{
			0, 4, 8, 12, 16, 22, 28, 38, 50, 64,
			80, 100, 126, 192
		},
		new int[14]
		{
			0, 4, 8, 12, 16, 22, 30, 42, 58, 78,
			104, 138, 180, 192
		},
		new int[14]
		{
			0, 4, 8, 12, 16, 22, 30, 40, 52, 66,
			84, 106, 136, 192
		},
		new int[14]
		{
			0, 4, 8, 12, 16, 22, 28, 38, 50, 64,
			80, 100, 126, 192
		},
		new int[14]
		{
			0, 4, 8, 12, 16, 22, 30, 42, 58, 78,
			104, 138, 180, 192
		},
		new int[14]
		{
			0, 4, 8, 12, 18, 26, 36, 48, 62, 80,
			104, 134, 174, 192
		},
		new int[14]
		{
			0, 4, 8, 12, 18, 26, 36, 48, 62, 80,
			104, 134, 174, 192
		},
		new int[14]
		{
			0, 8, 16, 24, 36, 52, 72, 96, 124, 160,
			162, 164, 166, 192
		}
	};

	private int[][][] _scalefac = new int[2][][]
	{
		new int[4][]
		{
			new int[13],
			new int[13],
			new int[13],
			new int[23]
		},
		new int[4][]
		{
			new int[13],
			new int[13],
			new int[13],
			new int[23]
		}
	};

	private static readonly int[][] _slen = new int[2][]
	{
		new int[16]
		{
			0, 0, 0, 0, 3, 1, 1, 1, 2, 2,
			2, 3, 3, 3, 4, 4
		},
		new int[16]
		{
			0, 1, 2, 3, 0, 1, 2, 3, 1, 2,
			3, 1, 2, 3, 2, 3
		}
	};

	private static readonly int[][][] _sfbBlockCntTab = new int[6][][]
	{
		new int[3][]
		{
			new int[4] { 6, 5, 5, 5 },
			new int[4] { 9, 9, 9, 9 },
			new int[4] { 6, 9, 9, 9 }
		},
		new int[3][]
		{
			new int[4] { 6, 5, 7, 3 },
			new int[4] { 9, 9, 12, 6 },
			new int[4] { 6, 9, 12, 6 }
		},
		new int[3][]
		{
			new int[4] { 11, 10, 0, 0 },
			new int[4] { 18, 18, 0, 0 },
			new int[4] { 15, 18, 0, 0 }
		},
		new int[3][]
		{
			new int[4] { 7, 7, 7, 0 },
			new int[4] { 12, 12, 12, 0 },
			new int[4] { 6, 15, 12, 0 }
		},
		new int[3][]
		{
			new int[4] { 6, 6, 6, 3 },
			new int[4] { 12, 9, 9, 6 },
			new int[4] { 6, 12, 9, 6 }
		},
		new int[3][]
		{
			new int[4] { 8, 8, 5, 0 },
			new int[4] { 15, 12, 9, 0 },
			new int[4] { 6, 18, 9, 0 }
		}
	};

	private float[][] _samples = new float[2][]
	{
		new float[579],
		new float[579]
	};

	private static readonly int[] PRETAB = new int[22]
	{
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 1, 1, 1, 1, 2, 2, 3, 3, 3,
		2, 0
	};

	private static readonly float[] POW2_TAB = new float[64]
	{
		1f,
		0.70710677f,
		0.5f,
		0.35355338f,
		0.25f,
		0.17677669f,
		0.125f,
		0.088388346f,
		0.0625f,
		0.044194173f,
		1f / 32f,
		0.022097087f,
		1f / 64f,
		0.011048543f,
		1f / 128f,
		0.0055242716f,
		1f / 256f,
		0.0027621358f,
		2f / 1024f,
		0.0013810679f,
		1f / 1024f,
		0.00069053395f,
		2f / 4096f,
		0.00034526698f,
		1f / 4096f,
		0.00017263349f,
		1f / 8192f,
		8.6316744E-05f,
		1f / 16384f,
		4.3158372E-05f,
		1f / 32768f,
		2.1579186E-05f,
		1f / 65536f,
		1.0789593E-05f,
		8f / 1048576f,
		5.3947965E-06f,
		4f / 1048576f,
		2.6973983E-06f,
		2f / 1048576f,
		1.3486991E-06f,
		1f / 1048576f,
		6.7434956E-07f,
		4.7683716E-07f,
		3.3717478E-07f,
		2.3841858E-07f,
		1.6858739E-07f,
		1.1920929E-07f,
		8.4293696E-08f,
		5.9604645E-08f,
		4.2146848E-08f,
		2.9802322E-08f,
		2.1073424E-08f,
		1.4901161E-08f,
		1.0536712E-08f,
		7.450581E-09f,
		5.268356E-09f,
		3.7252903E-09f,
		2.634178E-09f,
		1.8626451E-09f,
		1.317089E-09f,
		9.313226E-10f,
		6.585445E-10f,
		4.656613E-10f,
		3.2927225E-10f
	};

	private static readonly float[][] _isRatio = new float[2][]
	{
		new float[7] { 0f, 0.21132487f, 0.36602542f, 0.5f, 0.6339746f, 0.7886751f, 1f },
		new float[7] { 1f, 0.7886751f, 0.6339746f, 0.5f, 0.36602542f, 0.21132487f, 0f }
	};

	private static readonly float[][][] _lsfRatio = new float[2][][]
	{
		new float[2][]
		{
			new float[32]
			{
				1f, 0.8408964f, 1f, 0.70710677f, 1f, 0.59460354f, 1f, 0.5f, 1f, 0.4204482f,
				1f, 0.35355338f, 1f, 0.29730177f, 1f, 0.25f, 1f, 0.2102241f, 1f, 0.17677669f,
				1f, 0.14865088f, 1f, 0.125f, 1f, 0.10511205f, 1f, 0.088388346f, 1f, 0.07432544f,
				1f, 0.0625f
			},
			new float[32]
			{
				1f, 1f, 0.8408964f, 1f, 0.70710677f, 1f, 0.59460354f, 1f, 0.5f, 1f,
				0.4204482f, 1f, 0.35355338f, 1f, 0.29730177f, 1f, 0.25f, 1f, 0.2102241f, 1f,
				0.17677669f, 1f, 0.14865088f, 1f, 0.125f, 1f, 0.10511205f, 1f, 0.088388346f, 1f,
				0.07432544f, 1f
			}
		},
		new float[2][]
		{
			new float[32]
			{
				1f,
				0.70710677f,
				1f,
				0.5f,
				1f,
				0.35355338f,
				1f,
				0.25f,
				1f,
				0.17677669f,
				1f,
				0.125f,
				1f,
				0.088388346f,
				1f,
				0.0625f,
				1f,
				0.044194173f,
				1f,
				1f / 32f,
				1f,
				0.022097087f,
				1f,
				1f / 64f,
				1f,
				0.011048543f,
				1f,
				1f / 128f,
				1f,
				0.0055242716f,
				1f,
				1f / 256f
			},
			new float[32]
			{
				1f,
				1f,
				0.70710677f,
				1f,
				0.5f,
				1f,
				0.35355338f,
				1f,
				0.25f,
				1f,
				0.17677669f,
				1f,
				0.125f,
				1f,
				0.088388346f,
				1f,
				0.0625f,
				1f,
				0.044194173f,
				1f,
				1f / 32f,
				1f,
				0.022097087f,
				1f,
				1f / 64f,
				1f,
				0.011048543f,
				1f,
				1f / 128f,
				1f,
				0.0055242716f,
				1f
			}
		}
	};

	private float[] _reorderBuf = new float[576];

	private static readonly float[] _scs = new float[8] { 0.8574929f, 0.881742f, 0.94962865f, 0.9833146f, 0.9955178f, 0.9991606f, 0.9998992f, 0.99999315f };

	private static readonly float[] _sca = new float[8] { -0.51449573f, -0.47173196f, -0.31337744f, -0.1819132f, -0.09457419f, -0.040965583f, -0.014198569f, -0.0036999746f };

	private float[] _polyPhase = new float[32];

	internal static bool GetCRC(MpegFrame frame, ref uint crc)
	{
		int num = frame.GetSideDataSize();
		while (--num >= 0)
		{
			MpegFrame.UpdateCRC(frame.ReadBits(8), 8, ref crc);
		}
		return true;
	}

	internal LayerIIIDecoder()
	{
		_tableSelect = new int[2][][]
		{
			new int[2][]
			{
				new int[3],
				new int[3]
			},
			new int[2][]
			{
				new int[3],
				new int[3]
			}
		};
		_subblockGain = new float[2][][]
		{
			new float[2][]
			{
				new float[3],
				new float[3]
			},
			new float[2][]
			{
				new float[3],
				new float[3]
			}
		};
	}

	internal override int DecodeFrame(IMpegFrame frame, float[] ch0, float[] ch1)
	{
		ReadSideInfo(frame);
		if (!_bitRes.AddBits(frame, _mainDataBegin))
		{
			return 0;
		}
		PrepTables(frame);
		float[][] array = new float[2][];
		int num = 0;
		int num2 = _channels - 1;
		if (_channels == 1 || StereoMode == StereoMode.LeftOnly || StereoMode == StereoMode.DownmixToMono)
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
		int num3 = ((frame.Version != MpegVersion.Version1) ? 1 : 2);
		int num4 = 0;
		for (int i = 0; i < num3; i++)
		{
			for (int j = 0; j < _channels; j++)
			{
				int sfBits = ((frame.Version != MpegVersion.Version1) ? ReadLsfScalefactors(i, j, frame.ChannelModeExtension) : ReadScalefactors(i, j));
				ReadSamples(sfBits, i, j);
			}
			Stereo(frame.ChannelMode, frame.ChannelModeExtension, i, frame.Version != MpegVersion.Version1);
			for (int k = num; k <= num2; k++)
			{
				float[] array2 = _samples[k];
				int num5 = _blockType[i][k];
				bool flag = _blockSplitFlag[i][k];
				bool flag2 = _mixedBlockFlag[i][k];
				if (flag && num5 == 2)
				{
					if (flag2)
					{
						Reorder(array2, mixedBlock: true);
						AntiAlias(array2, mixedBlock: true);
					}
					else
					{
						Reorder(array2, mixedBlock: false);
					}
				}
				else
				{
					AntiAlias(array2, mixedBlock: false);
				}
				_hybrid.Apply(array2, k, num5, flag & flag2);
				FrequencyInversion(array2);
				InversePolyphase(array2, k, num4, array[k]);
			}
			num4 += 576;
		}
		return num4;
	}

	internal override void ResetForSeek()
	{
		base.ResetForSeek();
		_hybrid.Reset();
		_bitRes.Reset();
	}

	private void ReadSideInfo(IMpegFrame frame)
	{
		if (frame.Version == MpegVersion.Version1)
		{
			_mainDataBegin = frame.ReadBits(9);
			if (frame.ChannelMode == MpegChannelMode.Mono)
			{
				_privBits = frame.ReadBits(5);
				_channels = 1;
			}
			else
			{
				_privBits = frame.ReadBits(3);
				_channels = 2;
			}
			for (int i = 0; i < _channels; i++)
			{
				_scfsi[i][0] = frame.ReadBits(1);
				_scfsi[i][1] = frame.ReadBits(1);
				_scfsi[i][2] = frame.ReadBits(1);
				_scfsi[i][3] = frame.ReadBits(1);
			}
			for (int j = 0; j < 2; j++)
			{
				for (int k = 0; k < _channels; k++)
				{
					_part23Length[j][k] = frame.ReadBits(12);
					_bigValues[j][k] = frame.ReadBits(9);
					_globalGain[j][k] = GAIN_TAB[frame.ReadBits(8)];
					_scalefacCompress[j][k] = frame.ReadBits(4);
					_blockSplitFlag[j][k] = frame.ReadBits(1) == 1;
					if (_blockSplitFlag[j][k])
					{
						_blockType[j][k] = frame.ReadBits(2);
						_mixedBlockFlag[j][k] = frame.ReadBits(1) == 1;
						_tableSelect[j][k][0] = frame.ReadBits(5);
						_tableSelect[j][k][1] = frame.ReadBits(5);
						_tableSelect[j][k][2] = 0;
						if (_blockType[j][k] == 2 && !_mixedBlockFlag[j][k])
						{
							_regionAddress1[j][k] = 8;
						}
						else
						{
							_regionAddress1[j][k] = 7;
						}
						_regionAddress2[j][k] = 20 - _regionAddress1[j][k];
						_subblockGain[j][k][0] = (float)frame.ReadBits(3) * -2f;
						_subblockGain[j][k][1] = (float)frame.ReadBits(3) * -2f;
						_subblockGain[j][k][2] = (float)frame.ReadBits(3) * -2f;
					}
					else
					{
						_tableSelect[j][k][0] = frame.ReadBits(5);
						_tableSelect[j][k][1] = frame.ReadBits(5);
						_tableSelect[j][k][2] = frame.ReadBits(5);
						_regionAddress1[j][k] = frame.ReadBits(4);
						_regionAddress2[j][k] = frame.ReadBits(3);
						_blockType[j][k] = 0;
						_subblockGain[j][k][0] = 0f;
						_subblockGain[j][k][1] = 0f;
						_subblockGain[j][k][2] = 0f;
					}
					_preflag[j][k] = frame.ReadBits(1);
					_scalefacScale[j][k] = 0.5f * (1f + (float)frame.ReadBits(1));
					_count1TableSelect[j][k] = frame.ReadBits(1);
				}
			}
			return;
		}
		_mainDataBegin = frame.ReadBits(8);
		if (frame.ChannelMode == MpegChannelMode.Mono)
		{
			_privBits = frame.ReadBits(1);
			_channels = 1;
		}
		else
		{
			_privBits = frame.ReadBits(2);
			_channels = 2;
		}
		int num = 0;
		for (int l = 0; l < _channels; l++)
		{
			_part23Length[num][l] = frame.ReadBits(12);
			_bigValues[num][l] = frame.ReadBits(9);
			_globalGain[num][l] = GAIN_TAB[frame.ReadBits(8)];
			_scalefacCompress[num][l] = frame.ReadBits(9);
			_blockSplitFlag[num][l] = frame.ReadBits(1) == 1;
			if (_blockSplitFlag[num][l])
			{
				_blockType[num][l] = frame.ReadBits(2);
				_mixedBlockFlag[num][l] = frame.ReadBits(1) == 1;
				_tableSelect[num][l][0] = frame.ReadBits(5);
				_tableSelect[num][l][1] = frame.ReadBits(5);
				_tableSelect[num][l][2] = 0;
				if (_blockType[num][l] == 2 && !_mixedBlockFlag[num][l])
				{
					_regionAddress1[num][l] = 8;
				}
				else
				{
					_regionAddress1[num][l] = 7;
				}
				_regionAddress2[num][l] = 20 - _regionAddress1[num][l];
				_subblockGain[num][l][0] = (float)frame.ReadBits(3) * -2f;
				_subblockGain[num][l][1] = (float)frame.ReadBits(3) * -2f;
				_subblockGain[num][l][2] = (float)frame.ReadBits(3) * -2f;
			}
			else
			{
				_tableSelect[num][l][0] = frame.ReadBits(5);
				_tableSelect[num][l][1] = frame.ReadBits(5);
				_tableSelect[num][l][2] = frame.ReadBits(5);
				_regionAddress1[num][l] = frame.ReadBits(4);
				_regionAddress2[num][l] = frame.ReadBits(3);
				_blockType[num][l] = 0;
				_subblockGain[num][l][0] = 0f;
				_subblockGain[num][l][1] = 0f;
				_subblockGain[num][l][2] = 0f;
			}
			_scalefacScale[num][l] = 0.5f * (1f + (float)frame.ReadBits(1));
			_count1TableSelect[num][l] = frame.ReadBits(1);
		}
	}

	private void PrepTables(IMpegFrame frame)
	{
		if (_cbLookupSR == frame.SampleRate)
		{
			return;
		}
		switch (frame.SampleRate)
		{
		case 44100:
			_sfBandIndexL = _sfBandIndexLTable[0];
			_sfBandIndexS = _sfBandIndexSTable[0];
			break;
		case 48000:
			_sfBandIndexL = _sfBandIndexLTable[1];
			_sfBandIndexS = _sfBandIndexSTable[1];
			break;
		case 32000:
			_sfBandIndexL = _sfBandIndexLTable[2];
			_sfBandIndexS = _sfBandIndexSTable[2];
			break;
		case 22050:
			_sfBandIndexL = _sfBandIndexLTable[3];
			_sfBandIndexS = _sfBandIndexSTable[3];
			break;
		case 24000:
			_sfBandIndexL = _sfBandIndexLTable[4];
			_sfBandIndexS = _sfBandIndexSTable[4];
			break;
		case 16000:
			_sfBandIndexL = _sfBandIndexLTable[5];
			_sfBandIndexS = _sfBandIndexSTable[5];
			break;
		case 11025:
			_sfBandIndexL = _sfBandIndexLTable[6];
			_sfBandIndexS = _sfBandIndexSTable[6];
			break;
		case 12000:
			_sfBandIndexL = _sfBandIndexLTable[7];
			_sfBandIndexS = _sfBandIndexSTable[7];
			break;
		case 8000:
			_sfBandIndexL = _sfBandIndexLTable[8];
			_sfBandIndexS = _sfBandIndexSTable[8];
			break;
		}
		int num = 0;
		int num2 = 0;
		int num3 = _sfBandIndexL[1];
		int num4 = _sfBandIndexS[1] * 3;
		for (int i = 0; i < 576; i++)
		{
			if (i == num3)
			{
				num++;
				num3 = _sfBandIndexL[num + 1];
			}
			if (i == num4)
			{
				num2++;
				num4 = _sfBandIndexS[num2 + 1] * 3;
			}
			_cbLookupL[i] = (byte)num;
			_cbLookupS[i] = (byte)num2;
		}
		int num5 = 0;
		for (num2 = 0; num2 < 12; num2++)
		{
			int num6 = _sfBandIndexS[num2 + 1] - _sfBandIndexS[num2];
			for (int j = 0; j < 3; j++)
			{
				int num7 = 0;
				while (num7 < num6)
				{
					_cbwLookupS[num5] = (byte)j;
					num7++;
					num5++;
				}
			}
		}
		_cbLookupSR = frame.SampleRate;
	}

	private int ReadScalefactors(int gr, int ch)
	{
		int num = _slen[0][_scalefacCompress[gr][ch]];
		int num2 = _slen[1][_scalefacCompress[gr][ch]];
		int i = 0;
		int num3;
		if (_blockSplitFlag[gr][ch] && _blockType[gr][ch] == 2)
		{
			if (num > 0)
			{
				num3 = num * 18;
				if (_mixedBlockFlag[gr][ch])
				{
					for (; i < 8; i++)
					{
						_scalefac[ch][3][i] = _bitRes.GetBits(num);
					}
					i = 3;
					num3 -= num;
				}
				for (; i < 6; i++)
				{
					_scalefac[ch][0][i] = _bitRes.GetBits(num);
					_scalefac[ch][1][i] = _bitRes.GetBits(num);
					_scalefac[ch][2][i] = _bitRes.GetBits(num);
				}
			}
			else
			{
				Array.Clear(_scalefac[ch][3], 0, 8);
				Array.Clear(_scalefac[ch][0], 0, 6);
				Array.Clear(_scalefac[ch][1], 0, 6);
				Array.Clear(_scalefac[ch][2], 0, 6);
				num3 = 0;
			}
			if (num2 > 0)
			{
				num3 += num2 * 18;
				for (i = 6; i < 12; i++)
				{
					_scalefac[ch][0][i] = _bitRes.GetBits(num2);
					_scalefac[ch][1][i] = _bitRes.GetBits(num2);
					_scalefac[ch][2][i] = _bitRes.GetBits(num2);
				}
			}
			else
			{
				Array.Clear(_scalefac[ch][0], 6, 6);
				Array.Clear(_scalefac[ch][1], 6, 6);
				Array.Clear(_scalefac[ch][2], 6, 6);
			}
		}
		else
		{
			num3 = 0;
			if (gr == 0 || _scfsi[ch][0] == 0)
			{
				if (num > 0)
				{
					num3 += num * 6;
					_scalefac[ch][3][0] = _bitRes.GetBits(num);
					_scalefac[ch][3][1] = _bitRes.GetBits(num);
					_scalefac[ch][3][2] = _bitRes.GetBits(num);
					_scalefac[ch][3][3] = _bitRes.GetBits(num);
					_scalefac[ch][3][4] = _bitRes.GetBits(num);
					_scalefac[ch][3][5] = _bitRes.GetBits(num);
				}
				else
				{
					Array.Clear(_scalefac[ch][3], 0, 6);
				}
			}
			if (gr == 0 || _scfsi[ch][1] == 0)
			{
				if (num > 0)
				{
					num3 += num * 5;
					_scalefac[ch][3][6] = _bitRes.GetBits(num);
					_scalefac[ch][3][7] = _bitRes.GetBits(num);
					_scalefac[ch][3][8] = _bitRes.GetBits(num);
					_scalefac[ch][3][9] = _bitRes.GetBits(num);
					_scalefac[ch][3][10] = _bitRes.GetBits(num);
				}
				else
				{
					Array.Clear(_scalefac[ch][3], 6, 5);
				}
			}
			if (gr == 0 || _scfsi[ch][2] == 0)
			{
				if (num2 > 0)
				{
					num3 += num2 * 5;
					_scalefac[ch][3][11] = _bitRes.GetBits(num2);
					_scalefac[ch][3][12] = _bitRes.GetBits(num2);
					_scalefac[ch][3][13] = _bitRes.GetBits(num2);
					_scalefac[ch][3][14] = _bitRes.GetBits(num2);
					_scalefac[ch][3][15] = _bitRes.GetBits(num2);
				}
				else
				{
					Array.Clear(_scalefac[ch][3], 11, 5);
				}
			}
			if (gr == 0 || _scfsi[ch][3] == 0)
			{
				if (num2 > 0)
				{
					num3 += num2 * 5;
					_scalefac[ch][3][16] = _bitRes.GetBits(num2);
					_scalefac[ch][3][17] = _bitRes.GetBits(num2);
					_scalefac[ch][3][18] = _bitRes.GetBits(num2);
					_scalefac[ch][3][19] = _bitRes.GetBits(num2);
					_scalefac[ch][3][20] = _bitRes.GetBits(num2);
				}
				else
				{
					Array.Clear(_scalefac[ch][3], 16, 5);
				}
			}
		}
		return num3;
	}

	private int ReadLsfScalefactors(int gr, int ch, int chanModeExt)
	{
		int num = _scalefacCompress[gr][ch];
		int num2 = ((_blockType[gr][ch] == 2) ? ((!_mixedBlockFlag[gr][ch]) ? 1 : 2) : 0);
		int[] array = new int[4];
		int num4;
		if ((chanModeExt & 1) == 1 && ch == 1)
		{
			int num3 = num >> 1;
			if (num3 < 180)
			{
				array[0] = num3 / 36;
				array[1] = num3 % 36 / 6;
				array[2] = num3 % 6;
				array[3] = 0;
				_preflag[gr][ch] = 0;
				num4 = 3;
			}
			else if (num3 < 244)
			{
				array[0] = (num3 - 180) % 64 >> 4;
				array[1] = (num3 - 180) % 16 >> 2;
				array[2] = (num3 - 180) % 4;
				array[3] = 0;
				_preflag[gr][ch] = 0;
				num4 = 4;
			}
			else if (num3 < 255)
			{
				array[0] = (num3 - 244) / 3;
				array[1] = (num3 - 244) % 3;
				array[2] = 0;
				array[3] = 0;
				_preflag[gr][ch] = 0;
				num4 = 5;
			}
			else
			{
				num4 = 0;
			}
		}
		else if (num < 400)
		{
			array[0] = (num >> 4) / 5;
			array[1] = (num >> 4) % 5;
			array[2] = (num & 0xF) >> 2;
			array[3] = num & 3;
			_preflag[gr][ch] = 0;
			num4 = 0;
		}
		else if (num < 500)
		{
			array[0] = (num - 400 >> 2) / 5;
			array[1] = (num - 400 >> 2) % 5;
			array[2] = (num - 400) & 3;
			array[3] = 0;
			_preflag[gr][ch] = 0;
			num4 = 1;
		}
		else if (num < 512)
		{
			array[0] = (num - 500) / 3;
			array[1] = (num - 500) % 3;
			array[2] = 0;
			array[3] = 0;
			_preflag[gr][ch] = 1;
			num4 = 2;
		}
		else
		{
			num4 = 0;
		}
		int[] array2 = new int[54];
		int num5 = 0;
		int[] array3 = _sfbBlockCntTab[num4][num2];
		for (int i = 0; i < 4; i++)
		{
			if (array[i] != 0)
			{
				int num6 = 0;
				while (num6 < array3[i])
				{
					array2[num5] = _bitRes.GetBits(array[i]);
					num6++;
					num5++;
				}
			}
			else
			{
				num5 += array3[i];
			}
		}
		num5 = 0;
		int j = 0;
		if (_blockSplitFlag[gr][ch] && _blockType[gr][ch] == 2)
		{
			if (_mixedBlockFlag[gr][ch])
			{
				for (; j < 8; j++)
				{
					_scalefac[ch][3][j] = array2[num5++];
				}
				j = 3;
			}
			for (; j < 12; j++)
			{
				for (int k = 0; k < 3; k++)
				{
					_scalefac[ch][k][j] = array2[num5++];
				}
			}
			_scalefac[ch][0][12] = 0;
			_scalefac[ch][1][12] = 0;
			_scalefac[ch][2][12] = 0;
		}
		else
		{
			for (; j < 21; j++)
			{
				_scalefac[ch][3][j] = array2[num5++];
			}
			_scalefac[ch][3][22] = 0;
		}
		return array[0] * array3[0] + array[1] * array3[1] + array[2] * array3[2] + array[3] * array3[3];
	}

	private void ReadSamples(int sfBits, int gr, int ch)
	{
		int num;
		int num2;
		if (_blockSplitFlag[gr][ch] && _blockType[gr][ch] == 2)
		{
			num = 36;
			num2 = 576;
		}
		else
		{
			num = _sfBandIndexL[_regionAddress1[gr][ch] + 1];
			num2 = _sfBandIndexL[Math.Min(_regionAddress1[gr][ch] + _regionAddress2[gr][ch] + 2, 22)];
		}
		long num3 = _bitRes.BitsRead - sfBits + _part23Length[gr][ch];
		int num4 = 0;
		int table = _tableSelect[gr][ch][0];
		int num5 = _bigValues[gr][ch] * 2;
		float x;
		float y;
		while (num4 < num5 && num4 < num)
		{
			Huffman.Decode(_bitRes, table, out x, out y);
			_samples[ch][num4] = Dequantize(num4, x, gr, ch);
			num4++;
			_samples[ch][num4] = Dequantize(num4, y, gr, ch);
			num4++;
		}
		table = _tableSelect[gr][ch][1];
		while (num4 < num5 && num4 < num2)
		{
			Huffman.Decode(_bitRes, table, out x, out y);
			_samples[ch][num4] = Dequantize(num4, x, gr, ch);
			num4++;
			_samples[ch][num4] = Dequantize(num4, y, gr, ch);
			num4++;
		}
		table = _tableSelect[gr][ch][2];
		while (num4 < num5)
		{
			Huffman.Decode(_bitRes, table, out x, out y);
			_samples[ch][num4] = Dequantize(num4, x, gr, ch);
			num4++;
			_samples[ch][num4] = Dequantize(num4, y, gr, ch);
			num4++;
		}
		table = _count1TableSelect[gr][ch] + 32;
		while (num3 > _bitRes.BitsRead && num4 < 573)
		{
			Huffman.Decode(_bitRes, table, out x, out y, out var v, out var w);
			_samples[ch][num4] = Dequantize(num4, v, gr, ch);
			num4++;
			_samples[ch][num4] = Dequantize(num4, w, gr, ch);
			num4++;
			_samples[ch][num4] = Dequantize(num4, x, gr, ch);
			num4++;
			_samples[ch][num4] = Dequantize(num4, y, gr, ch);
			num4++;
		}
		if (_bitRes.BitsRead > num3)
		{
			_bitRes.RewindBits((int)(_bitRes.BitsRead - num3));
			num4 -= 4;
			if (num4 < 0)
			{
				num4 = 0;
			}
		}
		if (_bitRes.BitsRead < num3)
		{
			_bitRes.SkipBits((int)(num3 - _bitRes.BitsRead));
		}
		if (num4 < 576)
		{
			Array.Clear(_samples[ch], num4, 579 - num4);
		}
	}

	private float Dequantize(int idx, float val, int gr, int ch)
	{
		if (val != 0f)
		{
			int num;
			if (_blockSplitFlag[gr][ch] && _blockType[gr][ch] == 2 && (!_mixedBlockFlag[gr][ch] || idx >= _sfBandIndexL[8]))
			{
				num = _cbLookupS[idx];
				int num2 = _cbwLookupS[idx];
				return val * _globalGain[gr][ch] * POW2_TAB[(int)(-2f * (_subblockGain[gr][ch][num2] - _scalefacScale[gr][ch] * (float)_scalefac[ch][num2][num]))];
			}
			num = _cbLookupL[idx];
			return val * _globalGain[gr][ch] * POW2_TAB[(int)(2f * _scalefacScale[gr][ch] * (float)(_scalefac[ch][3][num] + _preflag[gr][ch] * PRETAB[num]))];
		}
		return 0f;
	}

	private void Stereo(MpegChannelMode channelMode, int chanModeExt, int gr, bool lsf)
	{
		if (channelMode == MpegChannelMode.JointStereo && chanModeExt != 0)
		{
			bool flag = (chanModeExt & 2) == 2;
			if ((chanModeExt & 1) == 1)
			{
				int num = -1;
				for (int num2 = 543; num2 >= 0; num2--)
				{
					if (_samples[1][num2] != 0f)
					{
						num = num2;
						break;
					}
				}
				int num3 = -1;
				int num4 = -1;
				if (_blockSplitFlag[gr][0] && _blockType[gr][0] == 2)
				{
					if (_mixedBlockFlag[gr][0])
					{
						if (num < _sfBandIndexL[8])
						{
							num3 = 8;
						}
						num4 = 3;
					}
					else
					{
						num4 = 0;
					}
				}
				else
				{
					num3 = 21;
				}
				int i = 0;
				if (num > -1)
				{
					i = _cbLookupL[num] + 1;
				}
				if (i > 0 && num4 == -1)
				{
					if (flag)
					{
						ApplyMidSide(0, _sfBandIndexL[i]);
					}
					else
					{
						ApplyFullStereo(0, _sfBandIndexL[i]);
					}
				}
				for (; i < num3; i++)
				{
					int i2 = _sfBandIndexL[i];
					int sb = _sfBandIndexL[i + 1] - _sfBandIndexL[i];
					int num5 = _scalefac[1][3][i];
					if (num5 == 7)
					{
						if (flag)
						{
							ApplyMidSide(i2, sb);
						}
						else
						{
							ApplyFullStereo(i2, sb);
						}
					}
					else if (lsf)
					{
						ApplyLsfIStereo(i2, sb, num5, _scalefacCompress[gr][0]);
					}
					else
					{
						ApplyIStereo(i2, sb, num5);
					}
				}
				if (num4 <= -1)
				{
					int num6 = _scalefac[1][3][20];
					if (num6 == 7)
					{
						if (flag)
						{
							ApplyMidSide(_sfBandIndexL[21], 576 - _sfBandIndexL[21]);
						}
						else
						{
							ApplyFullStereo(_sfBandIndexL[21], 576 - _sfBandIndexL[21]);
						}
					}
					else if (lsf)
					{
						ApplyLsfIStereo(_sfBandIndexL[21], 576 - _sfBandIndexL[21], num6, _scalefacCompress[gr][0]);
					}
					else
					{
						ApplyIStereo(_sfBandIndexL[21], 576 - _sfBandIndexL[21], num6);
					}
					return;
				}
				int[] array = new int[3] { -1, -1, -1 };
				int num7;
				if (num > -1)
				{
					i = _cbLookupS[num];
					num7 = _cbwLookupS[num];
					array[num7] = i;
				}
				else
				{
					i = 12;
					num7 = 3;
				}
				num7 = (num7 - 1) % 3;
				while (i >= num4 && num7 >= 0)
				{
					if (array[num7] != -1)
					{
						if (array[0] != -1 && array[1] != -1 && array[2] != -1)
						{
							break;
						}
					}
					else
					{
						int num8 = _sfBandIndexS[i + 1] - _sfBandIndexS[i];
						int num9 = _sfBandIndexS[i] * 3 + num8 * (num7 + 1);
						while (--num8 >= -1)
						{
							if (_samples[1][--num9] != 0f)
							{
								array[num7] = i;
								break;
							}
						}
						if (num7 == 0)
						{
							i--;
						}
					}
					num7 = (num7 - 1) % 3;
				}
				for (i = num4; i < 12; i++)
				{
					int num10 = _sfBandIndexS[i + 1] - _sfBandIndexS[i];
					int num11 = _sfBandIndexS[i] * 3;
					for (num7 = 0; num7 < 3; num7++)
					{
						if (i > array[num7])
						{
							int num12 = _scalefac[1][num7][i];
							if (num12 == 7)
							{
								if (flag)
								{
									ApplyMidSide(num11, num10);
								}
								else
								{
									ApplyFullStereo(num11, num10);
								}
							}
							else if (lsf)
							{
								ApplyLsfIStereo(num11, num10, num12, _scalefacCompress[gr][0]);
							}
							else
							{
								ApplyIStereo(num11, num10, num12);
							}
						}
						else if (flag)
						{
							ApplyMidSide(num11, num10);
						}
						else
						{
							ApplyFullStereo(num11, num10);
						}
						num11 += num10;
					}
				}
				int num13 = _sfBandIndexS[13] - _sfBandIndexS[12];
				for (num7 = 0; num7 < 3; num7++)
				{
					int num14 = _scalefac[1][num7][11];
					if (num14 == 7)
					{
						if (flag)
						{
							ApplyMidSide(_sfBandIndexS[11] * 3 + num13 * num7, num13);
						}
						else
						{
							ApplyFullStereo(_sfBandIndexS[11] * 3 + num13 * num7, num13);
						}
					}
					else if (lsf)
					{
						ApplyLsfIStereo(_sfBandIndexS[11] * 3 + num13 * num7, num13, num14, _scalefacCompress[gr][0]);
					}
					else
					{
						ApplyIStereo(_sfBandIndexS[11] * 3 + num13 * num7, num13, num14);
					}
				}
			}
			else if (flag)
			{
				ApplyMidSide(0, 576);
			}
			else
			{
				ApplyFullStereo(0, 576);
			}
		}
		else if (_channels != 1)
		{
			ApplyFullStereo(0, 576);
		}
	}

	private void ApplyIStereo(int i, int sb, int isPos)
	{
		if (StereoMode == StereoMode.DownmixToMono)
		{
			while (sb > 0)
			{
				_samples[0][i] /= 2f;
				sb--;
				i++;
			}
			return;
		}
		float num = _isRatio[0][isPos];
		float num2 = _isRatio[1][isPos];
		while (sb > 0)
		{
			_samples[1][i] = _samples[0][i] * num2;
			_samples[0][i] *= num;
			sb--;
			i++;
		}
	}

	private void ApplyLsfIStereo(int i, int sb, int isPos, int scalefacCompress)
	{
		float num = _lsfRatio[scalefacCompress % 1][isPos][0];
		float num2 = _lsfRatio[scalefacCompress % 1][isPos][1];
		if (StereoMode == StereoMode.DownmixToMono)
		{
			float num3 = 1f / (num + num2);
			while (sb > 0)
			{
				_samples[0][i] *= num3;
				sb--;
				i++;
			}
		}
		else
		{
			while (sb > 0)
			{
				_samples[1][i] = _samples[0][i] * num2;
				_samples[0][i] *= num;
				sb--;
				i++;
			}
		}
	}

	private void ApplyMidSide(int i, int sb)
	{
		if (StereoMode == StereoMode.DownmixToMono)
		{
			while (sb > 0)
			{
				_samples[0][i] *= 0.70710677f;
				sb--;
				i++;
			}
			return;
		}
		while (sb > 0)
		{
			float num = _samples[0][i];
			float num2 = _samples[1][i];
			_samples[0][i] = (num + num2) * 0.70710677f;
			_samples[1][i] = (num - num2) * 0.70710677f;
			sb--;
			i++;
		}
	}

	private void ApplyFullStereo(int i, int sb)
	{
		if (StereoMode == StereoMode.DownmixToMono)
		{
			while (sb > 0)
			{
				_samples[0][i] = (_samples[0][i] + _samples[1][i]) / 2f;
				sb--;
				i++;
			}
		}
	}

	private void Reorder(float[] buf, bool mixedBlock)
	{
		int i = 0;
		if (mixedBlock)
		{
			Array.Copy(buf, 0, _reorderBuf, 0, 36);
			i = 3;
		}
		for (; i < 13; i++)
		{
			int num = _sfBandIndexS[i];
			int num2 = _sfBandIndexS[i + 1] - num;
			for (int j = 0; j < 3; j++)
			{
				for (int k = 0; k < num2; k++)
				{
					int num3 = num * 3 + j * num2 + k;
					int num4 = num * 3 + j + k * 3;
					_reorderBuf[num4] = buf[num3];
				}
			}
		}
		Array.Copy(_reorderBuf, buf, 576);
	}

	private void AntiAlias(float[] buf, bool mixedBlock)
	{
		int num = (mixedBlock ? 1 : 31);
		int num2 = 0;
		int num3 = 0;
		while (num2 < num)
		{
			int num4 = 0;
			int num5 = num3 + 18 - 1;
			int num6 = num3 + 18;
			while (num4 < 8)
			{
				float num7 = buf[num5];
				float num8 = buf[num6];
				buf[num5] = num7 * _scs[num4] - num8 * _sca[num4];
				buf[num6] = num8 * _scs[num4] + num7 * _sca[num4];
				num4++;
				num5--;
				num6++;
			}
			num2++;
			num3 += 18;
		}
	}

	private void FrequencyInversion(float[] buf)
	{
		for (int i = 1; i < 18; i += 2)
		{
			for (int j = 1; j < 32; j += 2)
			{
				buf[j * 18 + i] = 0f - buf[j * 18 + i];
			}
		}
	}

	private void InversePolyphase(float[] buf, int ch, int ofs, float[] outBuf)
	{
		int num = 0;
		while (num < 18)
		{
			for (int i = 0; i < 32; i++)
			{
				_polyPhase[i] = buf[i * 18 + num];
			}
			InversePolyPhase(ch, _polyPhase);
			Array.Copy(_polyPhase, 0, outBuf, ofs, 32);
			num++;
			ofs += 32;
		}
	}
}
