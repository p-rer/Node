using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Node.Editor.ViewModel;
using Node.Graph.Port;
using Node.Localize;

namespace Node.Editor.View;

public partial class NodeView
{
    // ノードのドラッグ・クリック用
    private const double DragThreshold = 4.0;
    private bool _isDragging;

    private bool _isMouseDown;
    private GraphViewModel? _dragGraph;
    private NodeViewModel? _argumentPortsVm;
    private Point _mouseDownPos;

    public NodeView()
    {
        InitializeComponent();

        Loaded += (_, _) => ObserveArgumentPorts(DataContext as NodeViewModel);
        Unloaded += (_, _) =>
        {
            FinishDrag();
            ObserveArgumentPorts(null);
        };
        SizeChanged += OnSizeChanged;
        LostMouseCapture += OnLostMouseCapture;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isMouseDown) return;
        if (FindParent<GraphView>(this) is not { DataContext: GraphViewModel graphVm } graphView) return;
        _dragGraph = graphVm;
        _isMouseDown = true;
        _isDragging = false;

        _mouseDownPos = graphVm.TransformToCanvas(e.GetPosition(graphView));

        e.Handled = true;
        Focus();
        if (!CaptureMouse()) FinishDrag();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isMouseDown) return;
        if (DataContext is not NodeViewModel vm) return;
        if (FindParent<GraphView>(this) is not { DataContext: GraphViewModel graphVm } graphView) return;

        var currentPos = graphVm.TransformToCanvas(e.GetPosition(graphView));
        var delta = currentPos - _mouseDownPos;

        if (!_isDragging)
        {
            if (Math.Abs(delta.X) > DragThreshold || Math.Abs(delta.Y) > DragThreshold)
            {
                _isDragging = true;

                graphVm.BeginNodeDrag(vm);
            }
            else
            {
                return;
            }
        }

        graphVm.UpdateNodeDrag(delta);

        _mouseDownPos = currentPos;

        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isMouseDown) return;

        var wasDragging = _isDragging;
        var vm = DataContext as NodeViewModel;
        var graphVm = _dragGraph;

        _isMouseDown = false;
        _isDragging = false;
        _dragGraph = null;

        ReleaseMouseCapture();

        if (wasDragging)
        {
            graphVm?.EndNodeDrag();
        }
        else if (vm != null && graphVm != null)
        {
            if ((Keyboard.Modifiers & (ModifierKeys.Shift | ModifierKeys.Control)) != 0)
            {
                if (graphVm.SelectedNodes.Contains(vm))
                {
                    graphVm.SelectedNodes.Remove(vm);
                    vm.IsSelected = false;
                }
                else
                {
                    graphVm.AddToSelection(vm);
                }
            }
            else
            {
                graphVm.SelectSingle(vm);
            }
        }

        e.Handled = true;
    }

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
        if (DataContext is not NodeViewModel vm) return;
        if (FindParent<GraphView>(this)?.DataContext is not GraphViewModel graphVm) return;

        if (!graphVm.SelectedNodes.Contains(vm))
            graphVm.SelectSingle(vm);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        FinishDrag();
        ObserveArgumentPorts(IsLoaded ? e.NewValue as NodeViewModel : null);
    }

    private void ObserveArgumentPorts(NodeViewModel? vm)
    {
        if (_argumentPortsVm != null)
            _argumentPortsVm.EditArgumentPortsRequested -= OnEditArgumentPortsRequested;
        _argumentPortsVm = vm is { CanEditPorts: true } ? vm : null;
        if (_argumentPortsVm != null)
            _argumentPortsVm.EditArgumentPortsRequested += OnEditArgumentPortsRequested;
    }

    private void OnEditArgumentPortsRequested(object? sender, EventArgs e)
    {
        if (sender is NodeViewModel vm) EditArgumentPorts(vm);
    }

    private void AddArgumentPortButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not NodeViewModel { CanEditPorts: true } vm) return;
        if (GetDialogOwner() is not { } owner) return;

        var current = vm.GetArgumentPortDefinitions();
        if (!ArgumentPortEditWindow.TryCreate(owner, current.Select(GetLabel).ToList(), out var definition))
            return;

        vm.ApplyArgumentPorts([.. current, definition]);
    }

    private void EditArgumentPorts(NodeViewModel vm)
    {
        if (GetDialogOwner() is not { } owner) return;

        if (ArgumentPortsWindow.TryEdit(owner, vm.GetArgumentPortDefinitions(), out var result))
            vm.ApplyArgumentPorts(result);
    }

    private Window? GetDialogOwner()
    {
        return Window.GetWindow(this) ?? Application.Current?.MainWindow;
    }

    private static string GetLabel(PortDefinition definition)
    {
        return string.IsNullOrEmpty(definition.Label) ? definition.Name : definition.Label;
    }

    private void OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, this)) FinishDrag();
    }

    private void FinishDrag()
    {
        var graph = _dragGraph;
        var wasDragging = _isDragging;
        _isMouseDown = false;
        _isDragging = false;
        _dragGraph = null;
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (wasDragging) graph?.EndNodeDrag();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DataContext is NodeViewModel vm)
        {
            vm.Width = ActualWidth;
            vm.Height = ActualHeight;
        }
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