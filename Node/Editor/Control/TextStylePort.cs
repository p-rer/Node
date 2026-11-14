using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Node.Editor.Attributes;
using Node.Nodes.Generator.Outline;
using Node.Localize;
using YukkuriMovieMaker.Commons;
using SkiaSharp;

namespace Node.Editor.Control;

public sealed class TextStylePort : PortControlBase
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(TextStyle),
            typeof(TextStylePort),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged));

    private readonly StackPanel _panel = new() { Orientation = Orientation.Vertical };
    private TextStyle? _working;
    private bool _rebuilding;
    private bool _suppressValueRebuild;
    private FontWeightPort? _weightEditor;
    private FontWidthPort? _widthEditor;

    public TextStylePort()
    {
        Content = new ScrollViewer
        {
            Content = _panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Loaded += (_, _) => Rebuild();
    }

    public TextStyle? Value
    {
        get => (TextStyle?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public event EventHandler? ValueChanged;

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TextStylePort)d;
        if (!ReferenceEquals(e.OldValue, e.NewValue))
        {
            if (!control._suppressValueRebuild)
                control.Rebuild();
            control.ValueChanged?.Invoke(control, EventArgs.Empty);
        }
    }

    private void Rebuild()
    {
        if (_rebuilding)
            return;

        _rebuilding = true;
        try
        {
            DetachWorking();
            _working = (Value ?? new TextStyle()).Clone();
            _working.PropertyChanged += WorkingPropertyChanged;
            _panel.Children.Clear();

            AddYmmEditor(nameof(TextStyle.FontFamily), TextNode.FontFamilyLabel);
            AddNumberEditor(nameof(TextStyle.Size), TextNode.FontSizeLabel, 1, 2000, 0, "px", 64);
            _weightEditor = new FontWeightPort();
            _widthEditor = new FontWidthPort();
            AddCompositeEditor(nameof(TextStyle.Weight), TextNode.TextFontWeightLabel, _weightEditor);
            AddCompositeEditor(nameof(TextStyle.Width), TextNode.TextFontWidthLabel, _widthEditor);
            UpdateFontCapabilities();
            AddEnumEditor(nameof(TextStyle.Slant), TextNode.TextSlantLabel, typeof(TextFontSlant));
            AddNumberEditor(nameof(TextStyle.Scale), TextNode.TextScaleLabel, 0.1f, 10, 2, "", 1);
            AddNumberEditor(nameof(TextStyle.SkewX), TextNode.TextSkewLabel, -2, 2, 2, "", 0);
        }
        finally
        {
            _rebuilding = false;
        }
    }

    private void AddNumberEditor(string propertyName, string label, float min, float max, int digits, string unit, float defaultValue)
    {
        if (_working == null) return;
        var property = typeof(TextStyle).GetProperty(propertyName)!;
        var editor = new NumberPort { Min = min, Max = max, Digits = digits, Unit = unit, Default = defaultValue };
        editor.Value = Convert.ToSingle(property.GetValue(_working));
        editor.BeginEditCommand = BeginEditCommand;
        editor.EndEditCommand = EndEditCommand;
        DependencyPropertyDescriptor.FromProperty(NumberPort.ValueProperty, typeof(NumberPort))!.AddValueChanged(editor, (_, _) =>
        {
            if (!_rebuilding && _working != null) property.SetValue(_working, editor.Value);
        });
        AddRow(label, editor);
    }

    private void AddEnumEditor(string propertyName, string label, Type enumType)
    {
        if (_working == null) return;
        var property = typeof(TextStyle).GetProperty(propertyName)!;
        var editor = new EnumPort { Items = enumType, Default = Convert.ToInt32(property.GetValue(_working)) };
        editor.BeginEditCommand = BeginEditCommand;
        editor.EndEditCommand = EndEditCommand;
        DependencyPropertyDescriptor.FromProperty(EnumPort.ValueProperty, typeof(EnumPort))!.AddValueChanged(editor, (_, _) =>
        {
            if (!_rebuilding && _working != null) property.SetValue(_working, Enum.ToObject(enumType, editor.Value));
        });
        AddRow(label, editor);
    }

    private void AddCompositeEditor(string propertyName, string label, PortControlBase editor)
    {
        if (_working == null) return;
        var property = typeof(TextStyle).GetProperty(propertyName)!;
        if (editor is FontWeightPort weight)
        {
            weight.Value = Convert.ToInt32(property.GetValue(_working));
            DependencyPropertyDescriptor.FromProperty(FontWeightPort.ValueProperty, typeof(FontWeightPort))!.AddValueChanged(weight, (_, _) =>
            {
                if (!_rebuilding && _working != null) property.SetValue(_working, weight.Value);
            });
        }
        else if (editor is FontWidthPort width)
        {
            width.Value = Convert.ToInt32(property.GetValue(_working));
            DependencyPropertyDescriptor.FromProperty(FontWidthPort.ValueProperty, typeof(FontWidthPort))!.AddValueChanged(width, (_, _) =>
            {
                if (!_rebuilding && _working != null) property.SetValue(_working, width.Value);
            });
        }
        editor.BeginEditCommand = BeginEditCommand;
        editor.EndEditCommand = EndEditCommand;
        AddRow(label, editor);
    }

    private void AddRow(string label, FrameworkElement editor)
    {
        var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) };
        Grid.SetColumn(text, 0);
        Grid.SetColumn(editor, 1);
        row.Children.Add(text);
        row.Children.Add(editor);
        _panel.Children.Add(row);
    }
    private void AddYmmEditor(string propertyName, string label)
    {
        if (_working == null)
            return;

        var property = typeof(TextStyle).GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.Instance);
        var attribute = property?.GetCustomAttribute<PropertyEditorAttribute2>();
        if (property == null || attribute == null)
            return;

        FrameworkElement editor;
        try
        {
            editor = attribute.Create();
            attribute.SetBindings(editor, _working, _working, property);
        }
        catch
        {
            return;
        }

        if (editor is IPropertyEditorControl control)
        {
            control.BeginEdit += ChildBeginEdit;
            control.EndEdit += ChildEndEdit;
        }

        AddRow(label, editor);
    }

    private void WorkingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_rebuilding || _working == null)
            return;

        if (e.PropertyName == nameof(TextStyle.FontFamily))
            UpdateFontCapabilities();

        _suppressValueRebuild = true;
        try
        {
            Value = _working.Clone();
        }
        finally
        {
            _suppressValueRebuild = false;
        }
    }

    private void UpdateFontCapabilities()
    {
        if (_working == null)
            return;

        using var typeface = SKTypeface.FromFamilyName(_working.FontFamily);
        var axes = typeface?.VariationDesignParameters ?? [];
        var wght = axes.Any(axis => axis.Tag == SKFourByteTag.Parse("wght"));
        var wdth = axes.Any(axis => axis.Tag == SKFourByteTag.Parse("wdth"));

        if (_weightEditor != null)
            _weightEditor.IsVariableFont = wght;
        if (_widthEditor != null)
            _widthEditor.IsVariableFont = wdth;
    }

    private void ChildBeginEdit(object? sender, EventArgs e)
    {
        BeginEditCommand?.Execute(null);
    }

    private void ChildEndEdit(object? sender, EventArgs e)
    {
        EndEditCommand?.Execute(null);
    }

    private void DetachWorking()
    {
        if (_working == null)
            return;

        _working.PropertyChanged -= WorkingPropertyChanged;
        _working = null;
    }
}

public sealed class UnicodeRangeStylePort : PortControlBase
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(UnicodeRangeStyle),
            typeof(UnicodeRangeStylePort),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged));

    private readonly TextBox _start = new() { MinWidth = 78 };
    private readonly TextBox _end = new() { MinWidth = 78 };
    private readonly TextBox _offset = new() { MinWidth = 78 };
    private readonly TextStylePort _style = new();
    private bool _updating;

    public UnicodeRangeStylePort()
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical };

        panel.Children.Add(CreateRow("開始", _start));
        panel.Children.Add(CreateRow("終了", _end));
        panel.Children.Add(CreateRow("直交", _offset));
        panel.Children.Add(new TextBlock
        {
            Text = "文字スタイル",
            Margin = new Thickness(0, 4, 0, 1)
        });
        panel.Children.Add(_style);

        Content = panel;

        _start.LostFocus += (_, _) => Commit();
        _end.LostFocus += (_, _) => Commit();
        _offset.LostFocus += (_, _) => Commit();
        _style.ValueChanged += StyleChanged;
    }

    public UnicodeRangeStyle? Value
    {
        get => (UnicodeRangeStyle?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((UnicodeRangeStylePort)d).Refresh();
    }

    private void Refresh()
    {
        if (_updating)
            return;

        _updating = true;
        try
        {
            var value = Value ?? new UnicodeRangeStyle();
            _start.Text = $"U+{value.StartCodePoint:X4}";
            _end.Text = $"U+{value.EndCodePoint:X4}";
            _offset.Text = value.Offset.ToString(System.Globalization.CultureInfo.CurrentCulture);
            _style.Value = value.Style ?? new TextStyle();
        }
        finally
        {
            _updating = false;
        }
    }

    private void StyleChanged(object? sender, EventArgs e)
    {
        CommitPreservingEditor();
    }

    private void CommitPreservingEditor()
    {
        if (_updating)
            return;

        var start = ParseCodePoint(_start.Text);
        var end = ParseCodePoint(_end.Text);
        if (start == null || end == null || start > end)
            return;

        if (!float.TryParse(_offset.Text,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.CurrentCulture,
                out var offset))
            return;

        _updating = true;
        try
        {
            Value = new UnicodeRangeStyle
            {
                StartCodePoint = start.Value,
                EndCodePoint = end.Value,
                Offset = offset,
                Style = _style.Value?.CloneWithoutUnicodeMap() ?? new TextStyle()
            };
        }
        finally
        {
            _updating = false;
        }
    }

    private void Commit()
    {
        if (_updating)
            return;

        var start = ParseCodePoint(_start.Text);
        var end = ParseCodePoint(_end.Text);
        if (start == null || end == null || start > end)
            return;

        if (!float.TryParse(_offset.Text,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.CurrentCulture,
                out var offset))
            return;

        Value = new UnicodeRangeStyle
        {
            StartCodePoint = start.Value,
            EndCodePoint = end.Value,
            Offset = offset,
            Style = _style.Value?.CloneWithoutUnicodeMap() ?? new TextStyle()
        };
    }

    private static Grid CreateRow(string label, FrameworkElement editor)
    {
        var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var text = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 5, 0)
        };
        Grid.SetColumn(text, 0);
        Grid.SetColumn(editor, 1);
        row.Children.Add(text);
        row.Children.Add(editor);
        return row;
    }

    private static int? ParseCodePoint(string text)
    {
        var value = text.Trim();
        if (value.StartsWith("U+", StringComparison.OrdinalIgnoreCase))
            value = value[2..];
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            value = value[2..];

        return int.TryParse(value,
                   System.Globalization.NumberStyles.HexNumber,
                   System.Globalization.CultureInfo.InvariantCulture,
                   out var result) &&
               result is >= 0 and <= 0x10FFFF
            ? result
            : null;
    }
}
