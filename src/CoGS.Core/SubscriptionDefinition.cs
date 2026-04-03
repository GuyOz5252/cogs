using System;

namespace CoGS.Core;

/// <summary>
/// Describes a subscription from one component to another, with an optional rule.
/// </summary>
public sealed class SubscriptionDefinition
{
    public SubscriptionDefinition(
        string subscriberComponentName,
        string publisherComponentName,
        Func<IEvent, bool>? rule = null)
    {
        SubscriberComponentName = subscriberComponentName ?? throw new ArgumentNullException(nameof(subscriberComponentName));
        PublisherComponentName = publisherComponentName ?? throw new ArgumentNullException(nameof(publisherComponentName));
        Rule = rule;
    }

    /// <summary>
    /// Name of the downstream component that subscribes to events.
    /// </summary>
    public string SubscriberComponentName { get; }

    /// <summary>
    /// Name of the upstream component publishing events.
    /// </summary>
    public string PublisherComponentName { get; }

    /// <summary>
    /// Optional rule applied to events travelling along this subscription.
    /// Null means all events pass through.
    /// </summary>
    public Func<IEvent, bool>? Rule { get; }
}
