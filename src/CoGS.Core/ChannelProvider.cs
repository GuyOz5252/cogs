using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;

namespace CoGS.Core;

public class ChannelProvider
{
    private readonly IServiceProvider _services;

    public ChannelProvider(IServiceProvider services)
    {
        _services = services;
    }

    public Channel<IEvent> Provide(string key)
    {
        var channel = _services.GetKeyedService<Channel<IEvent>>(key);
        return channel ?? _services.GetRequiredService<Channel<IEvent>>();
    }
}
