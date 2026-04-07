using CoGS.Core;
using CoGS.Server.Builders;
using CoGS.Server.Configuration;
using CoGS.Server.HostedServices;
using CoGS.Server.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoGS.Server.Extensions;

public static class ServiceCollectionExtensions
{
    public static CogsServerBuilder AddCogs(this IServiceCollection services, IConfiguration configuration)
    {
        var descriptor = new PipelineDescriptor();
        var pendingRuleBindings = new List<PendingRuleBinding>();

        services.AddSingleton(sp =>
        {
            foreach (var binding in pendingRuleBindings)
            {
                var rule = sp.GetRequiredKeyedService<IRule>(binding.ConfigPath);
                binding.Registration.Subscriptions[binding.SubscriptionIndex] =
                    binding.Registration.Subscriptions[binding.SubscriptionIndex] with { Rule = rule };
            }

            return descriptor;
        });

        services.AddSingleton<EventRouter>();
        services.AddHostedService<CogsHostedService>();

        return new CogsServerBuilder(services, configuration, descriptor, pendingRuleBindings);
    }
    
    internal static void ConfigureComponentOptions(
        this IServiceCollection services,
        ComponentRegistration registration,
        IConfiguration configuration)
    {
        if (registration.OptionsType is null)
        {
            return;
        }

        var method = typeof(ServiceCollectionExtensions)
            .GetMethod(nameof(RegisterOptionsCore))!
            .MakeGenericMethod(registration.OptionsType);

        method.Invoke(null, [services, registration, configuration]);
    }

    public static void RegisterOptionsCore<TOptions>(
        IServiceCollection services,
        ComponentRegistration registration,
        IConfiguration configuration) where TOptions : class
    {
        if (registration.ConfigSectionPath is not null)
        {
            var section = configuration.GetSection(registration.ConfigSectionPath);
            services.Configure<TOptions>(registration.Name, section);
        }

        if (registration.ConfigureOptionsAction is not null)
        {
            var configure = registration.ConfigureOptionsAction;
            services.PostConfigure<TOptions>(registration.Name, options => configure.Invoke(options));
        }
    }
}
