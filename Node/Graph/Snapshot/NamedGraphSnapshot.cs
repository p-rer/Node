using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Node.Graph.Snapshot;

/// <summary>
///     グラフライブラリの1エントリ。プロジェクト単位で共有される名前付きグラフ。
///     Name/Order の変更をUI（ドロップダウン・タブ名・管理ダイアログ）へ伝えるため
///     INotifyPropertyChanged を実装する。
/// </summary>
public sealed class NamedGraphSnapshot : INotifyPropertyChanged
{
    private string _name = "New Graph";
    private int _order;

    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public int Order
    {
        get => _order;
        set => SetField(ref _order, value);
    }

    /// <summary>
    ///     グラフ本体。ライブグラフがコミットされるたびに Serializer.Create の結果で丸ごと置き換わる。
    ///     参照差し替えのみで済ませるため、変更通知は出さない（一覧のName/Order以外は
    ///     頻繁な差し替えでUIを揺らす必要が無いため）。
    /// </summary>
    public GraphSnapshot Graph { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}