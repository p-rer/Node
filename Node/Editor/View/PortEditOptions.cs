using System.Windows.Media;
using Node.Graph.Port;
using Node.Localize;
using Node.Nodes.Effect;
using Node.Nodes.Func;
using Node.Nodes.Generator.Outline;
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
    bool hasLimit,
    string namePrefix = "Arg_",
    string? kindLabel = null,
    Func<IPortKindEditor>? kindEditorFactory = null,
    Func<PortDefinition, string>? describeKind = null)
{
    public IReadOnlyList<PortTypeOption> Types { get; } = types;
    public string ListTitle { get; } = listTitle;
    public string ItemTitle { get; } = itemTitle;

    /// <summary>新しく追加する端子の内部名の接頭辞。</summary>
    public string NamePrefix { get; } = namePrefix;

    /// <summary>
    ///     種類設定部分のラベル。null の場合は「型」。
    /// </summary>
    public string? KindLabel { get; } = kindLabel;

    /// <summary>
    ///     種類設定部分を差し替えるエディタの生成処理。指定すると、型の選択の代わりにそのエディタが表示される。
    ///     ダイアログを開くたびに新しいエディタを作る。
    /// </summary>
    public Func<IPortKindEditor>? KindEditorFactory { get; } = kindEditorFactory;

    /// <summary>
    ///     端子の一覧で、型名の代わりに表示する種類の説明。null の場合は型名を表示する。
    /// </summary>
    public Func<PortDefinition, string>? DescribeKind { get; } = describeKind;

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

    /// <summary>Unicode 範囲スタイルノードの追加端子。1つの端子に複数の範囲を指定できる。</summary>
    public static PortEditOptions UnicodeRanges =>
        new([
                new PortTypeOption(typeof(UnicodeRangeStyle),
                    TextUi.UnicodeRangeStylePortName,
                    () => null)
            ],
            TextUi.EditUnicodeRangePortsTitle, TextUi.UnicodeRangePortDialogTitle, false,
            UnicodeTextStyleNode.PortNamePrefix,
            TextUi.UnicodeRangePortKindLabel,
            () => new UnicodeRangeKindEditor(),
            UnicodeRangePortSettings.Describe);

    /// <summary>品詞別文字スタイルノードの追加端子。1つの端子に複数の品詞を指定できる。</summary>
    public static PortEditOptions PartsOfSpeech =>
        new([
                new PortTypeOption(typeof(TextStyle),
                    TextUi.PartOfSpeechStylePortName,
                    () => null)
            ],
            TextUi.EditPartOfSpeechPortsTitle, TextUi.PartOfSpeechPortDialogTitle, false,
            PartOfSpeechTextStyleNode.PortNamePrefix,
            TextUi.PartOfSpeechPortKindLabel,
            () => new PartOfSpeechKindEditor(),
            PartOfSpeechPortSettings.Describe);

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