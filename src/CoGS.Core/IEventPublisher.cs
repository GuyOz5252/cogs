namespace CoGS.Core;

public interface IEventPublisher
{
    ValueTask PublishAsync(IEvent @event, CancellationToken cancellationToken = default);
}
