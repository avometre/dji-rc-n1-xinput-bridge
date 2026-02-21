namespace RcBridge.Input.Dji.Capture;

public sealed class CaptureInspectionReport
{
    public int FrameCount { get; init; }

    public long TotalPayloadBytes { get; init; }

    public int MinFrameLength { get; init; }

    public int MaxFrameLength { get; init; }

    public double AverageFrameLength { get; init; }

    public IReadOnlyList<FrameLengthBucket> FrameLengthHistogram { get; init; } = Array.Empty<FrameLengthBucket>();

    public IReadOnlyList<ByteFrequencyEntry> TopByteFrequencies { get; init; } = Array.Empty<ByteFrequencyEntry>();

    public IReadOnlyList<SyncByteCandidate> SyncByteCandidates { get; init; } = Array.Empty<SyncByteCandidate>();

    public IReadOnlyList<CorrelationHint> CorrelationHints { get; init; } = Array.Empty<CorrelationHint>();
}

public sealed record FrameLengthBucket(int Length, int Count);

public sealed record ByteFrequencyEntry(byte Value, long Count, double Percentage);

public sealed record SyncByteCandidate(byte Value, int Count, double Percentage);

public sealed record CorrelationHint(int PositionA, int PositionB, int SampleCount, double Correlation);
