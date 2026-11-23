using System.IO;
using System.Windows.Media;
using NMeCab.Specialized;
using Node.Editor.Attributes;
using Node.Graph;
using Node.Graph.Attributes;
using Node.Graph.Port;
using Node.Localize;

namespace Node.Nodes.Generator.Outline;

public enum JapanesePartOfSpeech
{
    Noun,
    Verb,
    Adjective,
    Adverb,
    Particle,
    AuxiliaryVerb,
    Conjunction,
    Interjection,
    Adnominal,
    Prefix,
    Suffix,
    Symbol,
    Filler,
    Other
}

internal readonly record struct JapanesePartOfSpeechToken(
    string Surface,
    JapanesePartOfSpeech PartOfSpeech);

internal static class JapanesePartOfSpeechAnalyzer
{
    // Tagger はスレッドセーフであるため、辞書を一度だけ読み込み再利用する。
    private static readonly Lazy<MeCabIpaDicTagger> Tagger = new(() =>
        MeCabIpaDicTagger.Create(
            Path.Combine(Path.GetDirectoryName(typeof(JapanesePartOfSpeechAnalyzer).Assembly.Location)!, "IpaDic")));

    private static readonly Lock TaggerLock = new();

    static JapanesePartOfSpeechAnalyzer()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            if (Tagger.IsValueCreated)
                Tagger.Value.Dispose();
        };
    }

    public static IEnumerable<JapanesePartOfSpeechToken> Analyze(string text)
    {
        if (string.IsNullOrEmpty(text))
            yield break;

        var cursor = 0;
        MeCabIpaDicNode[] nodes;
        lock (TaggerLock)
        {
            nodes = Tagger.Value.Parse(text).ToArray();
        }

        foreach (var node in nodes)
        {
            var surface = node.Surface;
            if (string.IsNullOrEmpty(surface))
                continue;

            // 解析結果の表層形を原文へ順番に対応付ける。解析器が返さない空白なども落とさない。
            var index = text.IndexOf(surface, cursor, StringComparison.Ordinal);
            if (index < 0)
            {
                if (cursor < text.Length)
                    yield return new JapanesePartOfSpeechToken(text[cursor..], JapanesePartOfSpeech.Other);
                yield break;
            }

            if (index > cursor)
                yield return new JapanesePartOfSpeechToken(text[cursor..index], JapanesePartOfSpeech.Other);

            yield return new JapanesePartOfSpeechToken(surface, GetPartOfSpeech(node));
            cursor = index + surface.Length;
        }

        if (cursor < text.Length)
            yield return new JapanesePartOfSpeechToken(text[cursor..], JapanesePartOfSpeech.Other);
    }

    private static JapanesePartOfSpeech GetPartOfSpeech(MeCabIpaDicNode node)
    {
        // IPA辞書では形容動詞語幹が「名詞」に分類されるため、細分類も確認する。
        if (node.PartsOfSpeech == "名詞" && node.PartsOfSpeechSection1 == "形容動詞語幹")
            return JapanesePartOfSpeech.Adjective;

        return node.PartsOfSpeech switch
        {
            "名詞" => JapanesePartOfSpeech.Noun,
            "動詞" => JapanesePartOfSpeech.Verb,
            "形容詞" => JapanesePartOfSpeech.Adjective,
            "副詞" => JapanesePartOfSpeech.Adverb,
            "助詞" => JapanesePartOfSpeech.Particle,
            "助動詞" => JapanesePartOfSpeech.AuxiliaryVerb,
            "接続詞" => JapanesePartOfSpeech.Conjunction,
            "感動詞" => JapanesePartOfSpeech.Interjection,
            "連体詞" => JapanesePartOfSpeech.Adnominal,
            "接頭詞" => JapanesePartOfSpeech.Prefix,
            "接尾詞" => JapanesePartOfSpeech.Suffix,
            "記号" => JapanesePartOfSpeech.Symbol,
            "フィラー" => JapanesePartOfSpeech.Filler,
            _ => JapanesePartOfSpeech.Other
        };
    }
}

[Node(typeof(OutlineCategory), nameof(TextNode.PartOfSpeechTextStyleNode),
    nameof(TextNode.PartOfSpeechTextStyleNodeDescription), typeof(TextNode))]
public sealed class PartOfSpeechTextStyleNode : NodeLogic, IPortDefinitionNode
{
    /// <summary>追加した端子の名前の接頭辞。</summary>
    public const string PortNamePrefix = "Pos_";

    private PortDefinition[] _styleDefinitions = [];

    public PartOfSpeechTextStyleNode()
    {
        Initialize(CreateDefaultDefinitions());
    }

    [OutputPort(nameof(TextNode.PartOfSpeechTextStyleMapOutputLabel),
        nameof(TextNode.PartOfSpeechTextStyleMapOutputDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkSlateGray))]
    public TextStyleMap? Output
    {
        get => GetOutput<TextStyleMap>();
        set => SetOutput(value);
    }

    public PortDefinition[] GetPortDefinitions() => _styleDefinitions;

    bool IPortDefinitionNode.IsValidPortSet(PortDefinition[] definitions) => IsValidStylePortSet(definitions);

    bool IPortDefinitionNode.DynamicPortsAreOutputs => false;

    DynamicPortKind IPortDefinitionNode.DynamicPortKind => DynamicPortKind.PartOfSpeech;

    void IPortDefinitionNode.Initialize(PortDefinition[] definitions) => Initialize(definitions);

    void IPortDefinitionNode.NotifyPortDefinitionsChanged() => InvalidateForce();

    /// <summary>
    ///     スタイル端子として使えるポート定義の組かどうか。型が TextStyle で、名前が空でなく、
    ///     名前の重複や出力端子との衝突、動的ポートの区切り文字（.）を含まないこと。
    /// </summary>
    internal static bool IsValidStylePortSet(PortDefinition[] definitions)
    {
        return definitions.All(definition =>
                   definition.ValueType == typeof(TextStyle) &&
                   definition.IsCustom &&
                   !string.IsNullOrEmpty(definition.Name) &&
                   !definition.Name.Contains('.') &&
                   definition.Name != nameof(Output)) &&
               definitions.Select(definition => definition.Name).Distinct().Count() == definitions.Length;
    }

    /// <summary>
    ///     スタイル端子を定義の並び順どおりに作り直す。名前が同じ端子は入力値と接続状態を引き継ぐ。
    ///     既定の端子は従来のプロパティ名（NounStyle など）と同じ名前なので、古いスナップショットもそのまま復元できる。
    /// </summary>
    internal void Initialize(PortDefinition[] definitions)
    {
        var previous = _styleDefinitions
            .Where(definition => Inputs.ContainsKey(definition.Name))
            .ToDictionary(definition => definition.Name, definition => Inputs[definition.Name]);

        Inputs.Clear();
        foreach (var definition in definitions)
        {
            if (!previous.TryGetValue(definition.Name, out var port))
            {
                port = new InputPort(this, typeof(TextStyle));
                if (definition.DefaultValue is TextStyle defaultStyle)
                    port.SetValue(defaultStyle);
            }

            Inputs.Add(definition.Name, port);
        }

        _styleDefinitions = definitions;
        InvalidateForce();
    }

    private static PortDefinition CreateDefaultDefinition(string name, JapanesePartOfSpeech partOfSpeech)
    {
        // Label を空にして、表示名を言語設定に追従させる（PortDefinition.GetDisplayLabel）。
        return new PortDefinition(name, typeof(TextStyle), "", "", null, true,
            PartOfSpeechPortSettings.Create(partOfSpeech));
    }

    private static PortDefinition[] CreateDefaultDefinitions()
    {
        return
        [
            CreateDefaultDefinition("NounStyle", JapanesePartOfSpeech.Noun),
            CreateDefaultDefinition("VerbStyle", JapanesePartOfSpeech.Verb),
        ];
    }

    protected override Task Calculate()
    {
        var styles = new Dictionary<JapanesePartOfSpeech, TextStyle>();

        // 上の端子から順に適用し、同じ品詞を指定した端子が複数ある場合は、より下の端子の設定で上書きする。
        foreach (var definition in _styleDefinitions)
        {
            if (!Inputs.TryGetValue(definition.Name, out var input))
                continue;

            var value = input.GetValue(EvaluationContext).GetAwaiter().GetResult();
            if (value is not TextStyle style)
                continue;

            var portStyle = style.CloneWithoutUnicodeMap();
            foreach (var partOfSpeech in PartOfSpeechPortSettings.Parse(definition.Settings).PartsOfSpeech)
                styles[partOfSpeech] = portStyle;
        }

        Output = new TextStyleMap
        {
            PartOfSpeechStyles = styles
        };

        return Task.CompletedTask;
    }
}