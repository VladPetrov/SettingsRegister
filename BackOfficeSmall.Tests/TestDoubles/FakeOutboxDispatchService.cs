using BackOfficeSmall.Application.Abstractions;

namespace BackOfficeSmall.Tests.TestDoubles;

internal sealed class FakeOutboxDispatchService : IOutboxDispatchService
{
    public int StartCalls { get; private set; }

    public int NotifyChangesCalls { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StartCalls++;
        return Task.CompletedTask;
    }

    public void NotifyChanges()
    {
        NotifyChangesCalls++;
    }
}
