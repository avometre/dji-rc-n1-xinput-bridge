using FluentAssertions;
using RcBridge.Core.Mapping;
using Xunit;

namespace RcBridge.Tests.Mapping;

public sealed class AxisMathTests
{
    [Fact]
    public void ApplyDeadzoneInsideDeadzoneReturnsZero()
    {
        AxisMath.ApplyDeadzone(0.03f, 0.05f).Should().BeApproximately(0.0f, 0.0001f);
        AxisMath.ApplyDeadzone(-0.02f, 0.05f).Should().BeApproximately(0.0f, 0.0001f);
    }

    [Fact]
    public void ApplyDeadzoneOutsideDeadzoneScalesValue()
    {
        float result = AxisMath.ApplyDeadzone(0.50f, 0.10f);
        result.Should().BeApproximately(0.4444f, 0.0005f);
    }

    [Fact]
    public void ApplyExpoZeroExpoIsLinear()
    {
        AxisMath.ApplyExpo(0.6f, 0.0f).Should().BeApproximately(0.6f, 0.0001f);
    }

    [Fact]
    public void ApplyExpoFullExpoIsCubic()
    {
        AxisMath.ApplyExpo(0.5f, 1.0f).Should().BeApproximately(0.125f, 0.0001f);
    }

    [Fact]
    public void SignedToUnsignedMapsRange()
    {
        AxisMath.SignedToUnsigned(-1.0f).Should().BeApproximately(0.0f, 0.0001f);
        AxisMath.SignedToUnsigned(0.0f).Should().BeApproximately(0.5f, 0.0001f);
        AxisMath.SignedToUnsigned(1.0f).Should().BeApproximately(1.0f, 0.0001f);
    }
}
