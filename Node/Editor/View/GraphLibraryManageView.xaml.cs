// 【一時無効化】プロジェクト共有グラフ機能の一時撤廃に伴い、このファイル全体をコメントアウトしている。
// 各エフェクトが固有のグラフを持つ方式へ変更したため、このクラスはどこからも参照されない。
// YMM4側に共有グラフ向けAPIが追加され、別方式で再実装する際に復元または置き換えること。
/*
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
*/

