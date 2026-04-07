using CoGS.Core;
using CoGS.Core.Abstract;
using CoGS.Server.Registrations;

namespace CoGS.Server.Builders;

public sealed class PipelineBuilder
{
    private readonly PipelineRegistration _registration;

    internal PipelineBuilder(PipelineRegistration registration)
    {
        _registration = registration;
    }

    public ComponentBuilder AddComponent<TComponent>(string name) where TComponent : ComponentBase
    {
        var registration = new ComponentRegistration
        {
            Name = name,
            ComponentType = typeof(TComponent)
        };
        _registration.AddComponent(registration);
        return new ComponentBuilder(registration);
    }
}
