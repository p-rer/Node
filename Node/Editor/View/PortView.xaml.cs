using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Node.Editor.ViewModel;

namespace Node.Editor.View;

public partial class PortView
{
    private GraphViewModel? _dragGraph;
    private bool _positionUpdatePending;
    public PortView()
    {
        InitializeComponent();
        Loaded += UpdatePosition;
        LayoutUpdated += UpdatePosition;
        LostMouseCapture += OnLostMouseCapture;
        Unloaded += (_, _) => CancelConnection();
        DataContextChanged += (_, _) => CancelConnection();
    }

    private void UpdatePosition(object? sender, EventArgs e)
    {
        if (_positionUpdatePending || !IsLoaded || DataContext is not PortViewModel vm)
            return;
        _positionUpdatePending = true;

        Dispatcher.BeginInvoke(() =>
        {
            _positionUpdatePending = false;
            if (!IsLoaded || !ReferenceEquals(DataContext, vm)) return;
            var graphView = FindParent<GraphView>(this);
            if (graphView == null)
                return;

            var root = graphView.FindName("RootGrid") as UIElement;
            if (root == null)
                return;

            if (ActualWidth == 0 || ActualHeight == 0)
                return;

            var transform = TransformToAncestor(root);
            var center = transform.Transform(
                new Point(ActualWidth / 2, ActualHeight / 2));

            if (graphView.DataContext is GraphViewModel graph)
                vm.Position = graph.TransformToCanvas(center);
        }, DispatcherPriority.Render);
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindParent<GraphView>(this)?.DataContext is not GraphViewModel graph) return;
        if (e.ClickCount == 2)
        {
            CancelConnection();
            if (DataContext is not PortViewModel vm) return;

            var related = graph.Connections
                .Where(c =>
                    (c.FromNodeId == vm.NodeId && c.FromPortName == vm.Name) ||
                    (c.ToNodeId == vm.NodeId && c.ToPortName == vm.Name))
                .ToList();

            foreach (var c in related)
                graph.DisconnectCommand.Execute(c);

            e.Handled = true;
            return;
        }

        if (DataContext is not PortViewModel port) return;

        _dragGraph = graph;
        graph.DraggingFromPort = port;
        graph.TemporaryEndPoint = null;
        if (!CaptureMouse()) CancelConnection();
        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var graph = _dragGraph;
        if (graph == null) return;
        var source = graph.DraggingFromPort;
        var targetView = HitTestPortView(e);
        var target = targetView?.DataContext as PortViewModel;

        // Resolve the target before release, and clear state before callbacks can
        // unload/rebind this view (ConnectPorts rebuilds the node views).
        CancelConnection();
        e.Handled = true;
        if (source != null && target != null)
            graph.ConnectPortsCommand.Execute((source, target));
    }

    internal void CancelConnection()
    {
        var graph = _dragGraph;
        _dragGraph = null;
        if (graph != null)
        {
            graph.DraggingFromPort = null;
            graph.TemporaryEndPoint = null;
        }
        if (IsMouseCaptured) ReleaseMouseCapture();
    }

    private void OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, this)) CancelConnection();
    }

    private PortView? HitTestPortView(MouseButtonEventArgs e)
    {
        var graphView = FindParent<GraphView>(this);
        if (graphView?.FindName("RootGrid") is not UIElement root) return null;

        var hit = root.InputHitTest(e.GetPosition(root)) as DependencyObject;
        return FindSelfOrParent<PortView>(hit);
    }

    private static T? FindSelfOrParent<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node != null)
        {
            if (node is T match) return match;
            node = VisualTreeHelper.GetParent(node);
        }

        return null;
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        if (child == null)
            return null;
        var parentObject = VisualTreeHelper.GetParent(child);

        return parentObject switch
        {
            null => null,
            T parent => parent,
            _ => FindParent<T>(parentObject)
        };
    }
}