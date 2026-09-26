using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Node.Graph.Port;
using Node.Localize;
using Node.Nodes.Func;
using Node.Utility;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;

namespace Node;

public sealed class ArgumentsEditor : UserControl, IPropertyEditorControl2
{
    private readonly StackPanel _panel = new();
    private readonly List<HostedRow> _rows = [];
    private IEditorInfo? _editorInfo;

    private NodeEffect? _effect;

    public ArgumentsEditor()
    {
        Content = _panel;
    }

    public ItemProperty[]? ItemProperties
    {
        get;
        set
        {
            Detach();
            field = value;

            if (value is { Length: > 0 } && value[0].Item is NodeEffect effect)
                Attach(effect);
        }
    }

    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;

    public void SetEditorInfo(IEditorInfo? info)
    {
        _editorInfo = info;

        foreach (var row in _rows)
            if (row.Control is IPropertyEditorControl2 control2 && info != null)
                control2.SetEditorInfo(info);
    }

    private void Attach(NodeEffect effect)
    {
        Debug.WriteLine("[ArgumentsEditor] Attach");
        _effect = effect;
        effect.ArgumentDefinitionsChanged += OnArgumentDefinitionsChanged;
        Rebuild();
    }

    private void Detach()
    {
        if (_effect != null)
            _effect.ArgumentDefinitionsChanged -= OnArgumentDefinitionsChanged;

        _effect = null;
        ClearRows();
    }

    private void OnArgumentDefinitionsChanged(object? sender, EventArgs e)
    {
        Rebuild();
    }

    private void Rebuild()
    {
        ClearRows();
        if (_effect == null) return;

        var definitions = _effect.GetCustomArgumentDefinitions();
        var liveNames = definitions.Select(definition => definition.Name).ToList();
        foreach (var definition in definitions)
            AddRow(_effect, definition, liveNames);

        Debug.WriteLine($"[ArgumentsEditor] Rebuild: {definitions.Length} definition(s), {_rows.Count} row(s)");
        Visibility = _panel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddRow(NodeEffect effect, PortDefinition definition, IReadOnlyCollection<string> liveNames)
    {
        if (!ArgumentPortKinds.TryGetKind(definition.ValueType, out var kind)) return;

        var label = new TextBlock
        {
            Text = string.IsNullOrEmpty(definition.Label) ? definition.Name : definition.Label,
            Margin = new Thickness(0, 6, 0, 2),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, SystemColors.ControlTextBrushKey);

        HostedRow? row = null;
        try
        {
            var attribute = CreateEditorAttribute(kind);
            var control = attribute.Create();
            if (control is not IPropertyEditorControl editorControl)
                throw new InvalidOperationException(
                    $"{control.GetType().FullName} は IPropertyEditorControl を実装していません。");

            if (kind == ArgumentPortKind.Color)
                control.MinHeight = 28;

            object owner;
            PropertyInfo valueProperty;
            IArgumentValueHost? host = null;

            if (kind == ArgumentPortKind.Number)
            {
                var slot = effect.EnsureArgumentSlot(definition.Name, liveNames)
                           ?? throw new InvalidOperationException(TextUi.ArgumentSlotsExhausted);
                owner = effect;
                valueProperty = NodeEffect.GetArgumentSlotProperty(slot);
            }
            else
            {
                host = kind switch
                {
                    ArgumentPortKind.Bool => new BoolArgumentValueHost(effect, definition),
                    ArgumentPortKind.Color => new ColorArgumentValueHost(effect, definition),
                    _ => new TextArgumentValueHost(effect, definition)
                };
                owner = host;
                valueProperty = host.GetType().GetProperty("Value")!;
            }

            EventHandler begin = (_, _) => BeginEdit?.Invoke(this, EventArgs.Empty);
            EventHandler end = (_, _) => EndEdit?.Invoke(this, EventArgs.Empty);

            row = new HostedRow(attribute, control, editorControl, host, label, begin, end);

            attribute.SetBindings(control, owner, owner, valueProperty);
            editorControl.BeginEdit += begin;
            editorControl.EndEdit += end;

            if (control is IPropertyEditorControl2 control2 && _editorInfo != null)
                control2.SetEditorInfo(_editorInfo);

            _rows.Add(row);
            _panel.Children.Add(label);
            _panel.Children.Add(control);
        }
        catch (Exception ex) when (!ExceptionPolicy.IsFatal(ex))
        {
            Debug.WriteLine($"[ArgumentsEditor] Failed to create editor for '{definition.Name}': {ex}");
            row?.Release();

            _panel.Children.Add(label);
            _panel.Children.Add(new TextBlock
            {
                Text = ex.Message,
                Foreground = Brushes.Red,
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                ToolTip = ex.ToString()
            });
        }
    }

    private void ClearRows()
    {
        foreach (var row in _rows)
            row.Release();

        _rows.Clear();
        _panel.Children.Clear();
    }

    private static PropertyEditorAttribute2 CreateEditorAttribute(ArgumentPortKind kind)
    {
        return kind switch
        {
            ArgumentPortKind.Number => new AnimationSliderAttribute("F2", "", -100, 100),
            ArgumentPortKind.Bool => new ToggleSliderAttribute(),
            ArgumentPortKind.Color => new ColorPickerAttribute(),
            _ => new TextEditorAttribute { AcceptsReturn = true }
        };
    }

    private sealed class HostedRow(
        PropertyEditorAttribute2 attribute,
        FrameworkElement control,
        IPropertyEditorControl editorControl,
        IArgumentValueHost? host,
        TextBlock label,
        EventHandler begin,
        EventHandler end)
    {
        public FrameworkElement Control { get; } = control;
        public TextBlock Label { get; } = label;

        public void Release()
        {
            editorControl.BeginEdit -= begin;
            editorControl.EndEdit -= end;

            try
            {
                attribute.ClearBindings(Control);
            }
            catch (Exception ex) when (!ExceptionPolicy.IsFatal(ex))
            {
                Debug.WriteLine($"[ArgumentsEditor] ClearBindings failed: {ex}");
            }

            host?.Dispose();
        }
    }
}