using System.Reflection;
using CoGS.Core.Attributes;
using CoGS.Server.Registrations;
using Microsoft.Extensions.Configuration;

namespace CoGS.Server.Configuration;

internal sealed class PipelineConfigurationBinder
{
    private readonly IReadOnlyDictionary<string, Type> _componentTypes;

    public PipelineConfigurationBinder(
        IReadOnlyDictionary<string, Type> componentTypes)
    {
        _componentTypes = componentTypes;
    }

    public void Bind(PipelineRegistration pipelineRegistration, IConfiguration configuration, string sectionName)
    {
        var section = configuration.GetSection($"{sectionName}:Components");

        foreach (var componentSection in section.GetChildren())
        {
            var name = componentSection.Key;
            var typeName = componentSection["Type"]
                           ?? throw new InvalidOperationException(
                               $"Component '{name}' is missing the required 'Type' property.");

            if (!_componentTypes.TryGetValue(typeName, out var componentType))
            {
                throw new InvalidOperationException(
                    $"Unknown component type '{typeName}'. Registered types: [{string.Join(", ", _componentTypes.Keys)}].");
            }

            var optionsType = GetOptionsType(componentType);

            var registration = new ComponentRegistration
            {
                Name = name,
                ComponentType = componentType,
                OptionsType = optionsType,
                ConfigSectionPath = $"{sectionName}:Components:{name}",
            };
            
            pipelineRegistration.AddComponent(registration);
        }
    }

    private static Type? GetOptionsType(Type componentType)
    {
        var attribute = componentType.GetCustomAttribute<CogsComponentAttribute>()
                        ?? throw new InvalidOperationException(
                            $"Component: '{componentType}' must be decorated with CogsComponentAttribute");
        return attribute.OptionsType;
    }
}
