namespace RcBridge.Input.Dji.Decoder;

public sealed class LengthPrefixedFrameExtractor
{
    private readonly DjiFrameExtractorOptions _options;
    private readonly List<byte> _buffer = new(512);

    public LengthPrefixedFrameExtractor(DjiFrameExtractorOptions options)
    {
        _options = options;
    }

    public byte[][] Push(ReadOnlySpan<byte> incoming)
    {
        List<byte[]> extracted = new();

        for (int i = 0; i < incoming.Length; i++)
        {
            _buffer.Add(incoming[i]);
        }

        if (_buffer.Count > _options.MaxBufferLength)
        {
            int keep = Math.Min(_options.MaxBufferLength / 2, _buffer.Count);
            _buffer.RemoveRange(0, _buffer.Count - keep);
        }

        while (true)
        {
            int syncIndex = FindSyncIndex(_buffer, _options.SyncByte);
            if (syncIndex < 0)
            {
                _buffer.Clear();
                break;
            }

            if (syncIndex > 0)
            {
                _buffer.RemoveRange(0, syncIndex);
            }

            if (_buffer.Count < 2)
            {
                break;
            }

            int payloadLength = _buffer[1];
            if (payloadLength < _options.MinPayloadLength || payloadLength > _options.MaxPayloadLength)
            {
                _buffer.RemoveAt(0);
                continue;
            }

            int totalFrameLength = 2 + payloadLength;
            if (_buffer.Count < totalFrameLength)
            {
                break;
            }

            byte[] frame = _buffer.Take(totalFrameLength).ToArray();
            extracted.Add(frame);
            _buffer.RemoveRange(0, totalFrameLength);
        }

        return extracted.ToArray();
    }

    private static int FindSyncIndex(List<byte> buffer, byte syncByte)
    {
        for (int i = 0; i < buffer.Count; i++)
        {
            if (buffer[i] == syncByte)
            {
                return i;
            }
        }

        return -1;
    }
}
