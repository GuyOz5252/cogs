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

    public IRule Not(IRule inner)
    {
        return new NotRule(inner);
    }

    public IRule All(params IRule[] rules)
    {
        return new AllRule(rules);
    }

    public IRule Any(params IRule[] rules)
    {
        return new AnyRule(rules);
    }
}
