namespace CoGS.Core.Abstract;

public interface IEventPublisher
{
    ValueTask PublishAsync(IEvent @event, CancellationToken cancellationToken = default);
}
