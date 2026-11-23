using System.Windows;
using Node.Graph.Port;

namespace Node.Editor.View;

/// <summary>
///     端子の追加・編集ダイアログの「種類」設定部分を差し替えるためのエディタ。
///     <see cref="PortEditOptions.KindEditorFactory" /> を指定すると、型の選択（コンボボックス）の代わりに
///     <see cref="Content" /> がその位置に表示され、端子ごとの設定を <see cref="PortDefinition.Settings" /> などへ保存できる。
/// </summary>
public interface IPortKindEditor
{
    /// <summary>種類設定部分に表示するUI。</summary>
    FrameworkElement Content { get; }

    /// <summary>
    ///     ダイアログを開いたときに呼ばれる。新規追加の場合は null。
    /// </summary>
    void Load(PortDefinition? existing);

    /// <summary>
    ///     OKが押されたときの入力チェック。問題がなければ null、あればダイアログに表示するメッセージを返す。
    /// </summary>
    string? Validate();

    /// <summary>
    ///     入力内容を端子定義へ反映して返す。<see cref="Validate" /> が null を返した後に呼ばれる。
    /// </summary>
    PortDefinition Apply(PortDefinition definition);
}