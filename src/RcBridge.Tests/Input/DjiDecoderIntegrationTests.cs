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
                ChecksumMode = ProtocolChecksumMode.None,
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

    [Fact]
    public void TryDecodeAcceptsFrameWithXorTailChecksum()
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
                ChecksumMode = ProtocolChecksumMode.Xor8Tail,
                ChecksumIncludesHeader = false,
            },
            NullLogger<DiagnosticDjiDecoder>.Instance);

        byte[] payload = Pack11BitValues([364, 1024, 1684, 600, 700, 800, 900, 1000]);
        byte[] frame = BuildFrame(payload, includeXorChecksumTail: true, includeHeaderInChecksum: false);

        bool ok = decoder.TryDecode(new RawFrame(DateTimeOffset.UtcNow, frame), out DecodedFrame decoded);

        ok.Should().BeTrue();
        decoded.DecoderHint.Should().Be("framed-packed11");
        decoded.Channels[1].Should().BeApproximately(-1.0f, 0.10f);
        decoded.Channels[3].Should().BeApproximately(1.0f, 0.10f);
    }

    [Fact]
    public void TryDecodeRejectsFrameWithInvalidXorTailChecksumWhenDiagnosticDisabled()
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
                ChecksumMode = ProtocolChecksumMode.Xor8Tail,
                ChecksumIncludesHeader = false,
            },
            NullLogger<DiagnosticDjiDecoder>.Instance);

        byte[] payload = Pack11BitValues([364, 1024, 1684, 600, 700, 800, 900, 1000]);
        byte[] frame = BuildFrame(payload, includeXorChecksumTail: true, includeHeaderInChecksum: false);
        frame[^1] ^= 0xFF;

        bool ok = decoder.TryDecode(new RawFrame(DateTimeOffset.UtcNow, frame), out _);

        ok.Should().BeFalse();
    }

    private static byte[] BuildFrame(byte[] payload, bool includeXorChecksumTail = false, bool includeHeaderInChecksum = false)
    {
        int payloadLength = payload.Length + (includeXorChecksumTail ? 1 : 0);
        byte[] frame = new byte[2 + payloadLength];
        frame[0] = 0x55;
        frame[1] = (byte)payloadLength;
        payload.CopyTo(frame, 2);

        if (includeXorChecksumTail)
        {
            byte checksum = 0;
            if (includeHeaderInChecksum)
            {
                checksum ^= frame[0];
                checksum ^= frame[1];
            }

            for (int i = 0; i < payload.Length; i++)
            {
                checksum ^= payload[i];
            }

            frame[^1] = checksum;
        }

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
