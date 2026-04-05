namespace CoGS.Core.Rules;

public sealed class PredicateRule(Func<IEvent, bool> predicate) : IRule
{
    public bool Evaluate(IEvent @event)
    {
        return predicate.Invoke(@event);
    }
}
