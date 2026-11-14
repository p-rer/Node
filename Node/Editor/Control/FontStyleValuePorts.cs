using System.ComponentModel;
using System.Windows;

namespace Node.Editor.Control;

public sealed class FontWeightPort : PortControlBase
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(int), typeof(FontWeightPort),
            new FrameworkPropertyMetadata(400, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    private readonly BoolPort _bold = new() { Default = false, HorizontalAlignment = HorizontalAlignment.Left };
    private bool _updating;

    public FontWeightPort()
    {
        _bold.BeginEditCommand = BeginEditCommand;
        _bold.EndEditCommand = EndEditCommand;
        Content = _bold;

        DependencyPropertyDescriptor.FromProperty(BoolPort.ValueProperty, typeof(BoolPort))!
            .AddValueChanged(_bold, (_, _) =>
            {
                if (!_updating)
                    Value = _bold.Value ? 700 : 400;
            });

        UpdateEditor();
    }

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((FontWeightPort)d).UpdateEditor();

    private void UpdateEditor()
    {
        if (_updating)
            return;

        _updating = true;
        try
        {
            _bold.Value = Value >= 700;
        }
        finally
        {
            _updating = false;
        }
    }
}

public sealed class FontWidthPort : PortControlBase
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(int), typeof(FontWidthPort),
            new FrameworkPropertyMetadata(5, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    private readonly NumberPort _number = new() { Min = 1, Max = 9, Digits = 0, Default = 5 };

    public FontWidthPort()
    {
        _number.BeginEditCommand = BeginEditCommand;
        _number.EndEditCommand = EndEditCommand;
        Content = _number;

        DependencyPropertyDescriptor.FromProperty(NumberPort.ValueProperty, typeof(NumberPort))!
            .AddValueChanged(_number, (_, _) => Value = (int)Math.Clamp(MathF.Round(_number.Value), 1, 9));

        UpdateEditor();
    }

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((FontWidthPort)d).UpdateEditor();

    private void UpdateEditor()
    {
        _number.Value = Math.Clamp(Value, 1, 9);
    }
}
