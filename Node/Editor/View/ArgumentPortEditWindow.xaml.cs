using System.Windows;
using Node.Graph.Port;
using Node.Localize;

namespace Node.Editor.View;

public partial class ArgumentPortEditWindow
{
    private readonly PortDefinition? _existing;
    private readonly IPortKindEditor? _kindEditor;
    private readonly PortEditOptions _options;
    private readonly IReadOnlyCollection<string> _takenLabels;

    private ArgumentPortEditWindow(PortDefinition? existing, IReadOnlyCollection<string> takenLabels,
        PortEditOptions options)
    {
        InitializeComponent();

        _existing = existing;
        _takenLabels = takenLabels;
        _options = options;

        // 選べる型は呼び出し側（引数・文字列結合・Node エフェクトノードの端子）ごとに決まる。
        KindComboBox.ItemsSource = options.Types
            .Select(type => new KindItem(type, type.Name))
            .ToList();

        var initialType = options.Types[0];
        if (existing != null)
            initialType = options.Types.FirstOrDefault(type => type.ValueType == existing.ValueType) ?? initialType;

        KindComboBox.SelectedValue = initialType;
        KindComboBox.IsEnabled = options.Types.Count > 1;

        // 種類設定部分を差し替えるエディタがある場合は、型の選択の代わりにそのエディタを表示する。
        if (options.KindEditorFactory != null)
        {
            _kindEditor = options.KindEditorFactory();
            _kindEditor.Load(existing);
            KindComboBox.Visibility = Visibility.Collapsed;
            KindEditorHost.Content = _kindEditor.Content;
            KindEditorHost.Visibility = Visibility.Visible;
            Width = 360;
        }

        if (options.KindLabel != null)
            KindLabelTextBlock.Text = options.KindLabel;
        Title = options.ItemTitle;
        NameTextBox.Text = existing?.GetDisplayLabel() ?? "";
        NameTextBox.SelectAll();
        Loaded += (_, _) => NameTextBox.Focus();
    }

    public PortDefinition? Result { get; private set; }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var label = NameTextBox.Text.Trim();
        if (label.Length == 0 || _takenLabels.Contains(label))
        {
            ShowError(TextUi.ArgumentPortNameInvalid);
            return;
        }

        if (_kindEditor?.Validate() is { } kindError)
        {
            ShowError(kindError);
            return;
        }

        var selectedType = KindComboBox.SelectedValue as PortTypeOption ?? _options.Types[0];
        var portType = selectedType.ValueType;

        // 組み込みの既定名（Label が空でローカライズ名を表示している端子）を変更しなかった場合は、
        // 表示名を固定の文字列として保存せず、言語切り替えに追従できる状態（Label が空）のままにする。
        if (_existing != null && string.IsNullOrEmpty(_existing.Label) && label == _existing.GetDisplayLabel())
            label = "";

        if (_existing == null)
            Result = new PortDefinition(
                _options.NamePrefix + Guid.NewGuid().ToString("N")[..8],
                portType,
                label,
                "",
                selectedType.CreateDefault(),
                true);
        else
            Result = _existing with
            {
                Label = label,
                ValueType = portType,
                DefaultValue = _existing.ValueType == portType
                    ? _existing.DefaultValue
                    : selectedType.CreateDefault()
            };

        if (_kindEditor != null)
            Result = _kindEditor.Apply(Result);

        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.Visibility = Visibility.Visible;
    }

    public static bool TryCreate(Window owner, IReadOnlyCollection<string> takenLabels, out PortDefinition definition,
        bool textOnly = false, PortEditOptions? options = null)
    {
        return Show(owner, null, takenLabels, out definition, options ?? Resolve(textOnly));
    }

    public static bool TryEdit(Window owner, PortDefinition existing, IReadOnlyCollection<string> takenLabels,
        out PortDefinition definition, bool textOnly = false, PortEditOptions? options = null)
    {
        return Show(owner, existing, takenLabels, out definition, options ?? Resolve(textOnly));
    }

    private static bool Show(Window owner, PortDefinition? existing, IReadOnlyCollection<string> takenLabels,
        out PortDefinition definition, PortEditOptions options)
    {
        var dialog = new ArgumentPortEditWindow(existing, takenLabels, options) { Owner = owner };
        var accepted = dialog.ShowDialog() == true && dialog.Result != null;
        definition = dialog.Result!;
        return accepted;
    }

    private static PortEditOptions Resolve(bool textOnly)
    {
        return textOnly ? PortEditOptions.Text : PortEditOptions.Arguments;
    }

    private sealed record KindItem(PortTypeOption Kind, string Name);
}