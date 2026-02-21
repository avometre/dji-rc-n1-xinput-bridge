using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using RcBridge.Core.Models;

namespace RcBridge.Input.Dji.Capture;

public sealed class BinaryCaptureWriter : IAsyncDisposable, IDisposable
{
    private static readonly byte[] V2Magic = Encoding.ASCII.GetBytes("RCB2");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly FileStream _stream;
    private bool _disposed;

    public BinaryCaptureWriter(
        string path,
        CaptureMetadata? metadata = null,
        CaptureFileFormat format = CaptureFileFormat.MetadataV2)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous);

        if (format == CaptureFileFormat.MetadataV2)
        {
            WriteMetadataHeader(metadata ?? new CaptureMetadata());
        }
    }

    public async ValueTask WriteFrameAsync(RawFrame frame, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        byte[] header = new byte[12];
        BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(0, 8), frame.TimestampUtc.UtcDateTime.Ticks);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(8, 4), frame.Data.Length);

        await _stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await _stream.WriteAsync(frame.Data, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stream.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        return _stream.DisposeAsync();
    }

    private void WriteMetadataHeader(CaptureMetadata metadata)
    {
        byte[] metadataBytes = JsonSerializer.SerializeToUtf8Bytes(metadata, JsonOptions);

        byte[] header = new byte[8];
        V2Magic.CopyTo(header, 0);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4, 4), metadataBytes.Length);

        _stream.Write(header, 0, header.Length);
        _stream.Write(metadataBytes, 0, metadataBytes.Length);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
