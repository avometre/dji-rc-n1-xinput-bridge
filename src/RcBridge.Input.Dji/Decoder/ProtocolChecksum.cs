namespace RcBridge.Input.Dji.Decoder;

public static class ProtocolChecksum
{
    public static bool TryValidate(
        ReadOnlySpan<byte> payload,
        byte syncByte,
        byte payloadLength,
        DjiDecoderOptions options,
        out int dataLength)
    {
        dataLength = payload.Length;

        switch (options.ChecksumMode)
        {
            case ProtocolChecksumMode.None:
                return true;

            case ProtocolChecksumMode.Xor8Tail:
                return TryValidateXor8Tail(payload, syncByte, payloadLength, options.ChecksumIncludesHeader, out dataLength);

            default:
                dataLength = 0;
                return false;
        }
    }

    private static bool TryValidateXor8Tail(
        ReadOnlySpan<byte> payload,
        byte syncByte,
        byte payloadLength,
        bool includeHeader,
        out int dataLength)
    {
        dataLength = 0;
        if (payload.Length < 2)
        {
            return false;
        }

        byte computed = 0;
        if (includeHeader)
        {
            computed ^= syncByte;
            computed ^= payloadLength;
        }

        for (int i = 0; i < payload.Length - 1; i++)
        {
            computed ^= payload[i];
        }

        byte expected = payload[^1];
        if (computed != expected)
        {
            return false;
        }

        dataLength = payload.Length - 1;
        return true;
    }
}
