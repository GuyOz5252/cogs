namespace CoGS.Core.Rules;

public sealed class CompositeRule : IRule
{
    private readonly IReadOnlyList<IRule> _rules;
    private readonly bool _requireAll;

    public CompositeRule(IEnumerable<IRule> rules, bool requireAll)
    {
        _rules = rules.ToList();
        _requireAll = requireAll;
    }

    public bool Evaluate(IEvent @event)
    {
        return _requireAll
            ? _rules.All(r => r.Evaluate(@event))
            : _rules.Any(r => r.Evaluate(@event));
    }
}
