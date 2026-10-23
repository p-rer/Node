namespace Node.Graph;

// Keeps delayed updates and asynchronous saves tied to the graph they started on.
internal sealed class SharedGraphUpdateState<T>(Guid graphId) where T : class
{
    private readonly Lock _gate = new();
    private Guid _graphId = graphId;
    private long _revision;
    private T? _pending;

    internal readonly record struct Stamp(Guid GraphId, long Revision);

    public Stamp BeginSave()
    {
        lock (_gate)
        {
            // A graph awaiting a remote update must not publish its old live state.
            if (_pending != null) return new(_graphId, -1);
            return new(_graphId, ++_revision);
        }
    }

    public bool IsCurrent(Stamp stamp)
    {
        lock (_gate) return stamp == new Stamp(_graphId, _revision);
    }

    public void Reset(Guid id)
    {
        lock (_gate)
        {
            _graphId = id;
            _revision++;
            _pending = null;
        }
    }

    public bool Queue(Guid id, T value)
    {
        lock (_gate)
        {
            if (id != _graphId) return false;
            _revision++;
            _pending = value;
            return true;
        }
    }

    public T? TakePending(Guid id)
    {
        lock (_gate)
        {
            if (id != _graphId) return null;
            var value = _pending;
            _pending = null;
            return value;
        }
    }
}
