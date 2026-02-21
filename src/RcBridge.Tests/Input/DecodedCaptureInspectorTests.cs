using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RcBridge.Core.Models;
using RcBridge.Input.Dji.Capture;
using RcBridge.Input.Dji.Decoder;
using Xunit;

namespace RcBridge.Tests.Input;

public sealed class DecodedCaptureInspectorTests
{
    [Fact]
    public async Task InspectAsyncDetectsButtonLikeChannelFromDecodedFrames()
    {
        string path = Path.Combine(Path.GetTempPath(), $"rcbridge-decoded-inspect-{Guid.NewGuid():N}.bin");

        try
        {
            await using (BinaryCaptureWriter writer = new(path))
            {
                DateTimeOffset start = DateTimeOffset.UtcNow;
                for (int i = 0; i < 60; i++)
                {
                    int sweepRaw = 364 + ((i % 30) * 45);
                    int buttonRaw = ((i / 10) % 2 == 0) ? 364 : 1684;

                    byte[] payload = Pack11BitValues([sweepRaw, 1024, 1024, 1024, 364, 364, buttonRaw, 364]);
                    byte[] frame = BuildFrame(payload);

                    await writer.WriteFrameAsync(
                        new RawFrame(start.AddMilliseconds(i * 10), frame),
                        CancellationToken.None);
                }

                await writer.FlushAsync(CancellationToken.None);
            }

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

            DecodedCaptureInspectionReport report =
                await DecodedCaptureInspector.InspectAsync(path, decoder, CancellationToken.None);

            report.FrameCount.Should().Be(60);
            report.DecodedFrameCount.Should().Be(60);
            report.ChannelStats.Should().NotBeEmpty();

            report.ButtonCandidates.Should().Contain(candidate => candidate.Channel == 7);
            ChannelActivityStat channel7 = report.ChannelStats.Single(stat => stat.Channel == 7);
            channel7.DistinctBucketCount.Should().BeLessOrEqualTo(2);
            (channel7.Max - channel7.Min).Should().BeGreaterThan(1.0f);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task InspectAsyncReturnsZeroDecodedFramesWhenDataCannotBeDecoded()
    {
        string path = Path.Combine(Path.GetTempPath(), $"rcbridge-decoded-inspect-invalid-{Guid.NewGuid():N}.bin");

        try
        {
            await using (BinaryCaptureWriter writer = new(path))
            {
                DateTimeOffset start = DateTimeOffset.UtcNow;
                for (int i = 0; i < 8; i++)
                {
                    await writer.WriteFrameAsync(
                        new RawFrame(start.AddMilliseconds(i * 10), new byte[] { 0x01, 0x02, 0x03 }),
                        CancellationToken.None);
                }

                await writer.FlushAsync(CancellationToken.None);
            }

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

            DecodedCaptureInspectionReport report =
                await DecodedCaptureInspector.InspectAsync(path, decoder, CancellationToken.None);

            report.FrameCount.Should().Be(8);
            report.DecodedFrameCount.Should().Be(0);
            report.ChannelStats.Should().BeEmpty();
            report.ButtonCandidates.Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
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
