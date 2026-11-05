// 【一時無効化】プロジェクト共有グラフ機能の一時撤廃に伴い、このファイル全体をコメントアウトしている。
// 各エフェクトが固有のグラフを持つ方式へ変更したため、このクラスはどこからも参照されない。
// YMM4側に共有グラフ向けAPIが追加され、別方式で再実装する際に復元または置き換えること。
/*
using Node.Editor.View;
using Node.Editor.ViewModel;
using Node.Localize;
using YukkuriMovieMaker.Plugin;

namespace Node.Editor;

public class GraphLibraryTool : IToolPlugin
{
    public string Name => TextUi.GraphLibraryToolName;
    public Type ViewModelType => typeof(GraphLibraryViewModel);
    public Type ViewType => typeof(GraphLibraryManageView);

    public bool AllowMultipleInstances => false;
}
*/

