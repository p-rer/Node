using System.Windows.Media;
using Node.Editor.Attributes;
using Node.Graph;
using Node.Graph.Attributes;
using Node.Graph.Port;
using Node.Localize;

namespace Node.Nodes.Generator.Outline;

[Node(typeof(OutlineCategory), nameof(TextNode.UnicodeTextStyleNode), nameof(TextNode.UnicodeTextStyleNodeDescription), typeof(TextNode))]
public sealed class UnicodeTextStyleNode : NodeLogic, IPortDefinitionNode
{
    private PortDefinition[] _rangeDefinitions = [];

    [InputPort(nameof(TextNode.TextStyleDefaultLabel), nameof(TextNode.TextStyleDefaultDescription), typeof(TextNode))]
    [TextStylePortControl]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public TextStyle DefaultStyle
    {
        get => GetInput<TextStyle>() ?? new TextStyle();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.TextStyleMapOutputLabel), nameof(TextNode.TextStyleMapOutputDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public TextStyleMap? Output
    {
        get => GetOutput<TextStyleMap>();
        set => SetOutput(value);
    }

    public PortDefinition[] GetPortDefinitions() => _rangeDefinitions;

    bool IPortDefinitionNode.IsValidPortSet(PortDefinition[] definitions)
    {
        return IsValidRangePortSet(definitions);
    }

    bool IPortDefinitionNode.DynamicPortsAreOutputs => false;

    DynamicPortKind IPortDefinitionNode.DynamicPortKind => DynamicPortKind.UnicodeRange;

    void IPortDefinitionNode.Initialize(PortDefinition[] definitions)
    {
        Initialize(definitions);
    }

    void IPortDefinitionNode.NotifyPortDefinitionsChanged()
    {
        NotifyPortDefinitionsChanged();
    }

    public static bool IsValidRangePortSet(PortDefinition[] definitions)
    {
        return definitions.Select(d => d.Name).Distinct().Count() == definitions.Length &&
               definitions.All(d => d.Name.StartsWith("Range_", StringComparison.Ordinal) &&
                                    d.ValueType == typeof(UnicodeRangeStyle) && d.IsCustom);
    }

    public void Initialize(PortDefinition[] definitions)
    {
        _rangeDefinitions = definitions;
        foreach (var key in Inputs.Keys.Where(k => k.StartsWith("Range_", StringComparison.Ordinal)).ToList())
            Inputs.Remove(key);

        foreach (var definition in definitions)
            Inputs[definition.Name] = new InputPort(this, definition.ValueType);

        foreach (var definition in definitions)
            if (definition.DefaultValue != null)
                Inputs[definition.Name].SetValue(definition.DefaultValue);

        InvalidateForce();
    }

    internal void NotifyPortDefinitionsChanged()
    {
        NeedToReinitializeInputPorts?.Invoke(this,
            new NeedToReinitializeInputPortsEvent(nameof(DefaultStyle), new UnicodeTextStyleRangeContainer(_rangeDefinitions)));
    }

    protected override Task Calculate()
    {
        var ranges = new List<UnicodeRangeStyle>();
        foreach (var definition in _rangeDefinitions)
        {
            if (!Inputs.TryGetValue(definition.Name, out var input)) continue;
            var value = input.GetValue(EvaluationContext).GetAwaiter().GetResult();
            if (value is UnicodeRangeStyle range)
                ranges.Add(range);
        }

        Output = new TextStyleMap
        {
            DefaultStyle = DefaultStyle,
            Ranges = ranges
        };
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        base.Dispose();
    }
}

/// <summary>
/// NodeViewModel の動的端子再構築イベントに利用する、定義を保持する軽量コンテナ。
/// </summary>
internal sealed class UnicodeTextStyleRangeContainer : InputsContainer
{
    private readonly PortDefinition[] _definitions;

    public UnicodeTextStyleRangeContainer(PortDefinition[] definitions)
    {
        _definitions = definitions;
    }

    public PortDefinition[] Definitions => _definitions;
}
