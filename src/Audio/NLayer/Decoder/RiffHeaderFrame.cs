#nullable disable
using System;

namespace ArcanumLib.Audio.NLayer.Decoder;

internal class RiffHeaderFrame : FrameBase
{
	internal static RiffHeaderFrame TrySync(uint syncMark)
	{
		if (syncMark == 1380533830)
		{
			return new RiffHeaderFrame();
		}
		return null;
	}

	private RiffHeaderFrame()
	{
	}

	protected override int Validate()
	{
		byte[] array = new byte[4];
		if (Read(8, array) != 4)
		{
			return -1;
		}
		if (array[0] != 87 || array[1] != 65 || array[2] != 86 || array[3] != 69)
		{
			return -1;
		}
		if (Read(12, array) != 4)
		{
			return -1;
		}
		if (array[0] != 102 || array[1] != 109 || array[2] != 116 || array[3] != 32)
		{
			return -1;
		}
		int num = 16;
		do
		{
			if (Read(num, array) != 4)
			{
				return -1;
			}
			num += 4 + BitConverter.ToInt32(array, 0);
			if (Read(num, array) != 4)
			{
				return -1;
			}
			num += 4;
		}
		while (array[0] != 100 || array[1] != 97 || array[2] != 116 || array[3] != 97);
		return num + 4;
	}
}
