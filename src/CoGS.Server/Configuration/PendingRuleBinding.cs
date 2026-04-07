using CoGS.Core;

namespace CoGS.Server.Configuration;

internal sealed record PendingRuleBinding(
    ComponentRegistration Registration,
    int SubscriptionIndex,
    string ConfigPath);
