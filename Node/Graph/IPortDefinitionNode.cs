using Node.Graph.Port;

namespace Node.Graph;

/// <summary>
/// 動的に端子定義を保持し、エディタから差し替え可能なノードの共通契約。
/// </summary>
public enum DynamicPortKind
{
    Default,
    Text,
    UnicodeRange
}

public interface IPortDefinitionNode
{
    PortDefinition[] GetPortDefinitions();

    bool IsValidPortSet(PortDefinition[] definitions);

    void Initialize(PortDefinition[] definitions);

    /// <summary>
    /// 動的端子が出力側ならtrue、入力側ならfalse。
    /// </summary>
    bool DynamicPortsAreOutputs { get; }

    /// <summary>
    /// 動的端子の編集UIが解釈する意味。
    /// </summary>
    DynamicPortKind DynamicPortKind { get; }

    /// <summary>
    /// 動的端子定義の変更後に、エディタなどへ再構築を通知する。
    /// </summary>
    void NotifyPortDefinitionsChanged();
}
