using Node.Editor.Control;

namespace Node.Editor.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public sealed class TextStylePortControlAttribute : PropertyControlBaseAttribute
{
    public override Type ControlType => typeof(TextStylePort);
    public override object GetDefaultValue() => new Node.Nodes.Generator.Outline.TextStyle();
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class UnicodeRangeStylePortControlAttribute : PropertyControlBaseAttribute
{
    public override Type ControlType => typeof(UnicodeRangeStylePort);
    public override object GetDefaultValue() => new Node.Nodes.Generator.Outline.UnicodeRangeStyle();
}


[AttributeUsage(AttributeTargets.Property)]
public sealed class FontWeightPortControlAttribute : PropertyControlBaseAttribute
{
    public override Type ControlType => typeof(FontWeightPort);
    public override object GetDefaultValue() => 400;
}