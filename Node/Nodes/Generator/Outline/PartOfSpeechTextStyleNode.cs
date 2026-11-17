using System.Windows.Media;
using NMeCab.Specialized;
using Node.Editor.Attributes;
using Node.Graph;
using Node.Graph.Attributes;
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
    private static readonly Lazy<MeCabIpaDicTagger> Tagger = new(() => MeCabIpaDicTagger.Create());

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
        foreach (var node in Tagger.Value.Parse(text))
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
public sealed class PartOfSpeechTextStyleNode : NodeLogic
{
    [InputPort(nameof(TextNode.NounStyleLabel), nameof(TextNode.TextStyleDescription), typeof(TextNode))]
    [OptionalTextStylePortControl]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public TextStyle? NounStyle
    {
        get => GetInput<TextStyle>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.VerbStyleLabel), nameof(TextNode.TextStyleDescription), typeof(TextNode))]
    [OptionalTextStylePortControl]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public TextStyle? VerbStyle
    {
        get => GetInput<TextStyle>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.AdjectiveStyleLabel), nameof(TextNode.TextStyleDescription), typeof(TextNode))]
    [OptionalTextStylePortControl]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public TextStyle? AdjectiveStyle
    {
        get => GetInput<TextStyle>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.AdverbStyleLabel), nameof(TextNode.TextStyleDescription), typeof(TextNode))]
    [OptionalTextStylePortControl]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public TextStyle? AdverbStyle
    {
        get => GetInput<TextStyle>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.ParticleStyleLabel), nameof(TextNode.TextStyleDescription), typeof(TextNode))]
    [OptionalTextStylePortControl]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public TextStyle? ParticleStyle
    {
        get => GetInput<TextStyle>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.AuxiliaryVerbStyleLabel), nameof(TextNode.TextStyleDescription), typeof(TextNode))]
    [OptionalTextStylePortControl]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public TextStyle? AuxiliaryVerbStyle
    {
        get => GetInput<TextStyle>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.ConjunctionStyleLabel), nameof(TextNode.TextStyleDescription), typeof(TextNode))]
    [OptionalTextStylePortControl]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public TextStyle? ConjunctionStyle
    {
        get => GetInput<TextStyle>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.InterjectionStyleLabel), nameof(TextNode.TextStyleDescription), typeof(TextNode))]
    [OptionalTextStylePortControl]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public TextStyle? InterjectionStyle
    {
        get => GetInput<TextStyle>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.AdnominalStyleLabel), nameof(TextNode.TextStyleDescription), typeof(TextNode))]
    [OptionalTextStylePortControl]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public TextStyle? AdnominalStyle
    {
        get => GetInput<TextStyle>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.PrefixStyleLabel), nameof(TextNode.TextStyleDescription), typeof(TextNode))]
    [OptionalTextStylePortControl]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public TextStyle? PrefixStyle
    {
        get => GetInput<TextStyle>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.SuffixStyleLabel), nameof(TextNode.TextStyleDescription), typeof(TextNode))]
    [OptionalTextStylePortControl]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public TextStyle? SuffixStyle
    {
        get => GetInput<TextStyle>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.SymbolStyleLabel), nameof(TextNode.TextStyleDescription), typeof(TextNode))]
    [OptionalTextStylePortControl]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public TextStyle? SymbolStyle
    {
        get => GetInput<TextStyle>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.FillerStyleLabel), nameof(TextNode.TextStyleDescription), typeof(TextNode))]
    [OptionalTextStylePortControl]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public TextStyle? FillerStyle
    {
        get => GetInput<TextStyle>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.OtherStyleLabel), nameof(TextNode.TextStyleDescription), typeof(TextNode))]
    [OptionalTextStylePortControl]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public TextStyle? OtherStyle
    {
        get => GetInput<TextStyle>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.PartOfSpeechTextStyleMapOutputLabel),
        nameof(TextNode.PartOfSpeechTextStyleMapOutputDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkSlateGray))]
    public TextStyleMap? Output
    {
        get => GetOutput<TextStyleMap>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        var styles = new Dictionary<JapanesePartOfSpeech, TextStyle>();

        AddStyle(nameof(NounStyle), JapanesePartOfSpeech.Noun, styles);
        AddStyle(nameof(VerbStyle), JapanesePartOfSpeech.Verb, styles);
        AddStyle(nameof(AdjectiveStyle), JapanesePartOfSpeech.Adjective, styles);
        AddStyle(nameof(AdverbStyle), JapanesePartOfSpeech.Adverb, styles);
        AddStyle(nameof(ParticleStyle), JapanesePartOfSpeech.Particle, styles);
        AddStyle(nameof(AuxiliaryVerbStyle), JapanesePartOfSpeech.AuxiliaryVerb, styles);
        AddStyle(nameof(ConjunctionStyle), JapanesePartOfSpeech.Conjunction, styles);
        AddStyle(nameof(InterjectionStyle), JapanesePartOfSpeech.Interjection, styles);
        AddStyle(nameof(AdnominalStyle), JapanesePartOfSpeech.Adnominal, styles);
        AddStyle(nameof(PrefixStyle), JapanesePartOfSpeech.Prefix, styles);
        AddStyle(nameof(SuffixStyle), JapanesePartOfSpeech.Suffix, styles);
        AddStyle(nameof(SymbolStyle), JapanesePartOfSpeech.Symbol, styles);
        AddStyle(nameof(FillerStyle), JapanesePartOfSpeech.Filler, styles);
        AddStyle(nameof(OtherStyle), JapanesePartOfSpeech.Other, styles);

        Output = new TextStyleMap
        {
            PartOfSpeechStyles = styles
        };

        return Task.CompletedTask;
    }

    private void AddStyle(string portName, JapanesePartOfSpeech partOfSpeech,
        IDictionary<JapanesePartOfSpeech, TextStyle> styles)
    {
        if (!Inputs.TryGetValue(portName, out var input))
            return;

        var value = input.GetValue(EvaluationContext).GetAwaiter().GetResult();
        if (value is TextStyle style)
            styles[partOfSpeech] = style.CloneWithoutUnicodeMap();
    }
}