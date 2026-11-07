using System.Globalization;
using System.Windows.Media;
using Node.Editor.Attributes;
using Node.Graph;
using Node.Graph.Attributes;
using Node.Graph.Port;
using Node.Localize;
using Node.Nodes.Func;
using Node.Utility;
using Node.ValueTypes;
using YukkuriMovieMaker.Plugin.Effects;

namespace Node.Nodes.Effect;

/// <summary>
///     Node エフェクト自身を、他のグラフから呼び出せるノードとして置いたもの。
///     動的に生成される映像エフェクトノードからは Node エフェクトを除外しているため、
///     その代わりに同じカテゴリー・同じ名前でここに置く。
///     内部にサブグラフを持ち、サブグラフの引数ノードの端子がこのノードの入力、
///     戻り値ノードの端子が出力になる。端子は型付きで、数の上限なく編集できる。
/// </summary>
[Node("NodeEffectKey_EffectCategoryName/NodeEffectKey_VideoEffectCategoryName/" + VideoEffectCategories.Filtering,
    nameof(TextUi.Node), nameof(TextUi.Node), typeof(TextUi))]
public class NodeEffectNode : NodeLogic
{
    /// <summary>
    ///     入力・出力の端子として選べる型。
    /// </summary>
    internal static readonly Type[] SupportedPortTypes =
    [
        typeof(ImageWrapper), typeof(float), typeof(int), typeof(bool), typeof(Color), typeof(string),
        typeof(MaskWrapper), typeof(OutlineWrapper), typeof(BrushWrapper)
    ];

    private NodeGraph? _graph;

    public NodeEffectNode()
    {
        SyncPorts();
    }

    /// <summary>
    ///     内部のサブグラフ。復元時は Serializer が SetSubgraph 経由で差し替える。
    /// </summary>
    [SubGraph(nameof(TextUi.NodeEditor), nameof(TextUi.NodeEditor), typeof(TextUi),
        ArgumentsNodeProperty = nameof(InnerArguments), ReturnNodeProperty = nameof(InnerReturn))]
    public NodeGraph Graph
    {
        get => _graph ??= CreateDefaultGraph();
        set
        {
            var old = _graph;
            _graph = value;
            BindInnerNodes(value);
            if (old != null && !ReferenceEquals(old, value))
                foreach (var node in old.Nodes.Values)
                    node.Dispose();
            SyncPorts();
        }
    }

    public ArgumentsNode? InnerArguments { get; set; }
    public ReturnNode? InnerReturn { get; set; }

    public override void Dispose()
    {
        if (_graph != null)
            foreach (var node in _graph.Nodes.Values)
                node.Dispose();
        base.Dispose();
    }

    private NodeGraph CreateDefaultGraph()
    {
        var graph = new NodeGraph();
        var arguments = new ArgumentsNode(new PortDefinition("InputImage", typeof(ImageWrapper), "", "", null, true))
            { Id = Guid.NewGuid() };
        var returns = new ReturnNode(new PortDefinition("OutputImage", typeof(ImageWrapper), "", "", null, true))
            { Id = Guid.NewGuid() };

        graph.AddNode(arguments);
        graph.AddNode(returns);
        graph.SetVisualState(arguments.Id, 100, 100);
        graph.SetVisualState(returns.Id, 500, 100);
        graph.Connect(arguments.Id, "InputImage", returns.Id, "OutputImage");

        InnerArguments = arguments;
        InnerReturn = returns;
        return graph;
    }

    private void BindInnerNodes(NodeGraph graph)
    {
        InnerArguments = graph.Nodes.Values.OfType<ArgumentsNode>().FirstOrDefault();
        InnerReturn = graph.Nodes.Values.OfType<ReturnNode>().FirstOrDefault();
    }

    /// <summary>
    ///     サブグラフの引数ノード・戻り値ノードの定義に合わせて、このノードの入力・出力端子を作り直す。
    ///     名前と型が同じ端子は、入力値と接続を引き継ぐ。
    /// </summary>
    internal void SyncPorts()
    {
        _ = Graph;
        var inputs = InnerArguments?.GetPortDefinitions() ?? [];
        var outputs = InnerReturn?.GetPortDefinitions() ?? [];

        var previousInputs = Inputs.ToDictionary(kv => kv.Key, kv => kv.Value);
        Inputs.Clear();
        foreach (var definition in inputs)
        {
            if (!previousInputs.TryGetValue(definition.Name, out var input) ||
                input.ValueType != definition.ValueType)
            {
                input = new InputPort(this, definition.ValueType);
                var value = ConvertPortValue(definition.ValueType, definition.DefaultValue);
                if (value != null) input.SetValue(value);
            }

            Inputs[definition.Name] = input;
        }

        var previousOutputs = Outputs.ToDictionary(kv => kv.Key, kv => kv.Value);
        Outputs.Clear();
        foreach (var definition in outputs)
        {
            if (!previousOutputs.TryGetValue(definition.Name, out var output) ||
                output.ValueType != definition.ValueType)
                output = new OutputPort(this, definition.ValueType);

            Outputs[definition.Name] = output;
        }

        InvalidateForce();
    }

    protected override async Task Calculate()
    {
        var graph = Graph;
        var arguments = InnerArguments;
        var returns = InnerReturn;
        if (arguments == null || returns == null)
            return;

        var values = new Dictionary<string, object?>();
        foreach (var (name, port) in Inputs.ToList())
            values[name] = ConvertPortValue(port.ValueType,
                await port.GetValue(EvaluationContext).ConfigureAwait(false));

        arguments.InjectArguments(values);
        graph.InvalidateAll();

        var results = await returns.ExtractReturns(EvaluationContext).ConfigureAwait(false);
        foreach (var (name, port) in Outputs.ToList())
            port.SetValue(ConvertPortValue(port.ValueType, results.GetValueOrDefault(name)));
    }

    /// <summary>
    ///     端子として使えるポート定義の組かどうか。型が対応していて、名前が空でなく、
    ///     名前が重複せず、動的ポートの区切り文字（.）を含まないこと。
    /// </summary>
    internal static bool IsValidPortSet(PortDefinition[] definitions)
    {
        return definitions.All(definition =>
                   SupportedPortTypes.Contains(definition.ValueType) &&
                   !string.IsNullOrEmpty(definition.Name) &&
                   !definition.Name.Contains('.')) &&
               definitions.Select(definition => definition.Name).Distinct().Count() == definitions.Length;
    }

    /// <summary>
    ///     値を端子の型へ変換する。変換できない場合は null。
    /// </summary>
    internal static object? ConvertPortValue(Type type, object? raw)
    {
        if (raw is null) return null;
        if (type.IsInstanceOfType(raw)) return raw;

        try
        {
            if (type == typeof(Color) && raw is string text)
                return ColorConverter.ConvertFromString(text);
            return Convert.ChangeType(raw, type, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (!ExceptionPolicy.IsFatal(ex))
        {
            return null;
        }
    }

    internal static string GetPortColor(Type type)
    {
        if (type == typeof(ImageWrapper)) return nameof(Colors.CornflowerBlue);
        if (type == typeof(MaskWrapper) || type == typeof(OutlineWrapper)) return nameof(Colors.MediumPurple);
        if (type == typeof(BrushWrapper)) return nameof(Colors.LawnGreen);
        if (type == typeof(Color)) return nameof(Colors.Gold);
        if (type == typeof(bool)) return nameof(Colors.Green);
        return nameof(Colors.DarkOrange);
    }

    internal static PropertyControlBaseAttribute? CreateControlAttribute(Type type)
    {
        if (type == typeof(float)) return new NumberPortControlAttribute();
        if (type == typeof(int)) return new NumberPortControlAttribute { Digits = 0 };
        if (type == typeof(bool)) return new BoolPortControlAttribute();
        if (type == typeof(Color)) return new ColorPortControlAttribute();
        if (type == typeof(string)) return new TextPortControlAttribute();
        return null;
    }
}