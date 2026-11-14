using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Node.Editor.Control;
using SkiaSharp;

namespace Node.Editor.Control;

public sealed class FontWeightPort : PortControlBase
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(int),
            typeof(FontWeightPort),
            new FrameworkPropertyMetadata(400, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged));

    private readonly EnumPort _preset;
    private readonly NumberPort _number;
    private bool _updating;

    public FontWeightPort()
    {
        _preset = new EnumPort { Items = typeof(FontWeightPreset), Default = (int)FontWeightPreset.Normal };
        _number = new NumberPort { Min = 1, Max = 1000, Digits = 0, Default = 400 };

        _preset.BeginEditCommand = BeginEditCommand;
        _preset.EndEditCommand = EndEditCommand;
        _number.BeginEditCommand = BeginEditCommand;
        _number.EndEditCommand = EndEditCommand;

        var panel = new StackPanel { Orientation = Orientation.Vertical };
        panel.Children.Add(_preset);
        panel.Children.Add(_number);
        Content = panel;

        DependencyPropertyDescriptor.FromProperty(EnumPort.ValueProperty, typeof(EnumPort))!
            .AddValueChanged(_preset, (_, _) => PresetChanged());
        DependencyPropertyDescriptor.FromProperty(NumberPort.ValueProperty, typeof(NumberPort))!
            .AddValueChanged(_number, (_, _) => NumberChanged());

        UpdateEditor();
    }

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((FontWeightPort)d).UpdateEditor();
    }

    private void PresetChanged()
    {
        if (_updating)
            return;

        var preset = (FontWeightPreset)_preset.Value;
        if (preset != FontWeightPreset.Custom)
            Value = (int)preset;

        UpdateEditor();
    }

    private void NumberChanged()
    {
        if (_updating || (FontWeightPreset)_preset.Value != FontWeightPreset.Custom)
            return;

        Value = Math.Clamp(MathF.Round(_number.Value), 1, 1000);
    }

    private void UpdateEditor()
    {
        if (_updating)
            return;

        _updating = true;
        try
        {
            var value = Math.Clamp(Value, 1, 1000);
            _number.Value = value;

            _preset.Value = Enum.IsDefined(typeof(FontWeightPreset), value)
                ? value
                : (int)FontWeightPreset.Custom;

            _number.Visibility = (FontWeightPreset)_preset.Value == FontWeightPreset.Custom
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        finally
        {
            _updating = false;
        }
    }
}

public enum FontWeightPreset
{
    [System.ComponentModel.DataAnnotations.Display(Name = nameof(Node.Localize.TextNode.FontWeightCustom), ResourceType = typeof(Node.Localize.TextNode))]
    Custom = -1,
    [System.ComponentModel.DataAnnotations.Display(Name = nameof(Node.Localize.TextNode.FontWeightThin), ResourceType = typeof(Node.Localize.TextNode))]
    Thin = 100,
    [System.ComponentModel.DataAnnotations.Display(Name = nameof(Node.Localize.TextNode.FontWeightExtraLight), ResourceType = typeof(Node.Localize.TextNode))]
    ExtraLight = 200,
    [System.ComponentModel.DataAnnotations.Display(Name = nameof(Node.Localize.TextNode.FontWeightLight), ResourceType = typeof(Node.Localize.TextNode))]
    Light = 300,
    [System.ComponentModel.DataAnnotations.Display(Name = nameof(Node.Localize.TextNode.FontWeightNormal), ResourceType = typeof(Node.Localize.TextNode))]
    Normal = 400,
    [System.ComponentModel.DataAnnotations.Display(Name = nameof(Node.Localize.TextNode.FontWeightMedium), ResourceType = typeof(Node.Localize.TextNode))]
    Medium = 500,
    [System.ComponentModel.DataAnnotations.Display(Name = nameof(Node.Localize.TextNode.FontWeightSemiBold), ResourceType = typeof(Node.Localize.TextNode))]
    SemiBold = 600,
    [System.ComponentModel.DataAnnotations.Display(Name = nameof(Node.Localize.TextNode.FontWeightBold), ResourceType = typeof(Node.Localize.TextNode))]
    Bold = 700,
    [System.ComponentModel.DataAnnotations.Display(Name = nameof(Node.Localize.TextNode.FontWeightExtraBold), ResourceType = typeof(Node.Localize.TextNode))]
    ExtraBold = 800,
    [System.ComponentModel.DataAnnotations.Display(Name = nameof(Node.Localize.TextNode.FontWeightBlack), ResourceType = typeof(Node.Localize.TextNode))]
    Black = 900,
    [System.ComponentModel.DataAnnotations.Display(Name = nameof(Node.Localize.TextNode.FontWeightExtraBlack), ResourceType = typeof(Node.Localize.TextNode))]
    ExtraBlack = 1000
}

public sealed class FontWidthPort : PortControlBase
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(int),
            typeof(FontWidthPort),
            new FrameworkPropertyMetadata(5, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged));

    private readonly EnumPort _preset;
    private readonly NumberPort _number;
    private bool _updating;

    public FontWidthPort()
    {
        _preset = new EnumPort { Items = typeof(FontWidthPreset), Default = (int)FontWidthPreset.Normal };
        _number = new NumberPort { Min = 1, Max = 9, Digits = 0, Default = 5 };

        _preset.BeginEditCommand = BeginEditCommand;
        _preset.EndEditCommand = EndEditCommand;
        _number.BeginEditCommand = BeginEditCommand;
        _number.EndEditCommand = EndEditCommand;

        var panel = new StackPanel { Orientation = Orientation.Vertical };
        panel.Children.Add(_preset);
        panel.Children.Add(_number);
        Content = panel;

        DependencyPropertyDescriptor.FromProperty(EnumPort.ValueProperty, typeof(EnumPort))!
            .AddValueChanged(_preset, (_, _) => PresetChanged());
        DependencyPropertyDescriptor.FromProperty(NumberPort.ValueProperty, typeof(NumberPort))!
            .AddValueChanged(_number, (_, _) => NumberChanged());

        UpdateEditor();
    }

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((FontWidthPort)d).UpdateEditor();
    }

    private void PresetChanged()
    {
        if (_updating)
            return;

        var preset = (FontWidthPreset)_preset.Value;
        if (preset != FontWidthPreset.Custom)
            Value = (int)preset;

        UpdateEditor();
    }

    private void NumberChanged()
    {
        if (_updating || (FontWidthPreset)_preset.Value != FontWidthPreset.Custom)
            return;

        Value = Math.Clamp(MathF.Round(_number.Value), 1, 9);
    }

    private void UpdateEditor()
    {
        if (_updating)
            return;

        _updating = true;
        try
        {
            var value = Math.Clamp(Value, 1, 9);
            _number.Value = value;
            _preset.Value = Enum.IsDefined(typeof(FontWidthPreset), value)
                ? value
                : (int)FontWidthPreset.Custom;
            _number.Visibility = (FontWidthPreset)_preset.Value == FontWidthPreset.Custom
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        finally
        {
            _updating = false;
        }
    }
}

public enum FontWidthPreset
{
    [System.ComponentModel.DataAnnotations.Display(Name = nameof(Node.Localize.TextNode.FontWidthCustom), ResourceType = typeof(Node.Localize.TextNode))]
    Custom = -1,
    [System.ComponentModel.DataAnnotations.Display(Name = nameof(Node.Localize.TextNode.FontWidthUltraCondensed), ResourceType = typeof(Node.Localize.TextNode))]
    UltraCondensed = 1,
    [System.ComponentModel.DataAnnotations.Display(Name = nameof(Node.Localize.TextNode.FontWidthExtraCondensed), ResourceType = typeof(Node.Localize.TextNode))]
    ExtraCondensed = 2,
    [System.ComponentModel.DataAnnotations.Display(Name = nameof(Node.Localize.TextNode.FontWidthCondensed), ResourceType = typeof(Node.Localize.TextNode))]
    Condensed = 3,
    [System.ComponentModel.DataAnnotations.Display(Name = nameof(Node.Localize.TextNode.FontWidthSemiCondensed), ResourceType = typeof(Node.Localize.TextNode))]
    SemiCondensed = 4,
    [System.ComponentModel.DataAnnotations.Display(Name = nameof(Node.Localize.TextNode.FontWidthNormal), ResourceType = typeof(Node.Localize.TextNode))]
    Normal = 5,
    [System.ComponentModel.DataAnnotations.Display(Name = nameof(Node.Localize.TextNode.FontWidthSemiExpanded), ResourceType = typeof(Node.Localize.TextNode))]
    SemiExpanded = 6,
    [System.ComponentModel.DataAnnotations.Display(Name = nameof(Node.Localize.TextNode.FontWidthExpanded), ResourceType = typeof(Node.Localize.TextNode))]
    Expanded = 7,
    [System.ComponentModel.DataAnnotations.Display(Name = nameof(Node.Localize.TextNode.FontWidthExtraExpanded), ResourceType = typeof(Node.Localize.TextNode))]
    ExtraExpanded = 8,
    [System.ComponentModel.DataAnnotations.Display(Name = nameof(Node.Localize.TextNode.FontWidthUltraExpanded), ResourceType = typeof(Node.Localize.TextNode))]
    UltraExpanded = 9
}
