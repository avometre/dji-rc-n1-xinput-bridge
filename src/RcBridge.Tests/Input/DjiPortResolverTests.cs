using FluentAssertions;
using RcBridge.Input.Dji.Serial;
using Xunit;

namespace RcBridge.Tests.Input;

public sealed class DjiPortResolverTests
{
    [Fact]
    public void ResolveAutoWithSingleDjiCandidateReturnsResolvedPort()
    {
        IReadOnlyList<SerialPortInfo> ports =
        [
            new SerialPortInfo("COM3", "DJI USB VCOM For Protocol (COM3)"),
            new SerialPortInfo("COM8", "USB Serial Device (COM8)"),
        ];

        DjiPortResolution result = DjiPortResolver.Resolve("auto", ports);

        result.Status.Should().Be(PortResolutionStatus.Resolved);
        result.PortName.Should().Be("COM3");
        result.Candidates.Should().HaveCount(1);
    }

    [Fact]
    public void ResolveAutoWithNoPortsReturnsNoPortsDetected()
    {
        DjiPortResolution result = DjiPortResolver.Resolve("auto", Array.Empty<SerialPortInfo>());

        result.Status.Should().Be(PortResolutionStatus.NoPortsDetected);
        result.PortName.Should().BeNull();
    }

    [Fact]
    public void ResolveAutoWithNoDjiMatchReturnsNoDjiMatch()
    {
        IReadOnlyList<SerialPortInfo> ports =
        [
            new SerialPortInfo("COM3", "USB Serial Device (COM3)"),
            new SerialPortInfo("COM8", "CP210x USB to UART Bridge (COM8)"),
        ];

        DjiPortResolution result = DjiPortResolver.Resolve("auto", ports);

        result.Status.Should().Be(PortResolutionStatus.NoDjiMatch);
        result.PortName.Should().BeNull();
    }

    [Fact]
    public void ResolveAutoWithMultipleDjiMatchesReturnsAmbiguous()
    {
        IReadOnlyList<SerialPortInfo> ports =
        [
            new SerialPortInfo("COM3", "DJI USB VCOM For Protocol (COM3)"),
            new SerialPortInfo("COM4", "DJI USB VCOM For Protocol (COM4)"),
        ];

        DjiPortResolution result = DjiPortResolver.Resolve("auto", ports);

        result.Status.Should().Be(PortResolutionStatus.AmbiguousMatches);
        result.PortName.Should().BeNull();
        result.Candidates.Should().HaveCount(2);
    }

    [Fact]
    public void ResolveAutoWithSingleLinuxUsbSerialCandidateReturnsResolvedPort()
    {
        IReadOnlyList<SerialPortInfo> ports =
        [
            new SerialPortInfo("/dev/ttyACM0", "USB Serial Device"),
            new SerialPortInfo("/dev/ttyS0", string.Empty),
        ];

        DjiPortResolution result = DjiPortResolver.Resolve("auto", ports);

        result.Status.Should().Be(PortResolutionStatus.Resolved);
        result.PortName.Should().Be("/dev/ttyACM0");
    }

    [Fact]
    public void ResolveAutoWithMultipleLinuxUsbSerialCandidatesReturnsAmbiguous()
    {
        IReadOnlyList<SerialPortInfo> ports =
        [
            new SerialPortInfo("/dev/ttyACM0", "USB Serial Device"),
            new SerialPortInfo("/dev/ttyUSB0", "USB Serial Device"),
        ];

        DjiPortResolution result = DjiPortResolver.Resolve("auto", ports);

        result.Status.Should().Be(PortResolutionStatus.AmbiguousMatches);
        result.PortName.Should().BeNull();
        result.Candidates.Should().HaveCount(2);
    }

    [Fact]
    public void ResolveManualPortSelectsRequestedPort()
    {
        IReadOnlyList<SerialPortInfo> ports =
        [
            new SerialPortInfo("COM3", "DJI USB VCOM For Protocol (COM3)"),
        ];

        DjiPortResolution result = DjiPortResolver.Resolve("COM3", ports);

        result.Status.Should().Be(PortResolutionStatus.ManualPortSelected);
        result.PortName.Should().Be("COM3");
    }

    [Fact]
    public void ResolveManualPortNotFoundReturnsErrorStatus()
    {
        IReadOnlyList<SerialPortInfo> ports =
        [
            new SerialPortInfo("COM3", "DJI USB VCOM For Protocol (COM3)"),
        ];

        DjiPortResolution result = DjiPortResolver.Resolve("COM7", ports);

        result.Status.Should().Be(PortResolutionStatus.ManualPortNotFound);
        result.PortName.Should().BeNull();
    }
}
