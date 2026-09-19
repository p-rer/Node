using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AvalonDock;
using AvalonDock.Layout;
using Node.Editor.View;
using Node.Editor.ViewModel;
using Node.Graph;
using Node.Graph.Events;
using Node.Graph.Port;
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
    private Window? _boundWindow;
    private EventHandler<CommittedEventArgs>? _committedHandler;
    private IEditorInfo? _editorInfo;
    private EventHandler? _graphUpdatedHandler;
    private ItemProperty[]? _itemProperties;
    private NodeEditorViewModel? _lastResolvedViewModel;
    private EventHandler? _layoutIsActiveChangedHandler;
    private EventHandler? _layoutIsSelectedChangedHandler;
    private KeyBinding[]? _nodeBindings;
    private NodeGraph? _subscribedGraph;
    private LayoutAnchorable? _subscribedLayout;
    private NodeEffect? _subscribedNodeEffect;
    private bool _suppressSelectionChanged;

    private int _syncRetryCount;

    public OpenNodeEditorButton()
    {
        InitializeComponent();
    }

    public ItemProperty[]? ItemProperties
    {
        get => _itemProperties;
        set
        {
            _itemProperties = value;
            SyncComboBox();
        }
    }

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
        ReleaseShortcutBindings();

        _committedHandler = null;
        _graphUpdatedHandler = null;
        _subscribedGraph = null;
        _subscribedNodeEffect = null;
        _lastResolvedViewModel = null;
    }

    private void ReleaseShortcutBindings()
    {
        if (_subscribedLayout != null && _layoutIsActiveChangedHandler != null)
            _subscribedLayout.IsActiveChanged -= _layoutIsActiveChangedHandler;
        if (_subscribedLayout != null && _layoutIsSelectedChangedHandler != null)
            _subscribedLayout.IsSelectedChanged -= _layoutIsSelectedChangedHandler;

        if (_boundWindow != null && _nodeBindings != null)
            foreach (var kb in _nodeBindings)
                _boundWindow.InputBindings.Remove(kb);

        _subscribedLayout = null;
        _layoutIsActiveChangedHandler = null;
        _layoutIsSelectedChangedHandler = null;
        _nodeBindings = null;
        _boundWindow = null;
    }

    private void SyncComboBox()
    {
        _suppressSelectionChanged = true;
        try
        {
            var library = GraphLibraryViewModel.Current;
            GraphComboBox.ItemsSource = library?.Entries;

            if (ItemProperties is not { Length: > 0 } || ItemProperties[0].Item is not NodeEffect pluginItem)
            {
                GraphComboBox.SelectedValue = null;
                return;
            }

            GraphComboBox.SelectedValue = pluginItem.GraphId;

            if (library is null && _syncRetryCount < 50)
            {
                _syncRetryCount++;
                Dispatcher.BeginInvoke(SyncComboBox, DispatcherPriority.Background);
            }
            else
            {
                _syncRetryCount = 0;
            }
        }
        finally
        {
            _suppressSelectionChanged = false;
        }
    }

    private void GraphComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionChanged) return;
        if (ItemProperties is not { Length: > 0 } || ItemProperties[0].Item is not NodeEffect pluginItem) return;
        if (GraphComboBox.SelectedValue is not Guid newGraphId) return;
        if (newGraphId == pluginItem.GraphId) return;

        BeginEdit?.Invoke(this, EventArgs.Empty);
        try
        {
            pluginItem.SwitchGraph(newGraphId);
        }
        catch (Exception ex) when (!ExceptionPolicy.IsFatal(ex))
        {
            Debug.WriteLine($"[OpenNodeEditorButton] Failed to switch graph: {ex}");
        }
        finally
        {
            EndEdit?.Invoke(this, EventArgs.Empty);
        }
    }

    private void NewButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ItemProperties is not { Length: > 0 } || ItemProperties[0].Item is not NodeEffect pluginItem) return;
            var library = GraphLibraryViewModel.Current;
            if (library is null) return;

            var parentWindow = Window.GetWindow(this);
            if (parentWindow is null) return;

            if (!GraphNameInputWindow.TryGetName(parentWindow, TextUi.NewGraphNamePrompt,
                    $"Graph {library.Entries.Count + 1}", out var name))
                return;

            var entry = library.CreateNew(name);

            BeginEdit?.Invoke(this, EventArgs.Empty);
            try
            {
                pluginItem.SwitchGraph(entry.Id);
            }
            finally
            {
                EndEdit?.Invoke(this, EventArgs.Empty);
            }

            SyncComboBox();
        }
        catch (Exception ex) when (!ExceptionPolicy.IsFatal(ex))
        {
            Debug.WriteLine($"[OpenNodeEditorButton] Failed to create new graph: {ex}");
        }
    }

    private void RenameButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ItemProperties is not { Length: > 0 } || ItemProperties[0].Item is not NodeEffect pluginItem) return;
            var library = GraphLibraryViewModel.Current;
            if (library is null || !library.TryGetEntry(pluginItem.GraphId, out var entry)) return;

            var parentWindow = Window.GetWindow(this);
            if (parentWindow is null) return;

            if (!GraphNameInputWindow.TryGetName(parentWindow, TextUi.RenameGraphNamePrompt, entry.Name,
                    out var name))
                return;

            library.Rename(pluginItem.GraphId, name);
        }
        catch (Exception ex) when (!ExceptionPolicy.IsFatal(ex))
        {
            Debug.WriteLine($"[OpenNodeEditorButton] Failed to rename graph: {ex}");
        }
    }

    private void CloneButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ItemProperties is not { Length: > 0 } || ItemProperties[0].Item is not NodeEffect pluginItem) return;
            var library = GraphLibraryViewModel.Current;
            if (library is null) return;

            var parentWindow = Window.GetWindow(this);
            if (parentWindow is null) return;

            var currentName =
                library.TryGetEntry(pluginItem.GraphId, out var currentEntry) ? currentEntry.Name : "Graph";

            if (!GraphNameInputWindow.TryGetName(parentWindow, TextUi.CloneGraphNamePrompt, $"{currentName} Copy",
                    out var name))
                return;

            var cloned = library.Clone(pluginItem.GraphId, name, pluginItem.Graph);

            BeginEdit?.Invoke(this, EventArgs.Empty);
            try
            {
                pluginItem.SwitchGraph(cloned.Id);
            }
            finally
            {
                EndEdit?.Invoke(this, EventArgs.Empty);
            }

            SyncComboBox();
        }
        catch (Exception ex) when (!ExceptionPolicy.IsFatal(ex))
        {
            Debug.WriteLine($"[OpenNodeEditorButton] Failed to clone graph: {ex}");
        }
    }

    private void ManageButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (GraphLibraryViewModel.Current is null) return;

            var parentWindow = Window.GetWindow(this);
            var window = new GraphLibraryManageWindow { Owner = parentWindow };
            window.ShowDialog();
        }
        catch (Exception ex) when (!ExceptionPolicy.IsFatal(ex))
        {
            Debug.WriteLine($"[OpenNodeEditorButton] Failed to open manage window: {ex}");
        }
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

        var parentWindow = Window.GetWindow(this)!;
        var mainViewModel = parentWindow.DataContext!;

        var toolAreaViewModels =
            mainViewModel.GetType().GetProperty("AnchorableAreaViewModels")!.GetValue(mainViewModel) as
                System.Collections.IEnumerable;
        var toolAreaViewModel =
            toolAreaViewModels
                ?.Cast<object>()
                .FirstOrDefault(x => (string)x.GetType().GetProperty("Title")?.GetValue(x)! == TextUi.Node);

        var layoutService = mainViewModel.GetType().GetProperty("LayoutService")!.GetValue(mainViewModel);
        var dockingManager = layoutService?.GetType().GetProperty("Manager")!.GetValue(layoutService) as DockingManager;
        var layout = dockingManager!.Layout.Descendents().OfType<LayoutAnchorable>().FirstOrDefault(anchorable =>
        {
            var id = toolAreaViewModel?.GetType().GetProperty("Id")!.GetValue(toolAreaViewModel) as string;
            return anchorable.ContentId == id;
        });

        toolAreaViewModel?.GetType().GetProperty("IsVisible")?.SetValue(toolAreaViewModel, true);
        toolAreaViewModel?.GetType().GetProperty("IsSelected")?.SetValue(toolAreaViewModel, true);
        toolAreaViewModel?.GetType().GetProperty("IsActive")?.SetValue(toolAreaViewModel, true);
        var vm =
            toolAreaViewModel?.GetType().GetProperty("ViewModel")?.GetValue(toolAreaViewModel) as NodeEditorViewModel;

        _lastResolvedViewModel = vm;

        EnsureInternalGraph(pluginItem);
        if (pluginItem.InternalGraph is null)
            throw new InvalidOperationException("グラフの解決に失敗しました。");

        var title = GraphLibraryViewModel.Current is { } library &&
                    library.TryGetEntry(pluginItem.GraphId, out var entry)
            ? entry.Name
            : "Main";

        vm?.OpenGraph(pluginItem.InternalGraph!, title, _editorInfo, pluginItem.GraphId);

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

        _committedHandler = async void (_, _) =>
        {
            try
            {
                if (pluginItem.InternalGraph == null) return;
                BeginEdit?.Invoke(this, EventArgs.Empty);
                try
                {
                    var snapshot = await Serializer.CreateAsync(pluginItem.InternalGraph);
                    pluginItem.InternalGraphSnapshot = snapshot;
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
                        vm?.CloseGraphTab(previousGraph);
                    previousGraph = newGraph;
                }

                var newTitle = GraphLibraryViewModel.Current is { } lib &&
                               lib.TryGetEntry(pluginItem.GraphId, out var newEntry)
                    ? newEntry.Name
                    : "Main";

                vm?.OnGraphUpdated();
                vm?.OpenGraph(newGraph, newTitle, _editorInfo, pluginItem.GraphId);

                SyncComboBox();
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

        if (vm is null) return;
        if (layout is null) return;

        // 既存のショートカット登録・ハンドラを解除してから積み直す。
        ReleaseShortcutBindings();

        var nodeBindings = new[]
        {
            new KeyBinding(vm.ZoomUpCommand, Key.Add, ModifierKeys.Control),
            new KeyBinding(vm.ZoomUpCommand, Key.OemPlus, ModifierKeys.Control),
            new KeyBinding(vm.ZoomDownCommand, Key.Subtract, ModifierKeys.Control),
            new KeyBinding(vm.ZoomDownCommand, Key.OemMinus, ModifierKeys.Control),
            new KeyBinding(vm.ResetZoomCommand, Key.D0, ModifierKeys.Control),
            new KeyBinding(vm.DeleteSelectedCommand, Key.Delete, ModifierKeys.None),
            new KeyBinding(vm.DeleteSelectedCommand, Key.Back, ModifierKeys.None),
            new KeyBinding(vm.CopyCommand, Key.C, ModifierKeys.Control),
            new KeyBinding(vm.CutCommand, Key.X, ModifierKeys.Control),
            new KeyBinding(vm.PasteCommand, Key.V, ModifierKeys.Control)
        };

        _nodeBindings = nodeBindings;
        _boundWindow = parentWindow;
        _subscribedLayout = layout;

        _layoutIsSelectedChangedHandler = (_, _) =>
        {
            try
            {
                if (!layout.IsSelected)
                    toolAreaViewModel?.GetType().GetProperty("ViewModel")?.SetValue(toolAreaViewModel, vm);
                else
                    vm.RefreshAllOpenGraphs();
            }
            catch (Exception ex) when (!ExceptionPolicy.IsFatal(ex))
            {
                Debug.WriteLine($"[OpenNodeEditorButton] IsSelectedChanged handler failed: {ex}");
            }
        };
        layout.IsSelectedChanged += _layoutIsSelectedChangedHandler;

        _layoutIsActiveChangedHandler = (_, _) =>
        {
            try
            {
                if (layout.IsActive)
                {
                    foreach (var kb in nodeBindings)
                        parentWindow.InputBindings.Add(kb);
                    // フォーカスを取り戻したタイミングでも同様に再同期しておく。
                    vm.RefreshAllOpenGraphs();
                }
                else
                {
                    foreach (var kb in nodeBindings)
                        parentWindow.InputBindings.Remove(kb);
                }
            }
            catch (Exception ex) when (!ExceptionPolicy.IsFatal(ex))
            {
                Debug.WriteLine($"[OpenNodeEditorButton] IsActiveChanged handler failed: {ex}");
            }
        };
        layout.IsActiveChanged += _layoutIsActiveChangedHandler;

        if (layout.IsActive)
        {
            foreach (var kb in nodeBindings)
                parentWindow.InputBindings.Add(kb);

            vm.RefreshAllOpenGraphs();
        }
    }
}