using CoGS.Core;

namespace CoGS.Server;

public sealed class RuleBuilder
{
    private Func<IEvent, bool>? _compiled;

    public RuleBuilder Where<TEvent>(Func<TEvent, bool> predicate) where TEvent : IEvent
    {
        Func<IEvent, bool> typed = e => e is TEvent te && predicate(te);
        _compiled = _compiled is null ? typed : e => _compiled(e) && typed(e);
        return this;
    }

    public RuleBuilder OfType<TEvent>() where TEvent : IEvent
        => Where<TEvent>(_ => true);

    public RuleBuilder Or<TEvent>(Func<TEvent, bool> predicate) where TEvent : IEvent
    {
        Func<IEvent, bool> typed = e => e is TEvent te && predicate(te);
        _compiled = _compiled is null ? typed : e => _compiled(e) || typed(e);
        return this;
    }

    internal Func<IEvent, bool>? Build() => _compiled;
}
