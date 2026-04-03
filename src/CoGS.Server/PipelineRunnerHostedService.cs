using System.Reflection;
using System.Threading.Channels;
using CoGS.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoGS.Server;

/// <summary>
/// Hosted service that activates pipeline components, wires subscriptions
/// via channels, and manages component lifecycle.
/// </summary>
internal sealed class PipelineRunnerHostedService : IHostedService, IDisposable
{
    private readonly IServiceProvider _services;
    private readonly IReadOnlyList<PipelineDefinition> _pipelines;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PipelineRunnerHostedService> _logger;

    private readonly List<ComponentBase> _activeComponents = new();
    private readonly List<Task> _backgroundTasks = new();
    private CancellationTokenSource? _cts;

    public PipelineRunnerHostedService(
        IServiceProvider services,
        IReadOnlyList<PipelineDefinition> pipelines,
        IConfiguration configuration,
        ILogger<PipelineRunnerHostedService> logger)
    {
        _services = services;
        _pipelines = pipelines;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        foreach (var pipeline in _pipelines)
        {
            _logger.LogInformation(
                "Starting pipeline '{PipelineName}' with {ComponentCount} component(s) and {SubscriptionCount} subscription(s)",
                pipeline.Name, pipeline.Components.Count, pipeline.Subscriptions.Count);

            ValidatePipeline(pipeline);

            var byName = new Dictionary<string, ComponentBase>(StringComparer.OrdinalIgnoreCase);
            var channels = new Dictionary<string, Channel<IEvent>>(StringComparer.OrdinalIgnoreCase);

            // 1. Activate all components and create their input channels.
            foreach (var comp in pipeline.Components)
            {
                var instance = ActivateComponent(pipeline.Name, comp);
                byName[comp.Name] = instance;
                channels[comp.Name] = Channel.CreateUnbounded<IEvent>();
                _activeComponents.Add(instance);
            }

            // 2. Wire dispatchers based on subscriptions.
            foreach (var comp in pipeline.Components)
            {
                var publisher = byName[comp.Name];

                var outboundSubs = pipeline.Subscriptions
                    .Where(s => s.PublisherComponentName == comp.Name)
                    .Select(s => (channel: channels[s.SubscriberComponentName], rule: s.Rule))
                    .ToList();

                publisher.SetDispatcher(async (evt, ct) =>
                {
                    foreach (var (ch, rule) in outboundSubs)
                    {
                        if (rule is null || rule(evt))
                        {
                            await ch.Writer.WriteAsync(evt, ct);
                        }
                    }
                });
            }

            // 3. Start channel reader loops (delivers events to HandleAsync).
            foreach (var comp in pipeline.Components)
            {
                var instance = byName[comp.Name];
                var reader = channels[comp.Name].Reader;
                var linkedToken = _cts.Token;

                _backgroundTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await foreach (var evt in reader.ReadAllAsync(linkedToken))
                        {
                            await instance.HandleAsync(evt, linkedToken);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected during shutdown.
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Unhandled exception in channel reader for component '{ComponentName}' in pipeline '{PipelineName}'",
                            comp.Name, pipeline.Name);
                    }
                }, linkedToken));
            }

            // 4. Call StartAsync on each component (declaration order).
            foreach (var comp in pipeline.Components)
            {
                await byName[comp.Name].StartAsync(cancellationToken);
                _logger.LogInformation("Component '{ComponentName}' started in pipeline '{PipelineName}'",
                    comp.Name, pipeline.Name);
            }

            // 5. Fire off ExecuteAsync for long-running components (e.g. Kafka sources).
            foreach (var comp in pipeline.Components)
            {
                var instance = byName[comp.Name];
                var linkedToken = _cts.Token;

                _backgroundTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await instance.ExecuteAsync(linkedToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected during shutdown.
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Unhandled exception in ExecuteAsync for component '{ComponentName}' in pipeline '{PipelineName}'",
                            comp.Name, pipeline.Name);
                    }
                }, linkedToken));
            }
        }

        _logger.LogInformation("All pipelines started");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping all pipelines");

        if (_cts is not null)
        {
            await _cts.CancelAsync();
        }

        // Stop components in reverse declaration order so sinks drain before sources stop.
        for (var i = _activeComponents.Count - 1; i >= 0; i--)
        {
            try
            {
                await _activeComponents[i].StopAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping component '{ComponentName}'", _activeComponents[i].Name);
            }
        }

        // Wait for background tasks to finish.
        await Task.WhenAll(_backgroundTasks).WaitAsync(cancellationToken);

        _activeComponents.Clear();
        _backgroundTasks.Clear();

        _logger.LogInformation("All pipelines stopped");
    }

    public void Dispose()
    {
        _cts?.Dispose();
    }

    // ──────────────────────────────────────────────────────────────────
    //  Component activation with per-instance IOptions<T> binding
    // ──────────────────────────────────────────────────────────────────

    private ComponentBase ActivateComponent(string pipelineName, ComponentDefinition comp)
    {
        var context = new PipelineComponentContext(comp.Name, pipelineName);
        var extraParams = new List<object> { context };

        if (comp.OptionsKey is not null)
        {
            var optionsType = DiscoverOptionsType(comp.ComponentType);
            if (optionsType is not null)
            {
                var section = _configuration.GetSection(comp.OptionsKey);
                var optionsInstance = section.Get(optionsType) ?? Activator.CreateInstance(optionsType)!;

                var wrapped = WrapInIOptions(optionsType, optionsInstance);
                extraParams.Add(wrapped);
            }
        }

        return (ComponentBase)ActivatorUtilities.CreateInstance(
            _services,
            comp.ComponentType,
            extraParams.ToArray());
    }

    /// <summary>
    /// Inspects the component constructor to find <c>IOptions&lt;T&gt;</c> and returns <c>T</c>.
    /// </summary>
    private static Type? DiscoverOptionsType(Type componentType)
    {
        var ctor = componentType.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault();

        if (ctor is null)
        {
            return null;
        }

        foreach (var param in ctor.GetParameters())
        {
            var t = param.ParameterType;
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IOptions<>))
            {
                return t.GetGenericArguments()[0];
            }
        }

        return null;
    }

    /// <summary>
    /// Calls <c>Options.Create&lt;T&gt;(value)</c> via reflection so the result is assignable to
    /// <c>IOptions&lt;T&gt;</c>, which <see cref="ActivatorUtilities"/> can match to constructor params.
    /// </summary>
    private static object WrapInIOptions(Type optionsType, object optionsInstance)
    {
        var createMethod = typeof(Options)
            .GetMethod(nameof(Options.Create), BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(optionsType);

        return createMethod.Invoke(null, [optionsInstance])!;
    }

    // ──────────────────────────────────────────────────────────────────
    //  Validation
    // ──────────────────────────────────────────────────────────────────

    private void ValidatePipeline(PipelineDefinition pipeline)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var comp in pipeline.Components)
        {
            if (!names.Add(comp.Name))
            {
                throw new InvalidOperationException(
                    $"Pipeline '{pipeline.Name}' has duplicate component name '{comp.Name}'.");
            }
        }

        foreach (var sub in pipeline.Subscriptions)
        {
            if (!names.Contains(sub.PublisherComponentName))
            {
                throw new InvalidOperationException(
                    $"Pipeline '{pipeline.Name}': subscription references unknown publisher '{sub.PublisherComponentName}'.");
            }

            if (!names.Contains(sub.SubscriberComponentName))
            {
                throw new InvalidOperationException(
                    $"Pipeline '{pipeline.Name}': subscription references unknown subscriber '{sub.SubscriberComponentName}'.");
            }
        }
    }
}
