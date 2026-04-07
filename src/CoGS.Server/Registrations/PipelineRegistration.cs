namespace CoGS.Server.Registrations;

public sealed class PipelineRegistration
{
    private readonly List<ComponentRegistration> _components = [];

    public IReadOnlyList<ComponentRegistration> Components => _components;

    public void AddComponent(ComponentRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        _components.Add(registration);
    }
}
