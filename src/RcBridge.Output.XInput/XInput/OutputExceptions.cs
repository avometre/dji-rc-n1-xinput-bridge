namespace RcBridge.Output.XInput.XInput;

public class XInputOutputException : Exception
{
    public XInputOutputException(string message)
        : base(message)
    {
    }

    public XInputOutputException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class ViGEmUnavailableException : XInputOutputException
{
    public ViGEmUnavailableException(string message)
        : base(message)
    {
    }

    public ViGEmUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
