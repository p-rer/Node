using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Node.Graph.Port;
using Node.Localize;
using Node.Nodes.Func;

namespace Node.Editor.View;

public partial class ArgumentPortsWindow
{
    private readonly ObservableCollection<PortItem> _items;
    private readonly PortEditOptions _options;

    private ArgumentPortsWindow(IEnumerable<PortDefinition> definitions, PortEditOptions options)
    {
        InitializeComponent();

        _options = options;
        Title = options.ListTitle;

        _items = new ObservableCollection<PortItem>(definitions.Select(d => new PortItem(d, options)));
        PortListBox.ItemsSource = _items;
        UpdateButtons();
    }

    public PortDefinition[] Result { get; private set; } = [];

    private PortItem? Selected => PortListBox.SelectedItem as PortItem;

    private bool HasReachedLimit => _options.HasLimit && CustomPortCount >= ArgumentPortKinds.MaxCustomPorts;

    private int CustomPortCount => _items.Count(item => item.Definition.IsCustom);

    private void PortListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var index = PortListBox.SelectedIndex;
        var selected = Selected;
        var isCustom = selected is { Definition.IsCustom: true };

        // 上限に達したら、それ以上の追加を受け付けない。上限は引数ポート（NodeEffect の引数スロット）のみに適用する。
        var atLimit = HasReachedLimit;
        AddButton.IsEnabled = !atLimit;
        AddButton.ToolTip = atLimit
            ? string.Format(TextUi.ArgumentPortLimitReached, ArgumentPortKinds.MaxCustomPorts)
            : null;

        EditButton.IsEnabled = isCustom;
        DeleteButton.IsEnabled = isCustom;
        UpButton.IsEnabled = isCustom && index > 0 && _items[index - 1].Definition.IsCustom;
        DownButton.IsEnabled = isCustom && index < _items.Count - 1;
    }

    private IReadOnlyCollection<string> TakenLabels(PortItem? except)
    {
        return _items.Where(item => !ReferenceEquals(item, except)).Select(item => item.Label).ToList();
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (HasReachedLimit) return;
        if (!ArgumentPortEditWindow.TryCreate(this, TakenLabels(null), out var definition, options: _options)) return;

        _items.Add(new PortItem(definition, _options));
        PortListBox.SelectedIndex = _items.Count - 1;
        UpdateButtons();
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { Definition.IsCustom: true } selected) return;
        if (!ArgumentPortEditWindow.TryEdit(this, selected.Definition, TakenLabels(selected), out var definition,
                options: _options))
            return;

        var index = _items.IndexOf(selected);
        _items[index] = new PortItem(definition, _options);
        PortListBox.SelectedIndex = index;
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { Definition.IsCustom: true } selected) return;

        var index = _items.IndexOf(selected);
        _items.RemoveAt(index);
        PortListBox.SelectedIndex = Math.Min(index, _items.Count - 1);
        UpdateButtons();
    }

    private void UpButton_Click(object sender, RoutedEventArgs e)
    {
        Move(-1);
    }

    private void DownButton_Click(object sender, RoutedEventArgs e)
    {
        Move(1);
    }

    private void Move(int offset)
    {
        var index = PortListBox.SelectedIndex;
        var newIndex = index + offset;
        if (index < 0 || newIndex < 0 || newIndex >= _items.Count) return;
        if (!_items[index].Definition.IsCustom || !_items[newIndex].Definition.IsCustom) return;

        _items.Move(index, newIndex);
        PortListBox.SelectedIndex = newIndex;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Result = _items.Select(item => item.Definition).ToArray();
        DialogResult = true;
    }

    public static bool TryEdit(Window owner, IEnumerable<PortDefinition> definitions, out PortDefinition[] result,
        bool textOnly = false, PortEditOptions? options = null)
    {
        var dialog = new ArgumentPortsWindow(definitions,
            options ?? (textOnly ? PortEditOptions.Text : PortEditOptions.Arguments)) { Owner = owner };
        var accepted = dialog.ShowDialog() == true;
        result = dialog.Result;
        return accepted;
    }

    private sealed class PortItem(PortDefinition definition, PortEditOptions options)
    {
        public PortDefinition Definition { get; } = definition;

        public string Label => Definition.GetDisplayLabel();

        public string DisplayText
        {
            get
            {
                var typeName = options.Types.FirstOrDefault(type => type.ValueType == Definition.ValueType)?.Name
                               ?? Definition.ValueType.Name;
                var fixedSuffix = Definition.IsCustom ? "" : $" [{TextUi.ArgumentPortFixedLabel}]";
                return $"{Label} ({typeName}){fixedSuffix}";
            }
        }
    }
}