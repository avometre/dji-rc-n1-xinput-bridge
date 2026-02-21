using RcBridge.Core.Abstractions;
using RcBridge.Core.Models;

namespace RcBridge.Input.Dji.Capture;

public static class DecodedCaptureInspector
{
    private const int MinSamplesForButtonCandidate = 20;
    private const float MinRangeForButtonCandidate = 0.45f;
    private const float BucketWidth = 0.20f;
    private const int MaxDistinctBucketsForButtonCandidate = 3;

    private static readonly int MaxBucketIndex = (int)Math.Round(2.0f / BucketWidth, MidpointRounding.AwayFromZero);

    public static async Task<DecodedCaptureInspectionReport> InspectAsync(
        string capturePath,
        IDjiDecoder decoder,
        CancellationToken cancellationToken)
    {
        await using BinaryCaptureReader reader = new(capturePath);
        return await InspectAsync(reader, decoder, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<DecodedCaptureInspectionReport> InspectAsync(
        BinaryCaptureReader reader,
        IDjiDecoder decoder,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(decoder);

        Dictionary<string, int> hintCounts = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<int, ChannelAccumulator> channelAccumulators = new();

        int frameCount = 0;
        int decodedFrameCount = 0;

        await foreach (RawFrame frame in reader.ReadFramesAsync(cancellationToken).ConfigureAwait(false))
        {
            frameCount++;

            if (!decoder.TryDecode(frame, out DecodedFrame decoded))
            {
                continue;
            }

            decodedFrameCount++;

            string hint = string.IsNullOrWhiteSpace(decoded.DecoderHint) ? "unknown" : decoded.DecoderHint.Trim();
            if (hintCounts.TryGetValue(hint, out int currentHintCount))
            {
                hintCounts[hint] = currentHintCount + 1;
            }
            else
            {
                hintCounts[hint] = 1;
            }

            foreach ((int channel, float value) in decoded.Channels)
            {
                if (!channelAccumulators.TryGetValue(channel, out ChannelAccumulator? accumulator))
                {
                    accumulator = new ChannelAccumulator();
                    channelAccumulators[channel] = accumulator;
                }

                accumulator.Add(value);
            }
        }

        ChannelActivityStat[] channelStats = channelAccumulators
            .Select(static kvp => kvp.Value.ToReport(kvp.Key))
            .OrderBy(static stat => stat.Channel)
            .ToArray();

        DecoderHintStat[] hintStats = hintCounts
            .OrderByDescending(static kvp => kvp.Value)
            .ThenBy(static kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kvp => new DecoderHintStat(
                kvp.Key,
                kvp.Value,
                decodedFrameCount == 0 ? 0.0d : (double)kvp.Value / decodedFrameCount))
            .ToArray();

        ButtonCandidateHint[] buttonCandidates = channelStats
            .Select(CreateButtonCandidate)
            .Where(static candidate => candidate is not null)
            .Cast<ButtonCandidateHint>()
            .OrderBy(static candidate => candidate.Channel)
            .ToArray();

        return new DecodedCaptureInspectionReport
        {
            FrameCount = frameCount,
            DecodedFrameCount = decodedFrameCount,
            DecoderHints = hintStats,
            ChannelStats = channelStats,
            ButtonCandidates = buttonCandidates,
        };
    }

    private static ButtonCandidateHint? CreateButtonCandidate(ChannelActivityStat stat)
    {
        if (stat.Samples < MinSamplesForButtonCandidate)
        {
            return null;
        }

        float range = stat.Max - stat.Min;
        if (range < MinRangeForButtonCandidate)
        {
            return null;
        }

        if (stat.DistinctBucketCount > MaxDistinctBucketsForButtonCandidate)
        {
            return null;
        }

        string kind = stat.DistinctBucketCount <= 2 ? "binary-like" : "switch-like";
        string reason = $"samples={stat.Samples}, range={range:F2}, buckets={stat.DistinctBucketCount}";

        return new ButtonCandidateHint(
            stat.Channel,
            kind,
            reason,
            stat.Min,
            stat.Max,
            stat.DistinctBucketCount);
    }

    private static int Quantize(float value)
    {
        float clamped = Math.Clamp(value, -1.0f, 1.0f);
        int bucket = (int)Math.Round((clamped + 1.0f) / BucketWidth, MidpointRounding.AwayFromZero);
        return Math.Clamp(bucket, 0, MaxBucketIndex);
    }

    private sealed class ChannelAccumulator
    {
        private readonly HashSet<int> _buckets = new();
        private double _sum;
        private double _sumSquares;
        private float _min = float.MaxValue;
        private float _max = float.MinValue;

        public int Samples { get; private set; }

        public void Add(float value)
        {
            float clamped = Math.Clamp(value, -1.0f, 1.0f);

            Samples++;
            _sum += clamped;
            _sumSquares += clamped * clamped;
            _min = Math.Min(_min, clamped);
            _max = Math.Max(_max, clamped);
            _buckets.Add(Quantize(clamped));
        }

        public ChannelActivityStat ToReport(int channel)
        {
            if (Samples == 0)
            {
                return new ChannelActivityStat(channel, 0, 0, 0, 0, 0, 0);
            }

            double mean = _sum / Samples;
            double variance = Math.Max(0, (_sumSquares / Samples) - (mean * mean));
            float stdDev = (float)Math.Sqrt(variance);

            return new ChannelActivityStat(
                channel,
                Samples,
                _min,
                _max,
                (float)mean,
                stdDev,
                _buckets.Count);
        }
    }
}
