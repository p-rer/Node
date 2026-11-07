using System.Windows.Media;
using Node.Localize;
using Node.Nodes.Effect;
using Node.Nodes.Func;
using Node.ValueTypes;

namespace Node.Editor.View;

/// <summary>
///     端子の編集ダイアログで選べる型の1項目。
/// </summary>
public sealed record PortTypeOption(Type ValueType, string Name, Func<object?> CreateDefault);

/// <summary>
///     端子の編集ダイアログに渡す、選べる型・タイトル・数の上限の有無。
/// </summary>
public sealed class PortEditOptions(
    IReadOnlyList<PortTypeOption> types,
    string listTitle,
    string itemTitle,
    bool hasLimit)
{
    public IReadOnlyList<PortTypeOption> Types { get; } = types;
    public string ListTitle { get; } = listTitle;
    public string ItemTitle { get; } = itemTitle;

    /// <summary>
    ///     数の上限（NodeEffect の引数スロット数）を適用するか。
    /// </summary>
    public bool HasLimit { get; } = hasLimit;

    /// <summary>引数ノードの引数（数値・真偽値・色・文字列。上限あり）。</summary>
    public static PortEditOptions Arguments =>
        new(ArgumentPortKinds.All.Select(FromKind).ToList(), TextUi.EditArgumentPortsTitle,
            TextUi.ArgumentPortDialogTitle, true);

    /// <summary>文字列結合ノードの文字列端子（文字列のみ）。</summary>
    public static PortEditOptions Text =>
        new([FromKind(ArgumentPortKind.Text)], TextUi.EditStringPortsTitle, TextUi.StringPortDialogTitle, false);

    /// <summary>Node エフェクトノードの入力端子。</summary>
    public static PortEditOptions SubGraphInput =>
        new(SubGraphTypes(), TextUi.EditInputPortsTitle, TextUi.InputPortDialogTitle, false);

    /// <summary>Node エフェクトノードの出力端子。</summary>
    public static PortEditOptions SubGraphOutput =>
        new(SubGraphTypes(), TextUi.EditOutputPortsTitle, TextUi.OutputPortDialogTitle, false);

    private static PortTypeOption FromKind(ArgumentPortKind kind)
    {
        return new PortTypeOption(ArgumentPortKinds.GetPortType(kind), ArgumentPortKinds.GetDisplayName(kind),
            () => ArgumentPortKinds.GetDefaultStorageValue(kind));
    }

    private static List<PortTypeOption> SubGraphTypes()
    {
        var names = new Dictionary<Type, string>
        {
            [typeof(ImageWrapper)] = TextUi.ArgumentTypeImage,
            [typeof(float)] = TextUi.ArgumentTypeNumber,
            [typeof(int)] = TextUi.ArgumentTypeInteger,
            [typeof(bool)] = TextUi.ArgumentTypeBool,
            [typeof(Color)] = TextUi.ArgumentTypeColor,
            [typeof(string)] = TextUi.ArgumentTypeText,
            [typeof(MaskWrapper)] = TextUi.ArgumentTypeMask,
            [typeof(OutlineWrapper)] = TextUi.ArgumentTypeOutline,
            [typeof(BrushWrapper)] = TextUi.ArgumentTypeBrush
        };

        return NodeEffectNode.SupportedPortTypes
            .Select(type => new PortTypeOption(type, names[type], () => CreateDefault(type)))
            .ToList();
    }

    private static object? CreateDefault(Type type)
    {
        if (type == typeof(float)) return ArgumentPortKinds.GetDefaultStorageValue(ArgumentPortKind.Number);
        if (type == typeof(bool)) return ArgumentPortKinds.GetDefaultStorageValue(ArgumentPortKind.Bool);
        if (type == typeof(Color)) return ArgumentPortKinds.GetDefaultStorageValue(ArgumentPortKind.Color);
        if (type == typeof(string)) return ArgumentPortKinds.GetDefaultStorageValue(ArgumentPortKind.Text);
        if (type == typeof(int)) return 0;
        return null;
    }
}