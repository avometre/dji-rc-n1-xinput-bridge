namespace RcBridge.Output.Linux.UInput;

public static class LinuxInputValueConverter
{
    public static int ToStick(float normalized)
    {
        float clamped = Math.Clamp(normalized, -1.0f, 1.0f);

        if (clamped >= 0.0f)
        {
            return (int)Math.Round(clamped * short.MaxValue, MidpointRounding.AwayFromZero);
        }

        return (int)Math.Round(clamped * 32768.0f, MidpointRounding.AwayFromZero);
    }

    public static int ToTrigger(float normalized)
    {
        float clamped = Math.Clamp(normalized, 0.0f, 1.0f);
        return (int)Math.Round(clamped * byte.MaxValue, MidpointRounding.AwayFromZero);
    }
}
