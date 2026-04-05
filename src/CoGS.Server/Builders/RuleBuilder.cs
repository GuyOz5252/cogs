using CoGS.Core;
using CoGS.Core.Rules;

namespace CoGS.Server.Builders;

public sealed class RuleBuilder
{
    public IRule EventType<TEvent>() where TEvent : IEvent
    {
        return new EventTypeRule<TEvent>();
    }

    public IRule Predicate(Func<IEvent, bool> predicate)
    {
        return new PredicateRule(predicate);
    }

    public IRule All(params IRule[] rules)
    {
        return new CompositeRule(rules, true);
    }

    public IRule Any(params IRule[] rules)
    {
        return new CompositeRule(rules, false);
    }
}
