namespace CoGS.Core.Pipeline;

public class ComponentRegistration
{
    public List<SubscriptionDescriptor> SubscriptionDescriptors { get; } = [];
    
    public required string Name { get; init; }
    public required Type ComponentType { get; init; }
    
    public string? ConfigSection { get; init; }
    public Type? OptionsType { get; init; }
}
