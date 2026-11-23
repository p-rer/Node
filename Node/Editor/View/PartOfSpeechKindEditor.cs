using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Node.Graph.Port;
using Node.Localize;
using Node.Nodes.Generator.Outline;

namespace Node.Editor.View;

/// <summary>
///     品詞別文字スタイルノードの端子の種類設定。対象にする品詞をチェックボックスで複数選択する。
/// </summary>
public sealed class PartOfSpeechKindEditor : IPortKindEditor
{
    private readonly UniformGrid _grid = new() { Columns = 2 };
    private readonly List<(JapanesePartOfSpeech PartOfSpeech, CheckBox CheckBox)> _items = [];

    public PartOfSpeechKindEditor()
    {
        foreach (var partOfSpeech in Enum.GetValues<JapanesePartOfSpeech>())
        {
            var checkBox = new CheckBox
            {
                Content = PartOfSpeechPortSettings.GetDisplayName(partOfSpeech),
                Margin = new Thickness(0, 0, 8, 4)
            };
            _items.Add((partOfSpeech, checkBox));
            _grid.Children.Add(checkBox);
        }
    }

    public FrameworkElement Content => _grid;

    public void Load(PortDefinition? existing)
    {
        var selected = existing == null
            ? new List<JapanesePartOfSpeech>()
            : PartOfSpeechPortSettings.Parse(existing.Settings).PartsOfSpeech;

        foreach (var (partOfSpeech, checkBox) in _items)
            checkBox.IsChecked = selected.Contains(partOfSpeech);
    }

    public string? Validate()
    {
        return _items.Any(item => item.CheckBox.IsChecked == true) ? null : TextUi.PartOfSpeechPortNoneSelected;
    }

    public PortDefinition Apply(PortDefinition definition)
    {
        var selected = _items.Where(item => item.CheckBox.IsChecked == true).Select(item => item.PartOfSpeech);
        return definition with { Settings = PartOfSpeechPortSettings.Serialize(selected) };
    }
}