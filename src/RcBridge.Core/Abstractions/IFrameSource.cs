using RcBridge.Core.Models;

namespace RcBridge.Core.Abstractions;

public interface IFrameSource : IDisposable
{
    IAsyncEnumerable<RawFrame> ReadFramesAsync(CancellationToken cancellationToken);
}
