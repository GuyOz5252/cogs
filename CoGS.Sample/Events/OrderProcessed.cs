using CoGS.Core;
using CoGS.Core.Abstract;

namespace CoGS.Sample.Events;

public sealed record OrderProcessed : IEvent
{
    public required EventMetadata Metadata { get; init; }

    public required string OrderId { get; init; }

    public required string Status { get; init; }
}
