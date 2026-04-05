namespace CoGS.Sample.Options;

public sealed class KafkaListenerOptions
{
    public string Topic { get; set; } = string.Empty;

    public string ConsumerGroup { get; set; } = string.Empty;

    public int ProduceIntervalMs { get; set; } = 2000;
}
