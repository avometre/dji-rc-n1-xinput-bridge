using RcBridge.Core.Models;

namespace RcBridge.Core.Abstractions;

public interface IXInputSink : IDisposable, IAsyncDisposable
{
    ValueTask ConnectAsync(CancellationToken cancellationToken);

    ValueTask SendAsync(NormalizedControllerState state, CancellationToken cancellationToken);
}
