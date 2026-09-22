using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Node.Graph.Port;
using Node.Localize;
using Node.Nodes.Func;

namespace Node.Editor.View;

public partial class ArgumentPortsWindow : Window
{
    private readonly ObservableCollection<PortItem> _items;

    private ArgumentPortsWindow(IEnumerable<PortDefinition> definitions)
    {
        InitializeComponent();

        _items = new ObservableCollection<PortItem>(definitions.Select(d => new PortItem(d)));
        PortListBox.ItemsSource = _items;
        UpdateButtons();
    }

    public PortDefinition[] Result { get; private set; } = [];

    private PortItem? Selected => PortListBox.SelectedItem as PortItem;

    private void PortListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var index = PortListBox.SelectedIndex;
        var selected = Selected;
        var isCustom = selected is { Definition.IsCustom: true };

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
        if (!ArgumentPortEditWindow.TryCreate(this, TakenLabels(null), out var definition)) return;

        _items.Add(new PortItem(definition));
        PortListBox.SelectedIndex = _items.Count - 1;
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { Definition.IsCustom: true } selected) return;
        if (!ArgumentPortEditWindow.TryEdit(this, selected.Definition, TakenLabels(selected), out var definition))
            return;

        var index = _items.IndexOf(selected);
        _items[index] = new PortItem(definition);
        PortListBox.SelectedIndex = index;
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { Definition.IsCustom: true } selected) return;

        var index = _items.IndexOf(selected);
        _items.RemoveAt(index);
        PortListBox.SelectedIndex = Math.Min(index, _items.Count - 1);
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

    public static bool TryEdit(Window owner, IEnumerable<PortDefinition> definitions, out PortDefinition[] result)
    {
        var dialog = new ArgumentPortsWindow(definitions) { Owner = owner };
        var accepted = dialog.ShowDialog() == true;
        result = dialog.Result;
        return accepted;
    }

    private sealed class PortItem(PortDefinition definition)
    {
        public PortDefinition Definition { get; } = definition;

        public string Label => string.IsNullOrEmpty(Definition.Label) ? Definition.Name : Definition.Label;

        public string DisplayText
        {
            get
            {
                var typeName = ArgumentPortKinds.TryGetKind(Definition.ValueType, out var kind)
                    ? ArgumentPortKinds.GetDisplayName(kind)
                    : Definition.ValueType.Name;
                var fixedSuffix = Definition.IsCustom ? "" : $" [{TextUi.ArgumentPortFixedLabel}]";
                return $"{Label} ({typeName}){fixedSuffix}";
            }
        }
    }
}