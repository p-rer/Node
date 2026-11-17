using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Text;
using Node.Editor.Attributes;
using Node.Localize;
using YukkuriMovieMaker.Controls;

namespace Node.Nodes.Generator.Outline;

public enum TextWritingDirection
{
    [Display(Name = nameof(TextNode.TextDirectionHorizontal), ResourceType = typeof(TextNode))]
    Horizontal,

    [Display(Name = nameof(TextNode.TextDirectionVertical), ResourceType = typeof(TextNode))]
    Vertical
}

public enum TextFontSlant
{
    [Display(Name = nameof(TextNode.TextSlantUpright), ResourceType = typeof(TextNode))]
    Upright,

    [Display(Name = nameof(TextNode.TextSlantItalic), ResourceType = typeof(TextNode))]
    Italic,

    [Display(Name = nameof(TextNode.TextSlantOblique), ResourceType = typeof(TextNode))]
    Oblique
}

public sealed class TextStyle : INotifyPropertyChanged
{
    private string _fontFamily = "メイリオ";
    private float _scaleX = 1f;
    private float _size = 64f;
    private float _skewX;
    private TextFontSlant _slant = TextFontSlant.Upright;
    private int _weight = 400;

    [Display(Name = nameof(TextNode.FontFamilyLabel), Description = nameof(TextNode.FontFamilyDescription),
        ResourceType = typeof(TextNode))]
    [FontComboBox]
    public string FontFamily
    {
        get => _fontFamily;
        set => Set(ref _fontFamily, value);
    }

    [Display(Name = nameof(TextNode.FontSizeLabel), Description = nameof(TextNode.FontSizeDescription),
        ResourceType = typeof(TextNode))]
    [TextBoxSlider("F0", "px", 1, 2000)]
    public float Size
    {
        get => _size;
        set => Set(ref _size, value);
    }

    [Display(Name = nameof(TextNode.BoldLabel), Description = nameof(TextNode.BoldDescription),
        ResourceType = typeof(TextNode))]
    [FontWeightPortControl]
    public int Weight
    {
        get => _weight;
        set => Set(ref _weight, value);
    }

    [Display(Name = nameof(TextNode.ItalicLabel), Description = nameof(TextNode.ItalicDescription),
        ResourceType = typeof(TextNode))]
    [EnumComboBox]
    public TextFontSlant Slant
    {
        get => _slant;
        set => Set(ref _slant, value);
    }

    [Display(Name = nameof(TextNode.TextScaleLabel), Description = nameof(TextNode.TextScaleDescription),
        ResourceType = typeof(TextNode))]
    [TextBoxSlider("F2", "", 0.1, 10)]
    public float Scale
    {
        get => _scaleX;
        set => Set(ref _scaleX, value);
    }

    [Browsable(false)]
    public float ScaleX
    {
        get => Scale;
        set => Scale = value;
    }

    [Display(Name = nameof(TextNode.TextSkewLabel), Description = nameof(TextNode.TextSkewDescription),
        ResourceType = typeof(TextNode))]
    [TextBoxSlider("F2", "", -2, 2)]
    public float SkewX
    {
        get => _skewX;
        set => Set(ref _skewX, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public TextStyle CloneWithoutUnicodeMap() => new()
    {
        FontFamily = FontFamily,
        Size = Size,
        Weight = Weight,
        Slant = Slant,
        ScaleX = ScaleX,
        SkewX = SkewX
    };

    public TextStyle Clone() => new()
    {
        FontFamily = FontFamily,
        Size = Size,
        Weight = Weight,
        Slant = Slant,
        ScaleX = ScaleX,
        SkewX = SkewX
    };

    private void Set<T>(ref T field, T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class UnicodeRangeStyle
{
    public int StartCodePoint { get; set; }
    public int EndCodePoint { get; set; } = 0x7F;
    public TextStyle Style { get; set; } = new();
    public float Offset { get; set; }
}

public readonly record struct StyledTextCharacter(Rune Character, TextStyle Style, float Offset);

public sealed class TextStyleMap
{
    public List<UnicodeRangeStyle> Ranges { get; set; } = [];

    public Dictionary<JapanesePartOfSpeech, TextStyle> PartOfSpeechStyles { get; set; } = [];

    public bool HasRules => Ranges.Count > 0 || PartOfSpeechStyles.Count > 0;

    public TextStyle? Resolve(int codePoint, out float offset)
    {
        for (var i = Ranges.Count - 1; i >= 0; i--)
        {
            var range = Ranges[i];

            if (range.StartCodePoint < 0 ||
                range.EndCodePoint > 0x10FFFF ||
                range.StartCodePoint > range.EndCodePoint)
                continue;

            if (codePoint < range.StartCodePoint || codePoint > range.EndCodePoint)
                continue;

            offset = range.Offset;
            return range.Style;
        }

        offset = 0f;
        return null;
    }

    /// <summary>
    ///     テキスト全体を順番に解析し、各Unicodeスカラーに適用するスタイルを返す。
    ///     品詞スタイルを優先し、該当する品詞スタイルがない文字にはUnicode範囲スタイルを適用する。
    /// </summary>
    public IEnumerable<StyledTextCharacter> ResolveText(string text, TextStyle fallbackStyle)
    {
        if (string.IsNullOrEmpty(text))
            yield break;

        if (PartOfSpeechStyles.Count == 0)
        {
            foreach (var rune in text.EnumerateRunes())
            {
                var style = Resolve(rune.Value, out var offset) ?? fallbackStyle;
                yield return new StyledTextCharacter(rune, style, offset);
            }

            yield break;
        }

        foreach (var token in JapanesePartOfSpeechAnalyzer.Analyze(text))
        {
            var hasPartOfSpeechStyle = PartOfSpeechStyles.TryGetValue(token.PartOfSpeech, out var partOfSpeechStyle);

            foreach (var rune in token.Surface.EnumerateRunes())
            {
                var rangeStyle = Resolve(rune.Value, out var offset);
                var style = hasPartOfSpeechStyle
                    ? partOfSpeechStyle!
                    : rangeStyle ?? fallbackStyle;

                yield return new StyledTextCharacter(rune, style, hasPartOfSpeechStyle ? 0f : offset);
            }
        }
    }
}