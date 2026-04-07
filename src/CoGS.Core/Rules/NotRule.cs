namespace CoGS.Core.Rules;

[CogsType("Not")]
public sealed class NotRule(IRule inner) : IRule
{
    public bool Evaluate(IEvent @event)
    {
        return !inner.Evaluate(@event);
    }
}
