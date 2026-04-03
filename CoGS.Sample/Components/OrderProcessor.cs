using CoGS.Core;
using CoGS.Sample.Events;
using Microsoft.Extensions.Logging;

namespace CoGS.Sample.Components;

/// <summary>
/// Stub processor that logs incoming orders and re-emits them downstream.
/// </summary>
public sealed class OrderProcessor : ComponentBase
{
    private readonly ILogger<OrderProcessor> _logger;

    public OrderProcessor(
        IPipelineComponentContext context,
        ILogger<OrderProcessor> logger)
        : base(context)
    {
        _logger = logger;
    }

    public override async Task HandleAsync(IEvent @event, CancellationToken cancellationToken)
    {
        if (@event is OrderEvent order)
        {
            _logger.LogInformation("[{Name}] Processing order {OrderId}, Amount: {Amount:C}, Region: {Region}",
                Name, order.OrderId, order.Amount, order.Region);

            await EmitAsync(order, cancellationToken);
        }
    }
}
