using FluentAssertions;
using RcBridge.Input.Dji.Decoder;
using Xunit;

namespace RcBridge.Tests.Input;

public sealed class LengthPrefixedFrameExtractorTests
{
    [Fact]
    public void PushExtractsSingleFrameWithSyncAndLength()
    {
        LengthPrefixedFrameExtractor extractor = new(
            new DjiFrameExtractorOptions
            {
                SyncByte = 0x55,
                MinPayloadLength = 3,
                MaxPayloadLength = 16,
            });

        byte[] result = extractor.Push(new byte[] { 0x00, 0x99, 0x55, 0x03, 0x11, 0x22, 0x33 })[0];

        result.Should().Equal(new byte[] { 0x55, 0x03, 0x11, 0x22, 0x33 });
    }

    [Fact]
    public void PushHandlesSplitFramesAcrossChunks()
    {
        LengthPrefixedFrameExtractor extractor = new(
            new DjiFrameExtractorOptions
            {
                SyncByte = 0x55,
                MinPayloadLength = 3,
                MaxPayloadLength = 16,
            });

        byte[][] first = extractor.Push(new byte[] { 0x55, 0x03, 0x11 });
        byte[][] second = extractor.Push(new byte[] { 0x22, 0x33, 0x55, 0x03, 0x44, 0x55, 0x66 });

        first.Should().BeEmpty();
        second.Should().HaveCount(2);
        second[0].Should().Equal(new byte[] { 0x55, 0x03, 0x11, 0x22, 0x33 });
        second[1].Should().Equal(new byte[] { 0x55, 0x03, 0x44, 0x55, 0x66 });
    }

    [Fact]
    public void PushSkipsInvalidLengthFrames()
    {
        LengthPrefixedFrameExtractor extractor = new(
            new DjiFrameExtractorOptions
            {
                SyncByte = 0x55,
                MinPayloadLength = 3,
                MaxPayloadLength = 8,
            });

        byte[][] extracted = extractor.Push(new byte[] { 0x55, 0x30, 0x01, 0x02, 0x03, 0x55, 0x03, 0xAA, 0xBB, 0xCC });

        extracted.Should().HaveCount(1);
        extracted[0].Should().Equal(new byte[] { 0x55, 0x03, 0xAA, 0xBB, 0xCC });
    }
}
