namespace CoGS.Core.Rules;

public sealed class AlwaysPassRule : IRule
{
    public bool Evaluate(IEvent @event)
    {
        return true;
    }
}
