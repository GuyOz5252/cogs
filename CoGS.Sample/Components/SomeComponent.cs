using CoGS.Core;

namespace CoGS.Sample.Components;

public class SomeComponent : ProcessorComponentBase
{
    public SomeComponent(ChannelProvider channelProvider) : base(channelProvider)
    {
    }

    [Processor]
    public async Task Process(IEvent @event)
    {
        await Task.CompletedTask;
    }
}
