using System;

namespace CoGS.Core;

/// <summary>
/// Fluent builder for composing subscription rules over <see cref="IEvent" /> instances.
/// </summary>
public sealed class RuleBuilder
{
    private Func<IEvent, bool>? _compiled;

    public RuleBuilder Where<TEvent>(Func<TEvent, bool> predicate) where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(predicate);

        Func<IEvent, bool> typed = e => e is TEvent te && predicate(te);
        _compiled = _compiled is null ? typed : And(_compiled, typed);
        return this;
    }

    public RuleBuilder OfType<TEvent>() where TEvent : IEvent
        => Where<TEvent>(_ => true);

    public RuleBuilder Or<TEvent>(Func<TEvent, bool> predicate) where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(predicate);

        Func<IEvent, bool> typed = e => e is TEvent te && predicate(te);
        _compiled = _compiled is null ? typed : (e => _compiled(e) || typed(e));
        return this;
    }

    internal Func<IEvent, bool>? Build() => _compiled;

    private static Func<IEvent, bool> And(Func<IEvent, bool> a, Func<IEvent, bool> b)
        => e => a(e) && b(e);
}

