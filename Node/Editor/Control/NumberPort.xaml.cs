using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Node.Editor.Control;

/// <summary>
///     Interaction logic for NumberPort.xaml
/// </summary>
public sealed partial class NumberPort
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(float),
            typeof(NumberPort),
            new FrameworkPropertyMetadata(
                0.0f,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged));

    public static readonly DependencyProperty MinProperty =
        DependencyProperty.Register(
            nameof(Min),
            typeof(float),
            typeof(NumberPort),
            new PropertyMetadata(float.NaN, OnConfigChanged));

    public static readonly DependencyProperty MaxProperty =
        DependencyProperty.Register(
            nameof(Max),
            typeof(float),
            typeof(NumberPort),
            new PropertyMetadata(float.NaN, OnConfigChanged));

    public static readonly DependencyProperty DigitsProperty =
        DependencyProperty.Register(
            nameof(Digits),
            typeof(int),
            typeof(NumberPort),
            new PropertyMetadata(2, OnConfigChanged));

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(
            nameof(Unit),
            typeof(string),
            typeof(NumberPort),
            new PropertyMetadata("", OnUnitChanged));

    public static readonly DependencyProperty HexadecimalProperty =
        DependencyProperty.Register(nameof(Hexadecimal), typeof(bool), typeof(NumberPort),
            new PropertyMetadata(false, OnConfigChanged));

    public static readonly DependencyProperty PrefixProperty =
        DependencyProperty.Register(nameof(Prefix), typeof(string), typeof(NumberPort),
            new PropertyMetadata(""));

    public static readonly DependencyProperty DefaultProperty =
        DependencyProperty.Register(
            nameof(Default),
            typeof(float),
            typeof(NumberPort),
            new PropertyMetadata(0f));

    private bool _isClicking;
    private bool _isDragging;
    private bool _isEditing;
    private Point _startPoint;

    public NumberPort()
    {
        InitializeComponent();
        Update();
        AddHandler(Mouse.LostMouseCaptureEvent, new MouseEventHandler((_, e) =>
        {
            if (_isClicking && !_isEditing && e.OriginalSource is TextBox)
                CancelNumericDrag();
        }), true);
        Unloaded += (_, _) => CancelNumericDrag();
    }

    public bool IsFocusable
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = true;

    public string Text
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = "";

    public float Value
    {
        get => (float)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public float Min
    {
        get => (float)GetValue(MinProperty);
        init => SetValue(MinProperty, value);
    }

    public float Max
    {
        get => (float)GetValue(MaxProperty);
        init => SetValue(MaxProperty, value);
    }

    public int Digits
    {
        get => (int)GetValue(DigitsProperty);
        init => SetValue(DigitsProperty, value);
    }

    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        init => SetValue(UnitProperty, value);
    }

    public bool Hexadecimal
    {
        get => (bool)GetValue(HexadecimalProperty);
        init => SetValue(HexadecimalProperty, value);
    }

    public string Prefix
    {
        get => (string)GetValue(PrefixProperty);
        init => SetValue(PrefixProperty, value);
    }

    public float Default
    {
        get => (float)GetValue(DefaultProperty);
        init
        {
            SetValue(DefaultProperty, value);
            SetValue(ValueProperty, value);
        }
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (NumberPort)d;
        if (!control._isEditing)
            control.Text = control.FormatValue((float)e.NewValue);
    }

    private static void OnConfigChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (NumberPort)d;
        control.Text = control.FormatValue(control.Value);
    }

    private static void OnUnitChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (NumberPort)d;
        control.OnPropertyChanged(nameof(Unit));
    }

    [DllImport("User32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    private void Update()
    {
        float v;
        if (Text == "")
            v = Default;
        else
            try
            {
                v = ParseValue(Text);
            }
            catch (Exception)
            {
                v = Value;
            }

        Update(v);
    }

    private void Update(float value)
    {
        var v = value;
        if (!float.IsNaN(Min) && value < Min) v = Min;
        if (!float.IsNaN(Max) && value > Max) v = Max;
        var newValue = (float)Math.Round(v, Digits);
        if (Math.Abs(Value - newValue) > 1e-8)
            Value = newValue;
        else
            Text = FormatValue(newValue);

        Keyboard.ClearFocus();
        IsFocusable = false;
        if (!_isDragging)
            Mouse.OverrideCursor = null;
    }

    private void Box_PreviewMouseDown(object _, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void Box_PreviewMouseLeftButtonDown(object o, MouseButtonEventArgs e)
    {
        if (o is not TextBox box) return;
        if (_isEditing || _isClicking) return;
        _isClicking = true;
        _startPoint = box.PointToScreen(e.GetPosition(box));
        e.Handled = true;
        IsFocusable = false;
        Mouse.OverrideCursor = Cursors.None;
        BeginEditCommand?.Execute(null);
        if (!box.CaptureMouse()) CancelNumericDrag();
    }

    private void Box_PreviewMouseMove(object o, MouseEventArgs e)
    {
        if (o is not TextBox box) return;
        try
        {
            if (!_isClicking || _isEditing) return;
            var currentPoint = box.PointToScreen(e.GetPosition(box));
            var delta = currentPoint.X - _startPoint.X;

            if (Math.Abs(delta) > SystemParameters.MinimumHorizontalDragDistance || _isDragging)
            {
                _isDragging = true;

                const float sensitivity = 0.01f;
                Update(Value + (float)delta * sensitivity);
                SetCursorPos((int)_startPoint.X, (int)_startPoint.Y);
            }

            e.Handled = true;
        }
        catch (Exception ex)
        {
            CancelNumericDrag();
            if (box.IsMouseCaptured) box.ReleaseMouseCapture();
            Console.Error.WriteLine(ex);
        }
    }

    private void Box_PreviewMouseLeftButtonUp(object o, MouseButtonEventArgs e)
    {
        if (o is not TextBox box || !_isClicking || _isEditing) return;
        _isClicking = false;
        Mouse.OverrideCursor = null;
        box.ReleaseMouseCapture();

        if (_isDragging)
        {
            _isDragging = false;
            Update(Value);
            SetCursorPos((int)_startPoint.X, (int)_startPoint.Y);
            EndEditCommand?.Execute(null);
            e.Handled = true;
        }
        else
        {
            _isEditing = true;
            IsFocusable = true;
            Keyboard.Focus(box);
        }
    }

    private void CancelNumericDrag()
    {
        if (!_isClicking || _isEditing) return;
        _isClicking = false;
        _isDragging = false;
        Mouse.OverrideCursor = null;
        EndEditCommand?.Execute(null);
    }

    private void Box_LostFocus(object _, RoutedEventArgs e)
    {
        _isEditing = false;
        Update();
        EndEditCommand?.Execute(null);
    }

    private new void PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = (Hexadecimal ? HexRegex() : NumberRegex()).IsMatch(e.Text);
    }

    private void TextBoxPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            var text = (string?)e.DataObject.GetData(typeof(string)) ?? string.Empty;
            if ((Hexadecimal ? HexRegex() : NumberRegex()).IsMatch(text)) e.CancelCommand();
        }
        else
        {
            e.CancelCommand();
        }
    }

    private void ValueSubmit(object sender, KeyEventArgs e)
    {
        if (e.Key is not Key.Return) return;
        Update();
        _isEditing = false;
        EndEditCommand?.Execute(null);
    }

    private float ParseValue(string text)
        => Hexadecimal
            ? int.Parse(text.Trim().TrimStart('+').Replace("U+", "", StringComparison.OrdinalIgnoreCase), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : float.Parse(text, CultureInfo.InvariantCulture);

    private string FormatValue(float value)
        => Hexadecimal
            ? ((int)Math.Clamp(MathF.Round(value), 0, 0x10FFFF)).ToString("X4", CultureInfo.InvariantCulture)
            : value.ToString("F" + Digits, CultureInfo.InvariantCulture);

    [GeneratedRegex("[^0-9.-]+")]
    private static partial Regex NumberRegex();

    [GeneratedRegex("[^0-9A-Fa-f]+")]
    private static partial Regex HexRegex();
}