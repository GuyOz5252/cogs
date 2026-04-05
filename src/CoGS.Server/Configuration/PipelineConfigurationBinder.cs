using System.Reflection;
using CoGS.Core;
using CoGS.Core.Rules;
using Microsoft.Extensions.Configuration;

namespace CoGS.Server.Configuration;

internal sealed class PipelineConfigurationBinder
{
    private readonly IReadOnlyDictionary<string, Type> _componentTypes;

    public PipelineConfigurationBinder(IReadOnlyDictionary<string, Type> componentTypes)
    {
        _componentTypes = componentTypes;
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

            var subscriptions = ParseSubscriptions(componentSection.GetSection("SubscribesTo"));
            var optionsType = GetOptionsType(componentType);

            var registration = new ComponentRegistration
            {
                Name = name,
                ComponentType = componentType,
                Subscriptions = subscriptions,
                OptionsType = optionsType,
                ConfigSectionPath = $"{sectionName}:Components:{name}",
            };

            descriptor.AddComponent(registration);
        }
    }

    private static Type? GetOptionsType(Type componentType)
    {
        var attribute = componentType.GetCustomAttribute<CogsComponentAttribute>();
        return attribute?.OptionsType;
    }

    private List<Subscription> ParseSubscriptions(IConfigurationSection section)
    {
        var subscriptions = new List<Subscription>();

        foreach (var child in section.GetChildren())
        {
            if (child.Value is not null)
            {
                subscriptions.Add(new Subscription(child.Value));
            }
            else
            {
                var componentName = child["Component"]
                    ?? throw new InvalidOperationException(
                        "Subscription object must have a 'Component' property.");

                var ruleExpression = child["Rule"];
                var rule = ruleExpression is not null ? ParseRule(ruleExpression) : null;
                subscriptions.Add(new Subscription(componentName, rule));
            }
        }

        return subscriptions;
    }

    private IRule ParseRule(string expression)
    {
        if (string.Equals(expression, "Always", StringComparison.OrdinalIgnoreCase))
        {
            return new AlwaysPassRule();
        }

        if (expression.StartsWith("EventType:", StringComparison.OrdinalIgnoreCase))
        {
            var typeName = expression["EventType:".Length..];
            var eventType = ResolveEventType(typeName);
            var ruleType = typeof(EventTypeRule<>).MakeGenericType(eventType);
            return (IRule)Activator.CreateInstance(ruleType)!;
        }

        throw new InvalidOperationException(
            $"Unknown rule expression '{expression}'. Supported formats: 'Always', 'EventType:<TypeName>'.");
    }

    private Type ResolveEventType(string typeName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetExportedTypes();
            }
            catch (ReflectionTypeLoadException)
            {
                continue;
            }

            foreach (var type in types)
            {
                if (type.IsAssignableTo(typeof(IEvent))
                    && (string.Equals(type.Name, typeName, StringComparison.Ordinal)
                        || string.Equals(type.FullName, typeName, StringComparison.Ordinal)))
                {
                    return type;
                }
            }
        }

        throw new InvalidOperationException(
            $"Could not resolve event type '{typeName}'. Ensure the type implements IEvent and is in a loaded assembly.");
    }
}
