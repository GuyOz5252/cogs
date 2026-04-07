namespace CoGS.Core;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CogsTypeAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
