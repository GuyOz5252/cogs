namespace CoGS.Core;

/// <summary>
/// Context information provided to each component instance by the pipeline host.
/// </summary>
public interface IPipelineComponentContext
{
    /// <summary>
    /// Logical name of this component instance, unique within a single pipeline.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Name of the pipeline this component instance belongs to.
    /// </summary>
    string PipelineName { get; }
}

