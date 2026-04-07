using CoGS.Core;
using CoGS.Core.Abstract;
using CoGS.Server.Builders;
using CoGS.Server.Configuration;
using CoGS.Server.HostedServices;
using CoGS.Server.Registrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoGS.Server.Extensions;

public static class ServiceCollectionExtensions
{
    public static CogsServerBuilder AddCogs(this IServiceCollection services, IConfiguration configuration)
    {
        var descriptor = new PipelineRegistration();
        
        services.AddSingleton<EventRouter>();
        services.AddHostedService<CogsHostedService>();

        return new CogsServerBuilder(services, configuration, descriptor);
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
