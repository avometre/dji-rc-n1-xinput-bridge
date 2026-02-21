namespace RcBridge.Core.Models;

public sealed class DecodedFrame
{
    public DecodedFrame(
        DateTimeOffset timestampUtc,
        IReadOnlyDictionary<int, float> channels,
        byte[] rawData,
        string decoderHint)
    {
        TimestampUtc = timestampUtc;
        Channels = channels;
        RawData = rawData;
        DecoderHint = decoderHint;
    }

    public DateTimeOffset TimestampUtc { get; }

    public IReadOnlyDictionary<int, float> Channels { get; }

    public byte[] RawData { get; }

    public string DecoderHint { get; }
}
