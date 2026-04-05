namespace CoGS.Core;

public sealed record Subscription(string SourceComponentName, IRule? Rule = null);
