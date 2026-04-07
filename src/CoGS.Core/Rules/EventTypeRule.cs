using System.Reflection;

namespace CoGS.Core.Rules;

[CogsType("EventType")]
public sealed class EventTypeRule : IRule
{
    private Type? _resolvedType;

    public string EventTypeName { get; set; } = "";

    public bool Evaluate(IEvent @event)
    {
        _resolvedType ??= ResolveEventType(EventTypeName);
        return _resolvedType.IsInstanceOfType(@event);
    }

    private static Type ResolveEventType(string typeName)
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

public sealed class EventTypeRule<TEvent> : IRule where TEvent : IEvent
{
    public bool Evaluate(IEvent @event)
    {
        return @event is TEvent;
    }
}
