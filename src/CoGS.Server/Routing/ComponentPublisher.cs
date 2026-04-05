namespace CoGS.Server.Routing;

internal sealed class ComponentPublisher(string sourceComponentName, EventRouter router) : IEventPublisher
{
    public ValueTask PublishAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        return router.RouteAsync(sourceComponentName, @event, cancellationToken);
    }
}
