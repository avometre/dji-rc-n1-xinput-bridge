using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using RcBridge.Core.Models;

namespace RcBridge.Input.Dji.Capture;

public sealed class BinaryCaptureReader : IAsyncDisposable, IDisposable
{
    private const int HeaderSize = 12;
    private const int MaxFrameSizeBytes = 1024 * 1024;

    private readonly FileStream _stream;
    private bool _disposed;

    public BinaryCaptureReader(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Capture file was not found: {path}", path);
        }

        _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    public async IAsyncEnumerable<RawFrame> ReadFramesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        byte[] header = new byte[HeaderSize];

        while (!cancellationToken.IsCancellationRequested)
        {
            int headerRead = await ReadExactAsync(_stream, header.AsMemory(0, HeaderSize), cancellationToken).ConfigureAwait(false);
            if (headerRead == 0)
            {
                yield break;
            }

            if (headerRead < HeaderSize)
            {
                throw new InvalidDataException("Capture file is truncated while reading frame header.");
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
