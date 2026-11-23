using Node.Graph.Port;
using Node.Nodes.Func;

namespace Node.Graph.Snapshot;

public sealed class PortDefinitionSnapshot
{
    public string Name { get; init; } = "";
    public string TypeName { get; init; } = "";
    public string Label { get; init; } = "";
    public string Description { get; init; } = "";
    public object? DefaultValue { get; init; }

    /// <summary>
    ///     ユーザーが後から追加した引数ポートかどうか。false（既定）は元から定義されている固定ポート。
    /// </summary>
    public bool IsCustom { get; init; }

    /// <summary>
    ///     端子ごとの追加設定（品詞の選択やUnicode範囲など）。古いスナップショットには存在せず、その場合は空。
    /// </summary>
    public string Settings { get; init; } = "";

    public PortDefinition ToDefinition()
    {
        return new PortDefinition(Name, ResolveType(), Label, Description, DefaultValue, IsCustom, Settings ?? "");
    }

    private Type ResolveType()
    {
        var type = Type.GetType(TypeName);
        if (type != null) return type;

        // アセンブリの解決に失敗しても、追加引数として使える型は完全名で照合する。
        var fullName = TypeName.Split(',')[0].Trim();
        foreach (var kind in ArgumentPortKinds.All)
        {
            var candidate = ArgumentPortKinds.GetPortType(kind);
            if (candidate.FullName == fullName) return candidate;
        }

        return typeof(object);
    }
}