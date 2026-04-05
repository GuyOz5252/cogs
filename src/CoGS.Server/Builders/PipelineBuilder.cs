namespace CoGS.Server.Builders;

public sealed class PipelineBuilder
{
    private readonly PipelineDescriptor _descriptor;

    internal PipelineBuilder(PipelineDescriptor descriptor)
    {
        _descriptor = descriptor;
    }

    public ComponentBuilder AddComponent<TComponent>(string name) where TComponent : ComponentBase
    {
        var registration = new ComponentRegistration
        {
            Name = name,
            ComponentType = typeof(TComponent)
        };
        _descriptor.AddComponent(registration);
        return new ComponentBuilder(registration);
    }
}
