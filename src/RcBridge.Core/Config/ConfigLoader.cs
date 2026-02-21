using System.Text.Json;

namespace RcBridge.Core.Config;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    public static ConfigRoot LoadAndValidate(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Configuration file was not found: {path}", path);
        }

        using FileStream stream = File.OpenRead(path);

        ConfigRoot? config = JsonSerializer.Deserialize<ConfigRoot>(stream, JsonOptions);
        if (config is null)
        {
            throw new InvalidDataException($"Configuration file '{path}' is empty or invalid JSON.");
        }

        ConfigValidationResult validation = ConfigValidator.Validate(config);
        if (!validation.IsValid)
        {
            throw new ConfigValidationException(validation.Errors);
        }

        return config;
    }
}
