using Microsoft.Extensions.Logging;
using RcBridge.Core.Abstractions;
using RcBridge.Core.Mapping;
using RcBridge.Core.Models;

namespace RcBridge.Input.Dji.Decoder;

public sealed partial class DiagnosticDjiDecoder : IDjiDecoder
{
    private readonly DjiDecoderOptions _options;
    private readonly ILogger<DiagnosticDjiDecoder> _logger;
    private readonly LengthPrefixedFrameExtractor _frameExtractor;
    private readonly Queue<DecodedFrame> _pendingFrames = new();

    public DiagnosticDjiDecoder(DjiDecoderOptions options, ILogger<DiagnosticDjiDecoder> logger)
    {
        _options = options;
        _logger = logger;
        _frameExtractor = new LengthPrefixedFrameExtractor(
            new DjiFrameExtractorOptions
            {
                SyncByte = options.FrameSyncByte,
                MinPayloadLength = options.MinFramePayloadLength,
                MaxPayloadLength = options.MaxFramePayloadLength,
            });
    }

    public bool TryDecode(RawFrame frame, out DecodedFrame decodedFrame)
    {
        if (_pendingFrames.Count > 0)
        {
            decodedFrame = _pendingFrames.Dequeue();
            return true;
        }

        if (frame.Data.Length == 0)
        {
            decodedFrame = new DecodedFrame(frame.TimestampUtc, new Dictionary<int, float>(), frame.Data, "empty-frame");
            return false;
        }

        if (_options.HexDumpFrames)
        {
            LogMessages.RxFrame(_logger, frame.Data.Length, Convert.ToHexString(frame.Data));
        }

        if (_options.EnableProtocolDecodeAttempt)
        {
            EnqueueProtocolFrames(frame);
            if (_pendingFrames.Count > 0)
            {
                decodedFrame = _pendingFrames.Dequeue();
                return true;
            }
        }

        if (_options.DiagnosticMode)
        {
            decodedFrame = DecodeDiagnostic(frame);
            return true;
        }

        decodedFrame = new DecodedFrame(frame.TimestampUtc, new Dictionary<int, float>(), frame.Data, "no-frame");
        return false;
    }

    private void EnqueueProtocolFrames(RawFrame frame)
    {
        byte[][] frames = _frameExtractor.Push(frame.Data);
        if (frames.Length == 0)
        {
            return;
        }

        foreach (byte[] protocolFrame in frames)
        {
            if (!TryDecodeProtocolFrame(frame.TimestampUtc, protocolFrame, out DecodedFrame decoded))
            {
                continue;
            }

            _pendingFrames.Enqueue(decoded);
        }
    }

    private bool TryDecodeProtocolFrame(DateTimeOffset timestampUtc, byte[] protocolFrame, out DecodedFrame decoded)
    {
        decoded = new DecodedFrame(timestampUtc, new Dictionary<int, float>(), protocolFrame, "invalid");

        if (protocolFrame.Length < 2 || protocolFrame[0] != _options.FrameSyncByte)
        {
            return false;
        }

        int payloadLength = protocolFrame[1];
        if (payloadLength <= 0 || 2 + payloadLength > protocolFrame.Length)
        {
            return false;
        }

        ReadOnlySpan<byte> payload = protocolFrame.AsSpan(2, payloadLength);

        bool parsed = Packed11BitChannelDecoder.TryDecode(
            payload,
            _options.MaxChannels,
            _options.PackedChannelMinRaw,
            _options.PackedChannelMaxRaw,
            out Dictionary<int, float> channels);

        if (!parsed)
        {
            return false;
        }

        decoded = new DecodedFrame(timestampUtc, channels, protocolFrame, "framed-packed11");
        LogMessages.ProtocolFrameDecoded(_logger, protocolFrame.Length, channels.Count);
        return true;
    }

    private DecodedFrame DecodeDiagnostic(RawFrame frame)
    {
        Dictionary<int, float> channels = new();

        // Diagnostic fallback: each byte is mapped to one signed channel
        // so captures can still be used while protocol parsing evolves.
        int maxChannels = Math.Min(_options.MaxChannels, frame.Data.Length);
        for (int i = 0; i < maxChannels; i++)
        {
            float normalized = ((frame.Data[i] - 127.5f) / 127.5f);
            channels[i + 1] = AxisMath.ClampSigned(normalized);
        }

        return new DecodedFrame(
            frame.TimestampUtc,
            channels,
            frame.Data,
            "diagnostic-byte-map");
    }

    private static partial class LogMessages
    {
        [LoggerMessage(EventId = 1001, Level = LogLevel.Debug, Message = "RX frame ({Length} bytes): {Hex}")]
        public static partial void RxFrame(ILogger logger, int length, string hex);

        [LoggerMessage(EventId = 1002, Level = LogLevel.Debug, Message = "Protocol frame decoded ({Length} bytes, {ChannelCount} channels)")]
        public static partial void ProtocolFrameDecoded(ILogger logger, int length, int channelCount);
    }
}
