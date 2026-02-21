namespace RcBridge.Input.Dji.Decoder;

public sealed class DjiDecoderOptions
{
    public bool DiagnosticMode { get; init; } = true;

    public bool HexDumpFrames { get; init; }

    public int MaxChannels { get; init; } = 8;
}
