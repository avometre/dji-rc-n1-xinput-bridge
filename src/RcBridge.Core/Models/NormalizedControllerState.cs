namespace RcBridge.Core.Models;

public sealed class NormalizedControllerState
{
    public float LeftThumbX { get; init; }

    public float LeftThumbY { get; init; }

    public float RightThumbX { get; init; }

    public float RightThumbY { get; init; }

    public float LeftTrigger { get; init; }

    public float RightTrigger { get; init; }

    public bool A { get; init; }

    public bool B { get; init; }

    public bool X { get; init; }

    public bool Y { get; init; }

    public bool LeftShoulder { get; init; }

    public bool RightShoulder { get; init; }

    public bool Back { get; init; }

    public bool Start { get; init; }
}
