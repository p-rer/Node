using System.Windows;
using System.Windows.Controls;
using Node.Editor.Attributes;
using Node.Nodes.Generator.Outline;

namespace Node.Editor.Control;

public sealed class TextStylePort : PortControlBase
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(TextStyle), typeof(TextStylePort),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));
    private readonly Button _button = new();
    public TextStylePort()
    {
        _button.Padding = new Thickness(6, 2, 6, 2);
        _button.MinWidth = 110;
        _button.HorizontalContentAlignment = HorizontalAlignment.Left;
        _button.Click += OnClick;
        Content = _button;
        UpdateLabel();
    }
    public TextStyle? Value { get => (TextStyle?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((TextStylePort)d).UpdateLabel();
    private void UpdateLabel() => _button.Content = $"{Value?.FontFamily ?? "メイリオ"} / {Value?.Size ?? 64:0.##}";
    private void OnClick(object sender, RoutedEventArgs e)
    {
        BeginEditCommand?.Execute(null);
        try
        {
            var dialog = new TextStyleEditorWindow(Value ?? new TextStyle()) { Owner = Window.GetWindow(this) ?? Application.Current?.MainWindow };
            if (dialog.ShowDialog() == true) Value = dialog.Result;
        }
        finally { EndEditCommand?.Execute(null); }
    }
}

public sealed class TextStyleMapPort : PortControlBase
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(TextStyleMap), typeof(TextStyleMapPort),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));
    private readonly Button _button = new();
    public TextStyleMapPort()
    {
        _button.Padding = new Thickness(6, 2, 6, 2);
        _button.MinWidth = 110;
        _button.HorizontalContentAlignment = HorizontalAlignment.Left;
        _button.Click += (_, _) => { };
        Content = _button;
        UpdateLabel();
    }
    public TextStyleMap? Value { get => (TextStyleMap?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((TextStyleMapPort)d).UpdateLabel();
    private void UpdateLabel() => _button.Content = Value == null ? "Unicode Style Map" : $"Unicode Style Map ({Value.Ranges.Count})";
}

public sealed class UnicodeRangeStylePort : PortControlBase
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(UnicodeRangeStyle), typeof(UnicodeRangeStylePort),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));
    private readonly Button _button = new();
    public UnicodeRangeStylePort()
    {
        _button.Padding = new Thickness(6, 2, 6, 2);
        _button.MinWidth = 130;
        _button.HorizontalContentAlignment = HorizontalAlignment.Left;
        _button.Click += OnClick;
        Content = _button;
        UpdateLabel();
    }
    public UnicodeRangeStyle? Value { get => (UnicodeRangeStyle?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((UnicodeRangeStylePort)d).UpdateLabel();
    private void UpdateLabel() => _button.Content = Value == null ? "Unicode Range" : $"U+{Value.StartCodePoint:X4}–U+{Value.EndCodePoint:X4}";
    private void OnClick(object sender, RoutedEventArgs e)
    {
        BeginEditCommand?.Execute(null);
        try
        {
            var dialog = new UnicodeRangeStyleEditorWindow(Value ?? new UnicodeRangeStyle()) { Owner = Window.GetWindow(this) ?? Application.Current?.MainWindow };
            if (dialog.ShowDialog() == true) Value = dialog.Result;
        }
        finally { EndEditCommand?.Execute(null); }
    }
}
