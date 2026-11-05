namespace Node.Graph.Port;

/// <summary>
///     出力ポートの値を入力ポートへ渡したときの、型の互換性の判定結果。
/// </summary>
public enum PortCompatibility
{
    /// <summary>
    ///     値をそのまま、または精度を失わずに受け渡せる。
    /// </summary>
    Compatible,

    /// <summary>
    ///     数値どうしの接続だが、小数部の丸めや値の範囲外などにより値が変わる可能性がある。
    /// </summary>
    ValueMayChange,

    /// <summary>
    ///     異なる列挙型どうしの接続。値は整数として対応付けられるため、意図した項目にならない。
    /// </summary>
    EnumMismatch,

    /// <summary>
    ///     型として受け渡せない。
    /// </summary>
    Incompatible
}

/// <summary>
///     出力ポートと入力ポートの型の互換性を判定する。
///     接続を許可するかどうか（NodeGraph.Connect）と、接続済みの組が抱える問題の表示の双方が、
///     この判定を唯一の基準として共有する。
/// </summary>
public static class PortTypeCompatibility
{
    private static readonly HashSet<Type> NumericPortTypes =
    [
        typeof(int), typeof(uint), typeof(long), typeof(ulong),
        typeof(short), typeof(ushort), typeof(byte), typeof(sbyte),
        typeof(float), typeof(double), typeof(decimal)
    ];

    /// <summary>
    ///     C# の暗黙的な数値変換の一覧。
    ///     キーが変換元、値が暗黙的に変換できる変換先。ここに無い組み合わせは値が変わる可能性がある。
    /// </summary>
    private static readonly Dictionary<Type, Type[]> ImplicitNumericConversions = new()
    {
        [typeof(sbyte)] = [typeof(short), typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal)],
        [typeof(byte)] =
        [
            typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong),
            typeof(float), typeof(double), typeof(decimal)
        ],
        [typeof(short)] = [typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal)],
        [typeof(ushort)] =
            [typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal)],
        [typeof(int)] = [typeof(long), typeof(float), typeof(double), typeof(decimal)],
        [typeof(uint)] = [typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal)],
        [typeof(long)] = [typeof(float), typeof(double), typeof(decimal)],
        [typeof(ulong)] = [typeof(float), typeof(double), typeof(decimal)],
        [typeof(float)] = [typeof(double)]
    };

    /// <summary>
    ///     出力ポートの型を入力ポートへ接続した場合の互換性を判定する。
    ///     Incompatible 以外はすべて接続を許可する。
    /// </summary>
    /// <param name="outputType">接続元の出力ポートの値の型</param>
    /// <param name="inputType">接続先の入力ポートの値の型</param>
    public static PortCompatibility Evaluate(Type outputType, Type inputType)
    {
        if (outputType == inputType) return PortCompatibility.Compatible;
        if (inputType.IsAssignableFrom(outputType)) return PortCompatibility.Compatible;

        var outputIsNumericLike = NumericPortTypes.Contains(outputType) || outputType.IsEnum;
        var inputIsNumericLike = NumericPortTypes.Contains(inputType) || inputType.IsEnum;
        if (!outputIsNumericLike || !inputIsNumericLike) return PortCompatibility.Incompatible;

        if (outputType.IsEnum && inputType.IsEnum) return PortCompatibility.EnumMismatch;

        var sourceType = outputType.IsEnum ? Enum.GetUnderlyingType(outputType) : outputType;
        var targetType = inputType.IsEnum ? Enum.GetUnderlyingType(inputType) : inputType;

        if (sourceType == targetType) return PortCompatibility.Compatible;
        return ImplicitNumericConversions.TryGetValue(sourceType, out var targets) &&
               Array.IndexOf(targets, targetType) >= 0
            ? PortCompatibility.Compatible
            : PortCompatibility.ValueMayChange;
    }
}