using CoGS.Core.Abstract;

namespace CoGS.Core.EventPublishers;

internal sealed class NullEventPublisher : IEventPublisher
{
    public ValueTask PublishAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
}
