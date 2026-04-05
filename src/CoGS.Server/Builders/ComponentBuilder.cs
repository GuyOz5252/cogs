namespace CoGS.Server.Builders;

public sealed class ComponentBuilder
{
    private readonly ComponentRegistration _registration;

    internal ComponentBuilder(ComponentRegistration registration)
    {
        _registration = registration;
    }

    public ComponentBuilder Configure<TOptions>(Action<TOptions> configure) where TOptions : class, new()
    {
        _registration.OptionsType = typeof(TOptions);
        _registration.ConfigureOptionsAction = obj => configure((TOptions)obj);
        return this;
    }

    public ComponentBuilder SubscribesTo(string sourceComponentName)
    {
        _registration.Subscriptions.Add(new Subscription(sourceComponentName));
        return this;
    }

    public ComponentBuilder SubscribesTo(string sourceComponentName, IRule rule)
    {
        _registration.Subscriptions.Add(new Subscription(sourceComponentName, rule));
        return this;
    }

    public ComponentBuilder SubscribesTo(string sourceComponentName, Func<RuleBuilder, IRule>? ruleFactory)
    {
        var rule = ruleFactory?.Invoke(new RuleBuilder());
        _registration.Subscriptions.Add(new Subscription(sourceComponentName, rule));
        return this;
    }
}
