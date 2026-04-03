using System;

namespace CoGS.Core;

/// <summary>
/// Fluent builder scoped to a specific component within a pipeline.
/// </summary>
public sealed class ComponentBuilder
{
    private readonly SinglePipelineBuilder _pipeline;
    private readonly ComponentDefinition _component;

    internal ComponentBuilder(SinglePipelineBuilder pipeline, ComponentDefinition component)
    {
        _pipeline = pipeline;
        _component = component;
    }

    /// <summary>
    /// Subscribe this component to another component by name, with an optional precompiled rule.
    /// </summary>
    public ComponentBuilder SubscribeTo(string publisherComponentName, Func<IEvent, bool>? rule = null)
    {
        _pipeline.AddSubscription(_component.Name, publisherComponentName, rule);
        return this;
    }

    /// <summary>
    /// Subscribe this component to another component by name, with a rule built using <see cref="RuleBuilder" />.
    /// </summary>
    public ComponentBuilder SubscribeTo(string publisherComponentName, Action<RuleBuilder> configureRule)
    {
        ArgumentNullException.ThrowIfNull(configureRule);

        var builder = new RuleBuilder();
        configureRule(builder);
        var compiled = builder.Build();

        _pipeline.AddSubscription(_component.Name, publisherComponentName, compiled);
        return this;
    }
}
