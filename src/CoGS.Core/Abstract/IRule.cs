namespace CoGS.Core.Abstract;

public interface IRule
{
    bool Evaluate(IEvent @event);
}
