using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using RcBridge.Core.Models;

namespace RcBridge.Input.Dji.Capture;

public sealed class BinaryCaptureReader : IAsyncDisposable, IDisposable
{
    private const int FrameHeaderSize = 12;
    private const int MaxFrameSizeBytes = 1024 * 1024;
    private const int MaxMetadataBytes = 128 * 1024;

    private static readonly byte[] V2Magic = Encoding.ASCII.GetBytes("RCB2");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly FileStream _stream;
    private bool _disposed;
    private bool _initialized;
    private bool _eofOnInit;
    private byte[]? _pendingLegacyPrefix;

    public BinaryCaptureReader(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Capture file was not found: {path}", path);
        }

        _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    public CaptureMetadata? Metadata { get; private set; }

    public async IAsyncEnumerable<RawFrame> ReadFramesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (_eofOnInit)
        {
            yield break;
        }

        byte[] header = new byte[FrameHeaderSize];

        while (!cancellationToken.IsCancellationRequested)
        {
            bool hasFrame = await TryReadFrameHeaderAsync(header, cancellationToken).ConfigureAwait(false);
            if (!hasFrame)
            {
                yield break;
            }

            long ticks = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(0, 8));
            int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(8, 4));

            if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
            {
                throw new InvalidDataException($"Capture file contains invalid timestamp ticks: {ticks}.");
            }

            if (payloadLength < 0 || payloadLength > MaxFrameSizeBytes)
            {
                throw new InvalidDataException($"Capture file contains invalid payload length: {payloadLength}.");
            }

            byte[] payload = new byte[payloadLength];
            int payloadRead = await ReadExactAsync(_stream, payload, cancellationToken).ConfigureAwait(false);
            if (payloadRead < payloadLength)
            {
                throw new InvalidDataException("Capture file is truncated while reading frame payload.");
            }

            DateTime timestampUtc = new(ticks, DateTimeKind.Utc);
            yield return new RawFrame(new DateTimeOffset(timestampUtc), payload);
        }
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

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        byte[] firstFour = new byte[4];
        int read = await ReadExactAsync(_stream, firstFour, cancellationToken).ConfigureAwait(false);

        if (read == 0)
        {
            _eofOnInit = true;
            return;
        }

        if (read < firstFour.Length)
        {
            throw new InvalidDataException("Capture file is truncated while reading file header.");
        }

        if (firstFour.AsSpan().SequenceEqual(V2Magic))
        {
            await ReadMetadataHeaderAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        _pendingLegacyPrefix = firstFour;
    }

    private async Task ReadMetadataHeaderAsync(CancellationToken cancellationToken)
    {
        byte[] lenBytes = new byte[4];
        int lenRead = await ReadExactAsync(_stream, lenBytes, cancellationToken).ConfigureAwait(false);
        if (lenRead < lenBytes.Length)
        {
            throw new InvalidDataException("Capture file is truncated while reading metadata length.");
        }

        int metadataLength = BinaryPrimitives.ReadInt32LittleEndian(lenBytes);
        if (metadataLength < 0 || metadataLength > MaxMetadataBytes)
        {
            throw new InvalidDataException($"Capture file contains invalid metadata length: {metadataLength}.");
        }

        byte[] metadataBytes = new byte[metadataLength];
        int metadataRead = await ReadExactAsync(_stream, metadataBytes, cancellationToken).ConfigureAwait(false);
        if (metadataRead < metadataLength)
        {
            throw new InvalidDataException("Capture file is truncated while reading metadata payload.");
        }

        CaptureMetadata? metadata = JsonSerializer.Deserialize<CaptureMetadata>(metadataBytes, JsonOptions);
        if (metadata is null)
        {
            throw new InvalidDataException("Capture metadata cannot be parsed.");
        }

        Metadata = metadata;
    }

    private async ValueTask<bool> TryReadFrameHeaderAsync(byte[] header, CancellationToken cancellationToken)
    {
        if (_pendingLegacyPrefix is not null)
        {
            _pendingLegacyPrefix.CopyTo(header, 0);
            _pendingLegacyPrefix = null;

            int rest = await ReadExactAsync(_stream, header.AsMemory(4, FrameHeaderSize - 4), cancellationToken).ConfigureAwait(false);
            if (rest < FrameHeaderSize - 4)
            {
                throw new InvalidDataException("Capture file is truncated while reading frame header.");
            }

            return true;
        }

        int read = await ReadExactAsync(_stream, header.AsMemory(0, FrameHeaderSize), cancellationToken).ConfigureAwait(false);
        if (read == 0)
        {
            return false;
        }

        if (read < FrameHeaderSize)
        {
            throw new InvalidDataException("Capture file is truncated while reading frame header.");
        }

        return true;
    }

    private static async ValueTask<int> ReadExactAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int totalRead = 0;

        while (totalRead < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer[totalRead..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return totalRead;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
