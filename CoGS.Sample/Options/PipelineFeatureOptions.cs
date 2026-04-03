namespace CoGS.Sample.Options;

/// <summary>
/// Feature flags that control which pipeline branches are active.
/// Bound from the "PipelineFeatures" section of appsettings.json.
/// </summary>
public sealed class PipelineFeatureOptions
{
    public bool EnableHighValueDispatcher { get; set; } = true;
    public bool EnableAuditDispatcher { get; set; }
}
