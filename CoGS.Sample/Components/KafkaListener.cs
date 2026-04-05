using CoGS.Core;
using CoGS.Sample.Events;
using CoGS.Sample.Options;
using Microsoft.Extensions.Options;

namespace CoGS.Sample.Components;

[CogsComponent("KafkaListener", OptionsType = typeof(KafkaListenerOptions))]
public sealed class KafkaListener(
    IOptionsMonitor<KafkaListenerOptions> optionsMonitor,
    ILogger<KafkaListener> logger) : ComponentBase, IDisposable
{
    private KafkaListenerOptions Options => optionsMonitor.Get(Name);

    private CancellationTokenSource? _cts;
    private Task? _producerTask;

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Listening on topic '{Topic}' (group: {ConsumerGroup})",
            Options.Topic, Options.ConsumerGroup);

        _cts = new CancellationTokenSource();
        _producerTask = ProduceEventsAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
        }

        if (_producerTask is not null)
        {
            try
            {
                await _producerTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
        }

        logger.LogInformation("Stopped listening on topic '{Topic}'", Options.Topic);
    }

    public void Dispose()
    {
        _cts?.Dispose();
    }

    private async Task ProduceEventsAsync(CancellationToken cancellationToken)
    {
        var counter = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(Options.ProduceIntervalMs, cancellationToken);
            counter++;

            var @event = new OrderReceived
            {
                Metadata = CreateMetadata(),
                OrderId = $"ORD-{counter:D4}",
#pragma warning disable CA5394 // Simulated demo data, not security-sensitive
                Amount = Random.Shared.Next(10, 1000),
#pragma warning restore CA5394
            };

            logger.LogInformation("[{Component}] Received order {OrderId} (${Amount})",
                Name, @event.OrderId, @event.Amount);

            await PublishAsync(@event, cancellationToken);
        }
    }
}
