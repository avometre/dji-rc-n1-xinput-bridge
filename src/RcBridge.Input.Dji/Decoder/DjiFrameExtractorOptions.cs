namespace RcBridge.Input.Dji.Decoder;

public sealed class DjiFrameExtractorOptions
{
    public byte SyncByte { get; init; } = 0x55;

    public int MinPayloadLength { get; init; } = 11;

    public int MaxPayloadLength { get; init; } = 64;

    public int MaxBufferLength { get; init; } = 4096;
}
