namespace CoGS.Core;

public sealed class ComponentRegistration
{
    public required string Name { get; init; }

    public required Type ComponentType { get; init; }

    public List<Subscription> Subscriptions { get; init; } = [];

    public Type? OptionsType { get; internal set; }

    public string? ConfigSectionPath { get; internal set; }

    public Action<object>? ConfigureOptionsAction { get; internal set; }
}
