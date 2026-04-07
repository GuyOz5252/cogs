using System.Threading.Channels;
using CoGS.Core;
using CoGS.Core.Abstract;
using CoGS.Core.EventPublishers;
using CoGS.Server.Hosting;
using CoGS.Server.Registrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoGS.Server.HostedServices;

internal sealed class CogsHostedService : IHostedService, IAsyncDisposable
{
    private readonly ILogger<CogsHostedService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly PipelineRegistration _pipelineRegistration;
    private readonly EventRouter _eventRouter;
    private readonly List<ManagedComponent> _managedComponents = [];
    
    private CancellationTokenSource? _cts;

    public CogsHostedService(
        ILogger<CogsHostedService> logger,
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider,
        PipelineRegistration pipelineRegistration, 
        EventRouter eventRouter)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _serviceProvider = serviceProvider;
        _pipelineRegistration = pipelineRegistration;
        _eventRouter = eventRouter;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        foreach (var registration in _pipelineRegistration.Components)
        {
            var component = _serviceProvider.GetRequiredKeyedService<ComponentBase>(registration.Name);

            var channel = Channel.CreateUnbounded<IEvent>(new UnboundedChannelOptions
            {
                SingleReader = true,
            });

            component.Publisher = new ComponentPublisher(registration.Name, _eventRouter);

            foreach (var subscription in registration.Subscriptions)
            {
                _eventRouter.AddRoute(subscription.SourceComponentName, channel.Writer, subscription.Rule);
            }

            var componentLogger = _loggerFactory.CreateLogger($"CoGS.{registration.Name}");
#pragma warning disable CA2000 // Lifetime managed by _managedComponents, disposed in DisposeAsync
            var host = new ComponentHost(component, channel.Reader, componentLogger);
#pragma warning restore CA2000
            _managedComponents.Add(new ManagedComponent(host, channel));

            _logger.LogInformation("Registered component '{ComponentName}' ({ComponentType})",
                registration.Name, registration.ComponentType.Name);
        }

        foreach (var mc in _managedComponents)
        {
            await mc.Host.StartAsync(_cts.Token);
        }

        _logger.LogInformation("CoGS pipeline started with {Count} component(s)", _managedComponents.Count);
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

        _logger.LogInformation("CoGS pipeline stopped");
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
