namespace RcBridge.Core.Config;

public static class ConfigValidator
{
    public static ConfigValidationResult Validate(ConfigRoot config)
    {
        List<string> errors = new();

        if (config.UpdateRateHz is < 10 or > 500)
        {
            errors.Add("updateRateHz must be between 10 and 500.");
        }

        if (config.Decoder.MaxChannels is < 1 or > 32)
        {
            errors.Add("decoder.maxChannels must be between 1 and 32.");
        }

        if (config.Decoder.FrameSyncByte is < 0 or > 255)
        {
            errors.Add("decoder.frameSyncByte must be between 0 and 255.");
        }

        if (config.Decoder.MinFramePayloadLength is < 1 or > 255)
        {
            errors.Add("decoder.minFramePayloadLength must be between 1 and 255.");
        }

        if (config.Decoder.MaxFramePayloadLength is < 1 or > 255)
        {
            errors.Add("decoder.maxFramePayloadLength must be between 1 and 255.");
        }

        if (config.Decoder.MinFramePayloadLength > config.Decoder.MaxFramePayloadLength)
        {
            errors.Add("decoder.minFramePayloadLength must be <= decoder.maxFramePayloadLength.");
        }

        if (config.Decoder.PackedChannelMinRaw >= config.Decoder.PackedChannelMaxRaw)
        {
            errors.Add("decoder.packedChannelMinRaw must be < decoder.packedChannelMaxRaw.");
        }

        ValidateAxisBinding("axes.leftThumbX", config.Axes.LeftThumbX, errors);
        ValidateAxisBinding("axes.leftThumbY", config.Axes.LeftThumbY, errors);
        ValidateAxisBinding("axes.rightThumbX", config.Axes.RightThumbX, errors);
        ValidateAxisBinding("axes.rightThumbY", config.Axes.RightThumbY, errors);
        ValidateAxisBinding("axes.leftTrigger", config.Axes.LeftTrigger, errors);
        ValidateAxisBinding("axes.rightTrigger", config.Axes.RightTrigger, errors);

        ValidateButtonBinding("buttons.a", config.Buttons.A, errors);
        ValidateButtonBinding("buttons.b", config.Buttons.B, errors);
        ValidateButtonBinding("buttons.x", config.Buttons.X, errors);
        ValidateButtonBinding("buttons.y", config.Buttons.Y, errors);
        ValidateButtonBinding("buttons.leftShoulder", config.Buttons.LeftShoulder, errors);
        ValidateButtonBinding("buttons.rightShoulder", config.Buttons.RightShoulder, errors);
        ValidateButtonBinding("buttons.back", config.Buttons.Back, errors);
        ValidateButtonBinding("buttons.start", config.Buttons.Start, errors);

        return new ConfigValidationResult(errors);
    }

    private static void ValidateAxisBinding(string path, AxisBinding binding, List<string> errors)
    {
        if (binding.Channel is < 1 or > 32)
        {
            errors.Add($"{path}.channel must be between 1 and 32.");
        }

        if (binding.Deadzone is < 0.0f or >= 1.0f)
        {
            errors.Add($"{path}.deadzone must be >= 0 and < 1.");
        }

        if (binding.Expo is < 0.0f or > 1.0f)
        {
            errors.Add($"{path}.expo must be between 0 and 1.");
        }

        if (binding.Smoothing is < 0.0f or > 1.0f)
        {
            errors.Add($"{path}.smoothing must be between 0 and 1.");
        }
    }

    private static void ValidateButtonBinding(string path, ButtonBinding binding, List<string> errors)
    {
        if (binding.Channel is < 1 or > 32)
        {
            errors.Add($"{path}.channel must be between 1 and 32.");
        }

        if (binding.Threshold is < -1.0f or > 1.0f)
        {
            errors.Add($"{path}.threshold must be between -1 and 1.");
        }
    }
}
