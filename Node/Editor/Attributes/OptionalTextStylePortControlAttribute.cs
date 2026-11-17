using Node.Editor.Control;

namespace Node.Editor.Attributes;

/// <summary>
///     TextStylePort を使用しつつ、未編集時の入力値を null のまま保持する。
///     品詞別スタイルの未指定カテゴリがベーススタイルを上書きしないようにする。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class OptionalTextStylePortControlAttribute : PropertyControlBaseAttribute
{
    public override Type ControlType => typeof(TextStylePort);

    public override object GetDefaultValue()
    {
        return null!;
    }
}