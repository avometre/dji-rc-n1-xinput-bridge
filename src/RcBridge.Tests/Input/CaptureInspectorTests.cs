using FluentAssertions;
using RcBridge.Core.Models;
using RcBridge.Input.Dji.Capture;
using Xunit;

namespace RcBridge.Tests.Input;

public sealed class CaptureInspectorTests
{
    [Fact]
    public async Task InspectReturnsExpectedBasicStatsAndSyncCandidates()
    {
        string path = Path.Combine(Path.GetTempPath(), $"rcbridge-inspect-{Guid.NewGuid():N}.bin");

        try
        {
            await using (BinaryCaptureWriter writer = new(path))
            {
                DateTimeOffset start = DateTimeOffset.UtcNow;
                for (int i = 0; i < 12; i++)
                {
                    byte first = i < 9 ? (byte)0xAA : (byte)0xBB;
                    byte p1 = (byte)(10 + i);
                    byte p2 = (byte)(20 + (i * 2));
                    byte p3 = (byte)(30 + (i * 3));
                    await writer.WriteFrameAsync(
                        new RawFrame(start.AddMilliseconds(i * 10), new byte[] { first, p1, p2, p3 }),
                        CancellationToken.None);
                }

                await writer.FlushAsync(CancellationToken.None);
            }

            CaptureInspectionReport report = await CaptureInspector.InspectAsync(path, CancellationToken.None);

            report.FrameCount.Should().Be(12);
            report.TotalPayloadBytes.Should().Be(48);
            report.MinFrameLength.Should().Be(4);
            report.MaxFrameLength.Should().Be(4);
            report.AverageFrameLength.Should().BeApproximately(4.0, 0.001);

            report.FrameLengthHistogram.Should().ContainSingle();
            report.FrameLengthHistogram[0].Length.Should().Be(4);
            report.FrameLengthHistogram[0].Count.Should().Be(12);

            report.SyncByteCandidates.Should().NotBeEmpty();
            report.SyncByteCandidates[0].Value.Should().Be(0xAA);
            report.SyncByteCandidates[0].Count.Should().Be(9);
            report.SyncByteCandidates[0].Percentage.Should().BeApproximately(0.75, 0.0001);

            report.CorrelationHints.Should().Contain(hint =>
                hint.PositionA == 1 &&
                hint.PositionB == 2 &&
                hint.Correlation > 0.9);
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
    public async Task InspectForEmptyCaptureReturnsZeroFrames()
    {
        string path = Path.Combine(Path.GetTempPath(), $"rcbridge-inspect-empty-{Guid.NewGuid():N}.bin");

        try
        {
            await using (BinaryCaptureWriter writer = new(path))
            {
                await writer.FlushAsync(CancellationToken.None);
            }

            CaptureInspectionReport report = await CaptureInspector.InspectAsync(path, CancellationToken.None);

            report.FrameCount.Should().Be(0);
            report.TotalPayloadBytes.Should().Be(0);
            report.FrameLengthHistogram.Should().BeEmpty();
            report.SyncByteCandidates.Should().BeEmpty();
            report.CorrelationHints.Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
