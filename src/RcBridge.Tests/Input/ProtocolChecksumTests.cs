using FluentAssertions;
using RcBridge.Input.Dji.Decoder;
using Xunit;

namespace RcBridge.Tests.Input;

public sealed class ProtocolChecksumTests
{
    [Fact]
    public void TryValidateReturnsTrueWhenChecksumModeIsNone()
    {
        DjiDecoderOptions options = new()
        {
            ChecksumMode = ProtocolChecksumMode.None,
        };

        bool ok = ProtocolChecksum.TryValidate([0x10, 0x20, 0x30], 0x55, 3, options, out int dataLength);

        ok.Should().BeTrue();
        dataLength.Should().Be(3);
    }

    [Fact]
    public void TryValidateAcceptsValidXorTailChecksumWithoutHeader()
    {
        DjiDecoderOptions options = new()
        {
            ChecksumMode = ProtocolChecksumMode.Xor8Tail,
            ChecksumIncludesHeader = false,
        };

        byte[] payloadWithChecksum = [0x11, 0x22, 0x33, 0x00];

        bool ok = ProtocolChecksum.TryValidate(payloadWithChecksum, 0x55, (byte)payloadWithChecksum.Length, options, out int dataLength);

        ok.Should().BeTrue();
        dataLength.Should().Be(3);
    }

    [Fact]
    public void TryValidateAcceptsValidXorTailChecksumWithHeader()
    {
        DjiDecoderOptions options = new()
        {
            ChecksumMode = ProtocolChecksumMode.Xor8Tail,
            ChecksumIncludesHeader = true,
        };

        byte sync = 0x55;
        byte payloadLength = 4;
        byte expectedChecksum = (byte)(sync ^ payloadLength ^ 0x11 ^ 0x22 ^ 0x33);
        byte[] payloadWithChecksum = [0x11, 0x22, 0x33, expectedChecksum];

        bool ok = ProtocolChecksum.TryValidate(payloadWithChecksum, sync, payloadLength, options, out int dataLength);

        ok.Should().BeTrue();
        dataLength.Should().Be(3);
    }

    [Fact]
    public void TryValidateRejectsInvalidXorTailChecksum()
    {
        DjiDecoderOptions options = new()
        {
            ChecksumMode = ProtocolChecksumMode.Xor8Tail,
            ChecksumIncludesHeader = false,
        };

        bool ok = ProtocolChecksum.TryValidate([0x10, 0x20, 0x30, 0xAA], 0x55, 4, options, out int dataLength);

        ok.Should().BeFalse();
        dataLength.Should().Be(0);
    }
}
