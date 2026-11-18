#nullable disable
namespace ArcanumLib.Audio.NLayer.Decoder;

internal class VBRInfo
{
	internal int SampleCount { get; set; }

	internal int SampleRate { get; set; }

	internal int Channels { get; set; }

	internal int VBRFrames { get; set; }

	internal int VBRBytes { get; set; }

	internal int VBRQuality { get; set; }

	internal int VBRDelay { get; set; }

	internal long VBRStreamSampleCount => VBRFrames * SampleCount;

	internal int VBRAverageBitrate => (int)((double)VBRBytes / ((double)VBRStreamSampleCount / (double)SampleRate) * 8.0);

	internal VBRInfo()
	{
	}
}
