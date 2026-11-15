using Node.Editor.Control;
using Node.Nodes.Generator.Outline;

namespace Node.Editor.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public sealed class TextStylePortControlAttribute : PropertyControlBaseAttribute
{
    public override Type ControlType => typeof(TextStylePort);

    public override object GetDefaultValue()
    {
        return new TextStyle();
    }
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class UnicodeRangeStylePortControlAttribute : PropertyControlBaseAttribute
{
    public override Type ControlType => typeof(UnicodeRangeStylePort);

    public override object GetDefaultValue()
    {
        return new UnicodeRangeStyle();
    }
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class FontWeightPortControlAttribute : PropertyControlBaseAttribute
{
    public override Type ControlType => typeof(FontWeightPort);
    public override object GetDefaultValue() => 400;
}