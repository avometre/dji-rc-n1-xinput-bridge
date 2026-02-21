namespace RcBridge.Output.XInput.XInput;

public static class XInputValueConverter
{
    public static short ToStick(float normalized)
    {
        float clamped = Math.Clamp(normalized, -1.0f, 1.0f);

        if (clamped >= 0.0f)
        {
            return (short)Math.Round(clamped * short.MaxValue, MidpointRounding.AwayFromZero);
        }

        return (short)Math.Round(clamped * 32768.0f, MidpointRounding.AwayFromZero);
    }

    public static byte ToTrigger(float normalized)
    {
        float clamped = Math.Clamp(normalized, 0.0f, 1.0f);
        return (byte)Math.Round(clamped * byte.MaxValue, MidpointRounding.AwayFromZero);
    }
}
