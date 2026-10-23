// Host boundaries only. The state, tab and library classes are linked from production.
using System.Windows.Input;
using Node.Graph.Snapshot;

namespace System.Windows
{
    public readonly record struct Point(double X, double Y);
    public sealed class Application
    {
        public static Application? Current { get; set; } = new();
        public Dispatcher Dispatcher { get; } = new();
    }
    public sealed class Dispatcher
    {
        public bool CheckAccess() => true;
        public void Invoke(Action action) => action();
    }
}
namespace YukkuriMovieMaker.Commons { public interface IEditorInfo { } }
namespace YukkuriMovieMaker.Plugin
{
    public interface IToolViewModel { }
    public sealed class ToolState { public string? SavedState { get; set; } }
    public sealed class CreateNewToolViewRequestedEventArgs : EventArgs { }
}
namespace Node
{
    public sealed class NodeEffect
    {
        public List<(Guid Id, GraphSnapshot Snapshot)> Received { get; } = [];
        public void ApplyExternalGraphUpdate(Guid id, GraphSnapshot snapshot) => Received.Add((id, snapshot));
    }
}
namespace Node.Graph
{
    public abstract class NodeLogic { }
    public sealed class NodeGraph
    {
        public void AddNode(object node) { }
        public void SetVisualState(Guid id, double x, double y) { }
        public void Connect(Guid from, string output, Guid to, string input) { }
    }
}
namespace Node.Graph.Port { public sealed record PortDefinition(string Name, Type ValueType); }
namespace Node.Graph.Snapshot
{
    public sealed class GraphSnapshot { public string Marker { get; set; } = ""; }
    public static class Serializer { public static GraphSnapshot Create(Node.Graph.NodeGraph graph) => new(); }
}
namespace Node.Nodes.Func
{
    public sealed class ArgumentsNode(params Node.Graph.Port.PortDefinition[] definitions) : Node.Graph.NodeLogic
    {
        public Guid Id { get; init; }
        public int PortCount => definitions.Length;
    }
    public sealed class ReturnNode(params Node.Graph.Port.PortDefinition[] definitions) : Node.Graph.NodeLogic
    {
        public Guid Id { get; init; }
        public int PortCount => definitions.Length;
    }
}
namespace Node.ValueTypes { public sealed class ImageWrapper { } }
namespace Node.Editor.Command
{
    public sealed class RelayCommand(Action action, Func<bool>? canExecute = null) : ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => action();
    }
}
namespace Node.Editor.ViewModel
{
    public sealed class GraphViewModel(Node.Graph.NodeGraph graph, NodeEditorViewModel editor,
        YukkuriMovieMaker.Commons.IEditorInfo? info) : IDisposable
    {
        public YukkuriMovieMaker.Commons.IEditorInfo? EditorInfo { get; set; } = info;
        public Node.Graph.NodeGraph Graph => graph;
        public NodeEditorViewModel Editor => editor;
        public bool Disposed { get; private set; }
        public int RefreshCount { get; private set; }
        public List<NodeViewModel> SelectedNodes { get; } = [];
        public List<NodeViewModel> Nodes { get; } = [];
        public double Width { get; set; }
        public double Height { get; set; }
        public double Zoom { get; set; }
        public double PanX { get; set; }
        public double PanY { get; set; }
        public ICommand AddNodeCommand { get; } = new Node.Editor.Command.RelayCommand(() => { });
        public void Refresh() => RefreshCount++;
        public void Copy(out GraphSnapshot? snapshot) => snapshot = new();
        public void Paste(GraphSnapshot? snapshot) { }
        public void ClearSelection() => SelectedNodes.Clear();
        public void DeleteSelectedNode() { }
        public void Dispose() => Disposed = true;
    }
}

namespace Node.Editor.Command
{
    public sealed class RelayCommand<T>(Action<T?> action, Func<T?, bool>? canExecute = null) : ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => canExecute?.Invoke((T?)parameter) ?? true;
        public void Execute(object? parameter) => action((T?)parameter);
    }
}
namespace Node.Editor.ViewModel
{
    public sealed class NodeViewModel
    {
        public Node.Graph.NodeLogic NodeLogic { get; set; } = null!;
        public bool CanEditPorts { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public ICommand EditArgumentPortsCommand { get; } = new Node.Editor.Command.RelayCommand(() => { });
    }
}
namespace Node.Editor.View
{
    public enum GraphControlMode { RectSelection }
    public sealed class AddNodePopup(Dictionary<string, List<Node.Editor.NodeTypeInfo>> categories, Action<Type> selected)
    {
        public bool IsOpen { get; set; }
        public int CategoryCount => categories.Count;
        public void Select(Type type) => selected(type);
    }
}
namespace Node.Graph.Attributes
{
    public sealed class NodeAttribute : Attribute
    {
        public string GetLabel() => "";
        public string GetDescription() => "";
        public string GetCategoryName() => "";
        public string GetCategoryColor() => "";
    }
}
namespace Node.Nodes.Effect.DynamicLoaded
{
    public static class EffectNodeFactory
    {
        public static Node.Graph.Attributes.NodeAttribute[]? GetMenuCategoryAttributes(Type type) => null;
    }
}
