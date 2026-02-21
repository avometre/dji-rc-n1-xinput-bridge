using RcBridge.Core.Mapping;

namespace RcBridge.Input.Dji.Decoder;

public static class Packed11BitChannelDecoder
{
    private const int ChannelBitWidth = 11;

    public static bool TryDecode(
        ReadOnlySpan<byte> payload,
        int maxChannels,
        int rawMin,
        int rawMax,
        out Dictionary<int, float> channels)
    {
        channels = new Dictionary<int, float>();

        if (payload.IsEmpty || maxChannels <= 0 || rawMax <= rawMin)
        {
            return false;
        }

        int availableChannels = (payload.Length * 8) / ChannelBitWidth;
        int channelCount = Math.Min(maxChannels, availableChannels);
        if (channelCount <= 0)
        {
            return false;
        }

        for (int channelIndex = 0; channelIndex < channelCount; channelIndex++)
        {
            if (!TryRead11BitValue(payload, channelIndex * ChannelBitWidth, out int raw))
            {
                break;
            }

            channels[channelIndex + 1] = Normalize(raw, rawMin, rawMax);
        }

        return channels.Count > 0;
    }

    private static bool TryRead11BitValue(ReadOnlySpan<byte> payload, int bitIndex, out int value)
    {
        int byteIndex = bitIndex / 8;
        int bitShift = bitIndex % 8;

        if (byteIndex >= payload.Length)
        {
            value = 0;
            return false;
        }

        int available = Math.Min(3, payload.Length - byteIndex);
        int chunk = 0;

        for (int i = 0; i < available; i++)
        {
            chunk |= payload[byteIndex + i] << (8 * i);
        }

        value = (chunk >> bitShift) & 0x7FF;
        return true;
    }

    private static float Normalize(int raw, int rawMin, int rawMax)
    {
        double center = (rawMin + rawMax) * 0.5d;
        double halfRange = (rawMax - rawMin) * 0.5d;
        if (halfRange <= 0)
        {
            return 0;
        }

        double normalized = (raw - center) / halfRange;
        return AxisMath.ClampSigned((float)normalized);
    }
}
