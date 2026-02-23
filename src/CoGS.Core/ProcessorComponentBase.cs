using System.Reflection;
using System.Threading.Channels;

namespace CoGS.Core;

public abstract class ProcessorComponentBase
{
    private readonly Channel<IEvent> _inputChannel;
    private readonly List<(ChannelWriter<IEvent> Writer, Func<IEvent, bool>? Rule)> _subscribers = [];
    private readonly Dictionary<Type, Func<IEvent, Task>> _inputTypeToProcessor = [];

    public string ComponentName { get; internal set; } = string.Empty;

    protected ProcessorComponentBase(ChannelProvider channelProvider)
    {
        _inputChannel = channelProvider.Provide(ComponentName);
        MapProcessors();
    }

    private void MapProcessors()
    {
        foreach (var method in GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (method.GetCustomAttribute<ProcessorAttribute>() is null)
            {
                continue;
            }

            var parameters = method.GetParameters();
            if (parameters.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Processor method '{method.Name}' must have exactly one parameter of type IEvent");
            }

            var paramType = parameters[0].ParameterType;
            if (!typeof(IEvent).IsAssignableFrom(paramType))
            {
                throw new InvalidOperationException(
                    $"Parameter of processor method '{method.Name}' must be assignable to IEvent");
            }

            // TODO: map the processor method
        }
    }

    public void Subscribe(ProcessorComponentBase source, Func<IEvent, bool>? rule = null)
    {
        source._subscribers.Add((_inputChannel.Writer, rule));
    }

    protected async Task PublishAsync(IEvent @event)
    {
        await Task.WhenAll(_subscribers
            .Where(s => s.Rule is null || s.Rule(@event))
            .Select(s => s.Writer.WriteAsync(@event).AsTask()));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await foreach (var @event in _inputChannel.Reader.ReadAllAsync(cancellationToken))
        {
            if (!_inputTypeToProcessor.TryGetValue(@event.GetType(), out var processor))
            {
                // TODO: Log warning
                continue;
            }

            await processor.Invoke(@event);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _inputChannel.Writer.TryComplete();
        return Task.CompletedTask;
    }
}
