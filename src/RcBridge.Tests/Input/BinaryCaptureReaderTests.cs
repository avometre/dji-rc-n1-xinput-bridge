using FluentAssertions;
using RcBridge.Core.Models;
using RcBridge.Input.Dji.Capture;
using Xunit;

namespace RcBridge.Tests.Input;

public sealed class BinaryCaptureReaderTests
{
    [Fact]
    public async Task ReaderRoundTripWithWriterPreservesFrames()
    {
        string path = Path.Combine(Path.GetTempPath(), $"rcbridge-{Guid.NewGuid():N}.bin");

        try
        {
            RawFrame first = new(DateTimeOffset.UtcNow, new byte[] { 1, 2, 3, 4 });
            RawFrame second = new(DateTimeOffset.UtcNow.AddMilliseconds(20), new byte[] { 5, 6 });

            await using (BinaryCaptureWriter writer = new(path))
            {
                await writer.WriteFrameAsync(first, CancellationToken.None);
                await writer.WriteFrameAsync(second, CancellationToken.None);
                await writer.FlushAsync(CancellationToken.None);
            }

            List<RawFrame> frames = new();
            await using (BinaryCaptureReader reader = new(path))
            {
                await foreach (RawFrame frame in reader.ReadFramesAsync(CancellationToken.None))
                {
                    frames.Add(frame);
                }
            }

            frames.Should().HaveCount(2);
            frames[0].Data.Should().Equal(first.Data);
            frames[1].Data.Should().Equal(second.Data);
            frames[0].TimestampUtc.UtcDateTime.Ticks.Should().Be(first.TimestampUtc.UtcDateTime.Ticks);
            frames[1].TimestampUtc.UtcDateTime.Ticks.Should().Be(second.TimestampUtc.UtcDateTime.Ticks);
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
    public async Task ReaderThrowsForTruncatedHeader()
    {
        string path = Path.Combine(Path.GetTempPath(), $"rcbridge-{Guid.NewGuid():N}.bin");

        try
        {
            await File.WriteAllBytesAsync(path, new byte[] { 1, 2, 3 }, CancellationToken.None);

            Func<Task> act = async () =>
            {
                await using BinaryCaptureReader reader = new(path);
                await foreach (RawFrame _ in reader.ReadFramesAsync(CancellationToken.None))
                {
                }
            };

            await act.Should().ThrowAsync<InvalidDataException>();
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
