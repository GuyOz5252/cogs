using CoGS.Core;

namespace CoGS.Server;

/// <summary>
/// Concrete implementation of <see cref="IPipelineComponentContext"/> created
/// by the pipeline host for each component instance.
/// </summary>
internal sealed class PipelineComponentContext : IPipelineComponentContext
{
    public PipelineComponentContext(string name, string pipelineName)
    {
        Name = name;
        PipelineName = pipelineName;
    }

    public string Name { get; }

    public string PipelineName { get; }
}
