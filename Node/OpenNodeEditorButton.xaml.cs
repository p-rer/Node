using System.Collections;
using System.Diagnostics;
using System.Windows;
using Node.Editor.ViewModel;
using Node.Graph;
using Node.Graph.Events;
using Node.Graph.Snapshot;
using Node.Localize;
using Node.Nodes.Effect.DynamicLoaded;
using Node.Nodes.Func;
using Node.Nodes.Generator.Brush;
using Node.Utility;
using Node.ValueTypes;
using YukkuriMovieMaker.Commons;
using PortDefinition = Node.Graph.Port.PortDefinition;

namespace Node;

public partial class OpenNodeEditorButton : IPropertyEditorControl2
{
    private EventHandler<CommittedEventArgs>? _committedHandler;
    private IEditorInfo? _editorInfo;
    private EventHandler? _graphUpdatedHandler;
    private NodeEditorViewModel? _lastResolvedViewModel;
    private NodeGraph? _subscribedGraph;
    private NodeEffect? _subscribedNodeEffect;

    public OpenNodeEditorButton()
    {
        InitializeComponent();
    }

    public ItemProperty[]? ItemProperties { get; set; }

    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;

    public void SetEditorInfo(IEditorInfo? info)
    {
        _editorInfo = info;

        if (_lastResolvedViewModel == null) return;
        if (ItemProperties is not { Length: > 0 } || ItemProperties[0].Item is not NodeEffect pluginItem) return;
        if (pluginItem.InternalGraph is not { } graph) return;

        _lastResolvedViewModel.UpdateEditorInfo(graph, info);
    }

    public void ReleaseSubscriptions()
    {
        if (_subscribedGraph != null && _committedHandler != null)
            _subscribedGraph.Committed -= _committedHandler;
        if (_subscribedNodeEffect != null && _graphUpdatedHandler != null)
            _subscribedNodeEffect.GraphUpdated -= _graphUpdatedHandler;
        _committedHandler = null;
        _graphUpdatedHandler = null;
        _subscribedGraph = null;
        _subscribedNodeEffect = null;
        _lastResolvedViewModel = null;
    }

    private static void EnsureInternalGraph(NodeEffect pluginItem)
    {
        if (pluginItem.InternalGraph is not null) return;

        lock (pluginItem.ProcessorInitializationLock)
        {
            if (pluginItem.InternalGraph is not null) return;

            if (pluginItem.Graph.Nodes.Count > 0)
            {
                pluginItem.InternalGraph = Serializer.Restore(pluginItem.Graph);
            }
            else
            {
                var graph = new NodeGraph();

                var inputNode = new ArgumentsNode(
                    new PortDefinition("InputImage", typeof(ImageWrapper)),
                    new PortDefinition("FrameIndex", typeof(int))
                )
                {
                    Id = Guid.NewGuid()
                };

                var outputNode = new ReturnNode(
                    new PortDefinition("OutputImage", typeof(ImageWrapper))
                )
                {
                    Id = Guid.NewGuid()
                };

                graph.AddNode(inputNode);
                graph.AddNode(outputNode);
                graph.SetVisualState(inputNode.Id, 100, 100);
                graph.SetVisualState(outputNode.Id, 500, 100);
                graph.Connect(inputNode.Id, "InputImage", outputNode.Id, "OutputImage");

                pluginItem.InternalGraph = graph;
                pluginItem.Graph = Serializer.Create(graph);
            }

            pluginItem.InvokeGraphUpdated();
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            EditButton_Click_Core();
        }
        catch (Exception ex) when (!ExceptionPolicy.IsFatal(ex))
        {
            Debug.WriteLine($"[OpenNodeEditorButton] Failed to open node editor: {ex}");
        }
    }

    private void EditButton_Click_Core()
    {
        if (ItemProperties is null) throw new InvalidOperationException(TextUi.ItemPropertiesNotSet);

        var pluginItem = (NodeEffect)ItemProperties[0].Item;

        // TODO: ツールウィンドウのハンドル取得・表示・アクティブ化は、YMM4本体側で公開APIが用意され次第、
        //       リフレクションをやめてそちらへ置き換える。
        var parentWindow = Window.GetWindow(this)!;
        var mainViewModel = parentWindow.DataContext!;

        var toolAreaViewModels =
            mainViewModel.GetType().GetProperty("AnchorableAreaViewModels")!.GetValue(mainViewModel) as
                IEnumerable;
        var toolAreaViewModel =
            toolAreaViewModels
                ?.Cast<object>()
                .FirstOrDefault(x => (string)x.GetType().GetProperty("Title")?.GetValue(x)! == TextUi.Node);

        toolAreaViewModel?.GetType().GetProperty("IsVisible")?.SetValue(toolAreaViewModel, true);
        toolAreaViewModel?.GetType().GetProperty("IsSelected")?.SetValue(toolAreaViewModel, true);
        toolAreaViewModel?.GetType().GetProperty("IsActive")?.SetValue(toolAreaViewModel, true);
        var vm =
            toolAreaViewModel?.GetType().GetProperty("ViewModel")?.GetValue(toolAreaViewModel) as NodeEditorViewModel;

        _lastResolvedViewModel = vm;

        EnsureInternalGraph(pluginItem);
        if (pluginItem.InternalGraph is null)
            throw new InvalidOperationException("グラフの解決に失敗しました。");

        vm?.OpenGraph(pluginItem.InternalGraph!, editorInfo: _editorInfo);

        if (vm != null)
        {
            var dynamicTypes = EffectNodeFactory.Create();
            vm.AddDynamicNodeTypes(dynamicTypes);
            var dynamicBrushTypes = DynamicBrushNodeFactory.Create();
            vm.AddDynamicNodeTypes(dynamicBrushTypes);
        }

        if (_subscribedGraph != null && _committedHandler != null)
            _subscribedGraph.Committed -= _committedHandler;
        if (_subscribedNodeEffect != null && _graphUpdatedHandler != null)
            _subscribedNodeEffect.GraphUpdated -= _graphUpdatedHandler;

        _committedHandler = async void (sender, _) =>
        {
            try
            {
                if (sender is not NodeGraph graph || !ReferenceEquals(pluginItem.InternalGraph, graph)) return;
                var stamp = pluginItem.CaptureGraphStamp();
                BeginEdit?.Invoke(this, EventArgs.Empty);
                try
                {
                    var snapshot = await Serializer.CreateAsync(graph);
                    if (ReferenceEquals(_subscribedNodeEffect, pluginItem))
                        pluginItem.TryCommitSnapshot(graph, stamp, snapshot);
                }
                finally
                {
                    EndEdit?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);
            }
        };

        var previousGraph = pluginItem.InternalGraph;
        _graphUpdatedHandler = (_, _) =>
        {
            try
            {
                var newGraph = pluginItem.InternalGraph;
                if (newGraph == null!) return;

                if (_committedHandler != null)
                {
                    newGraph.Committed -= _committedHandler;
                    newGraph.Committed += _committedHandler;
                }

                _subscribedGraph = newGraph;

                if (!ReferenceEquals(previousGraph, newGraph))
                {
                    if (previousGraph != null)
                    {
                        if (_committedHandler != null) previousGraph.Committed -= _committedHandler;
                        vm?.CloseGraphTab(previousGraph);
                    }

                    previousGraph = newGraph;
                }

                vm?.RefreshGraph(newGraph, _editorInfo);
            }
            catch (Exception ex) when (!ExceptionPolicy.IsFatal(ex))
            {
                Debug.WriteLine($"[OpenNodeEditorButton] GraphUpdated handler failed: {ex}");
            }
        };

        pluginItem.InternalGraph!.Committed += _committedHandler;
        pluginItem.GraphUpdated += _graphUpdatedHandler;
        _subscribedGraph = pluginItem.InternalGraph;
        _subscribedNodeEffect = pluginItem;
    }
}