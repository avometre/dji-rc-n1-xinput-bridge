namespace RcBridge.Output.Linux.UInput;

public class LinuxOutputException : Exception
{
    public LinuxOutputException(string message)
        : base(message)
    {
    }

    public LinuxOutputException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class LinuxUInputUnavailableException : LinuxOutputException
{
    public LinuxUInputUnavailableException(string message)
        : base(message)
    {
    }

    public LinuxUInputUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
