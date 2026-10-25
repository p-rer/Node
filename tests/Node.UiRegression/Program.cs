using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Node.Editor;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Node.Editor.Control;
using Node.Editor.Control.Bezier.Model;
using Node.Editor.Control.Bezier.View;
using Node.Editor.Control.Bezier.ViewModel;
using Node.Editor.View;
using Node.Editor.View.Adorners;
using Node.Editor.ViewModel;

internal static class Program
{
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    private static readonly List<(string Name, Action<Fixture> Test)> Tests =
    [
        ("localizers use generated classes and production CSV resources", f =>
        {
            var uiCulture = Node.Localize.TextUi.Culture;
            var nodeCulture = Node.Localize.TextNode.Culture;
            try
            {
                Node.Localize.TextUi.Culture = new System.Globalization.CultureInfo("ja-JP");
                Node.Localize.TextNode.Culture = new System.Globalization.CultureInfo("ja-JP");
                Check(Node.Localize.TextUi.CopyMenu == "コピー" && Node.Localize.TextNode.NoMatchingResults == "(該当なし)", "Japanese resources missing");
                Node.Localize.TextUi.Culture = new System.Globalization.CultureInfo("en-US");
                Node.Localize.TextNode.Culture = new System.Globalization.CultureInfo("en-US");
                Check(Node.Localize.TextUi.CopyMenu == "Copy" && Node.Localize.TextNode.NoMatchingResults == "(No results)", "English satellite resources missing");
            }
            finally
            {
                Node.Localize.TextUi.Culture = uiCulture;
                Node.Localize.TextNode.Culture = nodeCulture;
            }
        }),
        ("submenu timer closes child and stops on root close", f =>
        {
            var popup = new AddNodePopup(new() { ["Category/Subcategory"] = [new NodeTypeInfo { Type = typeof(string), Label = "Test", Color = "White" }] }, _ => { });
            var host = Get<StackPanel>(popup, "_itemsHost");
            var row = (Border)host.Children[0];
            row.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
            var children = Get<List<Popup>>(popup, "_openSubmenus"); Check(children.Count == 1 && children[0].IsOpen, "submenu did not open");
            row.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseLeaveEvent });
            var timers = Get<List<DispatcherTimer>>(popup, "_submenuCloseTimers"); Check(timers[0].IsEnabled, "submenu close timer not started");
            var frame = new DispatcherFrame(); var timeout = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            timeout.Tick += (_, _) => { timeout.Stop(); frame.Continue = false; }; timeout.Start(); Dispatcher.PushFrame(frame);
            Check(children.Count == 0 && !timers[0].IsEnabled, "timer failed to close submenu");
            Call(popup, "CloseAllSubmenus"); Check(timers.Count == 0, "timers retained after close");
        }),
        ("port release erases retained preview without another move", f =>
        {
            var p = f.Port(30, 30); Start(p); Preview(f); Check(DrawingCount(f) > 0, "preview was not rendered");
            f.Move(300, 250); Up(p); Pump();
            Check(f.Vm.DraggingFromPort == null && f.Vm.TemporaryEndPoint == null, "gesture remained");
            Check(DrawingCount(f) == 0 && f.Vm.ConnectCount == 0, "stale line or blank-space connection");
        }),
        ("port drop resolves physical target before capture release", f =>
        {
            var source = f.Port(30, 30); var target = f.Port(150, 80); Start(source); Preview(f);
            Check(source.IsMouseCaptured && ReferenceEquals(f.Vm.DraggingFromPort, source.DataContext), "source capture/owner missing before move");
            f.Move(155, 85);
            Up(source);
            Check(f.Vm.ConnectCount == 1, "connection was not requested");
            var pair = ((PortViewModel, PortViewModel))f.Vm.LastConnection!;
            Check(ReferenceEquals(pair.Item1, source.DataContext) && ReferenceEquals(pair.Item2, target.DataContext), "wrong ports");
            Check(f.Vm.DraggingFromPort == null && !source.IsMouseCaptured, "connection state survived completion");
        }),
        ("port capture theft cancels and erases preview", f =>
        {
            var p = f.Port(30, 30); Start(p); Preview(f); Check(f.Graph.CaptureMouse(), "capture unavailable"); Pump();
            Check(f.Vm.DraggingFromPort == null && DrawingCount(f) == 0, "capture theft left preview");
        }),
        ("port rebinding cancels the original graph", f =>
        {
            var p = f.Port(30, 30); Start(p); Preview(f); p.DataContext = new PortViewModel(); Pump();
            Check(f.Vm.DraggingFromPort == null && !p.IsMouseCaptured && DrawingCount(f) == 0, "old graph remained active");
        }),
        ("port unload cancels even after leaving its ancestor tree", f =>
        {
            var p = f.Port(30, 30); Start(p); Preview(f); f.Overlay.Children.Remove(p); Pump();
            Check(f.Vm.DraggingFromPort == null && DrawingCount(f) == 0, "detached port retained owner");
        }),
        ("Pan mode also creates a connection adorner", f =>
        {
            f.Reload(GraphControlMode.Pan); var p = f.Port(30, 30); Start(p); Preview(f);
            Check(DrawingCount(f) > 0, "Pan mode has no preview");
        }),
        ("mode change clears selection adorner and connection gesture", f =>
        {
            f.Graph.Mode = GraphControlMode.LassoSelection; var p = f.Port(30, 30); Start(p); Preview(f);
            f.Graph.Mode = GraphControlMode.Pan; Pump();
            Check(Get<SelectionAdornerBase?>(f.Graph, "_selectionAdorner") == null, "removed adorner retained");
            Check(!p.IsMouseCaptured && f.Vm.DraggingFromPort == null, "mode change left gesture");
        }),
        ("graph rebind updates adorner and cancels old VM", f =>
        {
            var old = f.Vm; var p = f.Port(30, 30); Start(p); Preview(f);
            var next = new GraphViewModel(); f.Graph.DataContext = next; Pump();
            Check(old.DraggingFromPort == null, "old VM gesture retained");
            Check(ReferenceEquals(f.Adorner.DataContext, next) && DrawingCount(f) == 0, "adorner still uses old VM");
            Check(next.Width > 0 && next.Height > 0, "new VM dimensions missing");
        }),
        ("reload restores preview property observation", f =>
        {
            f.Reload(GraphControlMode.RectSelection); Preview(f); Check(DrawingCount(f) > 0, "preview missing after reload");
            f.Vm.DraggingFromPort = null; f.Vm.TemporaryEndPoint = null; Pump();
            Check(DrawingCount(f) == 0, "property changes no longer observed");
        }),
        ("normal lasso release retains polygon until selection is applied", f =>
        {
            f.Graph.Mode = GraphControlMode.LassoSelection;
            var a = (LassoSelectionAdorner)Get<SelectionAdornerBase>(f.Graph, "_selectionAdorner");
            a.Begin(new(10, 10)); a.AddPoint(new(80, 10)); a.AddPoint(new(80, 80));
            Check(f.Graph.CaptureMouse(), "capture unavailable"); Set(f.Graph, "_isSelecting", true);
            Call(f.Graph, "OnMouseLeftButtonUp", f.Graph, Args(MouseButton.Left));
            Check(f.Vm.SelectionCount == 1 && f.Vm.LastPolygonCount == 3, "release reentrancy erased polygon or selected twice");
        }),
        ("unexpected graph capture loss cancels rather than commits selection", f =>
        {
            Check(f.Graph.CaptureMouse(), "capture unavailable"); Set(f.Graph, "_isSelecting", true);
            f.Overlay.CaptureMouse();
            Check(!Get<bool>(f.Graph, "_isSelecting") && f.Vm.SelectionCount == 0, "interrupted selection committed");
        }),
        ("child capture-loss event does not end graph gesture", f =>
        {
            var p = f.Port(30, 30); Set(f.Graph, "_isSelecting", true);
            p.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.LostMouseCaptureEvent });
            Check(Get<bool>(f.Graph, "_isSelecting"), "child event cancelled parent");
        }),
        ("unrelated mouse-up cannot end left-button pan", f =>
        {
            f.Graph.Mode = GraphControlMode.Pan; Check(f.Graph.CaptureMouse(), "capture unavailable");
            Set(f.Graph, "_isPanning", true); Set(f.Graph, "_panButton", (MouseButton?)MouseButton.Left);
            Call(f.Graph, "OnMouseUp", f.Graph, Args(MouseButton.Middle));
            Check(Get<bool>(f.Graph, "_isPanning") && f.Graph.IsMouseCaptured, "middle release ended left pan");
            Call(f.Graph, "OnMouseLeftButtonUp", f.Graph, Args(MouseButton.Left));
            Check(!Get<bool>(f.Graph, "_isPanning") && !f.Graph.IsMouseCaptured, "own release failed");
        }),
        ("fresh right click clears suppression left by Ctrl-right pan", f =>
        {
            Set(f.Graph, "_suppressNextContextMenu", true);
            Call(f.Graph, "OnMouseDown", f.Graph, Args(MouseButton.Right));
            Check(!Get<bool>(f.Graph, "_suppressNextContextMenu"), "next context menu still suppressed");
        }),
        ("node capture loss commits a drag once", f =>
        {
            var n = f.Node(); Check(n.CaptureMouse(), "capture unavailable");
            Set(n, "_dragGraph", f.Vm); Set(n, "_isMouseDown", true); Set(n, "_isDragging", true);
            f.Graph.CaptureMouse(); Up(n);
            Check(f.Vm.EndDragCount == 1 && !Get<bool>(n, "_isMouseDown"), "node drag completed twice or remained active");
        }),
        ("node edit-port subscriptions follow binding and unload", f =>
        {
            var a = new NodeViewModel { CanEditPorts = true }; var b = new NodeViewModel { CanEditPorts = true };
            var n = f.Node(); n.DataContext = a; Check(a.SubscriberCount == 1, "missing subscriber");
            n.DataContext = b; Check(a.SubscriberCount == 0 && b.SubscriberCount == 1, "old subscriber retained");
            f.Overlay.Children.Remove(n); Pump(); Check(b.SubscriberCount == 0, "unloaded subscriber retained");
        }),
        ("numeric capture theft restores cursor and ends edit once", f =>
        {
            int begin = 0, end = 0;
            var n = new NumberPort { BeginEditCommand = new TestCommand(_ => begin++), EndEditCommand = new TestCommand(_ => end++) };
            f.Add(n); var box = Find<TextBox>(n)!; Check(box != null, "numeric textbox missing");
            Call(n, "Box_PreviewMouseLeftButtonDown", box!, Args(MouseButton.Left));
            Check(begin == 1 && box!.IsMouseCaptured, "numeric edit did not start"); f.Graph.CaptureMouse();
            Call(n, "Box_PreviewMouseLeftButtonUp", box!, Args(MouseButton.Left));
            Check(end == 1 && Mouse.OverrideCursor == null && !Get<bool>(n, "_isClicking"), "numeric edit/cursor leaked");
        }),
        ("color wheel ignores right-button initiation and release", f =>
        {
            var c = new ColorPicker(); f.Add(c); var wheel = (Canvas)c.FindName("ColorWheelCanvas");
            Call(c, "ColorWheelCanvas_MouseDown", wheel, Args(MouseButton.Right)); Check(!Get<bool>(c, "_isDragging"), "right initiated color edit");
            Check(wheel.CaptureMouse(), "capture unavailable"); Set(c, "_isDragging", true); Set(c, "_draggingInRing", true);
            Call(c, "ColorWheelCanvas_MouseUp", wheel, Args(MouseButton.Right)); Check(Get<bool>(c, "_isDragging"), "right ended left edit");
            f.Graph.CaptureMouse(); Check(!Get<bool>(c, "_isDragging") && !Get<bool>(c, "_draggingInRing"), "color state remained");
        }),
        ("Bezier capture theft completes edit once; unrelated release is ignored", f =>
        {
            var curve = new BezierCurve(); var b = new BezierEditor { ViewModel = new BezierEditorViewModel(curve), Width = 200, Height = 200 };
            f.Add(b); int end = 0; b.EditCompleted += (_, _) => end++;
            Check(b.CaptureMouse(), "capture unavailable");
            Set(b, "_dragContext", new BezierDragContext(BezierHitType.Node, curve.Nodes[0], new(0, 0)));
            Call(b, "OnMouseUp", Args(MouseButton.Right)); Check(end == 0 && b.IsMouseCaptured, "right release ended left edit");
            f.Graph.CaptureMouse(); Call(b, "OnMouseUp", Args(MouseButton.Left));
            Check(end == 1 && Get<BezierDragContext?>(b, "_dragContext") == null, "Bezier edit completed twice or stuck");
        })
    ];

    [STAThread]
    private static int Main()
    {
        _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        int failures = 0;
        foreach (var (name, test) in Tests)
        {
            try { using var f = new Fixture(); test(f); Console.WriteLine($"PASS {name}"); }
            catch (Exception ex) { failures++; Console.Error.WriteLine($"FAIL {name}: {ex}"); }
        }
        Console.WriteLine($"{Tests.Count - failures}/{Tests.Count} passed");
        Application.Current.Shutdown(); return failures == 0 ? 0 : 1;
    }
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    private static FieldInfo Field(object o, string name) => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static T Get<T>(object o, string name) => (T)Field(o, name).GetValue(o)!;
    private static void Set(object o, string name, object? value) => Field(o, name).SetValue(o, value);
    private static object? Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!.Invoke(o, args);
    private static MouseButtonEventArgs Args(MouseButton button) => new(Mouse.PrimaryDevice, Environment.TickCount, button) { RoutedEvent = Mouse.MouseUpEvent };
    private static void Start(PortView p) => p.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
    private static void Up(UIElement p) => p.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
    private static void Preview(Fixture f) { f.Vm.DraggingFromPort ??= new PortViewModel { Position = new(20, 20) }; f.Vm.TemporaryEndPoint = new(180, 140); Pump(); }
    private static void Pump()
    {
        // A same-thread Dispatcher.Invoke can return before native WM_MOUSEMOVE
        // reaches WPF. Run a nested frame long enough to process native input.
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(20) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
    private static int DrawingCount(Fixture f)
    {
        Pump(); var bitmap = new RenderTargetBitmap(500, 400, 96, 96, PixelFormats.Pbgra32); bitmap.Render(f.Adorner);
        return VisualTreeHelper.GetDrawing(f.Adorner)?.Children.Count ?? 0;
    }
    private static T? Find<T>(DependencyObject o) where T : DependencyObject
    {
        if (o is T found) return found;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++)
            if (Find<T>(VisualTreeHelper.GetChild(o, i)) is { } child) return child;
        return null;
    }
    private sealed class Fixture : IDisposable
    {
        public GraphViewModel Vm { get; } = new();
        public GraphView Graph { get; } = new();
        public Canvas Overlay { get; } = new();
        private readonly AdornerDecorator _decorator;
        private readonly Window _window;
        public ConnectionAdorner Adorner => Get<ConnectionAdorner>(Graph, "_connectionAdorner");
        public Fixture()
        {
            Graph.DataContext = Vm;
            ((Grid)Graph.FindName("RootGrid")).Children.Add(Overlay);
            _decorator = new AdornerDecorator { Child = Graph };
            _window = new Window { Content = _decorator, Width = 520, Height = 440, Left = 20, Top = 20, ShowInTaskbar = false };
            _window.Show(); _window.Activate(); Pump();
        }
        public void Move(double x, double y)
        {
            var p = Graph.PointToScreen(new(x, y));
            Check(SetCursorPos((int)p.X, (int)p.Y), "cursor move failed");
            for (int i = 0; i < 50; i++)
            {
                Pump(); Mouse.Synchronize();
                var actual = Mouse.GetPosition(Graph);
                if (Math.Abs(actual.X - x) <= 1 && Math.Abs(actual.Y - y) <= 1) return;
            }
            throw new InvalidOperationException($"Native cursor movement was not delivered to WPF: expected={x},{y}, actual={Mouse.GetPosition(Graph)}");
        }
        public void Add(FrameworkElement element) { Overlay.Children.Add(element); Pump(); }
        public PortView Port(double x, double y)
        {
            var p = new PortView { DataContext = new PortViewModel(), Width = 10, Height = 10 };
            Canvas.SetLeft(p, x); Canvas.SetTop(p, y); Add(p); return p;
        }
        public NodeView Node() { var n = new NodeView { DataContext = new NodeViewModel(), Width = 100, Height = 80 }; Add(n); return n; }
        public void Reload(GraphControlMode mode) { _decorator.Child = null; Pump(); Graph.Mode = mode; _decorator.Child = Graph; Pump(); }
        public void Dispose() { Mouse.Capture(null); Mouse.OverrideCursor = null; _window.Close(); Pump(); }
    }
}
