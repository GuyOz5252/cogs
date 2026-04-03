using CoGS.Core;

namespace CoGS.Sample.Events;

public sealed record OrderEvent(string OrderId, decimal Amount, string Region) : IEvent;
