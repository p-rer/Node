using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

// Only graph/domain services are doubled. Views, capture, routing, dispatcher,
// adorners, drawing retention, and control XAML run on the real WPF runtime.
namespace Node.Editor.ViewModel
{
    public sealed class TestCommand(Action<object?> action) : ICommand
    {
        public bool CanExecute(object? p) => true;
        public void Execute(object? p) => action(p);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
    public sealed class PortViewModel
    {
        public Guid NodeId { get; } = Guid.NewGuid();
        public string Name { get; set; } = "port";
        public Point Position { get; set; }
        public Brush Color => Brushes.White;
    }
    public sealed class ConnectionViewModel
    {
        public Guid FromNodeId { get; set; }
        public Guid ToNodeId { get; set; }
        public string FromPortName { get; set; } = "";
        public string ToPortName { get; set; } = "";
    }
    public sealed class NodeViewModel
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public bool IsSelected { get; set; }
        public bool CanEditPorts { get; set; }
        public event EventHandler? EditArgumentPortsRequested;
        public int SubscriberCount => EditArgumentPortsRequested?.GetInvocationList().Length ?? 0;
        public Node.Graph.Port.PortDefinition[] GetArgumentPortDefinitions() => [];
        public void ApplyArgumentPorts(Node.Graph.Port.PortDefinition[] definitions) { }
    }
    public sealed class GraphViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
        public PortViewModel? DraggingFromPort { get; set { field = value; Notify(); } }
        public Point? TemporaryEndPoint { get; set { field = value; Notify(); } }
        public double Zoom { get; set { field = value; Notify(); } } = 1;
        public double PanX { get; set { field = value; Notify(); } }
        public double PanY { get; set { field = value; Notify(); } }
        public double Width { get; set; }
        public double Height { get; set; }
        public Point? PendingContextPoint { get; set; }
        public ObservableCollection<ConnectionViewModel> Connections { get; } = [];
        public ObservableCollection<NodeViewModel> Nodes { get; } = [];
        public ObservableCollection<NodeViewModel> SelectedNodes { get; } = [];
        public int ConnectCount, SelectionCount, EndDragCount, LastPolygonCount;
        public object? LastConnection;
        public ICommand ConnectPortsCommand => new TestCommand(p => { ConnectCount++; LastConnection = p; });
        public ICommand DisconnectCommand => new TestCommand(_ => { });
        public void ClearSelection() => SelectedNodes.Clear();
        public void ApplyRectSelection(Rect r, bool add) => SelectionCount++;
        public void ApplyLassoSelection(IReadOnlyList<Point> p, bool add) { SelectionCount++; LastPolygonCount = p.Count; }
        public Point TransformToCanvas(Point p) => new((p.X - PanX) / Zoom, (p.Y - PanY) / Zoom);
        public Point TransformToScreen(Point p) => new(p.X * Zoom + PanX, p.Y * Zoom + PanY);
        public void EndNodeDrag() => EndDragCount++;
        public void BeginNodeDrag(NodeViewModel n) { }
        public void UpdateNodeDrag(Vector d) { }
        public void SelectSingle(NodeViewModel n) { ClearSelection(); AddToSelection(n); }
        public void AddToSelection(NodeViewModel n) => SelectedNodes.Add(n);
    }
}
namespace Node.Graph.Port { public sealed class PortDefinition { public string Label = "", Name = ""; } }
namespace Node.Editor.View
{
    public static class ArgumentPortEditWindow
    {
        public static bool TryCreate(Window w, List<string> labels, out Node.Graph.Port.PortDefinition d) { d = new(); return false; }
    }
    public static class ArgumentPortsWindow
    {
        public static bool TryEdit(Window w, Node.Graph.Port.PortDefinition[] d, out Node.Graph.Port.PortDefinition[] r) { r = []; return false; }
    }
}
