namespace CoGS.Core.Rules;

[CogsType("Always")]
public sealed class AlwaysPassRule : IRule
{
    public bool Evaluate(IEvent @event)
    {
        return true;
    }
}
