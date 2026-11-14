using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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
    private float _size = 64f;
    private int _weight = 400;
    private int _width = 5;
    private TextFontSlant _slant = TextFontSlant.Upright;
    private float _scaleX = 1f;
    private float _skewX;
    private TextStyleMap? _unicodeMap;

    [Display(Name = nameof(TextNode.FontFamilyLabel), Description = nameof(TextNode.FontFamilyDescription), ResourceType = typeof(TextNode))]
    [FontComboBox]
    public string FontFamily
    {
        get => _fontFamily;
        set => Set(ref _fontFamily, value);
    }

    [Display(Name = nameof(TextNode.FontSizeLabel), Description = nameof(TextNode.FontSizeDescription), ResourceType = typeof(TextNode))]
    [TextBoxSlider("F0", "px", 1, 2000)]
    public float Size
    {
        get => _size;
        set => Set(ref _size, value);
    }

    [Display(Name = nameof(TextNode.TextFontWeightLabel), Description = nameof(TextNode.TextFontWeightDescription), ResourceType = typeof(TextNode))]
    [Node.Editor.Attributes.FontWeightPortControl]
    public int Weight
    {
        get => _weight;
        set => Set(ref _weight, value);
    }

    [Display(Name = nameof(TextNode.TextFontWidthLabel), Description = nameof(TextNode.TextFontWidthDescription), ResourceType = typeof(TextNode))]
    [Node.Editor.Attributes.FontWidthPortControl]
    public int Width
    {
        get => _width;
        set => Set(ref _width, value);
    }

    [Display(Name = nameof(TextNode.ItalicLabel), Description = nameof(TextNode.ItalicDescription), ResourceType = typeof(TextNode))]
    [EnumComboBox]
    public TextFontSlant Slant
    {
        get => _slant;
        set => Set(ref _slant, value);
    }

    [Display(Name = nameof(TextNode.TextScaleLabel), Description = nameof(TextNode.TextScaleDescription), ResourceType = typeof(TextNode))]
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

    [Display(Name = nameof(TextNode.TextSkewLabel), Description = nameof(TextNode.TextSkewDescription), ResourceType = typeof(TextNode))]
    [TextBoxSlider("F2", "", -2, 2)]
    public float SkewX
    {
        get => _skewX;
        set => Set(ref _skewX, value);
    }

    /// <summary>
    /// Unicode 範囲による差分スタイル。編集UIは持たず、UnicodeTextStyleNodeからのみ構築する。
    /// </summary>
    [Browsable(false)]
    public TextStyleMap? UnicodeMap
    {
        get => _unicodeMap;
        set => Set(ref _unicodeMap, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public TextStyle CloneWithoutUnicodeMap() => new()
    {
        FontFamily = FontFamily,
        Size = Size,
        Weight = Weight,
        Width = Width,
        Slant = Slant,
        ScaleX = ScaleX,
        SkewX = SkewX
    };

    public TextStyle Clone() => new()
    {
        FontFamily = FontFamily,
        Size = Size,
        Weight = Weight,
        Width = Width,
        Slant = Slant,
        ScaleX = ScaleX,
        SkewX = SkewX,
        UnicodeMap = UnicodeMap
    };

    private void Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
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

public sealed class TextStyleMap
{
    public TextStyle DefaultStyle { get; set; } = new();
    public List<UnicodeRangeStyle> Ranges { get; set; } = [];

    public TextStyle Resolve(int codePoint, out float offset)
    {
        for (var i = Ranges.Count - 1; i >= 0; i--)
        {
            var range = Ranges[i];
            if (range == null || range.StartCodePoint < 0 ||
                range.EndCodePoint > 0x10FFFF || range.StartCodePoint > range.EndCodePoint)
                continue;
            if (codePoint < range.StartCodePoint || codePoint > range.EndCodePoint)
                continue;
            offset = range.Offset;
            return range.Style ?? DefaultStyle ?? new TextStyle();
        }

        offset = 0f;
        return DefaultStyle ?? new TextStyle();
    }
}
