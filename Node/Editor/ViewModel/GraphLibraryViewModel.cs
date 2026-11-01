using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Node.Graph;
using Node.Graph.Port;
using Node.Graph.Snapshot;
using Node.Nodes.Func;
using Node.ValueTypes;
using YukkuriMovieMaker.Plugin;

namespace Node.Editor.ViewModel;

public sealed class GraphLibraryViewModel : IToolViewModel
{
    // YMM4はツールの表示中だけViewModelを生成し、非表示になると破棄する（ToolAreaViewModel.UpdateViewModelLifecycle）。
    // ライブラリ本体をインスタンスに持つと、ツールが未表示の間はエフェクトから参照できず、
    // 表示するたびに空のライブラリから始まるため、全インスタンスで共有する静的な状態として保持する。
    private static readonly Lock _lock = new();
    private static readonly Dictionary<Guid, NamedGraphSnapshot> _removedEntries = new();

    private static readonly Dictionary<Guid, List<WeakReference<NodeEffect>>> _subscribers = new();

    // プロジェクトから復元した名前・順序のうち、対応するエントリーがまだ無いもの。
    // エントリー生成時（PropagateChange / LoadState）に消費する。
    private static readonly Dictionary<Guid, (string? Name, int? Order)> _pendingMetadata = new();

    private static readonly ObservableCollection<NamedGraphSnapshot> SharedEntries = [];
    private static GraphLibraryViewModel? s_current;

    static GraphLibraryViewModel()
    {
        SharedEntries.CollectionChanged += OnEntriesChanged;
    }

    public GraphLibraryViewModel()
    {
        Current = this;
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    // ツールが一度も表示されていなくてもライブラリを使えるよう、未生成ならここで生成する。
    public static GraphLibraryViewModel? Current
    {
        get => s_current ?? new GraphLibraryViewModel();
        private set => s_current = value;
    }

    public ObservableCollection<NamedGraphSnapshot> Entries => SharedEntries;

    public string? Title => "ノードグラフライブラリ";

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<CreateNewToolViewRequestedEventArgs>? CreateNewToolViewRequested;

    public ToolState SaveState()
    {
        // NodeSnapshot.InputsValues などが object 型を含むため、プロジェクトの保存と同じ設定
        // （TypeNameHandling.Auto 等）で直列化する。既定設定では型情報が失われる。
        var json = YukkuriMovieMaker.Json.Json.GetJsonText(Entries.ToList());
        return new ToolState { SavedState = json };
    }

    public void LoadState(ToolState stateData)
    {
        if (string.IsNullOrEmpty(stateData.SavedState))
            return;

        var restored = YukkuriMovieMaker.Json.Json.LoadFromText<List<NamedGraphSnapshot>>(stateData.SavedState!);
        if (restored == null)
            return;

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => LoadStateCore(restored));
            return;
        }

        LoadStateCore(restored);
    }

    public static event EventHandler? CurrentChanged;

    // 回帰検査用。共有状態を空に戻す。
    internal static void ResetSharedState()
    {
        lock (_lock)
        {
            foreach (var entry in SharedEntries.ToList()) SharedEntries.Remove(entry);
            _removedEntries.Clear();
            _subscribers.Clear();
            _pendingMetadata.Clear();
            s_current = null;
        }
    }

    public bool TryGetEntry(Guid id, out NamedGraphSnapshot entry)
    {
        entry = Entries.FirstOrDefault(e => e.Id == id)!;
        return entry != null!;
    }

    public bool TryGetSnapshot(Guid id, out GraphSnapshot snapshot)
    {
        if (TryGetEntry(id, out var entry))
        {
            snapshot = entry.Graph;
            return true;
        }

        snapshot = new GraphSnapshot();
        return false;
    }

    public void Subscribe(Guid id, NodeEffect effect)
    {
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(id, out var list))
            {
                list = new List<WeakReference<NodeEffect>>();
                _subscribers[id] = list;
            }

            list.RemoveAll(wr => !wr.TryGetTarget(out var t) || ReferenceEquals(t, effect));
            list.Add(new WeakReference<NodeEffect>(effect));
        }
    }

    public void Unsubscribe(Guid id, NodeEffect effect)
    {
        lock (_lock)
        {
            if (_subscribers.TryGetValue(id, out var list))
                list.RemoveAll(wr => !wr.TryGetTarget(out var t) || ReferenceEquals(t, effect));
        }
    }

    public void PropagateChange(Guid id, NodeEffect origin, GraphSnapshot snapshot)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => PropagateChange(id, origin, snapshot));
            return;
        }

        List<NodeEffect>? targets = null;

        lock (_lock)
        {
            if (TryGetEntry(id, out var entry))
                entry.Graph = snapshot;
            else
            {
                if (!_removedEntries.Remove(id, out var restored))
                    restored = new NamedGraphSnapshot { Id = id, Name = $"Graph {Entries.Count + 1}" };
                restored.Graph = snapshot;
                restored.Order = Entries.Count;
                if (_pendingMetadata.Remove(id, out var pending))
                {
                    if (!string.IsNullOrEmpty(pending.Name)) restored.Name = pending.Name;
                    if (pending.Order is { } pendingOrder) restored.Order = pendingOrder;
                }

                Entries.Add(restored);
                SortEntry(restored);
            }

            if (_subscribers.TryGetValue(id, out var list))
            {
                targets = new List<NodeEffect>();
                list.RemoveAll(wr =>
                {
                    if (!wr.TryGetTarget(out var target)) return true; // GC済みなら間引く
                    if (!ReferenceEquals(target, origin)) targets.Add(target);
                    return false;
                });
            }
        }

        if (targets == null) return;
        foreach (var target in targets)
            target.ApplyExternalGraphUpdate(id, snapshot);
    }

    /// <summary>
    ///     プロジェクトに保存された名前・順序をエントリーへ反映する。
    ///     エントリーが未生成なら保留し、PropagateChange での生成時に適用する。
    /// </summary>
    public void RestoreEntryMetadata(Guid id, string? name, int? order)
    {
        if (string.IsNullOrEmpty(name) && order is null) return;

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => RestoreEntryMetadata(id, name, order));
            return;
        }

        lock (_lock)
        {
            if (!TryGetEntry(id, out var entry))
            {
                _pendingMetadata.TryGetValue(id, out var pending);
                _pendingMetadata[id] = (string.IsNullOrEmpty(name) ? pending.Name : name, order ?? pending.Order);
                return;
            }

            if (!string.IsNullOrEmpty(name)) entry.Name = name;
            if (order is { } value)
            {
                entry.Order = value;
                SortEntry(entry);
            }
        }
    }

    public NamedGraphSnapshot CreateNew(string name)
    {
        lock (_lock)
        {
            var snapshot = Serializer.Create(CreateDefaultGraph());
            var entry = new NamedGraphSnapshot { Name = name, Graph = snapshot, Order = Entries.Count };
            Entries.Add(entry);
            return entry;
        }
    }

    public NamedGraphSnapshot Clone(Guid sourceId, string newName, GraphSnapshot fallbackSnapshot)
    {
        lock (_lock)
        {
            var sourceSnapshot = TryGetEntry(sourceId, out var src)
                ? src.Graph
                : fallbackSnapshot;

            var entry = new NamedGraphSnapshot { Name = newName, Graph = sourceSnapshot, Order = Entries.Count };
            Entries.Add(entry);
            return entry;
        }
    }

    public void Rename(Guid id, string newName)
    {
        if (TryGetEntry(id, out var entry))
            entry.Name = newName;
    }

    public void Delete(Guid id)
    {
        lock (_lock)
        {
            var entry = Entries.FirstOrDefault(e => e.Id == id);
            if (entry == null) return;
            _removedEntries[id] = entry;
            Entries.Remove(entry);
        }
    }

    public void Reorder(IReadOnlyList<Guid> orderedIds)
    {
        for (var i = 0; i < orderedIds.Count; i++)
            if (TryGetEntry(orderedIds[i], out var entry))
                entry.Order = i;
    }

    public void Move(Guid id, int offset)
    {
        lock (_lock)
        {
            var index = -1;
            for (var i = 0; i < Entries.Count; i++)
                if (Entries[i].Id == id)
                {
                    index = i;
                    break;
                }

            if (index < 0) return;

            var newIndex = index + offset;
            if (newIndex < 0 || newIndex >= Entries.Count) return;

            Entries.Move(index, newIndex);
            for (var i = 0; i < Entries.Count; i++)
                Entries[i].Order = i;
        }
    }

    private static void SortEntry(NamedGraphSnapshot entry)
    {
        var fromIndex = SharedEntries.IndexOf(entry);
        if (fromIndex < 0) return;

        var toIndex = SharedEntries.Count(e => !ReferenceEquals(e, entry) && e.Order < entry.Order);
        if (toIndex != fromIndex) SharedEntries.Move(fromIndex, toIndex);
    }

    private static void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (NamedGraphSnapshot removed in e.OldItems)
                removed.PropertyChanged -= OnEntryPropertyChanged;

        if (e.NewItems != null)
            foreach (NamedGraphSnapshot added in e.NewItems)
                added.PropertyChanged += OnEntryPropertyChanged;
    }

    // 名前・順序の変更を、そのグラフを参照するエフェクトへ伝える。
    // エフェクト側のプロパティ経由でプロジェクトファイルに保存される。
    private static void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not NamedGraphSnapshot entry) return;
        if (e.PropertyName is not (nameof(NamedGraphSnapshot.Name) or nameof(NamedGraphSnapshot.Order))) return;

        List<NodeEffect> targets = [];
        lock (_lock)
        {
            if (_subscribers.TryGetValue(entry.Id, out var list))
                foreach (var weak in list)
                    if (weak.TryGetTarget(out var target))
                        targets.Add(target);
        }

        foreach (var target in targets)
            target.ApplyLibraryMetadata(entry.Name, entry.Order);
    }

    private static NodeGraph CreateDefaultGraph()
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

        return graph;
    }

    private void LoadStateCore(List<NamedGraphSnapshot> restored)
    {
        lock (_lock)
        {
            // 既存のエントリーはエフェクト側（プロジェクトに保存された最新の内容）が正なので上書きしない。
            // ToolState はツールが非表示だった間の変更を含まない古い内容の場合がある。
            foreach (var saved in restored.OrderBy(e => e.Order))
                if (!TryGetEntry(saved.Id, out _))
                {
                    if (_pendingMetadata.Remove(saved.Id, out var pending))
                    {
                        if (!string.IsNullOrEmpty(pending.Name)) saved.Name = pending.Name;
                        if (pending.Order is { } pendingOrder) saved.Order = pendingOrder;
                    }

                    Entries.Add(saved);
                    SortEntry(saved);
                }
        }

        OnPropertyChanged(nameof(Entries));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}