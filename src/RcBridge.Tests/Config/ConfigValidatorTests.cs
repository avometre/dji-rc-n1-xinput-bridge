using FluentAssertions;
using RcBridge.Core.Config;
using Xunit;

namespace RcBridge.Tests.Config;

public sealed class ConfigValidatorTests
{
    [Fact]
    public void ValidateValidConfigReturnsNoErrors()
    {
        ConfigRoot config = new();

        ConfigValidationResult result = ConfigValidator.Validate(config);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateInvalidConfigReturnsErrors()
    {
        ConfigRoot config = new()
        {
            UpdateRateHz = 2,
            Decoder = new DecoderOptions
            {
                DiagnosticMode = true,
                HexDumpFrames = false,
                MaxChannels = 0,
            },
            Axes = new AxisMappings
            {
                LeftThumbX = new AxisBinding { Channel = 0, Deadzone = -0.1f, Expo = 2.0f, Smoothing = 1.2f },
                LeftThumbY = new AxisBinding { Channel = 2 },
                RightThumbX = new AxisBinding { Channel = 3 },
                RightThumbY = new AxisBinding { Channel = 4 },
                LeftTrigger = new AxisBinding { Channel = 5 },
                RightTrigger = new AxisBinding { Channel = 6 },
            },
            Buttons = new ButtonMappings
            {
                A = new ButtonBinding { Channel = 1, Threshold = 2.0f },
                B = new ButtonBinding { Channel = 2, Threshold = 0.5f },
                X = new ButtonBinding { Channel = 3, Threshold = 0.5f },
                Y = new ButtonBinding { Channel = 4, Threshold = 0.5f },
                LeftShoulder = new ButtonBinding { Channel = 5, Threshold = 0.5f },
                RightShoulder = new ButtonBinding { Channel = 6, Threshold = 0.5f },
                Back = new ButtonBinding { Channel = 7, Threshold = 0.5f },
                Start = new ButtonBinding { Channel = 8, Threshold = 0.5f },
            },
        };

        ConfigValidationResult result = ConfigValidator.Validate(config);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }
}
