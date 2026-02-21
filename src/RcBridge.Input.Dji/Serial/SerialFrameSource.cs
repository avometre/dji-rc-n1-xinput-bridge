using System.IO.Ports;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using RcBridge.Core.Abstractions;
using RcBridge.Core.Models;

namespace RcBridge.Input.Dji.Serial;

public sealed partial class SerialFrameSource : IFrameSource
{
    private readonly SerialPort _serialPort;
    private readonly ILogger<SerialFrameSource> _logger;
    private readonly int _chunkSize;
    private bool _disposed;

    public SerialFrameSource(string portName, int baudRate, ILogger<SerialFrameSource> logger, int chunkSize = 64)
    {
        _logger = logger;
        _chunkSize = Math.Max(8, chunkSize);

        _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 500,
            WriteTimeout = 500,
            DtrEnable = true,
            RtsEnable = true,
        };
    }

    public async IAsyncEnumerable<RawFrame> ReadFramesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (!_serialPort.IsOpen)
        {
            _serialPort.Open();
            LogMessages.SerialOpened(_logger, _serialPort.PortName, _serialPort.BaudRate);
        }

        byte[] buffer = new byte[_chunkSize];

        while (!cancellationToken.IsCancellationRequested)
        {
            int bytesRead;
            try
            {
                bytesRead = await _serialPort.BaseStream
                    .ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }

            if (bytesRead <= 0)
            {
                continue;
            }

            byte[] frame = new byte[bytesRead];
            Buffer.BlockCopy(buffer, 0, frame, 0, bytesRead);
            yield return new RawFrame(DateTimeOffset.UtcNow, frame);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_serialPort.IsOpen)
        {
            _serialPort.Close();
            LogMessages.SerialClosed(_logger, _serialPort.PortName);
        }

        _serialPort.Dispose();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static partial class LogMessages
    {
        [LoggerMessage(EventId = 1101, Level = LogLevel.Information, Message = "Opened serial port {PortName} @ {BaudRate}")]
        public static partial void SerialOpened(ILogger logger, string portName, int baudRate);

        [LoggerMessage(EventId = 1102, Level = LogLevel.Information, Message = "Closed serial port {PortName}")]
        public static partial void SerialClosed(ILogger logger, string portName);
    }
}
