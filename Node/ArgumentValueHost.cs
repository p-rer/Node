using System.ComponentModel;
using Node.Graph.Port;
using Node.Nodes.Func;

namespace Node;

internal interface IArgumentValueHost : IDisposable
{
}

internal sealed class ArgumentValueHost<T> : IArgumentValueHost, INotifyPropertyChanged
{
    private readonly PortDefinition _definition;
    private readonly NodeEffect _effect;
    private readonly ArgumentPortKind _kind;
    private readonly INotifyPropertyChanged? _notifier;
    private bool _isWriting;

    public ArgumentValueHost(NodeEffect effect, PortDefinition definition, ArgumentPortKind kind)
    {
        _effect = effect;
        _definition = definition;
        _kind = kind;

        _notifier = effect;
        if (_notifier != null)
            _notifier.PropertyChanged += OnEffectPropertyChanged;
    }

    public T? Value
    {
        get
        {
            var stored = _effect.GetArgumentStorageValue(_definition.Name) ?? _definition.DefaultValue;
            return ArgumentPortKinds.ToEditorValue(_kind, stored) is T typed ? typed : default;
        }
        set
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
    }

    public void Dispose()
    {
        if (_notifier != null)
            _notifier.PropertyChanged -= OnEffectPropertyChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnEffectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isWriting) return;
        if (e.PropertyName is not (nameof(NodeEffect.ArgumentValues) or null or "")) return;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
    }
}