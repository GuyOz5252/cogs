using Microsoft.Extensions.Hosting;

namespace CoGS.Server;

public class CogsBackgroundService : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        throw new NotImplementedException();
    }
}
