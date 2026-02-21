namespace RcBridge.Core.Config;

public sealed class ConfigRoot
{
    public int UpdateRateHz { get; init; } = 100;

    public DecoderOptions Decoder { get; init; } = new();

    public AxisMappings Axes { get; init; } = new();

    public ButtonMappings Buttons { get; init; } = new();
}

public sealed class DecoderOptions
{
    public bool DiagnosticMode { get; init; } = true;

    public bool HexDumpFrames { get; init; }

    public int MaxChannels { get; init; } = 8;

    public bool EnableProtocolDecodeAttempt { get; init; } = true;

    public int FrameSyncByte { get; init; } = 0x55;

    public int MinFramePayloadLength { get; init; } = 11;

    public int MaxFramePayloadLength { get; init; } = 64;

    public int PackedChannelMinRaw { get; init; } = 364;

    public int PackedChannelMaxRaw { get; init; } = 1684;

    public string ChecksumMode { get; init; } = "none";

    public bool ChecksumIncludesHeader { get; init; }
}

public sealed class AxisMappings
{
    public AxisBinding LeftThumbX { get; init; } = new() { Channel = 1 };

    public AxisBinding LeftThumbY { get; init; } = new() { Channel = 2, Invert = true };

    public AxisBinding RightThumbX { get; init; } = new() { Channel = 3 };

    public AxisBinding RightThumbY { get; init; } = new() { Channel = 4, Invert = true };

    public AxisBinding LeftTrigger { get; init; } = new() { Channel = 5, Expo = 0.0f };

    public AxisBinding RightTrigger { get; init; } = new() { Channel = 6, Expo = 0.0f };
}

public sealed class AxisBinding
{
    public int Channel { get; init; }

    public float Deadzone { get; init; } = 0.04f;

    public float Expo { get; init; } = 0.20f;

    public bool Invert { get; init; }

    public float Smoothing { get; init; } = 0.10f;
}

public sealed class ButtonMappings
{
    public ButtonBinding A { get; init; } = new() { Channel = 7, Threshold = 0.60f };

    public ButtonBinding B { get; init; } = new() { Channel = 8, Threshold = 0.60f };

    public ButtonBinding X { get; init; } = new() { Channel = 9, Threshold = 0.60f };

    public ButtonBinding Y { get; init; } = new() { Channel = 10, Threshold = 0.60f };

    public ButtonBinding LeftShoulder { get; init; } = new() { Channel = 11, Threshold = 0.60f };

    public ButtonBinding RightShoulder { get; init; } = new() { Channel = 12, Threshold = 0.60f };

    public ButtonBinding Back { get; init; } = new() { Channel = 13, Threshold = 0.60f };

    public ButtonBinding Start { get; init; } = new() { Channel = 14, Threshold = 0.60f };
}

public sealed class ButtonBinding
{
    public int Channel { get; init; }

    public float Threshold { get; init; }

    public bool Invert { get; init; }
}
