using System;
using System.Threading;
using System.Threading.Tasks;

namespace CoGS.Core;

/// <summary>
/// Base class for all pipeline components.
/// Components receive their dependencies via constructor injection and
/// interact with the runtime through <see cref="IPipelineComponentContext"/>.
/// </summary>
public abstract class ComponentBase
{
    private Func<IEvent, CancellationToken, Task>? _dispatcher;

    protected ComponentBase(IPipelineComponentContext context)
    {
        Context = context;
    }

    public IPipelineComponentContext Context { get; }

    public string Name => Context.Name;

    public string PipelineName => Context.PipelineName;

    /// <summary>
    /// Called by the pipeline host to wire the event dispatch chain.
    /// When a component calls <see cref="EmitAsync"/>, events are delivered
    /// to all downstream subscribers through this delegate.
    /// </summary>
    internal void SetDispatcher(Func<IEvent, CancellationToken, Task> dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Sends an event to all downstream subscribers of this component.
    /// </summary>
    protected async Task EmitAsync(IEvent @event, CancellationToken cancellationToken)
    {
        if (_dispatcher is not null)
        {
            await _dispatcher(@event, cancellationToken);
        }
    }

    /// <summary>
    /// Called by the host when the pipeline is starting.
    /// Override to perform initialization before events begin flowing.
    /// </summary>
    public virtual Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Called by the host when the pipeline is stopping.
    /// Override to complete in-flight work and release resources.
    /// </summary>
    public virtual Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Long-running background work for this component (e.g. a source consuming from Kafka).
    /// The host starts this as a background task after <see cref="StartAsync"/>.
    /// </summary>
    public virtual Task ExecuteAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Called when an event is delivered to this component from an upstream subscription.
    /// Override to process events and optionally <see cref="EmitAsync"/> them downstream.
    /// </summary>
    public virtual Task HandleAsync(IEvent @event, CancellationToken cancellationToken) => Task.CompletedTask;
}
