#nullable disable
using System;
using ArcanumLib.Audio.NLayer.Decoder;

namespace ArcanumLib.Audio.NLayer;

public class MpegFrameDecoder
{
	private LayerIDecoder _layerIDecoder;

	private LayerIIDecoder _layerIIDecoder;

	private LayerIIIDecoder _layerIIIDecoder;

	private float[] _eqFactors;

	private float[] _ch0;

	private float[] _ch1;

	public StereoMode StereoMode { get; set; }

	public MpegFrameDecoder()
	{
		_ch0 = new float[1152];
		_ch1 = new float[1152];
	}

	public void SetEQ(float[] eq)
	{
		if (eq != null)
		{
			float[] array = new float[32];
			for (int i = 0; i < eq.Length; i++)
			{
				array[i] = (float)Math.Pow(2.0, eq[i] / 6f);
			}
			_eqFactors = array;
		}
		else
		{
			_eqFactors = null;
		}
	}

	public int DecodeFrame(IMpegFrame frame, byte[] dest, int destOffset)
	{
		if (frame == null)
		{
			throw new ArgumentNullException("frame");
		}
		if (dest == null)
		{
			throw new ArgumentNullException("dest");
		}
		if (destOffset % 4 != 0)
		{
			throw new ArgumentException("Must be an even multiple of 4", "destOffset");
		}
		if ((dest.Length - destOffset) / 4 < ((frame.ChannelMode == MpegChannelMode.Mono) ? 1 : 2) * frame.SampleCount)
		{
			throw new ArgumentException("Buffer not large enough!  Must be big enough to hold the frame's entire output.  This is up to 9,216 bytes.", "dest");
		}
		return DecodeFrameImpl(frame, dest, destOffset / 4) * 4;
	}

	public int DecodeFrame(IMpegFrame frame, float[] dest, int destOffset)
	{
		if (frame == null)
		{
			throw new ArgumentNullException("frame");
		}
		if (dest == null)
		{
			throw new ArgumentNullException("dest");
		}
		if (dest.Length - destOffset < ((frame.ChannelMode == MpegChannelMode.Mono) ? 1 : 2) * frame.SampleCount)
		{
			throw new ArgumentException("Buffer not large enough!  Must be big enough to hold the frame's entire output.  This is up to 2,304 elements.", "dest");
		}
		return DecodeFrameImpl(frame, dest, destOffset);
	}

	private int DecodeFrameImpl(IMpegFrame frame, Array dest, int destOffset)
	{
		frame.Reset();
		LayerDecoderBase layerDecoderBase = null;
		switch (frame.Layer)
		{
		case MpegLayer.LayerI:
			if (_layerIDecoder == null)
			{
				_layerIDecoder = new LayerIDecoder();
			}
			layerDecoderBase = _layerIDecoder;
			break;
		case MpegLayer.LayerII:
			if (_layerIIDecoder == null)
			{
				_layerIIDecoder = new LayerIIDecoder();
			}
			layerDecoderBase = _layerIIDecoder;
			break;
		case MpegLayer.LayerIII:
			if (_layerIIIDecoder == null)
			{
				_layerIIIDecoder = new LayerIIIDecoder();
			}
			layerDecoderBase = _layerIIIDecoder;
			break;
		}
		if (layerDecoderBase != null)
		{
			layerDecoderBase.SetEQ(_eqFactors);
			layerDecoderBase.StereoMode = StereoMode;
			int num = layerDecoderBase.DecodeFrame(frame, _ch0, _ch1);
			if (frame.ChannelMode == MpegChannelMode.Mono)
			{
				Buffer.BlockCopy(_ch0, 0, dest, destOffset * 4, num * 4);
			}
			else
			{
				for (int i = 0; i < num; i++)
				{
					Buffer.BlockCopy(_ch0, i * 4, dest, destOffset * 4, 4);
					destOffset++;
					Buffer.BlockCopy(_ch1, i * 4, dest, destOffset * 4, 4);
					destOffset++;
				}
				num *= 2;
			}
			return num;
		}
		return 0;
	}

	public void Reset()
	{
		if (_layerIDecoder != null)
		{
			_layerIDecoder.ResetForSeek();
		}
		if (_layerIIDecoder != null)
		{
			_layerIIDecoder.ResetForSeek();
		}
		if (_layerIIIDecoder != null)
		{
			_layerIIIDecoder.ResetForSeek();
		}
	}
}
