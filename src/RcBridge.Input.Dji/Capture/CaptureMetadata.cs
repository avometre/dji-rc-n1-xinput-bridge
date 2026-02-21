namespace RcBridge.Input.Dji.Capture;

public sealed class CaptureMetadata
{
    public int FormatVersion { get; init; } = 2;

    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;

    public string Port { get; init; } = string.Empty;

    public int BaudRate { get; init; }

    public string Note { get; init; } = string.Empty;

    public string Tool { get; init; } = "rcbridge";
}

public enum CaptureFileFormat
{
    LegacyV1 = 1,
    MetadataV2 = 2,
}
