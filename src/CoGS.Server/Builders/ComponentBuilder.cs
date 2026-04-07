using CoGS.Core;
using CoGS.Core.Abstract;
using CoGS.Server.Registrations;

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
        _registration.SubscriptionRegistrations.Add(new SubscriptionRegistration
        {
            SourceComponentName = sourceComponentName,
        });
        return this;
    }

    public ComponentBuilder SubscribesTo(string sourceComponentName, Func<IEvent, bool> rule)
    {
        _registration.SubscriptionRegistrations.Add(new SubscriptionRegistration
        {
            SourceComponentName = sourceComponentName,
        });
        return this;
    }
}
