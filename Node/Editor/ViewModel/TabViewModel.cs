using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Node.Editor.Command;
using Node.Graph;
using YukkuriMovieMaker.Commons;

namespace Node.Editor.ViewModel;

public sealed class TabViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly Action<TabViewModel>? _closeAction;
    private string _title;

    public TabViewModel(
        NodeGraph graph,
        string title,
        NodeEditorViewModel nodeEditorViewModel,
        Action<TabViewModel>? closeAction = null,
        IEditorInfo? editorInfo = null)
    {
        Graph = graph;
        _title = title;
        GraphViewModel = new GraphViewModel(graph, nodeEditorViewModel, editorInfo);
        _closeAction = closeAction;

        CloseCommand = new RelayCommand(Close, () => _closeAction != null);
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