using Microsoft.Extensions.Logging;
using RcBridge.Core.Abstractions;
using RcBridge.Core.Mapping;
using RcBridge.Core.Models;

namespace RcBridge.Input.Dji.Decoder;

public sealed partial class DiagnosticDjiDecoder : IDjiDecoder
{
    private readonly DjiDecoderOptions _options;
    private readonly ILogger<DiagnosticDjiDecoder> _logger;

    public DiagnosticDjiDecoder(DjiDecoderOptions options, ILogger<DiagnosticDjiDecoder> logger)
    {
        _options = options;
        _logger = logger;
    }

    public bool TryDecode(RawFrame frame, out DecodedFrame decodedFrame)
    {
        if (frame.Data.Length == 0)
        {
            decodedFrame = new DecodedFrame(frame.TimestampUtc, new Dictionary<int, float>(), frame.Data, "empty-frame");
            return false;
        }

        if (_options.HexDumpFrames)
        {
            LogMessages.RxFrame(_logger, frame.Data.Length, Convert.ToHexString(frame.Data));
        }

        Dictionary<int, float> channels = new();

        // Diagnostic heuristic: each byte is mapped to one signed channel
        // so captures can be used to infer real channel layouts later.
        int maxChannels = Math.Min(_options.MaxChannels, frame.Data.Length);
        for (int i = 0; i < maxChannels; i++)
        {
            float normalized = ((frame.Data[i] - 127.5f) / 127.5f);
            channels[i + 1] = AxisMath.ClampSigned(normalized);
        }

        decodedFrame = new DecodedFrame(
            frame.TimestampUtc,
            channels,
            frame.Data,
            _options.DiagnosticMode ? "diagnostic-byte-map" : "stub");

        return channels.Count > 0;
    }

    private static partial class LogMessages
    {
        [LoggerMessage(EventId = 1001, Level = LogLevel.Debug, Message = "RX frame ({Length} bytes): {Hex}")]
        public static partial void RxFrame(ILogger logger, int length, string hex);
    }
}
