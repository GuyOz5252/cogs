namespace CoGS.Core;

public sealed record EventMetadata
{
    public required string SourceComponent { get; init; }

    public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N");

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
