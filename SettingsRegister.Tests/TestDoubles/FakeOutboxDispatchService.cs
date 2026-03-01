using SettingsRegister.Application.Abstractions;

namespace SettingsRegister.Tests.TestDoubles;

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

