namespace CoGS.Core.Rules;

[CogsType("All")]
public sealed class AllRule(IReadOnlyList<IRule> rules) : IRule
{
    public bool Evaluate(IEvent @event)
    {
        return rules.All(r => r.Evaluate(@event));
    }
}
