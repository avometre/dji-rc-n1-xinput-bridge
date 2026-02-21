using RcBridge.Core.Abstractions;
using RcBridge.Core.Models;

namespace RcBridge.App.Cli;

public sealed class ReplayDryRunSink : IXInputSink
{
    public int SentCount { get; private set; }

    public ValueTask ConnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    public ValueTask SendAsync(NormalizedControllerState state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SentCount++;
        _ = state;
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
