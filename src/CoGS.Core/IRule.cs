namespace CoGS.Core;

public interface IRule
{
    bool Evaluate(IEvent @event);
}
