using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Node.Nodes.Generator.Outline;

namespace Node.Editor.Control;

internal sealed class TextStyleEditorPanel : StackPanel
{
    private readonly TextBox _fontFamily = new();
    private readonly TextBox _size = new();
    private readonly ComboBox _weight = new();
    private readonly ComboBox _width = new();
    private readonly ComboBox _slant = new();
    private readonly TextBox _scaleX = new();
    private readonly TextBox _skewX = new();

    public TextStyleEditorPanel(TextStyle value)
    {
        Orientation = Orientation.Vertical;
        _fontFamily.Text = value.FontFamily;
        _size.Text = value.Size.ToString(CultureInfo.CurrentCulture);
        _weight.ItemsSource = Enumerable.Range(1, 9).Select(i => i * 100).ToArray();
        _weight.SelectedItem = value.Weight;
        _width.ItemsSource = Enumerable.Range(1, 9).ToArray();
        _width.SelectedItem = value.Width;
        _slant.ItemsSource = Enum.GetValues<TextFontSlant>();
        _slant.SelectedItem = value.Slant;
        _scaleX.Text = value.ScaleX.ToString(CultureInfo.CurrentCulture);
        _skewX.Text = value.SkewX.ToString(CultureInfo.CurrentCulture);
        AddRow("フォント", _fontFamily); AddRow("サイズ", _size); AddRow("太さ", _weight); AddRow("幅", _width);
        AddRow("傾き", _slant); AddRow("横倍率", _scaleX); AddRow("せん断", _skewX);
    }

    public TextStyle CreateValue() => new()
    {
        FontFamily = string.IsNullOrWhiteSpace(_fontFamily.Text) ? "メイリオ" : _fontFamily.Text.Trim(),
        Size = Math.Clamp(Parse(_size.Text, 64f), 1f, 2000f),
        Weight = _weight.SelectedItem is int w ? w : 400,
        Width = _width.SelectedItem is int width ? width : 5,
        Slant = _slant.SelectedItem is TextFontSlant s ? s : TextFontSlant.Upright,
        ScaleX = Math.Clamp(Parse(_scaleX.Text, 1f), 0.1f, 10f),
        SkewX = Math.Clamp(Parse(_skewX.Text, 0f), -2f, 2f)
    };
    private void AddRow(string label, FrameworkElement editor)
    {
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        Grid.SetColumn(text, 0); Grid.SetColumn(editor, 1); grid.Children.Add(text); grid.Children.Add(editor); Children.Add(grid);
    }
    private static float Parse(string text, float fallback) => float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) ? value : fallback;
}

internal sealed class TextStyleEditorWindow : Window
{
    private readonly TextStyleEditorPanel _editor;
    public TextStyleEditorWindow(TextStyle initial)
    {
        Title = "文字スタイル";
        Width = 420; Height = 390; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _editor = new TextStyleEditorPanel(initial);
        var ok = new Button { Content = "OK", Width = 80, IsDefault = true, Margin = new Thickness(0, 0, 6, 0) };
        ok.Click += (_, _) => { Result = _editor.CreateValue(); DialogResult = true; };
        var cancel = new Button { Content = "キャンセル", Width = 80, IsCancel = true };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ok); buttons.Children.Add(cancel);
        var root = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons); root.Children.Add(new ScrollViewer { Content = _editor });
        Content = root;
    }
    public TextStyle Result { get; private set; } = new();
}

internal sealed class UnicodeRangeStyleEditorWindow : Window
{
    private readonly TextBox _start = new(), _end = new(), _offset = new();
    private readonly TextStyleEditorPanel _style;
    public UnicodeRangeStyleEditorWindow(UnicodeRangeStyle initial)
    {
        Title = "Unicode 範囲スタイル";
        Width = 440; Height = 620; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _start.Text = $"U+{initial.StartCodePoint:X4}"; _end.Text = $"U+{initial.EndCodePoint:X4}";
        _offset.Text = initial.Offset.ToString(CultureInfo.CurrentCulture); _style = new TextStyleEditorPanel(initial.Style ?? new TextStyle());
        var content = new StackPanel();
        AddRow(content, "開始", _start); AddRow(content, "終了", _end); AddRow(content, "直交オフセット", _offset);
        content.Children.Add(new TextBlock { Text = "文字スタイル", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 4) });
        content.Children.Add(_style);
        var ok = new Button { Content = "OK", Width = 80, IsDefault = true, Margin = new Thickness(0, 0, 6, 0) };
        ok.Click += (_, _) => { if (TryCreate(out var result)) { Result = result; DialogResult = true; } };
        var cancel = new Button { Content = "キャンセル", Width = 80, IsCancel = true };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ok); buttons.Children.Add(cancel);
        var root = new DockPanel { Margin = new Thickness(12) }; DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
        root.Children.Add(new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); Content = root;
    }
    public UnicodeRangeStyle Result { get; private set; } = new();
    private bool TryCreate(out UnicodeRangeStyle result)
    {
        result = new();
        var s = ParseCodePoint(_start.Text); var e = ParseCodePoint(_end.Text);
        if (s is null || e is null || s > e) return false;
        if (!float.TryParse(_offset.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var offset)) return false;
        result = new UnicodeRangeStyle { StartCodePoint = s.Value, EndCodePoint = e.Value, Offset = offset, Style = _style.CreateValue() };
        return true;
    }
    private static int? ParseCodePoint(string text)
    {
        var s = text.Trim();
        if (s.StartsWith("U+", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        return int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value) && value is >= 0 and <= 0x10FFFF ? value : null;
    }
    private static void AddRow(Panel panel, string label, FrameworkElement editor)
    {
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        Grid.SetColumn(text, 0); Grid.SetColumn(editor, 1); grid.Children.Add(text); grid.Children.Add(editor); panel.Children.Add(grid);
    }
}
