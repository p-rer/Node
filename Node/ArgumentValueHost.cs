using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using Node.Graph.Port;
using Node.Nodes.Func;
using YukkuriMovieMaker.Controls;

namespace Node;

internal interface IArgumentValueHost : IDisposable
{
}

internal abstract class ArgumentValueHostBase : IArgumentValueHost, INotifyPropertyChanged
{
    private readonly PortDefinition _definition;
    private readonly NodeEffect _effect;
    private readonly ArgumentPortKind _kind;
    private readonly INotifyPropertyChanged? _notifier;
    private bool _isWriting;

    protected ArgumentValueHostBase(NodeEffect effect, PortDefinition definition, ArgumentPortKind kind)
    {
        _effect = effect;
        _definition = definition;
        _kind = kind;

        _notifier = effect;
        if (_notifier != null)
            _notifier.PropertyChanged += OnEffectPropertyChanged;
    }

    public void Dispose()
    {
        if (_notifier != null)
            _notifier.PropertyChanged -= OnEffectPropertyChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected object GetEditorValue()
    {
        var stored = _effect.GetArgumentStorageValue(_definition.Name) ?? _definition.DefaultValue;
        return ArgumentPortKinds.ToEditorValue(_kind, stored);
    }

    protected void SetEditorValue(object? value)
    {
        _isWriting = true;
        try
        {
            _effect.SetArgumentStorageValue(_definition.Name, ArgumentPortKinds.ToStorage(_kind, value));
        }
        finally
        {
            _isWriting = false;
        }
    }

    private void OnEffectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isWriting) return;
        if (e.PropertyName is not (nameof(NodeEffect.ArgumentValues) or null or "")) return;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Value"));
    }
}

internal sealed class BoolArgumentValueHost(NodeEffect effect, PortDefinition definition)
    : ArgumentValueHostBase(effect, definition, ArgumentPortKind.Bool)
{
    [Display(Name = "Value")]
    [ToggleSlider]
    public bool Value
    {
        get => (bool)GetEditorValue();
        set => SetEditorValue(value);
    }
}

internal sealed class ColorArgumentValueHost(NodeEffect effect, PortDefinition definition)
    : ArgumentValueHostBase(effect, definition, ArgumentPortKind.Color)
{
    [Display(Name = "Value")]
    [ColorPicker]
    public Color Value
    {
        get => (Color)GetEditorValue();
        set => SetEditorValue(value);
    }
}

internal sealed class TextArgumentValueHost(NodeEffect effect, PortDefinition definition)
    : ArgumentValueHostBase(effect, definition, ArgumentPortKind.Text)
{
    [Display(Name = "Value")]
    [TextEditor(AcceptsReturn = true)]
    public string Value
    {
        get => (string)GetEditorValue();
        set => SetEditorValue(value);
    }
}