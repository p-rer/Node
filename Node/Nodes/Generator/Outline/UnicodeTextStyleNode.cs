using System.Windows.Media;
using Node.Editor.Attributes;
using Node.Graph;
using Node.Graph.Attributes;
using Node.Graph.Port;
using Node.Localize;

namespace Node.Nodes.Generator.Outline;

[Node(typeof(OutlineCategory), nameof(TextNode.UnicodeTextStyleNode), nameof(TextNode.UnicodeTextStyleNodeDescription),
    typeof(TextNode))]
public sealed class UnicodeTextStyleNode : NodeLogic, IPortDefinitionNode
{
    /// <summary>追加した端子の名前の接頭辞。</summary>
    public const string PortNamePrefix = "Range_";

    private PortDefinition[] _rangeDefinitions = [];

    [OutputPort(nameof(TextNode.TextStyleMapOutputLabel), nameof(TextNode.TextStyleMapOutputDescription),
        typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkSlateGray))]
    public TextStyleMap? Output
    {
        get => GetOutput<TextStyleMap>();
        set => SetOutput(value);
    }

    /// <summary>
    ///     端子の定義を返す。範囲を端子の設定として持たない古いグラフの端子は、
    ///     端子の値（UnicodeRangeStyle）に保存されていた開始・終了を範囲の設定へ引き継いで返す。
    /// </summary>
    public PortDefinition[] GetPortDefinitions() => _rangeDefinitions.Select(MigrateLegacyRange).ToArray();

    bool IPortDefinitionNode.IsValidPortSet(PortDefinition[] definitions) =>
        IsValidRangePortSet(definitions);

    bool IPortDefinitionNode.DynamicPortsAreOutputs => false;

    DynamicPortKind IPortDefinitionNode.DynamicPortKind => DynamicPortKind.UnicodeRange;

    void IPortDefinitionNode.Initialize(PortDefinition[] definitions) => Initialize(definitions);

    void IPortDefinitionNode.NotifyPortDefinitionsChanged() => NotifyPortDefinitionsChanged();

    public static bool IsValidRangePortSet(PortDefinition[] definitions)
    {
        return definitions.Select(d => d.Name).Distinct().Count() == definitions.Length &&
               definitions.All(d => d.Name.StartsWith(PortNamePrefix, StringComparison.Ordinal) &&
                                    d.ValueType == typeof(UnicodeRangeStyle) && d.IsCustom);
    }

    public void Initialize(PortDefinition[] definitions)
    {
        // 編集前の端子の値から古い形式の範囲を読み取れるよう、端子を作り直す前に定義を移行しておく。
        var migrated = GetPortDefinitions();
        var previousValues = migrated
            .Where(d => Inputs.ContainsKey(d.Name))
            .ToDictionary(d => d.Name, d => Inputs[d.Name].LocalValue);

        _rangeDefinitions = definitions
            .Select(d => d with
            {
                Settings = string.IsNullOrEmpty(d.Settings) && previousValues.ContainsKey(d.Name)
                    ? migrated.First(m => m.Name == d.Name).Settings
                    : d.Settings
            })
            .ToArray();

        foreach (var key in Inputs.Keys.Where(k => k.StartsWith(PortNamePrefix, StringComparison.Ordinal)).ToList())
            Inputs.Remove(key);

        foreach (var definition in _rangeDefinitions)
            Inputs[definition.Name] = new InputPort(this, definition.ValueType);

        foreach (var definition in _rangeDefinitions)
            if (definition.DefaultValue is UnicodeRangeStyle defaultValue)
                Inputs[definition.Name].SetValue(defaultValue);

        InvalidateForce();
    }

    internal void NotifyPortDefinitionsChanged()
    {
        InvalidateForce();
    }

    private PortDefinition MigrateLegacyRange(PortDefinition definition)
    {
        if (!string.IsNullOrEmpty(definition.Settings) ||
            !Inputs.TryGetValue(definition.Name, out var input) ||
            input.LocalValue is not UnicodeRangeStyle legacy)
            return definition;

        var range = new CodePointRange(legacy.StartCodePoint, legacy.EndCodePoint);
        return range.IsValid
            ? definition with { Settings = UnicodeRangePortSettings.Serialize([range]) }
            : definition;
    }

    protected override Task Calculate()
    {
        var ranges = new List<UnicodeRangeStyle>();

        // 上の端子から順に並べる。同じ文字に複数の範囲が一致する場合は、より後ろ（下の端子）の設定が優先される。
        foreach (var definition in GetPortDefinitions())
        {
            if (!Inputs.TryGetValue(definition.Name, out var input))
                continue;

            var value = input.GetValue(EvaluationContext).GetAwaiter().GetResult();
            var template = value as UnicodeRangeStyle ?? new UnicodeRangeStyle();
            var style = template.Style.CloneWithoutUnicodeMap();

            foreach (var range in UnicodeRangePortSettings.Parse(definition.Settings).Ranges)
                ranges.Add(new UnicodeRangeStyle
                {
                    StartCodePoint = range.Start,
                    EndCodePoint = range.End,
                    Offset = template.Offset,
                    Style = style
                });
        }

        Output = new TextStyleMap
        {
            Ranges = ranges
        };

        return Task.CompletedTask;
    }
}