namespace RcBridge.Core.Models;

public sealed record RawFrame(DateTimeOffset TimestampUtc, byte[] Data);
