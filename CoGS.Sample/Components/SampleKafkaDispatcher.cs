using CoGS.Core;
using CoGS.Sample.Events;
using CoGS.Sample.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoGS.Sample.Components;

/// <summary>
/// Stub Kafka dispatcher that logs events it would produce.
/// Replace with a real Kafka producer in production.
/// </summary>
public sealed class SampleKafkaDispatcher : ComponentBase
{
    private readonly KafkaDispatcherOptions _options;
    private readonly ILogger<SampleKafkaDispatcher> _logger;

    public SampleKafkaDispatcher(
        IPipelineComponentContext context,
        IOptions<KafkaDispatcherOptions> options,
        ILogger<SampleKafkaDispatcher> logger)
        : base(context)
    {
        _options = options.Value;
        _logger = logger;
    }

    public override Task HandleAsync(IEvent @event, CancellationToken cancellationToken)
    {
        if (@event is OrderEvent order)
        {
            _logger.LogInformation("[{Name}] Dispatching order {OrderId} to topic '{Topic}'",
                Name, order.OrderId, _options.Topic);
        }

        return Task.CompletedTask;
    }
}
