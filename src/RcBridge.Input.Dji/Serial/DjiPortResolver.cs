namespace RcBridge.Input.Dji.Serial;

public enum PortResolutionStatus
{
    Resolved,
    ManualPortSelected,
    ManualPortNotFound,
    NoPortsDetected,
    NoDjiMatch,
    AmbiguousMatches,
}

public sealed record DjiPortCandidate(SerialPortInfo Port, int Score, string MatchReason);

public sealed record DjiPortResolution(
    PortResolutionStatus Status,
    string? PortName,
    IReadOnlyList<DjiPortCandidate> Candidates);

public static class DjiPortResolver
{
    public static DjiPortResolution Resolve(string? requestedPort, IReadOnlyList<SerialPortInfo> ports)
    {
        string effectiveRequestedPort = string.IsNullOrWhiteSpace(requestedPort) ? "auto" : requestedPort.Trim();

        if (!effectiveRequestedPort.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            SerialPortInfo? manualPort = null;
            foreach (SerialPortInfo port in ports)
            {
                if (port.PortName.Equals(effectiveRequestedPort, StringComparison.OrdinalIgnoreCase))
                {
                    manualPort = port;
                    break;
                }
            }

            if (manualPort is not null)
            {
                return new DjiPortResolution(PortResolutionStatus.ManualPortSelected, manualPort.PortName, Array.Empty<DjiPortCandidate>());
            }

            return new DjiPortResolution(PortResolutionStatus.ManualPortNotFound, null, Array.Empty<DjiPortCandidate>());
        }

        if (ports.Count == 0)
        {
            return new DjiPortResolution(PortResolutionStatus.NoPortsDetected, null, Array.Empty<DjiPortCandidate>());
        }

        DjiPortCandidate[] candidates = BuildCandidates(ports);
        if (candidates.Length == 0)
        {
            return new DjiPortResolution(PortResolutionStatus.NoDjiMatch, null, Array.Empty<DjiPortCandidate>());
        }

        if (candidates.Length > 1)
        {
            return new DjiPortResolution(PortResolutionStatus.AmbiguousMatches, null, candidates);
        }

        return new DjiPortResolution(PortResolutionStatus.Resolved, candidates[0].Port.PortName, candidates);
    }

    private static DjiPortCandidate[] BuildCandidates(IReadOnlyList<SerialPortInfo> ports)
    {
        List<DjiPortCandidate> candidates = new(ports.Count);

        foreach (SerialPortInfo port in ports)
        {
            int score = 0;
            List<string> reasons = new();

            string friendlyName = port.FriendlyName;

            if (friendlyName.Contains("DJI USB VCOM For Protocol", StringComparison.OrdinalIgnoreCase))
            {
                score += 100;
                reasons.Add("contains 'DJI USB VCOM For Protocol'");
            }

            if (friendlyName.Contains("DJI", StringComparison.OrdinalIgnoreCase))
            {
                score += 40;
                reasons.Add("contains 'DJI'");
            }

            if (friendlyName.Contains("VCOM", StringComparison.OrdinalIgnoreCase))
            {
                score += 25;
                reasons.Add("contains 'VCOM'");
            }

            if (friendlyName.Contains("Protocol", StringComparison.OrdinalIgnoreCase))
            {
                score += 10;
                reasons.Add("contains 'Protocol'");
            }

            if (friendlyName.Contains("RC-N1", StringComparison.OrdinalIgnoreCase))
            {
                score += 20;
                reasons.Add("contains 'RC-N1'");
            }

            if (score <= 0)
            {
                continue;
            }

            string reason = reasons.Count == 0 ? "heuristic match" : string.Join(", ", reasons);
            candidates.Add(new DjiPortCandidate(port, score, reason));
        }

        return candidates
            .OrderByDescending(static candidate => candidate.Score)
            .ThenBy(static candidate => candidate.Port.PortName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
