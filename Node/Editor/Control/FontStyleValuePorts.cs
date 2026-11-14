using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SkiaSharp;

namespace Node.Editor.Control;

public sealed class FontWeightPort : PortControlBase
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(int), typeof(FontWeightPort),
            new FrameworkPropertyMetadata(400, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    public static readonly DependencyProperty IsVariableFontProperty =
        DependencyProperty.Register(nameof(IsVariableFont), typeof(bool), typeof(FontWeightPort),
            new PropertyMetadata(false, OnVariableFontChanged));

    private readonly BoolPort _bold = new() { Default = false };
    private readonly NumberPort _number = new() { Min = 1, Max = 1000, Digits = 0, Default = 400 };
    private readonly StackPanel _panel = new();
    private bool _updating;

    public FontWeightPort()
    {
        _bold.BeginEditCommand = BeginEditCommand;
        _bold.EndEditCommand = EndEditCommand;
        _number.BeginEditCommand = BeginEditCommand;
        _number.EndEditCommand = EndEditCommand;

        _panel.Children.Add(_bold);
        _panel.Children.Add(_number);
        Content = _panel;

        DependencyPropertyDescriptor.FromProperty(BoolPort.ValueProperty, typeof(BoolPort))!
            .AddValueChanged(_bold, (_, _) =>
            {
                if (_updating || IsVariableFont) return;
                Value = _bold.Value ? 700 : 400;
            });

        DependencyPropertyDescriptor.FromProperty(NumberPort.ValueProperty, typeof(NumberPort))!
            .AddValueChanged(_number, (_, _) =>
            {
                if (_updating || !IsVariableFont) return;
                Value = (int)Math.Clamp(MathF.Round(_number.Value), 1, 1000);
            });

        UpdateEditor();
    }

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public bool IsVariableFont
    {
        get => (bool)GetValue(IsVariableFontProperty);
        set => SetValue(IsVariableFontProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((FontWeightPort)d).UpdateEditor();

    private static void OnVariableFontChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((FontWeightPort)d).UpdateEditor();

    private void UpdateEditor()
    {
        if (_updating) return;

        _updating = true;
        try
        {
            var value = Math.Clamp(Value, 1, 1000);
            _number.Value = value;
            _bold.Value = value >= 700;
            _number.Visibility = IsVariableFont ? Visibility.Visible : Visibility.Collapsed;
            _bold.Visibility = IsVariableFont ? Visibility.Collapsed : Visibility.Visible;
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

    public static readonly DependencyProperty IsVariableFontProperty =
        DependencyProperty.Register(nameof(IsVariableFont), typeof(bool), typeof(FontWidthPort),
            new PropertyMetadata(false, OnVariableFontChanged));

    private readonly NumberPort _number = new() { Min = 1, Max = 9, Digits = 0, Default = 5 };
    private bool _updating;

    public FontWidthPort()
    {
        _number.BeginEditCommand = BeginEditCommand;
        _number.EndEditCommand = EndEditCommand;
        Content = _number;

        DependencyPropertyDescriptor.FromProperty(NumberPort.ValueProperty, typeof(NumberPort))!
            .AddValueChanged(_number, (_, _) =>
            {
                if (_updating || !IsVariableFont) return;
                Value = (int)Math.Clamp(MathF.Round(_number.Value), 1, 9);
            });

        UpdateEditor();
    }

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public bool IsVariableFont
    {
        get => (bool)GetValue(IsVariableFontProperty);
        set => SetValue(IsVariableFontProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((FontWidthPort)d).UpdateEditor();

    private static void OnVariableFontChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((FontWidthPort)d).UpdateEditor();

    private void UpdateEditor()
    {
        if (_updating) return;

        _updating = true;
        try
        {
            _number.Value = Math.Clamp(Value, 1, 9);
            _number.Visibility = IsVariableFont ? Visibility.Visible : Visibility.Collapsed;
        }
        finally
        {
            _updating = false;
        }
    }
}
