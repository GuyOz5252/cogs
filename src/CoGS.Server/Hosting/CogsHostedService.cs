namespace CoGS.Server.Hosting;

internal sealed class CogsHostedService(
    PipelineDescriptor descriptor,
    IServiceProvider serviceProvider,
    EventRouter router,
    ILoggerFactory loggerFactory) : IHostedService, IAsyncDisposable
{
    private readonly List<ManagedComponent> _managedComponents = [];
    private CancellationTokenSource? _cts;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var logger = loggerFactory.CreateLogger<CogsHostedService>();

        foreach (var registration in descriptor.Components)
        {
            var component = (ComponentBase)serviceProvider.GetRequiredKeyedService<IComponent>(registration.Name);

            var channel = Channel.CreateUnbounded<IEvent>(new UnboundedChannelOptions
            {
                SingleReader = true,
            });

            component.Publisher = new ComponentPublisher(registration.Name, router);

            foreach (var subscription in registration.Subscriptions)
            {
                router.AddRoute(subscription.SourceComponentName, channel.Writer, subscription.Rule);
            }

            var componentLogger = loggerFactory.CreateLogger($"CoGS.{registration.Name}");
#pragma warning disable CA2000 // Lifetime managed by _managedComponents, disposed in DisposeAsync
            var host = new ComponentHost(component, channel.Reader, componentLogger);
#pragma warning restore CA2000
            _managedComponents.Add(new ManagedComponent(host, channel));

            logger.LogInformation("Registered component '{ComponentName}' ({ComponentType})",
                registration.Name, registration.ComponentType.Name);
        }

        foreach (var mc in _managedComponents)
        {
            await mc.Host.StartAsync(_cts.Token);
        }

        logger.LogInformation("CoGS pipeline started with {Count} component(s)", _managedComponents.Count);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var mc in _managedComponents)
        {
            mc.Channel.Writer.TryComplete();
        }

        foreach (var mc in _managedComponents)
        {
            await mc.Host.StopAsync(cancellationToken);
        }

        loggerFactory.CreateLogger<CogsHostedService>()
            .LogInformation("CoGS pipeline stopped");
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
        }

        foreach (var mc in _managedComponents)
        {
            await mc.Host.DisposeAsync();
        }
    }

    private sealed record ManagedComponent(ComponentHost Host, Channel<IEvent> Channel);
}
