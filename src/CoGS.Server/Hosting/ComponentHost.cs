namespace CoGS.Server.Hosting;

internal sealed class ComponentHost(ComponentBase component, ChannelReader<IEvent> inbox, ILogger logger) : IAsyncDisposable
{
    private Task? _processingTask;
    private CancellationTokenSource? _cts;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await component.StartAsync(cancellationToken);
        _processingTask = ProcessInboxAsync(_cts.Token);
        logger.LogDebug("Component '{ComponentName}' started", component.Name);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
        }

        if (_processingTask is not null)
        {
            try
            {
                await _processingTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
        }

        await component.StopAsync(cancellationToken);
        logger.LogDebug("Component '{ComponentName}' stopped", component.Name);
    }

    public ValueTask DisposeAsync()
    {
        _cts?.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task ProcessInboxAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var @event in inbox.ReadAllAsync(cancellationToken))
            {
                try
                {
                    await component.HandleEventAsync(@event, cancellationToken);
                }
#pragma warning disable CA1031 // Pipeline must not crash on individual event failures
                catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
                {
                    logger.LogError(ex, "Error handling event in component '{ComponentName}'", component.Name);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
    }
}
