using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Node.Editor.ViewModel;
using Node.Graph;
using Node.Graph.Snapshot;
using YukkuriMovieMaker.Plugin;

var passed = 0;
void Check(string name, Action test)
{
    test();
    Console.WriteLine($"PASS {name}");
    passed++;
}
void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Expected {expected}; actual {actual}");
}

Check("switch discards delayed update from old graph", () =>
{
    var a = Guid.NewGuid(); var b = Guid.NewGuid();
    var state = new SharedGraphUpdateState<string>(a);
    state.Queue(a, "A1"); state.Reset(b);
    Equal<string?>(null, state.TakePending(b));
    Equal(false, state.Queue(a, "A2"));
    Equal(true, state.Queue(b, "B1"));
    Equal("B1", state.TakePending(b));
});
Check("newest received snapshot wins and is consumed once", () =>
{
    var id = Guid.NewGuid(); var state = new SharedGraphUpdateState<string>(id);
    state.Queue(id, "old"); state.Queue(id, "new");
    Equal("new", state.TakePending(id)); Equal<string?>(null, state.TakePending(id));
});
Check("switch rejects a suspended asynchronous save", () =>
{
    var state = new SharedGraphUpdateState<string>(Guid.NewGuid());
    var stamp = state.BeginSave(); state.Reset(Guid.NewGuid());
    Equal(false, state.IsCurrent(stamp));
});
Check("external update rejects a suspended save on the same graph", () =>
{
    var id = Guid.NewGuid(); var state = new SharedGraphUpdateState<string>(id);
    var stamp = state.BeginSave(); state.Queue(id, "remote");
    Equal(false, state.IsCurrent(stamp));
});
Check("out of order local saves retain the latest request", () =>
{
    var state = new SharedGraphUpdateState<string>(Guid.NewGuid());
    var older = state.BeginSave(); var newer = state.BeginSave();
    Equal(false, state.IsCurrent(older)); Equal(true, state.IsCurrent(newer));
});
Check("same graph reset also rejects a save (Undo / reload)", () =>
{
    var id = Guid.NewGuid(); var state = new SharedGraphUpdateState<string>(id);
    var stamp = state.BeginSave(); state.Reset(id);
    Equal(false, state.IsCurrent(stamp));
});
Check("pending remote update blocks saves of the old live graph", () =>
{
    var id = Guid.NewGuid(); var state = new SharedGraphUpdateState<string>(id);
    state.Queue(id, "remote"); var stamp = state.BeginSave();
    Equal(false, state.IsCurrent(stamp)); Equal("remote", state.TakePending(id));
    Equal(true, state.IsCurrent(state.BeginSave()));
});
Check("tab rebinding follows B renames and ignores A renames", () =>
{
    var library = new GraphLibraryViewModel();
    var a = library.CreateNew("A"); var b = library.CreateNew("B");
    using var tab = new TabViewModel(new NodeGraph(), "A", new NodeEditorViewModel(), graphId: a.Id);
    tab.SetGraphIdentity(b.Id, "B");
    library.Rename(b.Id, "B2"); Equal("B2", tab.Title);
    library.Rename(a.Id, "A2"); Equal("B2", tab.Title);
});
Check("reused editor tab is rebound when the graph identity changes", () =>
{
    var library = new GraphLibraryViewModel(); var a = library.CreateNew("A"); var b = library.CreateNew("B");
    var editor = new NodeEditorViewModel(); var graph = new NodeGraph();
    editor.OpenGraph(graph, "A", graphId: a.Id); editor.OpenGraph(graph, "B", graphId: b.Id);
    Equal(1, editor.Tabs.Count); library.Rename(b.Id, "B2"); Equal("B2", editor.Tabs[0].Title);
    library.Rename(a.Id, "A2"); Equal("B2", editor.Tabs[0].Title);
});
Check("shared refresh does not steal the selected tab or reopen closed tabs", () =>
{
    var editor = new NodeEditorViewModel(); var a = new NodeGraph(); var b = new NodeGraph();
    editor.OpenGraph(a, "A"); var tabA = editor.SelectedTab!;
    editor.OpenGraph(b, "B"); var tabB = editor.SelectedTab!;
    editor.RefreshGraph(a, "A2", null, null);
    Equal(tabB, editor.SelectedTab); Equal(1, tabA.GraphViewModel.RefreshCount);
    editor.CloseGraphTab(a); editor.RefreshGraph(a, "A3", null, null);
    Equal(1, editor.Tabs.Count); Equal(tabB, editor.SelectedTab);
});
Check("disposed tabs unsubscribe from rename notifications", () =>
{
    var library = new GraphLibraryViewModel(); var a = library.CreateNew("A");
    var tab = new TabViewModel(new NodeGraph(), "A", new NodeEditorViewModel(), graphId: a.Id);
    tab.Dispose(); library.Rename(a.Id, "A2"); Equal("A", tab.Title);
    Equal(true, tab.GraphViewModel.Disposed);
});
Check("deleted referenced entry is restored with identity, name and tab subscription", () =>
{
    var library = new GraphLibraryViewModel(); var a = library.CreateNew("Named A");
    using var tab = new TabViewModel(new NodeGraph(), a.Name, new NodeEditorViewModel(), graphId: a.Id);
    library.Delete(a.Id); Equal(false, library.TryGetEntry(a.Id, out _));
    library.PropagateChange(a.Id, new Node.NodeEffect(), new GraphSnapshot { Marker = "restored" });
    Equal(true, library.TryGetEntry(a.Id, out var restored)); Equal(true, ReferenceEquals(a, restored));
    Equal("Named A", restored.Name); Equal("restored", restored.Graph.Marker);
    library.Rename(a.Id, "renamed after restore"); Equal("renamed after restore", tab.Title);
});
Check("propagation carries graph identity and excludes origin", () =>
{
    var library = new GraphLibraryViewModel(); var a = library.CreateNew("A");
    var origin = new Node.NodeEffect(); var peer = new Node.NodeEffect();
    library.Subscribe(a.Id, origin); library.Subscribe(a.Id, peer);
    var snapshot = new GraphSnapshot { Marker = "update" };
    library.PropagateChange(a.Id, origin, snapshot);
    Equal(0, origin.Received.Count); Equal(1, peer.Received.Count);
    Equal(a.Id, peer.Received[0].Id); Equal(true, ReferenceEquals(snapshot, peer.Received[0].Snapshot));
    library.Unsubscribe(a.Id, peer); library.PropagateChange(a.Id, origin, snapshot);
    Equal(1, peer.Received.Count);
});
Check("duplicate display names retain separate graph identities", () =>
{
    var library = new GraphLibraryViewModel(); var a = library.CreateNew("same"); var b = library.CreateNew("same");
    Equal(false, a.Id == b.Id); library.Rename(a.Id, "A"); Equal("same", b.Name);
});
Check("reordered library survives JSON save and reload", () =>
{
    var library = new GraphLibraryViewModel(); var a = library.CreateNew("A"); var b = library.CreateNew("B");
    library.Move(b.Id, -1); var saved = library.SaveState();
    var restored = new GraphLibraryViewModel(); restored.LoadState(saved);
    Equal(b.Id, restored.Entries[0].Id); Equal(a.Id, restored.Entries[1].Id);
});
Check("cloned entry gets a new identity and later snapshot replacements remain independent", () =>
{
    var library = new GraphLibraryViewModel(); var a = library.CreateNew("A");
    var b = library.Clone(a.Id, "B", a.Graph); Equal(false, a.Id == b.Id);
    var original = a.Graph;
    library.PropagateChange(b.Id, new Node.NodeEffect(), new GraphSnapshot { Marker = "B" });
    Equal(true, ReferenceEquals(original, a.Graph)); Equal("B", b.Graph.Marker);
});
Check("all production C# files parse with the C# compiler", () =>
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "Node"), "*.cs", SearchOption.AllDirectories))
    {
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), new CSharpParseOptions(LanguageVersion.Preview), file);
        var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0) throw new Exception(string.Join(Environment.NewLine, errors.Select(e => e.ToString())));
    }
});
Console.WriteLine($"{passed} regression checks passed.");
