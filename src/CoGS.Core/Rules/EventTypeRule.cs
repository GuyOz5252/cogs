namespace CoGS.Core.Rules;

public sealed class EventTypeRule<TEvent> : IRule where TEvent : IEvent
{
    public bool Evaluate(IEvent @event)
    {
        return @event is TEvent;
    }
}
