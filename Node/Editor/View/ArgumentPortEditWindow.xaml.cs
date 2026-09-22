using System.Windows;
using Node.Graph.Port;
using Node.Nodes.Func;

namespace Node.Editor.View;

public partial class ArgumentPortEditWindow : Window
{
    private readonly PortDefinition? _existing;
    private readonly IReadOnlyCollection<string> _takenLabels;

    private ArgumentPortEditWindow(PortDefinition? existing, IReadOnlyCollection<string> takenLabels)
    {
        InitializeComponent();

        _existing = existing;
        _takenLabels = takenLabels;

        KindComboBox.ItemsSource = ArgumentPortKinds.All
            .Select(kind => new KindItem(kind, ArgumentPortKinds.GetDisplayName(kind)))
            .ToList();

        var initialKind = ArgumentPortKind.Number;
        if (existing != null && ArgumentPortKinds.TryGetKind(existing.ValueType, out var existingKind))
            initialKind = existingKind;

        KindComboBox.SelectedValue = initialKind;
        NameTextBox.Text = existing?.Label ?? "";
        NameTextBox.SelectAll();
        Loaded += (_, _) => NameTextBox.Focus();
    }

    public PortDefinition? Result { get; private set; }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var label = NameTextBox.Text.Trim();
        if (label.Length == 0 || _takenLabels.Contains(label))
        {
            ErrorTextBlock.Visibility = Visibility.Visible;
            return;
        }

        var kind = KindComboBox.SelectedValue is ArgumentPortKind selected ? selected : ArgumentPortKind.Number;
        var portType = ArgumentPortKinds.GetPortType(kind);

        if (_existing == null)
            Result = new PortDefinition(
                $"Arg_{Guid.NewGuid():N}"[..12],
                portType,
                label,
                "",
                ArgumentPortKinds.GetDefaultStorageValue(kind),
                true);
        else
            Result = _existing with
            {
                Label = label,
                ValueType = portType,
                DefaultValue = _existing.ValueType == portType
                    ? _existing.DefaultValue
                    : ArgumentPortKinds.GetDefaultStorageValue(kind)
            };

        DialogResult = true;
    }

    public static bool TryCreate(Window owner, IReadOnlyCollection<string> takenLabels, out PortDefinition definition)
    {
        return Show(owner, null, takenLabels, out definition);
    }

    public static bool TryEdit(Window owner, PortDefinition existing, IReadOnlyCollection<string> takenLabels,
        out PortDefinition definition)
    {
        return Show(owner, existing, takenLabels, out definition);
    }

    private static bool Show(Window owner, PortDefinition? existing, IReadOnlyCollection<string> takenLabels,
        out PortDefinition definition)
    {
        var dialog = new ArgumentPortEditWindow(existing, takenLabels) { Owner = owner };
        var accepted = dialog.ShowDialog() == true && dialog.Result != null;
        definition = dialog.Result!;
        return accepted;
    }

    private sealed record KindItem(ArgumentPortKind Kind, string Name);
}