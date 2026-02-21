using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;

namespace RcBridge.Output.XInput.XInput;

public readonly record struct ViGEmProbeResult(bool IsAvailable, string Message);

public static class ViGEmAvailabilityProbe
{
    public static ViGEmProbeResult Probe()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new ViGEmProbeResult(false, "ViGEm is available only on Windows 10/11.");
        }

        try
        {
            using ViGEmClient _ = new();
            return new ViGEmProbeResult(true, "ViGEmBus is installed and reachable.");
        }
        catch (VigemBusNotFoundException)
        {
            return new ViGEmProbeResult(false, "ViGEmBus driver not found. Install ViGEmBus first.");
        }
        catch (Exception ex)
        {
            return new ViGEmProbeResult(false, $"ViGEm probe failed: {ex.Message}");
        }
    }
}
