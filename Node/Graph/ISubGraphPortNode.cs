using Node.Nodes.Func;

namespace Node.Graph;

/// <summary>
/// サブグラフの ArgumentsNode / ReturnNode を外部へ公開するノードの共通契約。
/// </summary>
internal interface ISubGraphPortNode
{
    NodeGraph Graph { get; }

    ArgumentsNode? InnerArguments { get; }

    ReturnNode? InnerReturn { get; }

    void SyncPorts();
}
