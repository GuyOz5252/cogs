using System;
using System.Collections.Generic;

namespace CoGS.Core;

/// <summary>
/// Builder for a single named pipeline.
/// </summary>
public sealed class SinglePipelineBuilder
{
    private readonly string _name;
    private readonly List<ComponentDefinition> _components = new();
    private readonly List<SubscriptionDefinition> _subscriptions = new();

    public SinglePipelineBuilder(string name)
    {
        _name = name;
    }

    public ComponentBuilder AddComponent<TComponent>(
        string componentName,
        string? optionsKey = null,
        ComponentRole role = ComponentRole.Processor)
        where TComponent : ComponentBase
    {
        if (string.IsNullOrWhiteSpace(componentName))
        {
            throw new ArgumentException("Component name is required.", nameof(componentName));
        }

        var def = new ComponentDefinition(componentName, typeof(TComponent), optionsKey, role);
        _components.Add(def);

        return new ComponentBuilder(this, def);
    }

    internal void AddSubscription(string subscriberName, string publisherName, Func<IEvent, bool>? rule)
    {
        _subscriptions.Add(new SubscriptionDefinition(subscriberName, publisherName, rule));
    }

    internal PipelineDefinition Build()
        => new(_name, _components.AsReadOnly(), _subscriptions.AsReadOnly());
}
