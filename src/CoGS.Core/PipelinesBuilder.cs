using System;
using System.Collections.Generic;

namespace CoGS.Core;

/// <summary>
/// Root builder for defining one or more pipelines.
/// </summary>
public sealed class PipelinesBuilder
{
    private readonly List<PipelineDefinition> _pipelines = new();

    public PipelinesBuilder AddPipeline(string name, Action<SinglePipelineBuilder> configure)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Pipeline name is required.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(configure);

        var builder = new SinglePipelineBuilder(name);
        configure(builder);
        _pipelines.Add(builder.Build());

        return this;
    }

    public IReadOnlyList<PipelineDefinition> Build() => _pipelines;
}
