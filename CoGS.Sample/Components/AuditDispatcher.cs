using CoGS.Core;
using CoGS.Sample.Events;
using Microsoft.Extensions.Logging;

namespace CoGS.Sample.Components;

/// <summary>
/// Stub audit dispatcher that logs every event it receives unconditionally.
/// </summary>
public sealed class AuditDispatcher : ComponentBase
{
    private readonly ILogger<AuditDispatcher> _logger;

    public AuditDispatcher(
        IPipelineComponentContext context,
        ILogger<AuditDispatcher> logger)
        : base(context)
    {
        _logger = logger;
    }

    public override Task HandleAsync(IEvent @event, CancellationToken cancellationToken)
    {
        if (@event is OrderEvent order)
        {
            _logger.LogInformation("[{Name}] AUDIT: order {OrderId}, Amount: {Amount:C}, Region: {Region}",
                Name, order.OrderId, order.Amount, order.Region);
        }

        return Task.CompletedTask;
    }
}
