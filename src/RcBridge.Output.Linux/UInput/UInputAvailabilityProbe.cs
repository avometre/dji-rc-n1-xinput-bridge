namespace RcBridge.Output.Linux.UInput;

public sealed record UInputProbeResult(bool IsAvailable, string? DevicePath, string Message);

public static class UInputAvailabilityProbe
{
    private static readonly string[] CandidatePaths =
    [
        "/dev/uinput",
        "/dev/input/uinput",
    ];

    public static UInputProbeResult Probe()
    {
        if (!OperatingSystem.IsLinux())
        {
            return new UInputProbeResult(false, null, "uinput probing is only supported on Linux.");
        }

        foreach (string path in CandidatePaths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                using FileStream stream = new(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
                return new UInputProbeResult(true, path, $"uinput is available at {path}.");
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                return new UInputProbeResult(
                    false,
                    path,
                    $"uinput exists at {path} but is not writable: {ex.Message}");
            }
        }

        return new UInputProbeResult(
            false,
            null,
            "uinput device node not found (/dev/uinput or /dev/input/uinput). Try `sudo modprobe uinput`.");
    }

    public static string ResolveDevicePathOrThrow()
    {
        UInputProbeResult probe = Probe();
        if (!probe.IsAvailable || string.IsNullOrWhiteSpace(probe.DevicePath))
        {
            throw new LinuxUInputUnavailableException(probe.Message);
        }

        return probe.DevicePath;
    }
}
