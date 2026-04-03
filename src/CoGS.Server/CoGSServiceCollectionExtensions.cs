using System;
using System.Collections.Generic;
using CoGS.Core;
using Microsoft.Extensions.DependencyInjection;

namespace CoGS.Server;

public static class CoGSServiceCollectionExtensions
{
    /// <summary>
    /// Registers one or more pipelines using the fluent DSL and adds the
    /// background hosted service that activates, wires, and runs them.
    /// </summary>
    public static IServiceCollection AddCoGS(
        this IServiceCollection services,
        Action<PipelinesBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new PipelinesBuilder();
        configure(builder);

        var definitions = builder.Build();
        services.AddSingleton<IReadOnlyList<PipelineDefinition>>(definitions);
        services.AddHostedService<PipelineRunnerHostedService>();

        return services;
    }
}
