using System.Reflection;
using CoGS.Core;
using Microsoft.Extensions.Configuration;

namespace CoGS.Server.Configuration;

internal sealed class PipelineConfigurationBinder
{
    private readonly IReadOnlyDictionary<string, Type> _componentTypes;
    private readonly List<PendingRuleBinding> _pendingRuleBindings;

    public PipelineConfigurationBinder(
        IReadOnlyDictionary<string, Type> componentTypes,
        List<PendingRuleBinding> pendingRuleBindings)
    {
        _componentTypes = componentTypes;
        _pendingRuleBindings = pendingRuleBindings;
    }

    public void Bind(PipelineDescriptor descriptor, IConfiguration configuration, string sectionName)
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

            ParseSubscriptions(componentSection.GetSection("SubscribesTo"), registration);
            descriptor.AddComponent(registration);
        }
    }

    private static Type? GetOptionsType(Type componentType)
    {
        var attribute = componentType.GetCustomAttribute<CogsComponentAttribute>()
                        ?? throw new InvalidOperationException(
                            $"Component: '{componentType}' must be decorated with CogsComponentAttribute");
        return attribute.OptionsType;
    }

    private void ParseSubscriptions(IConfigurationSection section, ComponentRegistration registration)
    {
        foreach (var child in section.GetChildren())
        {
            if (child.Value is not null)
            {
                registration.Subscriptions.Add(new Subscription(child.Value));
            }
            else
            {
                var componentName = child["Component"]
                                    ?? throw new InvalidOperationException(
                                        "Subscription object must have a 'Component' property.");

                var ruleSection = child.GetSection("Rule");
                if (ruleSection.Exists())
                {
                    _pendingRuleBindings.Add(new PendingRuleBinding(
                        registration, registration.Subscriptions.Count, ruleSection.Path));
                }

                registration.Subscriptions.Add(new Subscription(componentName));
            }
        }
    }
}
