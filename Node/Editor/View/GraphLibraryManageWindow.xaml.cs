// 【一時無効化】プロジェクト共有グラフ機能の一時撤廃に伴い、このファイル全体をコメントアウトしている。
// 各エフェクトが固有のグラフを持つ方式へ変更したため、このクラスはどこからも参照されない。
// YMM4側に共有グラフ向けAPIが追加され、別方式で再実装する際に復元または置き換えること。
/*
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
*/

