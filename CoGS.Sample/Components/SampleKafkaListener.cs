using CoGS.Core;
using CoGS.Sample.Events;
using CoGS.Sample.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoGS.Sample.Components;

/// <summary>
/// Stub Kafka listener that emits fake <see cref="OrderEvent"/>s on a timer.
/// Replace with a real Kafka consumer in production.
/// </summary>
public sealed class SampleKafkaListener : ComponentBase
{
    private readonly KafkaListenerOptions _options;
    private readonly ILogger<SampleKafkaListener> _logger;
    private int _counter;

    public SampleKafkaListener(
        IPipelineComponentContext context,
        IOptions<KafkaListenerOptions> options,
        ILogger<SampleKafkaListener> logger)
        : base(context)
    {
        _options = options.Value;
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[{Name}] Listening on topic '{Topic}' (group '{GroupId}', servers '{Servers}')",
            Name, _options.Topic, _options.GroupId, _options.BootstrapServers);

        return Task.CompletedTask;
    }

    public override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        string[] regions = ["EU", "US"];

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(2_000, cancellationToken);

            var seq = Interlocked.Increment(ref _counter);
            var order = new OrderEvent(
                OrderId: $"ORD-{seq:D4}",
                Amount: (seq % 7 + 1) * 300m,
                Region: regions[seq % regions.Length]);

            _logger.LogInformation("[{Name}] Received order {OrderId} ({Amount:C})", Name, order.OrderId, order.Amount);
            await EmitAsync(order, cancellationToken);
        }
    }
}
