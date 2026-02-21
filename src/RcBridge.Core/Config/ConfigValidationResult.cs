namespace RcBridge.Core.Config;

public sealed class ConfigValidationResult
{
    public ConfigValidationResult(IReadOnlyList<string> errors)
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }

    public bool IsValid => Errors.Count == 0;
}
