using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RcBridge.Core.Models;
using RcBridge.Input.Dji.Decoder;
using Xunit;

namespace RcBridge.Tests.Input;

public sealed class DjiDecoderIntegrationTests
{
    [Fact]
    public void TryDecodeExtractsProtocolFramesFromChunk()
    {
        DiagnosticDjiDecoder decoder = new(
            new DjiDecoderOptions
            {
                DiagnosticMode = false,
                MaxChannels = 8,
                EnableProtocolDecodeAttempt = true,
                FrameSyncByte = 0x55,
                MinFramePayloadLength = 11,
                MaxFramePayloadLength = 64,
                PackedChannelMinRaw = 364,
                PackedChannelMaxRaw = 1684,
            },
            NullLogger<DiagnosticDjiDecoder>.Instance);

        byte[] payloadA = Pack11BitValues([364, 1024, 1684, 600, 700, 800, 900, 1000]);
        byte[] payloadB = Pack11BitValues([1684, 1024, 364, 1100, 1200, 1300, 1400, 1500]);

        byte[] frameA = BuildFrame(payloadA);
        byte[] frameB = BuildFrame(payloadB);

        byte[] chunk = new byte[2 + frameA.Length + frameB.Length];
        chunk[0] = 0x00;
        chunk[1] = 0x7E;
        frameA.CopyTo(chunk, 2);
        frameB.CopyTo(chunk, 2 + frameA.Length);

        bool firstOk = decoder.TryDecode(new RawFrame(DateTimeOffset.UtcNow, chunk), out DecodedFrame firstDecoded);
        bool secondOk = decoder.TryDecode(new RawFrame(DateTimeOffset.UtcNow, Array.Empty<byte>()), out DecodedFrame secondDecoded);

        firstOk.Should().BeTrue();
        secondOk.Should().BeTrue();

        firstDecoded.DecoderHint.Should().Be("framed-packed11");
        secondDecoded.DecoderHint.Should().Be("framed-packed11");

        firstDecoded.Channels[1].Should().BeApproximately(-1.0f, 0.10f);
        firstDecoded.Channels[3].Should().BeApproximately(1.0f, 0.10f);

        secondDecoded.Channels[1].Should().BeApproximately(1.0f, 0.10f);
        secondDecoded.Channels[3].Should().BeApproximately(-1.0f, 0.10f);
    }

    private static byte[] BuildFrame(byte[] payload)
    {
        byte[] frame = new byte[2 + payload.Length];
        frame[0] = 0x55;
        frame[1] = (byte)payload.Length;
        payload.CopyTo(frame, 2);
        return frame;
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
