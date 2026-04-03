using System;

namespace CoGS.Core;

/// <summary>
/// Describes a single component instance in a pipeline.
/// </summary>
public sealed class ComponentDefinition
{
    public ComponentDefinition(
        string name,
        Type componentType,
        string? optionsKey,
        ComponentRole role)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        ComponentType = componentType ?? throw new ArgumentNullException(nameof(componentType));
        OptionsKey = optionsKey;
        Role = role;
    }

    /// <summary>
    /// Logical name of this component instance, unique within its pipeline.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Concrete CLR type of the component (must derive from <see cref="ComponentBase" />).
    /// </summary>
    public Type ComponentType { get; }

    /// <summary>
    /// Configuration section path for per-instance options binding.
    /// Null if the component has no external options.
    /// </summary>
    public string? OptionsKey { get; }

    /// <summary>
    /// Logical role of this component in the pipeline (source/processor/sink).
    /// </summary>
    public ComponentRole Role { get; }
}
