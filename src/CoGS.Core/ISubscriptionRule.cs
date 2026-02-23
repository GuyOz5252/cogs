namespace CoGS.Core;

public interface ISubscriptionRule
{
    bool IsMatch(IEvent @event);
}
