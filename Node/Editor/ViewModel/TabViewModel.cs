using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Node.Editor.Command;
using Node.Graph;
using Node.Graph.Snapshot;
using YukkuriMovieMaker.Commons;

namespace Node.Editor.ViewModel;

public sealed class TabViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly Action<TabViewModel>? _closeAction;
    private readonly PropertyChangedEventHandler? _entryPropertyChangedHandler;
    private readonly NamedGraphSnapshot? _subscribedEntry;
    private string _title;

    public TabViewModel(
        NodeGraph graph,
        string title,
        NodeEditorViewModel nodeEditorViewModel,
        Action<TabViewModel>? closeAction = null,
        IEditorInfo? editorInfo = null,
        Guid? graphId = null)
    {
        Graph = graph;
        _title = title;
        GraphViewModel = new GraphViewModel(graph, nodeEditorViewModel, editorInfo);
        _closeAction = closeAction;

        CloseCommand = new RelayCommand(Close, () => _closeAction != null);

        if (graphId is { } id &&
            GraphLibraryViewModel.Current is { } library &&
            library.TryGetEntry(id, out var entry))
        {
            _subscribedEntry = entry;
            _entryPropertyChangedHandler = (_, args) =>
            {
                if (args.PropertyName == nameof(NamedGraphSnapshot.Name))
                    Title = entry.Name;
            };
            entry.PropertyChanged += _entryPropertyChangedHandler;
        }
    }

    public string Title
    {
        get => _title;
        private set => SetField(ref _title, value);
    }

    public GraphViewModel GraphViewModel { get; }
    public ICommand CloseCommand { get; }
    public NodeGraph Graph { get; }

    public void Dispose()
    {
        if (_subscribedEntry != null && _entryPropertyChangedHandler != null)
            _subscribedEntry.PropertyChanged -= _entryPropertyChangedHandler;

        GraphViewModel.Dispose();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    internal void SetTitle(string title)
    {
        Title = title;
    }

    internal void SetEditorInfo(IEditorInfo? info)
    {
        GraphViewModel.EditorInfo = info;
    }

    private void Close()
    {
        _closeAction?.Invoke(this);
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