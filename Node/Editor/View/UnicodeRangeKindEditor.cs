using System.Windows;
using System.Windows.Controls;
using Node.Graph.Port;
using Node.Localize;
using Node.Nodes.Generator.Outline;

namespace Node.Editor.View;

/// <summary>
///     Unicode範囲スタイルノードの端子の種類設定。1つの端子に対して複数のコードポイント範囲を指定する。
/// </summary>
public sealed class UnicodeRangeKindEditor : IPortKindEditor
{
    private readonly Button _addButton;
    private readonly StackPanel _root = new();
    private readonly List<RangeRow> _rows = [];
    private readonly StackPanel _rowsPanel = new();

    public UnicodeRangeKindEditor()
    {
        _addButton = new Button
        {
            Content = TextUi.UnicodeRangeAddLabel,
            MinWidth = 80,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 2, 0, 0)
        };
        _addButton.Click += (_, _) => AddRow(new CodePointRange(0, 0x7F));

        // 範囲が増えてもダイアログが縦に伸び続けないよう、一覧だけスクロールさせる。
        _root.Children.Add(new ScrollViewer
        {
            Content = _rowsPanel,
            MaxHeight = 150,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        });
        _root.Children.Add(_addButton);
    }

    public FrameworkElement Content => _root;

    public void Load(PortDefinition? existing)
    {
        _rows.Clear();
        _rowsPanel.Children.Clear();

        var ranges = existing == null
            ? new List<CodePointRange>()
            : UnicodeRangePortSettings.Parse(existing.Settings).Ranges;
        if (ranges.Count == 0)
            ranges = [new CodePointRange(0, 0x7F)];

        foreach (var range in ranges)
            AddRow(range);
    }

    public string? Validate()
    {
        return TryReadRanges(out _) ? null : TextUi.UnicodeRangeInvalid;
    }

    public PortDefinition Apply(PortDefinition definition)
    {
        TryReadRanges(out var ranges);
        return definition with { Settings = UnicodeRangePortSettings.Serialize(ranges) };
    }

    private bool TryReadRanges(out List<CodePointRange> ranges)
    {
        ranges = [];
        foreach (var row in _rows)
        {
            if (!CodePointRange.TryParseCodePoint(row.Start.Text, out var start) ||
                !CodePointRange.TryParseCodePoint(row.End.Text, out var end) ||
                start > end)
                return false;

            ranges.Add(new CodePointRange(start, end));
        }

        return ranges.Count > 0;
    }

    private void AddRow(CodePointRange range)
    {
        var row = new RangeRow(
            new TextBox
            {
                Text = CodePointRange.FormatHex(range.Start), VerticalContentAlignment = VerticalAlignment.Center
            },
            new TextBox
            {
                Text = CodePointRange.FormatHex(range.End), VerticalContentAlignment = VerticalAlignment.Center
            },
            new Button { Content = "×", Width = 24, Margin = new Thickness(6, 0, 0, 0) });

        var grid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        AddCell(grid, CreateText("U+", new Thickness(0, 0, 2, 0)), 0);
        AddCell(grid, row.Start, 1);
        AddCell(grid, CreateText("～", new Thickness(6, 0, 6, 0)), 2);
        AddCell(grid, CreateText("U+", new Thickness(0, 0, 2, 0)), 3);
        AddCell(grid, row.End, 4);
        AddCell(grid, row.Remove, 5);

        row.Remove.Click += (_, _) => RemoveRow(row, grid);

        _rows.Add(row);
        _rowsPanel.Children.Add(grid);
        UpdateRemoveButtons();
    }

    private void RemoveRow(RangeRow row, UIElement element)
    {
        // 範囲は1つ以上必要。
        if (_rows.Count <= 1)
            return;

        _rows.Remove(row);
        _rowsPanel.Children.Remove(element);
        UpdateRemoveButtons();
    }

    private void UpdateRemoveButtons()
    {
        foreach (var row in _rows)
            row.Remove.IsEnabled = _rows.Count > 1;
    }

    private static TextBlock CreateText(string text, Thickness margin)
    {
        return new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = margin };
    }

    private static void AddCell(Grid grid, UIElement element, int column)
    {
        Grid.SetColumn(element, column);
        grid.Children.Add(element);
    }

    private sealed record RangeRow(TextBox Start, TextBox End, Button Remove);
}