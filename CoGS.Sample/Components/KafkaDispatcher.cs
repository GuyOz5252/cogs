using CoGS.Core;
using CoGS.Sample.Options;
using Microsoft.Extensions.Options;

namespace CoGS.Sample.Components;

[CogsComponent("KafkaDispatcher", OptionsType = typeof(KafkaDispatcherOptions))]
public sealed class KafkaDispatcher(
    IOptionsMonitor<KafkaDispatcherOptions> optionsMonitor,
    ILogger<KafkaDispatcher> logger) : ComponentBase
{
    private KafkaDispatcherOptions Options => optionsMonitor.Get(Name);

    public override Task HandleEventAsync(IEvent @event, CancellationToken cancellationToken)
    {
        logger.LogInformation("[{Component}] Dispatched to '{Topic}': {EventType} (correlation: {CorrelationId})",
            Name, Options.Topic, @event.GetType().Name, @event.Metadata.CorrelationId);

        return Task.CompletedTask;
    }
}
