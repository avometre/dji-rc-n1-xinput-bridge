using RcBridge.Core.Config;
using RcBridge.Core.Models;

namespace RcBridge.Core.Mapping;

public sealed class AxisMapper
{
    private readonly ConfigRoot _config;
    private readonly Dictionary<string, float> _smoothingState = new(StringComparer.Ordinal);

    public AxisMapper(ConfigRoot config)
    {
        _config = config;
    }

    public NormalizedControllerState Map(DecodedFrame decodedFrame)
    {
        float leftThumbX = MapSignedAxis("leftThumbX", _config.Axes.LeftThumbX, decodedFrame);
        float leftThumbY = MapSignedAxis("leftThumbY", _config.Axes.LeftThumbY, decodedFrame);
        float rightThumbX = MapSignedAxis("rightThumbX", _config.Axes.RightThumbX, decodedFrame);
        float rightThumbY = MapSignedAxis("rightThumbY", _config.Axes.RightThumbY, decodedFrame);

        float leftTriggerSigned = MapSignedAxis("leftTrigger", _config.Axes.LeftTrigger, decodedFrame);
        float rightTriggerSigned = MapSignedAxis("rightTrigger", _config.Axes.RightTrigger, decodedFrame);

        return new NormalizedControllerState
        {
            LeftThumbX = leftThumbX,
            LeftThumbY = leftThumbY,
            RightThumbX = rightThumbX,
            RightThumbY = rightThumbY,
            LeftTrigger = AxisMath.SignedToUnsigned(leftTriggerSigned),
            RightTrigger = AxisMath.SignedToUnsigned(rightTriggerSigned),
            A = MapButton(_config.Buttons.A, decodedFrame),
            B = MapButton(_config.Buttons.B, decodedFrame),
            X = MapButton(_config.Buttons.X, decodedFrame),
            Y = MapButton(_config.Buttons.Y, decodedFrame),
            LeftShoulder = MapButton(_config.Buttons.LeftShoulder, decodedFrame),
            RightShoulder = MapButton(_config.Buttons.RightShoulder, decodedFrame),
            Back = MapButton(_config.Buttons.Back, decodedFrame),
            Start = MapButton(_config.Buttons.Start, decodedFrame),
        };
    }

    private float MapSignedAxis(string axisName, AxisBinding binding, DecodedFrame decodedFrame)
    {
        float raw = GetChannelValue(decodedFrame, binding.Channel);
        if (binding.Invert)
        {
            raw *= -1.0f;
        }

        float processed = AxisMath.ApplyDeadzone(raw, binding.Deadzone);
        processed = AxisMath.ApplyExpo(processed, binding.Expo);

        if (!_smoothingState.TryGetValue(axisName, out float previous))
        {
            previous = 0.0f;
        }

        float smoothed = AxisMath.ApplySmoothing(previous, processed, binding.Smoothing);
        _smoothingState[axisName] = smoothed;

        return AxisMath.ClampSigned(smoothed);
    }

    private static bool MapButton(ButtonBinding binding, DecodedFrame decodedFrame)
    {
        float value = GetChannelValue(decodedFrame, binding.Channel);
        bool pressed = value >= binding.Threshold;
        return binding.Invert ? !pressed : pressed;
    }

    private static float GetChannelValue(DecodedFrame decodedFrame, int channel)
    {
        if (!decodedFrame.Channels.TryGetValue(channel, out float value))
        {
            return 0.0f;
        }

        return AxisMath.ClampSigned(value);
    }
}
