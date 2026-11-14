using Node.Localize;

namespace Node.Nodes.Generator.Outline;

public enum TextWritingDirection
{
    [System.ComponentModel.DataAnnotations.Display(Name = nameof(TextNode.TextDirectionHorizontal), ResourceType = typeof(TextNode))]
    Horizontal,
    [System.ComponentModel.DataAnnotations.Display(Name = nameof(TextNode.TextDirectionVertical), ResourceType = typeof(TextNode))]
    Vertical
}

public enum TextFontSlant
{
    Upright,
    Italic,
    Oblique
}

public sealed class TextStyle
{
    public string FontFamily { get; set; } = "メイリオ";
    public float Size { get; set; } = 64f;
    public int Weight { get; set; } = 400;
    public int Width { get; set; } = 5;
    public TextFontSlant Slant { get; set; } = TextFontSlant.Upright;
    public float ScaleX { get; set; } = 1f;
    public float SkewX { get; set; }
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
