#nullable disable
namespace ArcanumLib.Audio.NLayer.Decoder;

internal class LayerIDecoder : LayerIIDecoderBase
{
	private static readonly int[] _rateTable = new int[32];

	private static readonly int[][] _allocLookupTable = new int[1][] { new int[17]
	{
		4, 0, 2, 3, 4, 5, 6, 7, 8, 9,
		10, 11, 12, 13, 14, 15, 16
	} };

	internal static bool GetCRC(MpegFrame frame, ref uint crc)
	{
		return LayerIIDecoderBase.GetCRC(frame, _rateTable, _allocLookupTable, readScfsiBits: false, ref crc);
	}

	internal LayerIDecoder()
		: base(_allocLookupTable, 1)
	{
	}

	protected override int[] GetRateTable(IMpegFrame frame)
	{
		return _rateTable;
	}

	protected override void ReadScaleFactorSelection(IMpegFrame frame, int[][] scfsi, int channels)
	{
	}
}
