using System.Reflection;
using CoGS.Core;
using CoGS.Core.Abstract;
using CoGS.Core.Attributes;
using CoGS.Server.Configuration;
using CoGS.Server.Extensions;
using CoGS.Server.Registrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoGS.Server.Builders;

public sealed class CogsServerBuilder
{
    private readonly IServiceCollection _services;
    private readonly IConfiguration _configuration;
    private readonly PipelineRegistration _registration;

    internal CogsServerBuilder(
        IServiceCollection services,
        IConfiguration configuration,
        PipelineRegistration registration)
    {
        _services = services;
        _configuration = configuration;
        _registration = registration;
    }

    public void FromConfiguration(string sectionName = "CoGS")
    {
        var binder = new PipelineConfigurationBinder(ScanComponentTypes());
        binder.Bind(_registration, _configuration, sectionName);
        RegisterComponents();
    }

    public void FromPipeline(Action<PipelineBuilder> configure, string sectionName = "CoGS")
    {
        var pipelineBuilder = new PipelineBuilder(_registration);
        configure.Invoke(pipelineBuilder);

        foreach (var registration in _registration.Components)
        {
            registration.ConfigSectionPath ??= $"{sectionName}:Components:{registration.Name}";
        }

        RegisterComponents();
    }

    public void FromPipeline<TDefinition>(string sectionName = "CoGS")
        where TDefinition : IPipelineDefinition, new()
    {
        var definition = new TDefinition();
        FromPipeline(definition.Define, sectionName);
    }

    private void RegisterComponents()
    {
        foreach (var registration in _registration.Components)
        {
            _services.ConfigureComponentOptions(registration, _configuration);
            _services.AddKeyedSingleton<ComponentBase>(registration.Name, (serviceProvider, _) =>
            {
                var component = (ComponentBase)ActivatorUtilities.CreateInstance(
                    serviceProvider, registration.ComponentType);
                component.Name = registration.Name;
                return component;
            });
        }
    }

    private static Dictionary<string, Type> ScanComponentTypes()
    {
        var types = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] exportedTypes;
            try
            {
                exportedTypes = assembly.GetExportedTypes();
            }
            catch (ReflectionTypeLoadException)
            {
                continue;
            }

            foreach (var type in exportedTypes)
            {
                if (type is { IsAbstract: false, IsClass: true }
                    && type.IsAssignableTo(typeof(ComponentBase)))
                {
                    var attribute = type.GetCustomAttribute<CogsComponentAttribute>()
                                    ?? throw new InvalidOperationException(
                                        $"Component: '{type}' must be decorated with CogsComponentAttribute");
                    types[attribute.Name] = type;
                }
            }
        }

        return types;
    }
}
