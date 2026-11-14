using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Node.Editor.Attributes;
using Node.Graph;
using Node.Graph.Attributes;
using Node.Graph.Port;
using Node.Localize;
using Node.ValueTypes;
using SkiaSharp;
using YukkuriMovieMaker.Controls;

namespace Node.Nodes.Generator.Outline;

public enum ShapeKind
{
    [Display(Name = nameof(TextNode.ShapeRect), ResourceType = typeof(TextNode))]
    Rect,

    [Display(Name = nameof(TextNode.ShapeRoundRect), ResourceType = typeof(TextNode))]
    RoundRect,

    [Display(Name = nameof(TextNode.ShapeOval), ResourceType = typeof(TextNode))]
    Oval,

    [Display(Name = nameof(TextNode.ShapePolygon), ResourceType = typeof(TextNode))]
    Polygon,

    [Display(Name = nameof(TextNode.ShapeStar), ResourceType = typeof(TextNode))]
    Star,

    [Display(Name = nameof(TextNode.ShapeLine), ResourceType = typeof(TextNode))]
    Line
}

public class ShapeCommonInputs : InputsContainer
{
    [InputPort(nameof(TextNode.ShapeCenterXLabel), nameof(TextNode.ShapeCenterXDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000, Default = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float CenterX
    {
        get;
        set => Set(ref field, value);
    }

    [InputPort(nameof(TextNode.ShapeCenterYLabel), nameof(TextNode.ShapeCenterYDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000, Default = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float CenterY
    {
        get;
        set => Set(ref field, value);
    }

    [InputPort(nameof(TextNode.ShapeWidthLabel), nameof(TextNode.ShapeWidthDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000, Default = 200)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Width
    {
        get;
        set => Set(ref field, value);
    }

    [InputPort(nameof(TextNode.ShapeHeightLabel), nameof(TextNode.ShapeHeightDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000, Default = 200)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Height
    {
        get;
        set => Set(ref field, value);
    }

    [InputPort(nameof(TextNode.RotationPortLabel), nameof(TextNode.ShapeRotationDescription), typeof(TextNode))]
    [NumberPortControl(Min = -3600, Max = 3600, Default = 0, Unit = "deg")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Rotation
    {
        get;
        set => Set(ref field, value);
    }
}

public class RectInputs : ShapeCommonInputs
{
}

public class RoundRectInputs : ShapeCommonInputs
{
    private float _cornerRadius;

    [InputPort(nameof(TextNode.ShapeCornerRadiusLabel), nameof(TextNode.ShapeCornerRadiusDescription),
        typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 4000, Default = 20)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float CornerRadius
    {
        get => _cornerRadius;
        set => Set(ref _cornerRadius, value);
    }
}

public class OvalInputs : ShapeCommonInputs
{
}

public class PolygonInputs : ShapeCommonInputs
{
    private float _sides;

    [InputPort(nameof(TextNode.VertexCountLabel), nameof(TextNode.PolygonSidesDescription), typeof(TextNode))]
    [NumberPortControl(Min = 3, Max = 64, Digits = 0, Default = 5)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Sides
    {
        get => _sides;
        set => Set(ref _sides, value);
    }
}

public class StarInputs : ShapeCommonInputs
{
    private float _innerRadiusRatio;
    private float _sides;

    [InputPort(nameof(TextNode.VertexCountLabel), nameof(TextNode.StarSidesDescription), typeof(TextNode))]
    [NumberPortControl(Min = 3, Max = 64, Digits = 0, Default = 5)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Sides
    {
        get => _sides;
        set => Set(ref _sides, value);
    }

    [InputPort(nameof(TextNode.InnerRadiusRatioLabel), nameof(TextNode.InnerRadiusRatioDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 100, Default = 50, Unit = "%")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float InnerRadiusRatio
    {
        get => _innerRadiusRatio;
        set => Set(ref _innerRadiusRatio, value);
    }
}

public class LineInputs : ShapeCommonInputs
{
}

[Node(typeof(OutlineCategory), nameof(TextNode.ShapeOutlineNode), nameof(TextNode.ShapeOutlineNodeDescription),
    typeof(TextNode))]
public class ShapeOutlineNode : NodeLogic
{
    private readonly LineInputs _lineInputs = new();
    private readonly OvalInputs _ovalInputs = new();
    private readonly PolygonInputs _polygonInputs = new();
    private readonly RectInputs _rectInputs = new();
    private readonly RoundRectInputs _roundRectInputs = new();
    private readonly StarInputs _starInputs = new();

    private ShapeKind _kind = ShapeKind.Rect;
    private SKPath? _path;

    [InputPort(nameof(TextNode.ShapeKindLabel), nameof(TextNode.ShapeKindDescription), typeof(TextNode))]
    [EnumPortControl(Default = 0, IsEditable = false, Items = typeof(ShapeKind))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public ShapeKind Kind
    {
        get => GetInput<ShapeKind>();
        set => SetInput(value);
    }


    [InputPort(nameof(TextNode.ShapeParamsLabel), nameof(TextNode.ShapeParamsDescription), typeof(TextNode),
        true)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public InputsContainer Params
    {
        get => GetCurrentInputs();
        set => SetDynamicContainer(value);
    }

    [OutputPort(nameof(TextNode.OutlinePortLabel), nameof(TextNode.ShapeOutlineOutputDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public OutlineWrapper? Output
    {
        get => GetOutput<OutlineWrapper>();
        set => SetOutput(value);
    }

    protected internal override void OnInputValueChanged(string portName, object? value)
    {
        if (portName != nameof(Kind))
            return;

        var kind = value is ShapeKind shapeKind
            ? shapeKind
            : (ShapeKind)Convert.ToInt32(value);

        if (kind == _kind)
            return;

        PreserveCurrentParamsValues();

        _kind = kind;

        SetDynamicContainer(GetCurrentInputs(), nameof(Params));
    }

    private void PreserveCurrentParamsValues()
    {
        const string prefix = nameof(Params) + ".";
        foreach (var (key, port) in Inputs)
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal)) continue;
            if (port.IsConnected) continue;
            var value = port.LocalValue;
            if (value is null) continue;
            QueuePendingDynamicValue(key, value);
        }
    }

    private InputsContainer GetCurrentInputs()
    {
        return _kind switch
        {
            ShapeKind.Rect => _rectInputs,
            ShapeKind.RoundRect => _roundRectInputs,
            ShapeKind.Oval => _ovalInputs,
            ShapeKind.Polygon => _polygonInputs,
            ShapeKind.Star => _starInputs,
            ShapeKind.Line => _lineInputs,
            _ => _rectInputs
        };
    }

    protected override Task Calculate()
    {
        var kind = Kind;

        if (kind != _kind)
        {
            PreserveCurrentParamsValues();

            _kind = kind;

            var newContainer = GetCurrentInputs();
            SwapDynamicContainer(nameof(Params), newContainer);
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null)
                _ = dispatcher.BeginInvoke(() => SetDynamicContainer(newContainer, nameof(Params)));
        }

        var centerX = GetDynamicValue<float>("CenterX");
        var centerY = GetDynamicValue<float>("CenterY");
        var width = GetDynamicValue<float>("Width");
        var height = GetDynamicValue<float>("Height");
        var rotation = GetDynamicValue<float>("Rotation");

        var builder = new SKPathBuilder();

        switch (kind)
        {
            case ShapeKind.Rect:
            {
                builder.AddRect(
                    new SKRect(centerX - width / 2f, centerY - height / 2f, centerX + width / 2f,
                        centerY + height / 2f));

                break;
            }

            case ShapeKind.RoundRect:
            {
                var cornerRadius = GetDynamicValue<float>("CornerRadius");

                builder.AddRoundRect(
                    new SKRect(centerX - width / 2f, centerY - height / 2f, centerX + width / 2f,
                        centerY + height / 2f),
                    Math.Max(0, cornerRadius), Math.Max(0, cornerRadius));

                break;
            }

            case ShapeKind.Oval:
            {
                builder.AddOval(
                    new SKRect(centerX - width / 2f, centerY - height / 2f, centerX + width / 2f,
                        centerY + height / 2f));

                break;
            }

            case ShapeKind.Polygon:
            {
                var sides = Math.Max(3, (int)GetDynamicValue<float>("Sides"));

                builder.AddPoly(
                    BuildStarPoints(centerX, centerY, width / 2f, height / 2f,
                        width / 2f, height / 2f, sides));

                break;
            }
            case ShapeKind.Star:
            {
                var sides = Math.Max(3, (int)GetDynamicValue<float>("Sides"));
                var innerRadiusRatio = Math.Clamp(
                    GetDynamicValue<float>("InnerRadiusRatio") / 100f, 0f, 1f);

                builder.AddPoly(
                    BuildStarPoints(centerX, centerY, width / 2f, height / 2f,
                        width / 2f * innerRadiusRatio, height / 2f * innerRadiusRatio, sides * 2));

                break;
            }
            case ShapeKind.Line:
            {
                builder.MoveTo(centerX, centerY);
                builder.LineTo(centerX + width, centerY + height);

                break;
            }
        }

        var path = builder.Detach();

        if (rotation != 0)
            path.Transform(SKMatrix.CreateRotationDegrees(rotation, centerX, centerY));

        _path?.Dispose();
        _path = path;
        Output = new OutlineWrapper { Path = _path };

        return Task.CompletedTask;
    }

    private T GetDynamicValue<T>(string propertyName)
    {
        var portName = $"Params.{propertyName}";

        if (!Inputs.TryGetValue(portName, out var port))
            return default!;

        var value = port
            .GetValue(EvaluationContext)
            .GetAwaiter()
            .GetResult();

        if (value is null)
            return default!;
        if (value is T typed)
            return typed;

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return default!;
        }
    }

    private static SKPoint[] BuildStarPoints(float cx, float cy, float outerRx, float outerRy, float innerRx,
        float innerRy, int vertexCount)
    {
        var points = new SKPoint[vertexCount];
        for (var i = 0; i < vertexCount; i++)
        {
            var angle = MathF.PI / 2f + i * (2f * MathF.PI / vertexCount);
            var useOuter = i % 2 == 0;
            var rx = useOuter ? outerRx : innerRx;
            var ry = useOuter ? outerRy : innerRy;
            points[i] = new SKPoint(cx + rx * MathF.Cos(angle), cy - ry * MathF.Sin(angle));
        }

        return points;
    }

    public override void Dispose()
    {
        _path?.Dispose();
        _path = null;
        base.Dispose();
    }
}

[Node(typeof(OutlineCategory), nameof(TextNode.TextOutlineNode), nameof(TextNode.TextOutlineNodeDescription),
    typeof(TextNode))]
public class TextOutlineNode : NodeLogic
{
    private SKPath? _path;

    [InputPort(nameof(TextNode.TextOutlineTextLabel), nameof(TextNode.TextOutlineTextDescription), typeof(TextNode))]
    [TextPortControl(Default = "テキスト")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string Text
    {
        get => GetInput<string>() ?? "";
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.FontFamilyLabel), nameof(TextNode.FontFamilyDescription), typeof(TextNode))]
    [LegacyInputPort]
    [FontComboBox]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string FontFamily
    {
        get
        {
            var name = GetInput<string>();
            if (string.IsNullOrEmpty(name))
                name = "メイリオ";
            return name;
        }
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.FontSizeLabel), nameof(TextNode.FontSizeDescription), typeof(TextNode))]
    [LegacyInputPort]
    [NumberPortControl(Min = 1, Max = 2000, Default = 64)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Size
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.BoldLabel), nameof(TextNode.BoldDescription), typeof(TextNode))]
    [LegacyInputPort]
    [BoolPortControl(Default = false)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public bool Bold
    {
        get => GetInput<bool>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.ItalicLabel), nameof(TextNode.ItalicDescription), typeof(TextNode))]
    [LegacyInputPort]
    [BoolPortControl(Default = false)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public bool Italic
    {
        get => GetInput<bool>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.TextStyleLabel), nameof(TextNode.TextStyleDescription), typeof(TextNode))]
    [TextStylePortControl]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public TextStyle Style
    {
        get
        {
            var value = GetInput<TextStyle>();
            return value ?? new TextStyle
            {
                FontFamily = FontFamily,
                Size = Math.Max(1f, Size),
                Weight = Bold ? 700 : 400,
                Width = 5,
                Slant = Italic ? TextFontSlant.Italic : TextFontSlant.Upright
            };
        }
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.TextStyleMapLabel), nameof(TextNode.TextStyleMapDescription), typeof(TextNode))]
    [TextStyleMapPortControl]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public TextStyleMap? StyleMap
    {
        get => GetInput<TextStyleMap>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.TextWritingDirectionLabel), nameof(TextNode.TextWritingDirectionDescription), typeof(TextNode))]
    [EnumPortControl(Default = 0, IsEditable = false, Items = typeof(TextWritingDirection))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public TextWritingDirection WritingDirection
    {
        get => GetInput<TextWritingDirection>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.TextOriginXLabel), nameof(TextNode.TextOriginXDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000, Default = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float OriginX
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.TextOriginYLabel), nameof(TextNode.TextOriginYDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000, Default = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float OriginY
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.TextHorizontalOffsetLabel), nameof(TextNode.TextHorizontalOffsetDescription),
        typeof(TextNode))]
    [NumberPortControl(Min = -1f, Max = 1f, Default = 0f)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float HorizontalOffset
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.TextVerticalOffsetLabel), nameof(TextNode.TextVerticalOffsetDescription),
        typeof(TextNode))]
    [NumberPortControl(Min = -1f, Max = 1f, Default = 0f)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float VerticalOffset
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.OutlinePortLabel), nameof(TextNode.TextOutlineOutputDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public OutlineWrapper? Output
    {
        get => GetOutput<OutlineWrapper>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        var style = Style;
        var weight = (SKFontStyleWeight)Math.Clamp(style.Weight, 1, 1000);
        var slant = style.Slant switch
        {
            TextFontSlant.Italic => SKFontStyleSlant.Italic,
            TextFontSlant.Oblique => SKFontStyleSlant.Oblique,
            _ => SKFontStyleSlant.Upright
        };
        var width = (SKFontStyleWidth)Math.Clamp(style.Width, 1, 9);
        using var typeface = SKTypeface.FromFamilyName(string.IsNullOrWhiteSpace(style.FontFamily) ? null : style.FontFamily,
            weight, width, slant);
        using var font = new SKFont(typeface, Math.Max(1f, style.Size)) { ScaleX = Math.Max(0.01f, style.ScaleX), SkewX = style.SkewX };

        var path = WritingDirection == TextWritingDirection.Horizontal
            ? BuildHorizontalPath(font, Text, StyleMap)
            : BuildVerticalPath(font, Text, StyleMap);
        var bounds = path.Bounds;
        var horizontalRatio = Math.Clamp((HorizontalOffset + 1f) / 2f, 0f, 1f);
        var verticalRatio = Math.Clamp((VerticalOffset + 1f) / 2f, 0f, 1f);
        var anchorX = bounds.Left + (bounds.Right - bounds.Left) * horizontalRatio;
        var anchorY = bounds.Top + (bounds.Bottom - bounds.Top) * verticalRatio;
        path.Transform(SKMatrix.CreateTranslation(OriginX - anchorX, OriginY - anchorY));
        _path?.Dispose();
        _path = path;
        Output = new OutlineWrapper { Path = _path };
        return Task.CompletedTask;
    }

    private static SKPath BuildHorizontalPath(SKFont fallbackFont, string text, TextStyleMap? map)
    {
        if (map == null || map.Ranges.Count == 0) return fallbackFont.GetTextPath(text, new SKPoint(0, 0));
        var result = new SKPath();
        var x = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            var style = map.Resolve(rune.Value, out var offset);
            using var tf = SKTypeface.FromFamilyName(style.FontFamily,
                (SKFontStyleWeight)Math.Clamp(style.Weight, 1, 1000),
                (SKFontStyleWidth)Math.Clamp(style.Width, 1, 9),
                style.Slant == TextFontSlant.Italic ? SKFontStyleSlant.Italic : style.Slant == TextFontSlant.Oblique ? SKFontStyleSlant.Oblique : SKFontStyleSlant.Upright);
            using var font = new SKFont(tf, Math.Max(1f, style.Size)) { ScaleX = Math.Max(0.01f, style.ScaleX), SkewX = style.SkewX };
            using var glyph = font.GetTextPath(rune.ToString(), new SKPoint(x, offset));
            result.AddPath(glyph);
            x += font.MeasureText(rune.ToString());
        }
        return result;
    }

    private static SKPath BuildVerticalPath(SKFont fallbackFont, string text, TextStyleMap? map)
    {
        if (map == null || map.Ranges.Count == 0)
        {
            var fallback = fallbackFont.GetTextPath(text, new SKPoint(0, 0));
            fallback.Transform(SKMatrix.CreateRotationDegrees(-90));
            return fallback;
        }
        var result = new SKPath();
        var y = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            var style = map.Resolve(rune.Value, out var offset);
            using var tf = SKTypeface.FromFamilyName(style.FontFamily,
                (SKFontStyleWeight)Math.Clamp(style.Weight, 1, 1000),
                (SKFontStyleWidth)Math.Clamp(style.Width, 1, 9),
                style.Slant == TextFontSlant.Italic ? SKFontStyleSlant.Italic : style.Slant == TextFontSlant.Oblique ? SKFontStyleSlant.Oblique : SKFontStyleSlant.Upright);
            using var font = new SKFont(tf, Math.Max(1f, style.Size));
            using var glyph = font.GetTextPath(rune.ToString(), new SKPoint(0, 0));
            glyph.Transform(SKMatrix.CreateRotationDegrees(-90));
            glyph.Transform(SKMatrix.CreateTranslation(offset, y));
            result.AddPath(glyph);
            y += font.Size;
        }
        return result;
    }

    public override void Dispose()
    {
        _path?.Dispose();
        _path = null;
        base.Dispose();
    }
}