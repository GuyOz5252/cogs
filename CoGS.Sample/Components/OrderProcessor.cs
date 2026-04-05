using CoGS.Sample.Events;

namespace CoGS.Sample.Components;

[CogsComponent("OrderProcessor")]
public sealed class OrderProcessor(ILogger<OrderProcessor> logger) : ComponentBase
{
    public override async Task HandleEventAsync(IEvent @event, CancellationToken cancellationToken)
    {
        if (@event is not OrderReceived order)
        {
            return;
        }

        logger.LogInformation("[{Component}] Processing order {OrderId} (${Amount})",
            Name, order.OrderId, order.Amount);

        var status = order.Amount > 500 ? "RequiresApproval" : "Approved";

        var processed = new OrderProcessed
        {
            Metadata = CreateMetadata(order.Metadata.CorrelationId),
            OrderId = order.OrderId,
            Status = status,
        };

        await PublishAsync(processed, cancellationToken);
    }
}
