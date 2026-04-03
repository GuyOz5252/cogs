namespace CoGS.Sample.Options;

public sealed class KafkaDispatcherOptions
{
    public string Topic { get; set; } = string.Empty;
    public string BootstrapServers { get; set; } = string.Empty;
}
