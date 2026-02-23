namespace CoGS.Core.Pipeline;

public class SubscriptionDescriptor
{
    public string SourceComponentName { get; init; }
    public Func<IEvent, bool>? Rule { get; init; }

    public SubscriptionDescriptor(string sourceComponentName, Func<IEvent, bool>? rule = null)
    {
        SourceComponentName = sourceComponentName;
        Rule = rule;
    }
}
