namespace RcBridge.Input.Dji.Capture;

public static class CaptureInspector
{
    private const int TopByteCount = 10;
    private const int TopSyncCandidateCount = 5;
    private const int MaxCorrelationPositions = 8;
    private const int MinCorrelationSamples = 10;
    private const double StrongCorrelationThreshold = 0.70;

    public static async Task<CaptureInspectionReport> InspectAsync(string capturePath, CancellationToken cancellationToken)
    {
        await using BinaryCaptureReader reader = new(capturePath);
        return await InspectAsync(reader, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<CaptureInspectionReport> InspectAsync(BinaryCaptureReader reader, CancellationToken cancellationToken)
    {
        Dictionary<int, int> frameLengthCounts = new();
        long[] byteFrequencies = new long[256];
        int[] firstByteCounts = new int[256];

        CorrelationAccumulator[,] correlations = CreateCorrelationAccumulators(MaxCorrelationPositions);

        int frameCount = 0;
        long totalPayloadBytes = 0;
        int minFrameLength = int.MaxValue;
        int maxFrameLength = 0;

        await foreach (var frame in reader.ReadFramesAsync(cancellationToken).ConfigureAwait(false))
        {
            frameCount++;

            int length = frame.Data.Length;
            totalPayloadBytes += length;
            minFrameLength = Math.Min(minFrameLength, length);
            maxFrameLength = Math.Max(maxFrameLength, length);

            if (frameLengthCounts.TryGetValue(length, out int count))
            {
                frameLengthCounts[length] = count + 1;
            }
            else
            {
                frameLengthCounts[length] = 1;
            }

            if (length > 0)
            {
                firstByteCounts[frame.Data[0]]++;
            }

            for (int i = 0; i < length; i++)
            {
                byteFrequencies[frame.Data[i]]++;
            }

            int availablePositions = Math.Min(length, MaxCorrelationPositions);
            for (int i = 0; i < availablePositions; i++)
            {
                for (int j = i + 1; j < availablePositions; j++)
                {
                    correlations[i, j].AddSample(frame.Data[i], frame.Data[j]);
                }
            }
        }

        if (frameCount == 0)
        {
            return new CaptureInspectionReport
            {
                FrameCount = 0,
                TotalPayloadBytes = 0,
                MinFrameLength = 0,
                MaxFrameLength = 0,
                AverageFrameLength = 0,
            };
        }

        return new CaptureInspectionReport
        {
            FrameCount = frameCount,
            TotalPayloadBytes = totalPayloadBytes,
            MinFrameLength = minFrameLength,
            MaxFrameLength = maxFrameLength,
            AverageFrameLength = (double)totalPayloadBytes / frameCount,
            FrameLengthHistogram = BuildFrameLengthHistogram(frameLengthCounts),
            TopByteFrequencies = BuildTopByteFrequencies(byteFrequencies, totalPayloadBytes),
            SyncByteCandidates = BuildSyncByteCandidates(firstByteCounts, frameCount),
            CorrelationHints = BuildCorrelationHints(correlations),
        };
    }

    private static CorrelationAccumulator[,] CreateCorrelationAccumulators(int maxPositions)
    {
        CorrelationAccumulator[,] accumulators = new CorrelationAccumulator[maxPositions, maxPositions];
        for (int i = 0; i < maxPositions; i++)
        {
            for (int j = 0; j < maxPositions; j++)
            {
                accumulators[i, j] = new CorrelationAccumulator();
            }
        }

        return accumulators;
    }

    private static FrameLengthBucket[] BuildFrameLengthHistogram(Dictionary<int, int> counts)
    {
        return counts
            .Select(static kvp => new FrameLengthBucket(kvp.Key, kvp.Value))
            .OrderByDescending(static entry => entry.Count)
            .ThenBy(static entry => entry.Length)
            .ToArray();
    }

    private static ByteFrequencyEntry[] BuildTopByteFrequencies(long[] byteFrequencies, long totalPayloadBytes)
    {
        if (totalPayloadBytes == 0)
        {
            return Array.Empty<ByteFrequencyEntry>();
        }

        return byteFrequencies
            .Select(static (count, index) => new { Value = (byte)index, Count = count })
            .Where(static entry => entry.Count > 0)
            .OrderByDescending(static entry => entry.Count)
            .ThenBy(static entry => entry.Value)
            .Take(TopByteCount)
            .Select(entry => new ByteFrequencyEntry(
                entry.Value,
                entry.Count,
                (double)entry.Count / totalPayloadBytes))
            .ToArray();
    }

    private static SyncByteCandidate[] BuildSyncByteCandidates(int[] firstByteCounts, int frameCount)
    {
        if (frameCount == 0)
        {
            return Array.Empty<SyncByteCandidate>();
        }

        return firstByteCounts
            .Select(static (count, index) => new { Value = (byte)index, Count = count })
            .Where(static entry => entry.Count > 0)
            .OrderByDescending(static entry => entry.Count)
            .ThenBy(static entry => entry.Value)
            .Take(TopSyncCandidateCount)
            .Select(entry => new SyncByteCandidate(
                entry.Value,
                entry.Count,
                (double)entry.Count / frameCount))
            .ToArray();
    }

    private static CorrelationHint[] BuildCorrelationHints(CorrelationAccumulator[,] accumulators)
    {
        List<CorrelationHint> hints = new();

        int size = accumulators.GetLength(0);
        for (int i = 0; i < size; i++)
        {
            for (int j = i + 1; j < size; j++)
            {
                CorrelationAccumulator accumulator = accumulators[i, j];
                if (accumulator.SampleCount < MinCorrelationSamples)
                {
                    continue;
                }

                if (!accumulator.TryGetCorrelation(out double correlation))
                {
                    continue;
                }

                if (Math.Abs(correlation) < StrongCorrelationThreshold)
                {
                    continue;
                }

                hints.Add(new CorrelationHint(i, j, accumulator.SampleCount, correlation));
            }
        }

        return hints
            .OrderByDescending(static hint => Math.Abs(hint.Correlation))
            .ThenBy(static hint => hint.PositionA)
            .ThenBy(static hint => hint.PositionB)
            .ToArray();
    }

    private sealed class CorrelationAccumulator
    {
        private double _sumX;
        private double _sumY;
        private double _sumXX;
        private double _sumYY;
        private double _sumXY;

        public int SampleCount { get; private set; }

        public void AddSample(byte x, byte y)
        {
            double dx = x;
            double dy = y;

            SampleCount++;
            _sumX += dx;
            _sumY += dy;
            _sumXX += dx * dx;
            _sumYY += dy * dy;
            _sumXY += dx * dy;
        }

        public bool TryGetCorrelation(out double correlation)
        {
            correlation = 0;
            if (SampleCount < 2)
            {
                return false;
            }

            double n = SampleCount;
            double numerator = (n * _sumXY) - (_sumX * _sumY);

            double denominatorX = (n * _sumXX) - (_sumX * _sumX);
            double denominatorY = (n * _sumYY) - (_sumY * _sumY);
            double denominator = Math.Sqrt(denominatorX * denominatorY);

            if (denominator <= 0)
            {
                return false;
            }

            correlation = numerator / denominator;
            return true;
        }
    }
}
