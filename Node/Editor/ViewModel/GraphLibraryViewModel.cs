using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Node.Graph;
using Node.Graph.Port;
using Node.Graph.Snapshot;
using Node.Nodes.Func;
using Node.ValueTypes;
using YukkuriMovieMaker.Plugin;

namespace Node.Editor.ViewModel;

public sealed class GraphLibraryViewModel : IToolViewModel
{
    private readonly Lock _lock = new();

    private readonly Dictionary<Guid, List<WeakReference<NodeEffect>>> _subscribers = new();

    public GraphLibraryViewModel()
    {
        Current = this;
    }

    public static GraphLibraryViewModel? Current { get; private set; }

    public ObservableCollection<NamedGraphSnapshot> Entries { get; } = [];

    public string? Title => "ノードグラフライブラリ";

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<CreateNewToolViewRequestedEventArgs>? CreateNewToolViewRequested;

    public ToolState SaveState()
    {
        var json = JsonConvert.SerializeObject(Entries.ToList());
        return new ToolState { SavedState = json };
    }

    public void LoadState(ToolState stateData)
    {
        if (string.IsNullOrEmpty(stateData.SavedState))
            return;

        var restored = JsonConvert.DeserializeObject<List<NamedGraphSnapshot>>(stateData.SavedState!);
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
                Entries.Add(new NamedGraphSnapshot
                {
                    Id = id, Name = $"Graph {Entries.Count + 1}", Graph = snapshot, Order = Entries.Count
                });

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
            target.ApplyExternalGraphUpdate(snapshot);
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
            foreach (var saved in restored.OrderBy(e => e.Order))
                if (TryGetEntry(saved.Id, out var existing))
                {
                    existing.Name = saved.Name;
                    existing.Order = saved.Order;
                    existing.Graph = saved.Graph;
                }
                else
                {
                    Entries.Add(saved);
                }
        }

        OnPropertyChanged(nameof(Entries));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}