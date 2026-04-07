namespace CoGS.Core.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CogsComponentAttribute(string name) : Attribute
{
    public string Name { get; } = name;
    public Type? OptionsType { get; set; }
}
