using FluentAssertions;
using RcBridge.Core.Config;
using RcBridge.Core.Mapping;
using RcBridge.Core.Models;
using Xunit;

namespace RcBridge.Tests.Mapping;

public sealed class AxisMapperTests
{
    [Fact]
    public void MapAppliesInvertDeadzoneAndTriggerTransform()
    {
        ConfigRoot config = new()
        {
            Axes = new AxisMappings
            {
                LeftThumbX = new AxisBinding { Channel = 1, Deadzone = 0.0f, Expo = 0.0f, Invert = true, Smoothing = 0.0f },
                LeftThumbY = new AxisBinding { Channel = 2, Deadzone = 0.1f, Expo = 0.0f, Invert = false, Smoothing = 0.0f },
                RightThumbX = new AxisBinding { Channel = 3, Deadzone = 0.0f, Expo = 0.0f, Invert = false, Smoothing = 0.0f },
                RightThumbY = new AxisBinding { Channel = 4, Deadzone = 0.0f, Expo = 0.0f, Invert = false, Smoothing = 0.0f },
                LeftTrigger = new AxisBinding { Channel = 5, Deadzone = 0.0f, Expo = 0.0f, Invert = false, Smoothing = 0.0f },
                RightTrigger = new AxisBinding { Channel = 6, Deadzone = 0.0f, Expo = 0.0f, Invert = false, Smoothing = 0.0f },
            },
        };

        AxisMapper mapper = new(config);

        DecodedFrame frame = new(
            DateTimeOffset.UtcNow,
            new Dictionary<int, float>
            {
                [1] = 0.50f,
                [2] = 0.05f,
                [3] = -0.75f,
                [4] = 0.30f,
                [5] = 1.0f,
                [6] = -1.0f,
            },
            Array.Empty<byte>(),
            "test");

        NormalizedControllerState state = mapper.Map(frame);

        state.LeftThumbX.Should().BeApproximately(-0.50f, 0.0001f);
        state.LeftThumbY.Should().BeApproximately(0.0f, 0.0001f);
        state.RightThumbX.Should().BeApproximately(-0.75f, 0.0001f);
        state.RightThumbY.Should().BeApproximately(0.30f, 0.0001f);
        state.LeftTrigger.Should().BeApproximately(1.0f, 0.0001f);
        state.RightTrigger.Should().BeApproximately(0.0f, 0.0001f);
    }

    [Fact]
    public void MapButtonThresholdsAreApplied()
    {
        ConfigRoot config = new()
        {
            Buttons = new ButtonMappings
            {
                A = new ButtonBinding { Channel = 7, Threshold = 0.5f, Invert = false },
                B = new ButtonBinding { Channel = 8, Threshold = 0.5f, Invert = true },
                X = new ButtonBinding { Channel = 9, Threshold = 0.5f, Invert = false },
                Y = new ButtonBinding { Channel = 10, Threshold = 0.5f, Invert = false },
                LeftShoulder = new ButtonBinding { Channel = 11, Threshold = 0.5f, Invert = false },
                RightShoulder = new ButtonBinding { Channel = 12, Threshold = 0.5f, Invert = false },
                Back = new ButtonBinding { Channel = 13, Threshold = 0.5f, Invert = false },
                Start = new ButtonBinding { Channel = 14, Threshold = 0.5f, Invert = false },
            },
        };

        AxisMapper mapper = new(config);

        DecodedFrame frame = new(
            DateTimeOffset.UtcNow,
            new Dictionary<int, float>
            {
                [7] = 0.7f,
                [8] = 0.7f,
            },
            Array.Empty<byte>(),
            "test");

        NormalizedControllerState state = mapper.Map(frame);

        state.A.Should().BeTrue();
        state.B.Should().BeFalse();
    }
}
