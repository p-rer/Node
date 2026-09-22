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