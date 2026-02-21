namespace RcBridge.Input.Dji.Capture;

public sealed class DecodedCaptureInspectionReport
{
    public int FrameCount { get; init; }

    public int DecodedFrameCount { get; init; }

    public IReadOnlyList<DecoderHintStat> DecoderHints { get; init; } = Array.Empty<DecoderHintStat>();

    public IReadOnlyList<ChannelActivityStat> ChannelStats { get; init; } = Array.Empty<ChannelActivityStat>();

    public IReadOnlyList<ButtonCandidateHint> ButtonCandidates { get; init; } = Array.Empty<ButtonCandidateHint>();
}

public sealed record DecoderHintStat(string Hint, int Count, double PercentageOfDecodedFrames);

public sealed record ChannelActivityStat(
    int Channel,
    int Samples,
    float Min,
    float Max,
    float Mean,
    float StdDev,
    int DistinctBucketCount);

public sealed record ButtonCandidateHint(
    int Channel,
    string Kind,
    string Reason,
    float Min,
    float Max,
    int DistinctBucketCount);
