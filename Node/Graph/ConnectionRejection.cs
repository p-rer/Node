namespace Node.Graph;

/// <summary>
///     出力ポートから入力ポートへの接続が拒否される理由。
/// </summary>
public enum ConnectionRejection
{
    /// <summary>
    ///     拒否されない（接続できる）。
    /// </summary>
    None,

    /// <summary>
    ///     接続によってグラフが循環する（同一ノード内の接続を含む）。
    /// </summary>
    Cycle,

    /// <summary>
    ///     指定したノードまたはポートが存在しない。
    /// </summary>
    PortNotFound,

    /// <summary>
    ///     ポートの型が互換性を持たず、値を受け渡せない。
    /// </summary>
    IncompatibleTypes
}