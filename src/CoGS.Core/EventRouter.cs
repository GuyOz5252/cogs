using System.Threading.Channels;
using CoGS.Core.Abstract;

namespace CoGS.Core;

internal sealed class EventRouter
{
    private readonly Dictionary<string, List<RouteTarget>> _routes = [];

    public void AddRoute(string sourceComponent, ChannelWriter<IEvent> targetWriter, IRule? rule)
    {
        if (!_routes.TryGetValue(sourceComponent, out var targets))
        {
            targets = [];
            _routes[sourceComponent] = targets;
        }

        targets.Add(new RouteTarget(targetWriter, rule));
    }

    public async ValueTask RouteAsync(string sourceComponent, IEvent @event, CancellationToken cancellationToken)
    {
        if (!_routes.TryGetValue(sourceComponent, out var targets))
        {
            return;
        }

        foreach (var target in targets)
        {
            if (target.Rule is null || target.Rule.Evaluate(@event))
            {
                await target.Writer.WriteAsync(@event, cancellationToken);
            }
        }
    }

    private sealed record RouteTarget(ChannelWriter<IEvent> Writer, IRule? Rule);
}
