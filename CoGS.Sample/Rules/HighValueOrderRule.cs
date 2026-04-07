using CoGS.Core;
using CoGS.Sample.Events;

namespace CoGS.Sample.Rules;

[CogsType("HighValueOrder")]
public sealed class HighValueOrderRule(ILogger<HighValueOrderRule> logger) : IRule
{
    public decimal MinAmount { get; set; } = 500;

    public bool Evaluate(IEvent @event)
    {
        if (@event is not OrderReceived order)
        {
            return true;
        }

        var passes = order.Amount >= MinAmount;

        logger.LogDebug("Order {OrderId} amount ${Amount} vs threshold ${MinAmount}: {Result}",
            order.OrderId, order.Amount, MinAmount, passes ? "PASS" : "REJECT");

        return passes;
    }
}
