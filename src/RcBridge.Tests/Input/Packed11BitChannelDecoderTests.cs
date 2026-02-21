using FluentAssertions;
using RcBridge.Input.Dji.Decoder;
using Xunit;

namespace RcBridge.Tests.Input;

public sealed class Packed11BitChannelDecoderTests
{
    [Fact]
    public void TryDecodeReturnsNormalizedValues()
    {
        int[] rawChannels = [364, 1024, 1684, 364, 1024, 1684, 700, 1200];
        byte[] payload = Pack11BitValues(rawChannels);

        bool ok = Packed11BitChannelDecoder.TryDecode(
            payload,
            maxChannels: 8,
            rawMin: 364,
            rawMax: 1684,
            out Dictionary<int, float> channels);

        ok.Should().BeTrue();
        channels.Should().HaveCount(8);
        channels[1].Should().BeApproximately(-1.0f, 0.05f);
        channels[2].Should().BeApproximately(0.0f, 0.05f);
        channels[3].Should().BeApproximately(1.0f, 0.05f);
    }

    [Fact]
    public void TryDecodeFailsForEmptyPayload()
    {
        bool ok = Packed11BitChannelDecoder.TryDecode(
            ReadOnlySpan<byte>.Empty,
            maxChannels: 8,
            rawMin: 364,
            rawMax: 1684,
            out Dictionary<int, float> channels);

        ok.Should().BeFalse();
        channels.Should().BeEmpty();
    }

    private static byte[] Pack11BitValues(IReadOnlyList<int> values)
    {
        int totalBits = values.Count * 11;
        byte[] data = new byte[(totalBits + 7) / 8];

        int bitIndex = 0;
        foreach (int raw in values)
        {
            int value = raw & 0x7FF;
            for (int bit = 0; bit < 11; bit++)
            {
                if (((value >> bit) & 0x01) == 0)
                {
                    continue;
                }

                int absoluteBit = bitIndex + bit;
                int byteIndex = absoluteBit / 8;
                int shift = absoluteBit % 8;
                data[byteIndex] |= (byte)(1 << shift);
            }

            bitIndex += 11;
        }

        return data;
    }
}
