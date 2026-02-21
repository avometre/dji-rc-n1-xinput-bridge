using RcBridge.Core.Models;

namespace RcBridge.Core.Abstractions;

public interface IDjiDecoder
{
    bool TryDecode(RawFrame frame, out DecodedFrame decodedFrame);
}
