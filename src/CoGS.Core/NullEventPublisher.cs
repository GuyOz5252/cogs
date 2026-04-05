namespace CoGS.Core;

internal sealed class NullEventPublisher : IEventPublisher
{
    public ValueTask PublishAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
}
