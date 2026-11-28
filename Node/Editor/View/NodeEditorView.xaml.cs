using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Node.Editor.ViewModel;

namespace Node.Editor.View;

public partial class NodeEditorView
{
    private Window? _hostWindow;
    private IInputElement? _lastFocused;

    public NodeEditorView()
    {
        InitializeComponent();
#if DEBUG
        DebugKeyTrace.Install();
        Loaded +=
 (_, _) => Debug.WriteLine($"[NodeEditorView] Loaded. Focusable={Focusable} DataContext={DataContext?.GetType().Name}");
        Unloaded += (_, _) => Debug.WriteLine("[NodeEditorView] Unloaded");
        GotKeyboardFocus += (_, e) => Debug.WriteLine($"[NodeEditorView] GotKeyboardFocus new={e.NewFocus}");
#endif
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        GotKeyboardFocus += (_, e) => _lastFocused = e.NewFocus;
        IsKeyboardFocusWithinChanged += (_, e) =>
        {
            // フォーカスを取り戻したタイミングでグラフを再同期する。
            if (e.NewValue is true) RefreshGraphs();
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshGraphs();

        // YMM4本体は Ctrl+A/C/V や Delete などを、ルーティングイベントの最上流（ウィンドウ側）で先に処理して
        // Handled にしてしまうため、Viewの InputBindings やウィンドウの PreviewKeyDown では間に合わない。
        // ルーティングが始まる前の入力段階で拾い、ノードエディタにフォーカスがあるときだけ自前で処理する。
        InputManager.Current.PreProcessInput -= OnPreProcessInput;
        InputManager.Current.PreProcessInput += OnPreProcessInput;

        // フローティングウィンドウの再アクティブ化などで、AvalonDockがフォーカスを外側のコンテナへ移すことがある。
        var window = Window.GetWindow(this);
        if (window == null || ReferenceEquals(window, _hostWindow)) return;
        DetachHostWindow();
        _hostWindow = window;
        window.Activated += OnHostActivated;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        InputManager.Current.PreProcessInput -= OnPreProcessInput;
        DetachHostWindow();
    }

    private void DetachHostWindow()
    {
        if (_hostWindow == null) return;
        _hostWindow.Activated -= OnHostActivated;
        _hostWindow = null;
    }

    /// <summary>
    ///     フォーカスがこのViewを内包するコンテナ自身にある（＝具体的な編集対象にフォーカスが無い）か。
    /// </summary>
    private bool IsFocusOnHostingContainer()
    {
        return IsVisible && Keyboard.FocusedElement is Visual focused && !ReferenceEquals(focused, this) &&
               focused.IsAncestorOf(this);
    }

    private void OnHostActivated(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (!IsFocusOnHostingContainer()) return;
            (_lastFocused as UIElement)?.Focus();
        });
    }

    private void OnPreProcessInput(object sender, PreProcessInputEventArgs e)
    {
        if (e.StagingItem.Input is not KeyEventArgs { RoutedEvent: var routed } keyArgs ||
            routed != Keyboard.PreviewKeyDownEvent) return;
        if (keyArgs.Handled || !IsVisible) return;

        // テキスト入力中は、コピー・貼り付け・削除などを入力欄本来の動作に任せる。
        if (Keyboard.FocusedElement is TextBoxBase or PasswordBox) return;

        // フォーカス位置は、ノードの再描画やAvalonDockの動作で不意に外れるため、それだけには頼らない。
        // マウスがノードエディタの上にあるときも、操作対象とみなす。
        if (!IsKeyboardFocusWithin && !IsFocusOnHostingContainer() && !IsMouseOver) return;

        foreach (InputBinding binding in InputBindings)
        {
            if (binding.Gesture?.Matches(this, keyArgs) != true) continue;
            if (binding.Command is not { } command) continue;

            // 実行できない状態（ノード未選択でDeleteなど）でも、キーはここで消費する。
            // 消費しないと、同じキーがYMM4本体側（タイムラインの削除など）で実行されてしまう。
            if (command.CanExecute(binding.CommandParameter))
                command.Execute(binding.CommandParameter);
            keyArgs.Handled = true;
            e.Cancel();
            return;
        }
    }

    private void RefreshGraphs()
    {
        (DataContext as NodeEditorViewModel)?.RefreshAllOpenGraphs();
    }
}

#if DEBUG
/// <summary>
///     ショートカットが届かない原因の切り分け用。アプリ全体のキー・マウス入力と、その時のフォーカス位置を記録する。
/// </summary>
internal static class DebugKeyTrace
{
    private static bool _installed;

    public static void Install()
    {
        if (_installed) return;
        _installed = true;

        EventManager.RegisterClassHandler(typeof(UIElement), UIElement.PreviewKeyDownEvent,
            new KeyEventHandler((sender, e) =>
            {
                if (!ReferenceEquals(sender, e.OriginalSource) && e.OriginalSource is not UIElement) return;
                if (e.Key is Key.LeftCtrl or Key.RightCtrl) return;
                Debug.WriteLine(
                    $"[KeyTrace] PreviewKeyDown {e.Key} ctrl={Keyboard.Modifiers.HasFlag(ModifierKeys.Control)} " +
                    $"handled={e.Handled} source={e.OriginalSource?.GetType().Name} focus={Keyboard.FocusedElement?.GetType().Name} " +
                    $"inNodeEditor={IsInNodeEditor(Keyboard.FocusedElement as DependencyObject)}");
            }), true);

        EventManager.RegisterClassHandler(typeof(UIElement), UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler((sender, e) =>
            {
                if (e.OriginalSource is DependencyObject d && IsInNodeEditor(d))
                    Debug.WriteLine(
                        $"[KeyTrace] Click in NodeEditor source={e.OriginalSource.GetType().Name} focus(before)={Keyboard.FocusedElement?.GetType().Name}");
            }), true);
    }

    private static bool IsInNodeEditor(DependencyObject? d)
    {
        while (d != null)
        {
            if (d is NodeEditorView) return true;
            d = d is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(d)
                : LogicalTreeHelper.GetParent(d);
        }

        return false;
    }
}
#endif