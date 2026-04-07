using CoGS.Core.EventPublishers;

namespace CoGS.Core.Abstract;

public abstract class ComponentBase
{
    public string Name { get; internal set; } = string.Empty;

    internal IEventPublisher Publisher { get; set; } = new NullEventPublisher();

    public virtual Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public virtual Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public virtual Task HandleEventAsync(IEvent @event, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    protected ValueTask PublishAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        return Publisher.PublishAsync(@event, cancellationToken);
    }

    protected EventMetadata CreateMetadata(string? correlationId = null)
    {
        return new EventMetadata
        {
            SourceComponent = Name,
            CorrelationId = correlationId ?? Guid.NewGuid().ToString("N"),
        };
    }
}
