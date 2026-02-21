using FluentAssertions;
using RcBridge.Output.Linux.UInput;
using Xunit;

namespace RcBridge.Tests.Output;

public sealed class LinuxInputValueConverterTests
{
    [Theory]
    [InlineData(-1.0f, -32768)]
    [InlineData(0.0f, 0)]
    [InlineData(1.0f, 32767)]
    public void ToStickMapsSignedRange(float input, int expected)
    {
        int value = LinuxInputValueConverter.ToStick(input);

        value.Should().Be(expected);
    }

    [Theory]
    [InlineData(-0.5f, 0)]
    [InlineData(0.0f, 0)]
    [InlineData(0.5f, 128)]
    [InlineData(1.0f, 255)]
    [InlineData(1.5f, 255)]
    public void ToTriggerMapsUnsignedRange(float input, int expected)
    {
        int value = LinuxInputValueConverter.ToTrigger(input);

        value.Should().Be(expected);
    }
}
