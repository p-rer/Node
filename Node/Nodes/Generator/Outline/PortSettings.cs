using System.Globalization;
using Newtonsoft.Json;
using Node.Graph.Port;
using Node.Localize;

namespace Node.Nodes.Generator.Outline;

/// <summary>
///     品詞別文字スタイルノードの端子が対象とする品詞の一覧。<see cref="PortDefinition.Settings" /> にJSONで保存する。
/// </summary>
public sealed class PartOfSpeechPortSettings
{
    public List<JapanesePartOfSpeech> PartsOfSpeech { get; init; } = [];

    public static PartOfSpeechPortSettings Parse(string? json)
    {
        var result = new List<JapanesePartOfSpeech>();
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                foreach (var name in JsonConvert.DeserializeObject<List<string>>(json) ?? [])
                    if (Enum.TryParse<JapanesePartOfSpeech>(name, out var partOfSpeech) &&
                        Enum.IsDefined(partOfSpeech) && !result.Contains(partOfSpeech))
                        result.Add(partOfSpeech);
            }
            catch (JsonException)
            {
                // 壊れた設定は「品詞の指定なし」として扱う。
            }
        }

        return new PartOfSpeechPortSettings { PartsOfSpeech = result };
    }

    public static string Serialize(IEnumerable<JapanesePartOfSpeech> partsOfSpeech)
    {
        // 列挙型の定義順に並べ、保存結果を安定させる。
        var names = partsOfSpeech.Distinct().OrderBy(p => (int)p).Select(p => p.ToString()).ToList();
        return JsonConvert.SerializeObject(names);
    }

    public static string Create(params JapanesePartOfSpeech[] partsOfSpeech) => Serialize(partsOfSpeech);

    /// <summary>ノード上のツールチップや編集ダイアログの一覧に表示する、対象品詞の要約。</summary>
    public static string Describe(PortDefinition definition)
    {
        return string.Join(", ", Parse(definition.Settings).PartsOfSpeech.Select(GetDisplayName));
    }

    public static string GetDisplayName(JapanesePartOfSpeech partOfSpeech)
    {
        return partOfSpeech switch
        {
            JapanesePartOfSpeech.Noun => TextNode.NounStyleLabel,
            JapanesePartOfSpeech.Verb => TextNode.VerbStyleLabel,
            JapanesePartOfSpeech.Adjective => TextNode.AdjectiveStyleLabel,
            JapanesePartOfSpeech.Adverb => TextNode.AdverbStyleLabel,
            JapanesePartOfSpeech.Particle => TextNode.ParticleStyleLabel,
            JapanesePartOfSpeech.AuxiliaryVerb => TextNode.AuxiliaryVerbStyleLabel,
            JapanesePartOfSpeech.Conjunction => TextNode.ConjunctionStyleLabel,
            JapanesePartOfSpeech.Interjection => TextNode.InterjectionStyleLabel,
            JapanesePartOfSpeech.Adnominal => TextNode.AdnominalStyleLabel,
            JapanesePartOfSpeech.Prefix => TextNode.PrefixStyleLabel,
            JapanesePartOfSpeech.Suffix => TextNode.SuffixStyleLabel,
            JapanesePartOfSpeech.Symbol => TextNode.SymbolStyleLabel,
            JapanesePartOfSpeech.Filler => TextNode.FillerStyleLabel,
            _ => TextNode.OtherStyleLabel
        };
    }
}

/// <summary>Unicodeコードポイントの範囲（両端を含む）。</summary>
public readonly record struct CodePointRange(int Start, int End)
{
    public const int MaxCodePoint = 0x10FFFF;

    public bool IsValid => Start >= 0 && End <= MaxCodePoint && Start <= End;

    public override string ToString()
    {
        return Start == End ? FormatCodePoint(Start) : $"{FormatCodePoint(Start)}–{FormatCodePoint(End)}";
    }

    public static string FormatCodePoint(int codePoint) => "U+" + FormatHex(codePoint);

    /// <summary>「U+」なしの16進表記（最低4桁）。</summary>
    public static string FormatHex(int codePoint) => codePoint.ToString("X4", CultureInfo.InvariantCulture);

    /// <summary>「U+0041」「0x41」「41」のいずれの表記も16進のコードポイントとして読み取る。</summary>
    public static bool TryParseCodePoint(string? text, out int codePoint)
    {
        codePoint = 0;
        var trimmed = text?.Trim() ?? "";
        if (trimmed.StartsWith("U+", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[2..];

        return trimmed.Length > 0 &&
               int.TryParse(trimmed, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out codePoint) &&
               codePoint is >= 0 and <= MaxCodePoint;
    }
}

/// <summary>
///     Unicode範囲スタイルノードの端子が対象とする範囲の一覧。<see cref="PortDefinition.Settings" /> にJSONで保存する。
/// </summary>
public sealed class UnicodeRangePortSettings
{
    public List<CodePointRange> Ranges { get; init; } = [];

    public static UnicodeRangePortSettings Parse(string? json)
    {
        var result = new List<CodePointRange>();
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                foreach (var pair in JsonConvert.DeserializeObject<List<int[]>>(json) ?? [])
                {
                    if (pair is not { Length: 2 }) continue;
                    var range = new CodePointRange(pair[0], pair[1]);
                    if (range.IsValid) result.Add(range);
                }
            }
            catch (JsonException)
            {
                // 壊れた設定は「範囲の指定なし」として扱う。
            }
        }

        return new UnicodeRangePortSettings { Ranges = result };
    }

    public static string Serialize(IEnumerable<CodePointRange> ranges)
    {
        return JsonConvert.SerializeObject(ranges.Where(r => r.IsValid).Select(r => new[] { r.Start, r.End }));
    }

    /// <summary>ノード上のツールチップや編集ダイアログの一覧に表示する、対象範囲の要約。</summary>
    public static string Describe(PortDefinition definition)
    {
        return string.Join(", ", Parse(definition.Settings).Ranges.Select(r => r.ToString()));
    }
}