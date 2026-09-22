using System.Globalization;
using System.Windows.Media;
using Node.Localize;
using Node.Utility;

namespace Node.Nodes.Func;

public enum ArgumentPortKind
{
    Number,
    Bool,
    Color,
    Text
}

public static class ArgumentPortKinds
{
    public static IReadOnlyList<ArgumentPortKind> All { get; } =
    [
        ArgumentPortKind.Number,
        ArgumentPortKind.Bool,
        ArgumentPortKind.Color,
        ArgumentPortKind.Text
    ];

    public static Type GetPortType(ArgumentPortKind kind)
    {
        return kind switch
        {
            ArgumentPortKind.Number => typeof(float),
            ArgumentPortKind.Bool => typeof(bool),
            ArgumentPortKind.Color => typeof(Color),
            _ => typeof(string)
        };
    }

    public static Type GetEditorValueType(ArgumentPortKind kind)
    {
        return kind == ArgumentPortKind.Number ? typeof(double) : GetPortType(kind);
    }

    public static bool TryGetKind(Type portType, out ArgumentPortKind kind)
    {
        foreach (var candidate in All)
        {
            if (GetPortType(candidate) != portType) continue;
            kind = candidate;
            return true;
        }

        kind = default;
        return false;
    }

    public static string GetDisplayName(ArgumentPortKind kind)
    {
        return kind switch
        {
            ArgumentPortKind.Number => TextUi.ArgumentTypeNumber,
            ArgumentPortKind.Bool => TextUi.ArgumentTypeBool,
            ArgumentPortKind.Color => TextUi.ArgumentTypeColor,
            _ => TextUi.ArgumentTypeText
        };
    }

    public static object GetDefaultStorageValue(ArgumentPortKind kind)
    {
        return kind switch
        {
            ArgumentPortKind.Number => 0d,
            ArgumentPortKind.Bool => false,
            ArgumentPortKind.Color => Colors.White.ToString(CultureInfo.InvariantCulture),
            _ => ""
        };
    }

    public static object ToEditorValue(ArgumentPortKind kind, object? storage)
    {
        try
        {
            switch (kind)
            {
                case ArgumentPortKind.Number:
                    return storage is null ? 0d : Convert.ToDouble(storage, CultureInfo.InvariantCulture);
                case ArgumentPortKind.Bool:
                    return storage is not null && Convert.ToBoolean(storage, CultureInfo.InvariantCulture);
                case ArgumentPortKind.Color:
                    return storage switch
                    {
                        Color color => color,
                        string text => (Color)ColorConverter.ConvertFromString(text),
                        _ => Colors.White
                    };
                default:
                    return storage?.ToString() ?? "";
            }
        }
        catch (Exception ex) when (!ExceptionPolicy.IsFatal(ex))
        {
            return kind switch
            {
                ArgumentPortKind.Number => 0d,
                ArgumentPortKind.Bool => false,
                ArgumentPortKind.Color => Colors.White,
                _ => ""
            };
        }
    }

    public static object ToStorage(ArgumentPortKind kind, object? editorValue)
    {
        return ToEditorValue(kind, editorValue) switch
        {
            Color color => color.ToString(CultureInfo.InvariantCulture),
            var value => value
        };
    }

    public static object ToPortValue(ArgumentPortKind kind, object? storage)
    {
        var value = ToEditorValue(kind, storage);
        return kind == ArgumentPortKind.Number ? (float)(double)value : value;
    }
}