using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Windows.Media;
using Newtonsoft.Json;
using Node.Editor.Attributes;
using Node.Graph;
using Node.Graph.Attributes;
using Node.Graph.Port;
using Node.Graph.Snapshot;
using Node.Localize;

namespace Node.Nodes.Value;

public enum StringEncodingKind
{
    [Display(Name = nameof(TextNode.EncodingUtf8), ResourceType = typeof(TextNode))]
    Utf8,

    [Display(Name = nameof(TextNode.EncodingUtf16), ResourceType = typeof(TextNode))]
    Utf16,

    [Display(Name = nameof(TextNode.EncodingUtf32), ResourceType = typeof(TextNode))]
    Utf32,

    [Display(Name = nameof(TextNode.EncodingShiftJis), ResourceType = typeof(TextNode))]
    ShiftJis,

    [Display(Name = nameof(TextNode.EncodingAscii), ResourceType = typeof(TextNode))]
    Ascii
}

public enum NumberIntegerPaddingKind
{
    [Display(Name = nameof(TextNode.IntegerPaddingZero), ResourceType = typeof(TextNode))]
    Zero,

    [Display(Name = nameof(TextNode.IntegerPaddingSpace), ResourceType = typeof(TextNode))]
    Space
}

public enum NumberZeroFormatKind
{
    [Display(Name = nameof(TextNode.ZeroFormatInclude), ResourceType = typeof(TextNode))]
    Include,

    [Display(Name = nameof(TextNode.ZeroFormatOmit), ResourceType = typeof(TextNode))]
    Omit
}

internal static class StringEncodingUtility
{
    static StringEncodingUtility()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static Encoding Resolve(StringEncodingKind kind)
    {
        return kind switch
        {
            StringEncodingKind.Utf8 => new UTF8Encoding(false),
            StringEncodingKind.Utf16 => Encoding.Unicode,
            StringEncodingKind.Utf32 => Encoding.UTF32,
            StringEncodingKind.ShiftJis => Encoding.GetEncoding("shift_jis"),
            StringEncodingKind.Ascii => Encoding.ASCII,
            _ => Encoding.UTF8
        };
    }
}

[Node(typeof(StringCategory), nameof(TextNode.UnicodeToCharNode), nameof(TextNode.UnicodeToCharNodeDescription),
    typeof(TextNode))]
public class UnicodeToCharNode : NodeLogic
{
    [InputPort(nameof(TextNode.CodePoint), nameof(TextNode.CodePointDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 0x10FFFF, Digits = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public int CodePoint
    {
        get => GetInput<int>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.ResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string Result
    {
        get => GetOutput<string>() ?? "";
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        var isSurrogate = CodePoint is >= 0xD800 and <= 0xDFFF;
        Result = CodePoint is >= 0 and <= 0x10FFFF && !isSurrogate
            ? char.ConvertFromUtf32(CodePoint)
            : "";
        return Task.CompletedTask;
    }
}

[Node(typeof(StringCategory), nameof(TextNode.CharToUnicodeNode), nameof(TextNode.CharToUnicodeNodeDescription),
    typeof(TextNode))]
public class CharToUnicodeNode : NodeLogic
{
    [InputPort(nameof(TextNode.Character), nameof(TextNode.CharacterDescription), typeof(TextNode))]
    [TextPortControl(Default = "A")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string Character
    {
        get => GetInput<string>() ?? "";
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.ResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public int Result
    {
        get => GetOutput<int>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        try
        {
            Result = string.IsNullOrEmpty(Character) ? 0 : char.ConvertToUtf32(Character, 0);
        }
        catch (ArgumentException)
        {
            Result = 0;
        }

        return Task.CompletedTask;
    }
}

[Node(typeof(StringCategory), nameof(TextNode.StringNthCharNode), nameof(TextNode.StringNthCharNodeDescription),
    typeof(TextNode))]
public class StringNthCharNode : NodeLogic
{
    [InputPort(nameof(TextNode.Text), nameof(TextNode.TextDescription), typeof(TextNode))]
    [TextPortControl(Default = "")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string Text
    {
        get => GetInput<string>() ?? "";
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.Index), nameof(TextNode.IndexDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = int.MaxValue, Digits = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public int Index
    {
        get => GetInput<int>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.ResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string Result
    {
        get => GetOutput<string>() ?? "";
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        var e = StringInfo.GetTextElementEnumerator(Text);
        var i = 0;
        Result = "";
        while (e.MoveNext())
        {
            if (i == Index)
            {
                Result = (string)e.Current;
                break;
            }

            i++;
        }

        return Task.CompletedTask;
    }
}

[Node(typeof(StringCategory), nameof(TextNode.StringEncodeNode), nameof(TextNode.StringEncodeNodeDescription),
    typeof(TextNode))]
public class StringEncodeNode : NodeLogic
{
    [InputPort(nameof(TextNode.Text), nameof(TextNode.TextDescription), typeof(TextNode))]
    [TextPortControl(Default = "")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string Text
    {
        get => GetInput<string>() ?? "";
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.Encoding), nameof(TextNode.EncodingDescription), typeof(TextNode))]
    [EnumPortControl(Default = 0, IsEditable = false, Items = typeof(StringEncodingKind))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public StringEncodingKind Encoding
    {
        get => GetInput<StringEncodingKind>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Bytes), nameof(TextNode.BytesDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public byte[] Result
    {
        get => GetOutput<byte[]>() ?? [];
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = StringEncodingUtility.Resolve(Encoding).GetBytes(Text);
        return Task.CompletedTask;
    }
}

[Node(typeof(StringCategory), nameof(TextNode.StringDecodeNode), nameof(TextNode.StringDecodeNodeDescription),
    typeof(TextNode))]
public class StringDecodeNode : NodeLogic
{
    [InputPort(nameof(TextNode.Bytes), nameof(TextNode.BytesDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public byte[] Bytes
    {
        get => GetInput<byte[]>() ?? [];
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.Encoding), nameof(TextNode.EncodingDescription), typeof(TextNode))]
    [EnumPortControl(Default = 0, IsEditable = false, Items = typeof(StringEncodingKind))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public StringEncodingKind Encoding
    {
        get => GetInput<StringEncodingKind>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.ResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string Result
    {
        get => GetOutput<string>() ?? "";
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        try
        {
            Result = StringEncodingUtility.Resolve(Encoding).GetString(Bytes);
        }
        catch (DecoderFallbackException)
        {
            Result = "";
        }

        return Task.CompletedTask;
    }
}

[Node(typeof(StringCategory), nameof(TextNode.NumberToStringNode), nameof(TextNode.NumberToStringNodeDescription),
    typeof(TextNode))]
public class NumberToStringNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -1000000, Max = 1000000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.MinIntegerDigits), nameof(TextNode.MinIntegerDigitsDescription), typeof(TextNode))]
    [NumberPortControl(Min = 1, Max = 20, Digits = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public int MinIntegerDigits
    {
        get => GetInput<int>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.FractionDigits), nameof(TextNode.FractionDigitsDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 20, Digits = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public int FractionDigits
    {
        get => GetInput<int>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.IntegerPadding), nameof(TextNode.IntegerPaddingDescription), typeof(TextNode))]
    [EnumPortControl(Default = 1, IsEditable = false, Items = typeof(NumberIntegerPaddingKind))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public NumberIntegerPaddingKind IntegerPadding
    {
        get => GetInput<NumberIntegerPaddingKind>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.ZeroFormat), nameof(TextNode.ZeroFormatDescription), typeof(TextNode))]
    [EnumPortControl(Default = 0, IsEditable = false, Items = typeof(NumberZeroFormatKind))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public NumberZeroFormatKind ZeroFormat
    {
        get => GetInput<NumberZeroFormatKind>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.ResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string Result
    {
        get => GetOutput<string>() ?? "";
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        var minIntegerDigits = Math.Max(1, MinIntegerDigits);
        var fractionDigits = Math.Max(0, FractionDigits);

        var integerFormat = IntegerPadding switch
        {
            NumberIntegerPaddingKind.Zero =>
                new string('0', minIntegerDigits),

            // '#' だけだと有効桁が無い場合（0 など）に最下位桁まで省略されてしまうため、
            // 最下位桁だけは '0' プレースホルダーにして必ず 1 桁は出力されるようにする。
            // 不足する上位桁は後段のロジックで空白に置き換える。
            NumberIntegerPaddingKind.Space =>
                new string('#', minIntegerDigits - 1) + "0",

            _ =>
                new string('0', minIntegerDigits)
        };

        var fractionFormat = fractionDigits > 0
            ? "." + new string('0', fractionDigits)
            : "";

        var format = integerFormat + fractionFormat;

        var result = Value.ToString(
            format,
            CultureInfo.InvariantCulture);

        if (IntegerPadding == NumberIntegerPaddingKind.Space)
        {
            var signLength = result.StartsWith('-')
                ? 1
                : 0;
            var integerEnd = result.IndexOf('.');
            if (integerEnd < 0)
                integerEnd = result.Length;

            var integerLength = integerEnd - signLength;
            if (integerLength < minIntegerDigits)
            {
                var paddingLength =
                    minIntegerDigits - integerLength;

                result = result.Insert(
                    signLength,
                    new string(' ', paddingLength));
            }
        }

        if (ZeroFormat == NumberZeroFormatKind.Omit)
        {
            if (result.StartsWith("0.", StringComparison.Ordinal))
                result = result[1..];
            else if (result.StartsWith("-0.", StringComparison.Ordinal)) result = "-" + result[2..];
        }

        Result = result;
        return Task.CompletedTask;
    }
}

[Node(typeof(StringCategory), nameof(TextNode.StringConcatNode), nameof(TextNode.StringConcatNodeDescription),
    typeof(TextNode))]
public class StringConcatNode : NodeLogic, ISerializableNode
{
    private const string TextPortsKey = "TextPorts";

    private PortDefinition[] _textPorts = [];

    public StringConcatNode()
    {
        InitializeTextPorts(CreateDefaultTextPorts());
    }

    [InputPort(nameof(TextNode.StringConcatSeparator), nameof(TextNode.StringConcatSeparatorDescription),
        typeof(TextNode))]
    [TextPortControl(Default = "")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string Separator
    {
        get => GetInput<string>() ?? "";
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.ResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string Result
    {
        get => GetOutput<string>() ?? "";
        set => SetOutput(value);
    }

    public Dictionary<string, object?> SerializeCustomData()
    {
        var entries = _textPorts.Select(definition => new[] { definition.Name, definition.Label });
        return new Dictionary<string, object?> { [TextPortsKey] = JsonConvert.SerializeObject(entries) };
    }

    public void DeserializeCustomData(Dictionary<string, object?> data)
    {
        if (!data.TryGetValue(TextPortsKey, out var raw) || raw?.ToString() is not { } json) return;

        List<string[]>? entries;
        try
        {
            entries = JsonConvert.DeserializeObject<List<string[]>>(json);
        }
        catch (JsonException)
        {
            return;
        }

        if (entries == null) return;

        var definitions = entries
            .Where(entry => entry is { Length: >= 2 })
            .Select(entry => CreateTextPort(entry[0], entry[1]))
            .ToArray();
        if (IsValidTextPortSet(definitions)) InitializeTextPorts(definitions);
    }

    public PortDefinition[] GetPortDefinitions()
    {
        return _textPorts;
    }

    /// <summary>
    ///     文字列端子として使えるポート定義の組かどうか。型が文字列で、名前が空でなく、
    ///     名前の重複や他の端子（区切り文字・結果）との衝突、動的ポートの区切り文字（.）を含まないこと。
    /// </summary>
    internal static bool IsValidTextPortSet(PortDefinition[] definitions)
    {
        var reserved = new[] { nameof(Separator), nameof(Result) };
        return definitions.All(definition =>
                   definition.ValueType == typeof(string) &&
                   !string.IsNullOrEmpty(definition.Name) &&
                   !definition.Name.Contains('.') &&
                   !reserved.Contains(definition.Name)) &&
               definitions.Select(definition => definition.Name).Distinct().Count() == definitions.Length;
    }

    /// <summary>
    ///     文字列端子を定義の並び順どおりに作り直す。名前が同じ端子は入力値と接続状態を引き継ぐ。
    ///     既定の端子（Text1～Text4）は従来のプロパティ名と同じ名前なので、古いスナップショットもそのまま復元できる。
    /// </summary>
    internal void InitializeTextPorts(PortDefinition[] definitions)
    {
        var separatorPort = Inputs[nameof(Separator)];
        var previous = _textPorts
            .Where(definition => Inputs.ContainsKey(definition.Name))
            .ToDictionary(definition => definition.Name, definition => Inputs[definition.Name]);

        Inputs.Clear();
        foreach (var definition in definitions)
        {
            if (!previous.TryGetValue(definition.Name, out var port))
            {
                port = new InputPort(this, typeof(string));
                if (definition.DefaultValue != null) port.SetValue(definition.DefaultValue);
            }

            Inputs.Add(definition.Name, port);
        }

        Inputs.Add(nameof(Separator), separatorPort);
        _textPorts = definitions;
        InvalidateForce();
    }

    private static PortDefinition CreateTextPort(string name, string label)
    {
        return new PortDefinition(name, typeof(string), label ?? "", "", null, true);
    }

    private static PortDefinition[] CreateDefaultTextPorts()
    {
        return
        [
            CreateTextPort("Text1", ""),
            CreateTextPort("Text2", ""),
            CreateTextPort("Text3", ""),
            CreateTextPort("Text4", "")
        ];
    }

    protected override Task Calculate()
    {
        var ports = _textPorts;
        Result = string.Join(Separator, ports.Select(definition => GetInput<string>(definition.Name) ?? ""));
        return Task.CompletedTask;
    }
}