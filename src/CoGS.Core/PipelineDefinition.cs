using System;
using System.Collections.Generic;

namespace CoGS.Core;

/// <summary>
/// Immutable description of a single pipeline: its name, components and subscriptions.
/// This is an internal model consumed by the hosting layer and builders.
/// </summary>
public sealed class PipelineDefinition
{
    public PipelineDefinition(
        string name,
        IReadOnlyList<ComponentDefinition> components,
        IReadOnlyList<SubscriptionDefinition> subscriptions)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Components = components ?? throw new ArgumentNullException(nameof(components));
        Subscriptions = subscriptions ?? throw new ArgumentNullException(nameof(subscriptions));
    }

    public string Name { get; }

    public IReadOnlyList<ComponentDefinition> Components { get; }

    public IReadOnlyList<SubscriptionDefinition> Subscriptions { get; }
}


