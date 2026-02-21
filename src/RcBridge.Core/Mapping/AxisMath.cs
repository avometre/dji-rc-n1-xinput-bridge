namespace RcBridge.Core.Mapping;

public static class AxisMath
{
    public static float ClampSigned(float value)
    {
        return Math.Clamp(value, -1.0f, 1.0f);
    }

    public static float ApplyDeadzone(float value, float deadzone)
    {
        float clampedValue = ClampSigned(value);
        float clampedDeadzone = Math.Clamp(deadzone, 0.0f, 0.9999f);

        float magnitude = Math.Abs(clampedValue);
        if (magnitude <= clampedDeadzone)
        {
            return 0.0f;
        }

        float scaled = (magnitude - clampedDeadzone) / (1.0f - clampedDeadzone);
        return Math.Sign(clampedValue) * scaled;
    }

    public static float ApplyExpo(float value, float expo)
    {
        float clampedValue = ClampSigned(value);
        float clampedExpo = Math.Clamp(expo, 0.0f, 1.0f);

        // Blend linear and cubic responses to preserve center precision while
        // retaining full-range output.
        float cubic = clampedValue * clampedValue * clampedValue;
        return ((1.0f - clampedExpo) * clampedValue) + (clampedExpo * cubic);
    }

    public static float ApplySmoothing(float previous, float current, float smoothing)
    {
        float clampedSmoothing = Math.Clamp(smoothing, 0.0f, 1.0f);
        float alpha = 1.0f - clampedSmoothing;
        return previous + ((current - previous) * alpha);
    }

    public static float SignedToUnsigned(float signedValue)
    {
        return (ClampSigned(signedValue) + 1.0f) * 0.5f;
    }
}
