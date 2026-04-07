namespace CoGS.Core.Rules;

[CogsType("Any")]
public sealed class AnyRule(IReadOnlyList<IRule> rules) : IRule
{
    public bool Evaluate(IEvent @event)
    {
        return rules.Any(r => r.Evaluate(@event));
    }
}
