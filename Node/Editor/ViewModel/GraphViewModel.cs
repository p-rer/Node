using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Node.Editor.Command;
using Node.Graph;
using Node.Graph.Events;
using Node.Graph.Port;
using Node.Graph.Snapshot;
using Node.Localize;
using Node.Nodes.Func;
using Node.Utility;
using YukkuriMovieMaker.Commons;
using VisualStateChangedEventArgs =
    Node.Graph.Events.VisualStateChangedEventArgs;

namespace Node.Editor.ViewModel;

public sealed class GraphViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly NodeGraph _graph;
    private readonly EventHandler _graphUpdatedHandler;
    private bool _isDisposed;
    private int _syncQueued;

    public GraphViewModel(NodeGraph graph, NodeEditorViewModel nodeEditorViewModel, IEditorInfo? editorInfo = null)
    {
        _graph = graph;
        ParentEditor = nodeEditorViewModel;
        EditorInfo = editorInfo;
        _graphUpdatedHandler = (_, _) => RequestSync();

        _graph.GraphChanged += OnGraphChanged;
        ParentEditor.GraphUpdated += _graphUpdatedHandler;

        SyncFromGraph();

        AddNodeCommand = new RelayCommand<Type>(AddNode);
        DeleteNodeCommand = new RelayCommand<Guid>(DeleteNode);
        ConnectPortsCommand = new RelayCommand<(PortViewModel From, PortViewModel To)>(ConnectPorts);
        DisconnectCommand = new RelayCommand<ConnectionViewModel>(Disconnect);
    }

    public NodeEditorViewModel ParentEditor { get; }
    public ObservableCollection<NodeViewModel> Nodes { get; } = [];
    public ObservableCollection<ConnectionViewModel> Connections { get; } = [];

    public IEditorInfo? EditorInfo
    {
        get;
        internal set => SetField(ref field, value);
    }

    public PortViewModel? DraggingFromPort
    {
        get;
        set
        {
            if (!SetField(ref field, value)) return;
            HoveringPort = null;
        }
    }

    public Point? TemporaryEndPoint
    {
        get;
        set => SetField(ref field, value);
    }

    public PortViewModel? HoveringPort
    {
        get;
        set
        {
            if (!SetField(ref field, value)) return;
            ConnectionRejectionMessage = CreateConnectionRejectionMessage(DraggingFromPort, value);
        }
    }

    public string? ConnectionRejectionMessage
    {
        get;
        private set => SetField(ref field, value);
    }

    public Point? PendingContextPoint
    {
        get;
        set => SetField(ref field, value);
    }

    public ICommand AddNodeCommand { get; }
    public ICommand DeleteNodeCommand { get; }
    public ICommand ConnectPortsCommand { get; }
    public ICommand DisconnectCommand { get; }

    public ObservableCollection<NodeViewModel> SelectedNodes { get; } = [];

    public double Zoom
    {
        get;
        set => SetField(ref field, value);
    } = 1.0;

    public double PanX
    {
        get;
        set => SetField(ref field, value);
    } = 0.0;

    public double PanY
    {
        get;
        set => SetField(ref field, value);
    } = 0.0;

    public double Width { get; set; }

    public double Height { get; set; }

    /// <summary>
    ///     タブを閉じる際に呼び出す。_graph.GraphChanged と ParentEditor.GraphUpdated への
    ///     購読を解除しないと、閉じた後もこの VM がグラフ変更に反応し続けてしまう。
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _graph.GraphChanged -= OnGraphChanged;
        ParentEditor.GraphUpdated -= _graphUpdatedHandler;

        foreach (var node in Nodes)
            node.Dispose();
        Nodes.Clear();

        foreach (var connection in Connections)
            connection.Dispose();
        Connections.Clear();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     グラフの現在状態から Nodes/Connections を作り直す。
    ///     通常は GraphChanged/GraphUpdated イベント経由で自動的に呼ばれるが、
    ///     Undo/Redo がプロパティセッタを経由せずグラフを書き換えるような実装だと、
    ///     そのイベントチェーンが発火せず、パネルの表示が古いままになることがある。
    ///     そのための保険として、パネルがアクティブになったタイミングなどで
    ///     外部から明示的に呼び直せるようにしておく。
    /// </summary>
    public void Refresh()
    {
        RequestSync();
    }

    /// <summary>
    ///     SyncFromGraph を後続のディスパッチャー処理へ回し、短時間の複数要求は 1 回にまとめる。
    ///     要求はレイアウトやバインディング更新の最中にも届くため、その場で Nodes/Connections を
    ///     書き換えると、測定中のパネルの子要素が消えて例外になる。
    /// </summary>
    private void RequestSync()
    {
        if (_isDisposed) return;

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            SyncFromGraph();
            return;
        }

        if (Interlocked.Exchange(ref _syncQueued, 1) != 0) return;

        dispatcher.BeginInvoke(new Action(() =>
        {
            Interlocked.Exchange(ref _syncQueued, 0);
            if (_isDisposed) return;

            try
            {
                SyncFromGraph();
            }
            catch (Exception ex) when (!ExceptionPolicy.IsFatal(ex))
            {
                Debug.WriteLine($"[GraphViewModel] SyncFromGraph failed: {ex}");
            }
        }));
    }

    private void UpdatePortValue(ValueChangedEventArgs e)
    {
        var node = Nodes.FirstOrDefault(n => n.Id == e.NodeId);
        if (node == null) return;

        var port = node.InputPorts.FirstOrDefault(p => p.Name == e.PortName);
        if (port != null)
            port.UpdateValueFromGraph(e.NewValue);
    }

    private void SyncFromGraph()
    {
        var selectedIds = SelectedNodes.Select(node => node.Id).ToHashSet();

        lock (_graph.GraphLock)
        {
            // NodeLogic が同一で端子構成も変わっていない VM は使い回す。作り直すとノードの View が
            // 破棄され、ドラッグ中のマウスキャプチャや選択、編集中のコントロールの状態が失われる。
            var existing = new Dictionary<Guid, NodeViewModel>();
            foreach (var oldVm in Nodes)
                existing[oldVm.Id] = oldVm;

            var resolved = new Dictionary<Guid, NodeViewModel>();
            var order = new List<NodeViewModel>();
            foreach (var node in _graph.Nodes.Values)
            {
                NodeViewModel vm;
                if (existing.TryGetValue(node.Id, out var reusable) && reusable.IsBackedBy(node))
                    vm = reusable;
                else
                    try
                    {
                        vm = new NodeViewModel(node, _graph, this);
                    }
                    catch (Exception ex) when (!ExceptionPolicy.IsFatal(ex))
                    {
                        Debug.WriteLine($"[GraphViewModel] Failed to build NodeViewModel for {node.Id}: {ex}");
                        continue;
                    }

                resolved[node.Id] = vm;
                order.Add(vm);
            }

            // Nodes の並びは描画順（前面に出した順）。既存の並びを保ったまま差分だけを反映する。
            var keep = order.ToHashSet();
            var claimed = new HashSet<Guid>();
            for (var i = Nodes.Count - 1; i >= 0; i--)
            {
                var vm = Nodes[i];
                if (resolved.TryGetValue(vm.Id, out var next) && claimed.Add(vm.Id))
                {
                    if (ReferenceEquals(vm, next)) continue;
                    vm.Dispose();
                    Nodes[i] = next;
                }
                else
                {
                    if (!keep.Contains(vm))
                        vm.Dispose();
                    Nodes.RemoveAt(i);
                }
            }

            var present = Nodes.ToHashSet();
            foreach (var vm in order)
                if (!present.Contains(vm))
                    Nodes.Add(vm);

            var newSelection = new List<NodeViewModel>();
            foreach (var vm in Nodes)
            {
                var selected = selectedIds.Contains(vm.Id);
                vm.IsSelected = selected;
                if (selected)
                    newSelection.Add(vm);
            }

            if (!SelectedNodes.SequenceEqual(newSelection))
            {
                SelectedNodes.Clear();
                foreach (var vm in newSelection)
                    SelectedNodes.Add(vm);
            }

            foreach (var oldConnection in Connections)
                oldConnection.Dispose();
            Connections.Clear();

            foreach (var conn in _graph.Connections)
            {
                if (!resolved.ContainsKey(conn.FromId) || !resolved.ContainsKey(conn.ToId))
                    continue;

                Connections.Add(new ConnectionViewModel(conn, resolved));
            }

            // IsConnected は一度 false に戻さず、結果が変わる端子だけを更新する（View のちらつきを避ける）。
            var connectedPorts = Connections.Select(c => c.ToPort).OfType<PortViewModel>().ToHashSet();
            foreach (var node in Nodes)
            foreach (var port in node.InputPorts)
                port.IsConnected = connectedPorts.Contains(port);
        }
    }

    private void OnGraphChanged(object? sender, GraphChangedEventArgs e)
    {
        switch (e)
        {
            case NodeAddedEventArgs added:
            {
                NodeViewModel vm;
                try
                {
                    vm = new NodeViewModel(added.Node, _graph, this);
                }
                catch (Exception ex) when (!ExceptionPolicy.IsFatal(ex))
                {
                    Debug.WriteLine($"[GraphViewModel] Failed to build NodeViewModel for {added.NodeId}: {ex}");
                    break;
                }

                Nodes.Add(vm);
                foreach (var port in vm.InputPorts)
                    port.ApplyDefaultToGraph();
                break;
            }

            case NodeRemovedEventArgs removed:
            {
                var node = Nodes.FirstOrDefault(n => n.Id == removed.NodeId);
                if (node != null)
                {
                    node.Dispose();
                    Nodes.Remove(node);
                }

                break;
            }

            case ConnectionChangedEventArgs:
            {
                RequestSync();
                break;
            }

            case ValueChangedEventArgs valueChanged:
            {
                UpdatePortValue(valueChanged);
                break;
            }

            case VisualStateChangedEventArgs visualStateChanged:
            {
                var node = Nodes.FirstOrDefault(nodeViewModel => nodeViewModel.Id == visualStateChanged.NodeId);
                if (node == null) break;
                node.X = visualStateChanged.NewState.X;
                node.Y = visualStateChanged.NewState.Y;
                break;
            }
        }
    }

    private void AddNode(Type? nodeType)
    {
        if (nodeType == null) return;

        lock (_graph.GraphLock)
        {
            _graph.BeginEdit();
            try
            {
                var node = (NodeLogic)Activator.CreateInstance(nodeType)!;
                node.Id = Guid.NewGuid();

                _graph.AddNode(node);

                var pos = PendingContextPoint ?? new Point(100, 100);
                _graph.SetVisualState(node.Id, pos.X, pos.Y);
            }
            finally
            {
                _graph.EndEdit();
            }
        }
    }

    internal void DeleteSelectedNode()
    {
        var nodeIds = SelectedNodes
            .Where(nodeVm =>
                nodeVm.NodeLogic.GetType() != typeof(ArgumentsNode) &&
                nodeVm.NodeLogic.GetType() != typeof(ReturnNode))
            .Select(vm => vm.Id)
            .ToList();

        lock (_graph.GraphLock)
        {
            _graph.BeginEdit();
            try
            {
                foreach (var nodeId in nodeIds)
                    _graph.RemoveNode(nodeId);
            }
            finally
            {
                _graph.EndEdit();
            }
        }
    }

    private void DeleteNode(Guid nodeId)
    {
        lock (_graph.GraphLock)
        {
            _graph.BeginEdit();
            try
            {
                _graph.RemoveNode(nodeId);
            }
            finally
            {
                _graph.EndEdit();
            }
        }
    }

    internal void ApplyArgumentPorts(Guid nodeId, PortDefinition[] definitions)
    {
        lock (_graph.GraphLock)
        {
            _graph.BeginEdit();
            try
            {
                _graph.ReplaceArgumentPorts(nodeId, definitions);
            }
            finally
            {
                _graph.EndEdit();
            }
        }
    }

    internal void ApplySubGraphPorts(Guid nodeId, bool outputs, PortDefinition[] definitions)
    {
        lock (_graph.GraphLock)
        {
            if (!_graph.Nodes.TryGetValue(nodeId, out var logic) || logic is not ISubGraphPortNode node)
                return;

            NodeLogic? childNode = outputs ? node.InnerReturn : node.InnerArguments;
            if (childNode == null)
                return;

            _graph.BeginEdit();
            try
            {
                var affectedConnections = outputs
                    ? _graph.Connections.Where(c => c.FromId == nodeId).ToList()
                    : _graph.Connections.Where(c => c.ToId == nodeId).ToList();

                foreach (var connection in affectedConnections)
                    _graph.DisconnectSilently(connection.FromId, connection.FromPort, connection.ToId,
                        connection.ToPort);

                if (!node.Graph.ReplaceArgumentPorts(childNode.Id, definitions))
                {
                    foreach (var connection in affectedConnections)
                        _graph.Connect(connection.FromId, connection.FromPort, connection.ToId, connection.ToPort);
                    return;
                }

                node.SyncPorts();

                foreach (var connection in affectedConnections)
                {
                    var portName = outputs ? connection.FromPort : connection.ToPort;

                    var portExists = outputs
                        ? logic.Outputs.ContainsKey(portName)
                        : logic.Inputs.ContainsKey(portName);

                    if (portExists)
                        _graph.Connect(connection.FromId, connection.FromPort, connection.ToId, connection.ToPort);
                }

                _graph.OnGraphChanged(new ConnectionChangedEventArgs(null, null, nodeId, null));
            }
            finally
            {
                _graph.EndEdit();
            }
        }
    }

    private void ConnectPorts((PortViewModel A, PortViewModel B) ports)
    {
        var p1 = ports.A;
        var p2 = ports.B;

        if (p1.Direction == p2.Direction)
            return;

        var from = p1.Direction == PortDirection.Output ? p1 : p2;
        var to = p1.Direction == PortDirection.Input ? p1 : p2;

        lock (_graph.GraphLock)
        {
            if (_graph.WouldCreateCycle(from.NodeId, to.NodeId))
                return;

            _graph.BeginEdit();
            try
            {
                _graph.Connect(
                    from.NodeId, from.Name,
                    to.NodeId, to.Name
                );
            }
            finally
            {
                _graph.EndEdit();
            }
        }
    }

    /// <summary>
    ///     ConnectPorts / NodeGraph.Connect が接続を拒否する場合に、その理由の表示文言を返す。
    /// </summary>
    private string? CreateConnectionRejectionMessage(PortViewModel? source, PortViewModel? target)
    {
        if (source is null || target is null || ReferenceEquals(source, target))
            return null;

        if (source.Direction == target.Direction)
            return source.Direction == PortDirection.Output
                ? TextUi.ConnectionRejectedBothOutputs
                : TextUi.ConnectionRejectedBothInputs;

        var from = source.Direction == PortDirection.Output ? source : target;
        var to = source.Direction == PortDirection.Input ? source : target;

        ConnectionRejection rejection;
        lock (_graph.GraphLock)
        {
            rejection = _graph.GetConnectionRejection(from.NodeId, from.Name, to.NodeId, to.Name);
        }

        return rejection switch
        {
            ConnectionRejection.Cycle => TextUi.ConnectionRejectedCycle,
            ConnectionRejection.IncompatibleTypes =>
                ConnectionViewModel.CreateProblemMessage(from.ValueType, to.ValueType)
                ?? TextUi.ConnectionRejectedUnknown,
            ConnectionRejection.PortNotFound => TextUi.ConnectionRejectedUnknown,
            _ => null
        };
    }

    private void Disconnect(ConnectionViewModel? connection)
    {
        if (connection == null) return;
        lock (_graph.GraphLock)
        {
            _graph.BeginEdit();
            try
            {
                _graph.Disconnect(
                    connection.FromNodeId, connection.FromPortName,
                    connection.ToNodeId, connection.ToPortName
                );
            }
            finally
            {
                _graph.EndEdit();
            }
        }
    }

    public void SelectAll()
    {
        // AddToSelection は選択ノードを最前面へ並べ替えるため、全選択では使わない。
        foreach (var node in Nodes)
        {
            if (SelectedNodes.Contains(node)) continue;
            SelectedNodes.Add(node);
            node.IsSelected = true;
        }
    }

    public void ClearSelection()
    {
        foreach (var n in SelectedNodes)
            n.IsSelected = false;

        SelectedNodes.Clear();
    }

    public void ApplyRectSelection(Rect rect, bool additive)
    {
        if (!additive)
            ClearSelection();

        foreach (var node in Nodes.ToList())
        {
            var nodeRect = new Rect(node.X * Zoom + PanX, node.Y * Zoom + PanY, node.Width * Zoom, node.Height * Zoom);

            if (rect.IntersectsWith(nodeRect))
                AddToSelection(node);
        }
    }

    public void ApplyLassoSelection(IReadOnlyList<Point> polygon, bool additive)
    {
        if (polygon.Count < 3) return;

        if (!additive)
            ClearSelection();

        foreach (var node in Nodes.ToList())
        {
            var center = TransformToScreen(new Point(
                node.X + node.Width / 2,
                node.Y + node.Height / 2));

            if (IsPointInPolygon(center))
                AddToSelection(node);
        }

        return;

        bool IsPointInPolygon(Point p)
        {
            var inside = false;

            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var pi = polygon[i];
                var pj = polygon[j];

                var intersect =
                    pi.Y > p.Y != pj.Y > p.Y &&
                    p.X < (pj.X - pi.X) * (p.Y - pi.Y) / (pj.Y - pi.Y) + pi.X;

                if (intersect)
                    inside = !inside;
            }

            return inside;
        }
    }

    public void AddToSelection(NodeViewModel node)
    {
        if (!SelectedNodes.Contains(node))
        {
            SelectedNodes.Add(node);
            node.IsSelected = true;
        }

        var index = Nodes.IndexOf(node);
        if (index >= 0 && index != Nodes.Count - 1)
            Nodes.Move(index, Nodes.Count - 1);
    }

    public void SelectSingle(NodeViewModel node)
    {
        ClearSelection();
        AddToSelection(node);
    }

    public void BeginNodeDrag(NodeViewModel origin)
    {
        if (!SelectedNodes.Contains(origin)) SelectSingle(origin);
    }

    public void UpdateNodeDrag(Vector delta)
    {
        foreach (var nodeVm in SelectedNodes)
        {
            nodeVm.X += delta.X;
            nodeVm.Y += delta.Y;
        }
    }

    public void EndNodeDrag()
    {
        lock (_graph.GraphLock)
        {
            _graph.BeginEdit();
            try
            {
                foreach (var nodeVm in SelectedNodes)
                    nodeVm.CommitPosition();
            }
            finally
            {
                _graph.EndEdit();
            }
        }
    }

    public Point TransformToCanvas(Point screenPoint)
    {
        return new Point(
            (screenPoint.X - PanX) / Zoom,
            (screenPoint.Y - PanY) / Zoom
        );
    }

    public Point TransformToScreen(Point canvasPoint)
    {
        return new Point(
            canvasPoint.X * Zoom + PanX,
            canvasPoint.Y * Zoom + PanY
        );
    }

    internal void Copy(out GraphSnapshot? clipboard)
    {
        if (SelectedNodes.Count == 0)
        {
            clipboard = null;
            return;
        }

        var selectedIds = SelectedNodes.Where(nodeVm =>
                nodeVm.NodeLogic.GetType() != typeof(ArgumentsNode) && nodeVm.NodeLogic.GetType() != typeof(ReturnNode))
            .Select(n => n.Id)
            .ToHashSet();

        if (selectedIds.Count == 0)
        {
            clipboard = null;
            return;
        }

        GraphSnapshot fullSnapshot;
        lock (_graph.GraphLock)
        {
            fullSnapshot = Serializer.Create(_graph);
        }

        var tempGraph = new GraphSnapshot();

        tempGraph.Nodes.AddRange(fullSnapshot.Nodes.Where(n => selectedIds.Contains(n.Id)));
        tempGraph.Connections.AddRange(fullSnapshot.Connections.Where(c =>
            selectedIds.Contains(c.FromId) && selectedIds.Contains(c.ToId)));

        foreach (var typeName in tempGraph.Nodes.Select(n => n.TypeName).Distinct())
        {
            if (fullSnapshot.EffectTypeNames.TryGetValue(typeName, out var effectName))
                tempGraph.EffectTypeNames[typeName] = effectName;
            if (fullSnapshot.BrushTypeNames.TryGetValue(typeName, out var brushName))
                tempGraph.BrushTypeNames[typeName] = brushName;
        }

        clipboard = tempGraph;
        CommandManager.InvalidateRequerySuggested();
    }

    internal void Paste(GraphSnapshot? clipboard)
    {
        if (clipboard == null)
            return;

        var tempGraph = Serializer.Restore(clipboard);

        foreach (var kv in tempGraph.Nodes.ToList())
            if (kv.Value.GetType() == typeof(ArgumentsNode) || kv.Value.GetType() == typeof(ReturnNode))
                tempGraph.RemoveNode(kv.Key);

        var nodes = tempGraph.Nodes.Values.ToList();
        if (nodes.Count == 0)
            return;

        var positions = nodes.Select(n =>
        {
            if (tempGraph.VisualStates.TryGetValue(n.Id, out var vs))
                return new Point(vs.X, vs.Y);

            return new Point(0, 0);
        }).ToList();

        var minX = positions.Min(p => p.X);
        var minY = positions.Min(p => p.Y);

        var anchor = PendingContextPoint ?? new Point(minX + 50, minY + 50);
        var dx = anchor.X - minX;
        var dy = anchor.Y - minY;

        var idMapping = new Dictionary<Guid, Guid>();

        lock (_graph.GraphLock)
        {
            _graph.BeginEdit();
            try
            {
                foreach (var node in nodes)
                {
                    var oldId = node.Id;
                    var newId = Guid.NewGuid();

                    idMapping[oldId] = newId;
                    node.Id = newId;

                    var dynamicValues = node.Inputs
                        .Where(kv => kv.Key.Contains('.') && kv.Value.LocalValue != null)
                        .ToDictionary(kv => kv.Key, kv => kv.Value.LocalValue);

                    _graph.AddNode(node);

                    foreach (var (key, value) in dynamicValues)
                        if (node.Inputs.TryGetValue(key, out var port))
                            port.SetValue(value);

                    if (tempGraph.VisualStates.TryGetValue(oldId, out var oldVisualState))
                        _graph.SetVisualState(newId, oldVisualState.X + dx, oldVisualState.Y + dy);
                }

                foreach (var conn in tempGraph.Connections)
                    if (idMapping.TryGetValue(conn.FromId, out var newFromId) &&
                        idMapping.TryGetValue(conn.ToId, out var newToId))
                        _graph.Connect(newFromId, conn.FromPort, newToId, conn.ToPort);
            }
            finally
            {
                _graph.EndEdit();
            }
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}