using System.Windows;

namespace Node.Editor.View;

public partial class GraphNameInputWindow : Window
{
    public GraphNameInputWindow(string prompt, string defaultName)
    {
        InitializeComponent();
        PromptTextBlock.Text = prompt;
        NameTextBox.Text = defaultName;
        NameTextBox.SelectAll();
        Loaded += (_, _) => NameTextBox.Focus();
    }

    public string InputName { get; private set; } = "";

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        InputName = NameTextBox.Text;
        DialogResult = true;
    }

    public static bool TryGetName(Window owner, string prompt, string defaultName, out string name)
    {
        var dialog = new GraphNameInputWindow(prompt, defaultName) { Owner = owner };
        var result = dialog.ShowDialog();
        name = dialog.InputName;
        return result == true && !string.IsNullOrWhiteSpace(name);
    }
}