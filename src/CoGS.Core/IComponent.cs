namespace CoGS.Core;

public interface IComponent
{
    string Name { get; }

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    Task HandleEventAsync(IEvent @event, CancellationToken cancellationToken);
}
