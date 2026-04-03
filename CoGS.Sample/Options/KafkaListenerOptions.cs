namespace CoGS.Sample.Options;

public sealed class KafkaListenerOptions
{
    public string Topic { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string BootstrapServers { get; set; } = string.Empty;
}
