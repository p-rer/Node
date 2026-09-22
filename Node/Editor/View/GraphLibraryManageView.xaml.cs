using System.Windows;
using System.Windows.Controls;
using Node.Editor.ViewModel;
using Node.Graph.Snapshot;
using Node.Localize;

namespace Node.Editor.View;

public partial class GraphLibraryManageView
{
    public GraphLibraryManageView()
    {
        InitializeComponent();
    }

    private GraphLibraryViewModel? ViewModel => DataContext as GraphLibraryViewModel;

    private void MoveUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: NamedGraphSnapshot entry })
            ViewModel?.Move(entry.Id, -1);
    }

    private void MoveDownButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: NamedGraphSnapshot entry })
            ViewModel?.Move(entry.Id, 1);
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: NamedGraphSnapshot entry }) return;

        var result = MessageBox.Show(
            string.Format(TextUi.DeleteGraphConfirmMessage, entry.Name),
            TextUi.DeleteGraphConfirmTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        ViewModel?.Delete(entry.Id);
    }
}