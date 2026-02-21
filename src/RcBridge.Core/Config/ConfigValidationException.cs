namespace RcBridge.Core.Config;

public sealed class ConfigValidationException : Exception
{
    public ConfigValidationException(IReadOnlyList<string> errors)
        : base(BuildMessage(errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }

    private static string BuildMessage(IReadOnlyList<string> errors)
    {
        return $"Configuration validation failed:{Environment.NewLine}- {string.Join(Environment.NewLine + "- ", errors)}";
    }
}
