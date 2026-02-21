namespace RcBridge.Input.Dji.Decoder;

public sealed class DjiDecoderOptions
{
    public bool DiagnosticMode { get; init; } = true;

    public bool HexDumpFrames { get; init; }

    public int MaxChannels { get; init; } = 8;

    public bool EnableProtocolDecodeAttempt { get; init; } = true;

    public byte FrameSyncByte { get; init; } = 0x55;

    public int MinFramePayloadLength { get; init; } = 11;

    public int MaxFramePayloadLength { get; init; } = 64;

    public int PackedChannelMinRaw { get; init; } = 364;

    public int PackedChannelMaxRaw { get; init; } = 1684;

    public ProtocolChecksumMode ChecksumMode { get; init; } = ProtocolChecksumMode.None;

    public bool ChecksumIncludesHeader { get; init; }
}
