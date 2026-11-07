using Node.Localize;

namespace Node.Graph.Port;

public record PortDefinition(
    string Name,
    Type ValueType,
    string Label = "",
    string Description = "",
    object? DefaultValue = null,
    bool IsCustom = false
)
{
    /// <summary>
    ///     Label が空の組み込みポート（既定グラフの引数・戻り値、文字列結合の初期ポート）について、
    ///     Name に対応するローカライズ済みの表示名を返すための対応表。
    ///     表示名をスナップショットへ保存すると言語切り替えに追従できなくなるため、表示のたびにここで解決する。
    /// </summary>
    private static readonly Dictionary<string, Func<string>> BuiltInLabels = new()
    {
        ["InputImage"] = () => TextNode.InputImagePortLabel,
        ["FrameIndex"] = () => TextNode.FrameIndexLabel,
        ["OutputImage"] = () => TextNode.OutputImagePortLabel,
        ["Text1"] = () => TextNode.StringConcatInput1,
        ["Text2"] = () => TextNode.StringConcatInput2,
        ["Text3"] = () => TextNode.StringConcatInput3,
        ["Text4"] = () => TextNode.StringConcatInput4
    };

    /// <summary>
    ///     ノード上やダイアログに表示するポート名。Label があればそれを、
    ///     なければ組み込みポートのローカライズ名、それも無ければ Name を返す。
    /// </summary>
    public string GetDisplayLabel()
    {
        if (!string.IsNullOrEmpty(Label)) return Label;
        return BuiltInLabels.TryGetValue(Name, out var resolve) ? resolve() : Name;
    }
}