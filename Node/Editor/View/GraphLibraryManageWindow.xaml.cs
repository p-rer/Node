using System.Windows;
using Node.Editor.ViewModel;

namespace Node.Editor.View;

public partial class GraphLibraryManageWindow : Window
{
    public GraphLibraryManageWindow()
    {
        InitializeComponent();
        DataContext = GraphLibraryViewModel.Current;
    }
}