using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Node.Editor.Attributes;
using Node.Editor.Command;
using Node.Editor.View;
using Node.Graph;
using Node.Graph.Attributes;
using Node.Graph.Events;
using Node.Graph.Port;
using Node.Localize;
using Node.Nodes.Func;
using Node.Nodes.Generator.Outline;
using Node.Utility;
using Node.ValueTypes;
using YukkuriMovieMaker.Commons;
using VisualStateChangedEventArgs = Node.Graph.Events.VisualStateChangedEventArgs;

namespace Node.Editor.ViewModel;

public sealed class NodeViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly EventHandler _errorStateChangedHandler;
    private readonly NodeGraph _graph;
    private readonly GraphViewModel _graphViewModel;
    private readonly EventHandler<NeedToReinitializeInputPortsEvent> _needToReinitializeInputPortsHandler;
    internal readonly NodeLogic NodeLogic;
    private bool _isDisposed;

    /// <summary>
    ///     この VM が表示している端子構成の識別子。GraphViewModel が VM を使い回してよいかの判定に使う。
    /// </summary>
    private string _portSignature = "";

    private double _x;
    private double _y;

    public NodeViewModel(NodeLogic nodeLogic, NodeGraph graph, GraphViewModel graphViewModel)
    {
        NodeLogic = nodeLogic;
        _graph = graph;
        _graphViewModel = graphViewModel;
        ParentEditor = graphViewModel.ParentEditor;

        Id = nodeLogic.Id;

        EditArgumentPortsCommand =
            new RelayCommand(() => { EditArgumentPortsRequested?.Invoke(this, EventArgs.Empty); });

        var nodeAttr = nodeLogic.GetType().GetCustomAttribute<NodeAttribute>();
        if (nodeAttr == null) throw new InvalidOperationException();
        DisplayName = nodeAttr.GetLabel();
        Category = nodeAttr.GetCategoryName();
        Description = nodeAttr.GetDescription();
        Color = nodeAttr.GetCategoryColor();

        InputPorts = new ObservableCollection<PortViewModel>(
            CreateInputPorts(nodeLogic, graph)
        );
        OutputPorts = new ObservableCollection<PortViewModel>(
            CreateOutputPorts(nodeLogic, graph)
        );
        SubGraphs = new ObservableCollection<SubGraphViewModel>(
            CreateSubGraphs(nodeLogic)
        );
        _portSignature = CreatePortSignature(nodeLogic);

        HasSubGraph = nodeLogic.SubGraphs.Count > 0;

        var visualState = graph.GetVisualState(Id);
        _x = visualState?.X ?? 0;
        _y = visualState?.Y ?? 0;

        IsError = nodeLogic.HasError;
        _errorStateChangedHandler = (sender, _) =>
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(() => IsError = ((NodeLogic)sender!).HasError);
                return;
            }

            IsError = ((NodeLogic)sender!).HasError;
        };
        nodeLogic.ErrorStateChanged += _errorStateChangedHandler;

        _needToReinitializeInputPortsHandler = QueueReinitializeInputPorts;
        nodeLogic.NeedToReinitializeInputPorts += _needToReinitializeInputPortsHandler;
    }

    public ICommand EditArgumentPortsCommand { get; }

    public NodeEditorViewModel ParentEditor { get; }

    public bool IsSelected
    {
        get;
        internal set => SetField(ref field, value);
    }

    public bool IsError
    {
        get;
        private set => SetField(ref field, value);
    }

    public string DisplayName { get; }
    public string Category { get; }
    public string Description { get; }
    public string Color { get; }

    public Guid Id { get; }

    public ObservableCollection<PortViewModel> InputPorts { get; }
    public ObservableCollection<PortViewModel> OutputPorts { get; }
    public ObservableCollection<SubGraphViewModel> SubGraphs { get; }

    public bool HasSubGraph { get; }

    public bool CanEditPorts =>
        NodeLogic is ArgumentsNode { AllowCustomPorts: true } ||
        NodeLogic is IPortDefinitionNode { DynamicPortKind: not DynamicPortKind.Default };

    public bool IsTextPortsOnly =>
        NodeLogic is IPortDefinitionNode { DynamicPortKind: DynamicPortKind.Text };

    public bool IsUnicodeStylePorts =>
        NodeLogic is IPortDefinitionNode { DynamicPortKind: DynamicPortKind.UnicodeRange };

    public bool IsPartOfSpeechPorts =>
        NodeLogic is IPortDefinitionNode { DynamicPortKind: DynamicPortKind.PartOfSpeech };

    public bool CanEditSubGraphPorts => NodeLogic is ISubGraphPortNode;

    public string AddPortButtonLabel =>
        IsTextPortsOnly ? TextUi.AddStringPortButton :
        IsPartOfSpeechPorts ? TextUi.AddPartOfSpeechPortButton :
        IsUnicodeStylePorts ? TextUi.AddUnicodeRangePortButton :
        TextUi.AddArgumentPortButton;

    public bool CanAddArgumentPort =>
        IsUnicodeStylePorts ||
        IsPartOfSpeechPorts ||
        IsTextPortsOnly ||
        (NodeLogic is ArgumentsNode { AllowCustomPorts: true } argumentsNode &&
         argumentsNode.GetPortDefinitions().Count(definition => definition.IsCustom) <
         ArgumentPortKinds.MaxCustomPorts);

    public string? AddArgumentPortToolTip =>
        CanEditPorts && !CanAddArgumentPort
            ? string.Format(TextUi.ArgumentPortLimitReached, ArgumentPortKinds.MaxCustomPorts)
            : null;

    public double X
    {
        get => _x;
        set
        {
            if (Math.Abs(_x - value) > 0.0001)
            {
                _x = value;
                OnPropertyChanged();
            }
        }
    }

    public double Y
    {
        get => _y;
        set
        {
            if (Math.Abs(_y - value) > 0.0001)
            {
                _y = value;
                OnPropertyChanged();
            }
        }
    }

    public double Width
    {
        get;
        internal set => SetField(ref field, value);
    }

    public double Height
    {
        get;
        internal set => SetField(ref field, value);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        NodeLogic.ErrorStateChanged -= _errorStateChangedHandler;
        NodeLogic.NeedToReinitializeInputPorts -= _needToReinitializeInputPortsHandler;

        foreach (var subGraph in SubGraphs)
            subGraph.Dispose();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     端子の追加・編集ダイアログに渡す設定。種類ごとの専用の設定がないノードは null（既定の設定を使う）。
    /// </summary>
    internal PortEditOptions? GetPortEditOptions()
    {
        return NodeLogic is IPortDefinitionNode { DynamicPortKind: var kind }
            ? kind switch
            {
                DynamicPortKind.UnicodeRange => PortEditOptions.UnicodeRanges,
                DynamicPortKind.PartOfSpeech => PortEditOptions.PartsOfSpeech,
                _ => null
            }
            : null;
    }

    /// <summary>
    ///     端子の再構築要求は必ず後続のディスパッチャー処理へ回す。要求は値のバインディング更新
    ///     （レイアウト中のテンプレート適用を含む）から同期的に届くことがあり、その場で InputPorts を
    ///     書き換えると、測定中の StackPanel の子要素が消えて ArgumentOutOfRangeException になる。
    ///     処理が走る時点でこの VM が GraphViewModel に作り直されていた場合は、同じ NodeLogic の現在の VM へ渡す。
    /// </summary>
    private void QueueReinitializeInputPorts(object? sender, NeedToReinitializeInputPortsEvent @event)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            HandleNeedToReinitializeInputPorts(sender, @event);
            return;
        }

        dispatcher.BeginInvoke(new Action(() =>
        {
            var target = _isDisposed
                ? _graphViewModel.Nodes.FirstOrDefault(n => n.Id == Id && ReferenceEquals(n.NodeLogic, NodeLogic))
                : this;
            target?.HandleNeedToReinitializeInputPorts(sender, @event);
        }));
    }

    /// <summary>
    ///     NodeLogic が同一で、表示中の端子構成が現在の NodeLogic と一致している場合のみ true。
    /// </summary>
    internal bool IsBackedBy(NodeLogic node)
    {
        return ReferenceEquals(NodeLogic, node) && _portSignature == CreatePortSignature(node);
    }

    private static string CreatePortSignature(NodeLogic node)
    {
        try
        {
            var builder = new StringBuilder();
            foreach (var (name, port) in node.Inputs)
                builder.Append(name).Append(':').Append(RuntimeHelpers.GetHashCode(port.ValueType)).Append(';');
            builder.Append('|');
            foreach (var (name, port) in node.Outputs)
                builder.Append(name).Append(':').Append(RuntimeHelpers.GetHashCode(port.ValueType)).Append(';');
            builder.Append('|');

            var subGraphNode = node as ISubGraphPortNode;
            AppendDefinitions(builder, (node as IPortDefinitionNode)?.GetPortDefinitions());
            AppendDefinitions(builder, subGraphNode?.InnerArguments?.GetPortDefinitions());
            AppendDefinitions(builder, subGraphNode?.InnerReturn?.GetPortDefinitions());
            builder.Append('|');

            foreach (var (name, subGraph) in node.SubGraphs)
                builder.Append(name).Append(':').Append(RuntimeHelpers.GetHashCode(subGraph)).Append(';');
            return builder.ToString();
        }
        catch (Exception ex) when (!ExceptionPolicy.IsFatal(ex))
        {
            // 別スレッドが Inputs を書き換えている最中などで読み取れない場合は、再利用せず作り直す側に倒す。
            return Guid.NewGuid().ToString();
        }
    }

    private static void AppendDefinitions(StringBuilder builder, PortDefinition[]? definitions)
    {
        builder.Append('[');
        if (definitions != null)
            foreach (var definition in definitions)
                builder.Append(definition.Name).Append(':')
                    .Append(RuntimeHelpers.GetHashCode(definition.ValueType)).Append(':')
                    .Append(definition.GetDisplayLabel()).Append(':')
                    .Append(definition.IsCustom).Append(':')
                    .Append(definition.Settings).Append(';');
        builder.Append(']');
    }

    private void HandleNeedToReinitializeInputPorts(object? sender, NeedToReinitializeInputPortsEvent @event)
    {
        var logic = (NodeLogic)sender!;

        if (logic is IPortDefinitionNode { DynamicPortKind: not DynamicPortKind.Default })
        {
            // 先に新しい端子を作り切ってから差し替える。途中で例外が出ても InputPorts が空のまま残らない。
            var newPorts = CreateInputPorts(logic, _graph).ToList();
            InputPorts.Clear();
            foreach (var portVm in newPorts)
                InputPorts.Add(portVm);
            foreach (var portVm in InputPorts)
                portVm.ApplyDefaultToGraph();
            _portSignature = CreatePortSignature(logic);
            _graph.OnGraphChanged(new ConnectionChangedEventArgs(null, null, Id, null));
            return;
        }

        var savedConnections = InputPorts
            .Select(portVm => new
            {
                portVm.Name,
                ControlAttributeType = portVm.ControlAttribute?.ControlType,
                EditorAttributeType = portVm.EditorAttribute?.GetType(),
                Connections = _graph.Connections
                    .Where(c => c.ToId == Id && c.ToPort == portVm.Name)
                    .Select(c => new { c.FromId, c.FromPort })
                    .ToList(),
                LocalValue = logic.Inputs.TryGetValue(portVm.Name, out var oldPort)
                    ? oldPort.LocalValue
                    : null
            }).ToList();

        var prefix = @event.PropName + ".";
        var newSubPortNames = @event.NewContainer.GetType()
            .GetProperties()
            .Where(p => Attribute.IsDefined(p, typeof(InputPortAttribute)))
            .Select(p => prefix + p.Name)
            .ToHashSet();

        foreach (var port in savedConnections)
        {
            if (!port.Name.StartsWith(prefix)) continue;
            if (newSubPortNames.Contains(port.Name)) continue;
            foreach (var conn in port.Connections)
                _graph.DisconnectSilently(conn.FromId, conn.FromPort, Id, port.Name);
        }

        logic.SwapDynamicContainer(@event.PropName, @event.NewContainer);

        var rebuiltPorts = CreateInputPorts(logic, _graph).ToList();
        InputPorts.Clear();
        foreach (var portVm in rebuiltPorts)
            InputPorts.Add(portVm);

        var newPortsByName = InputPorts.ToDictionary(p => p.Name);
        foreach (var saved in savedConnections)
        {
            if (!newPortsByName.TryGetValue(saved.Name, out var newPortVm)) continue;
            if (newPortVm.ControlAttribute?.ControlType != saved.ControlAttributeType) continue;
            if (newPortVm.EditorAttribute?.GetType() != saved.EditorAttributeType) continue;

            if (saved.Connections.Count > 0)
            {
                foreach (var conn in saved.Connections)
                    _graph.Connect(conn.FromId, conn.FromPort, Id, saved.Name);
            }
            else if (saved.Name.StartsWith(prefix))
            {
                var subName = saved.Name[prefix.Length..];
                var restoreValue = saved.LocalValue
                                   ?? @event.NewContainer.GetType().GetProperty(subName)
                                       ?.GetValue(@event.NewContainer);
                _graph.SetInputValue(Id, saved.Name, restoreValue);
            }
        }

        foreach (var portVm in InputPorts)
            portVm.ApplyDefaultToGraph();

        _portSignature = CreatePortSignature(logic);
        _graph.OnGraphChanged(new ConnectionChangedEventArgs(null, null, Id, null));
    }

    internal PortDefinition[] GetArgumentPortDefinitions()
    {
        return NodeLogic switch
        {
            ArgumentsNode argumentsNode => argumentsNode.GetPortDefinitions(),
            IPortDefinitionNode dynamicNode when NodeLogic is not ReturnNode => dynamicNode.GetPortDefinitions(),
            _ => []
        };
    }

    internal PortDefinition[] GetSubGraphPortDefinitions(bool outputs)
    {
        if (NodeLogic is not ISubGraphPortNode node) return [];
        return (outputs ? node.InnerReturn?.GetPortDefinitions() : node.InnerArguments?.GetPortDefinitions()) ?? [];
    }

    internal void ApplySubGraphPorts(bool outputs, PortDefinition[] definitions)
    {
        _graphViewModel.ApplySubGraphPorts(Id, outputs, definitions);
    }

    internal void ApplyArgumentPorts(PortDefinition[] definitions)
    {
        _graphViewModel.ApplyArgumentPorts(Id, definitions);
    }

    public event EventHandler? EditArgumentPortsRequested;

    public void CommitPosition()
    {
        _graph.SetVisualState(Id, _x, _y);
    }

    private IEnumerable<PortViewModel> CreateInputPorts(NodeLogic node, NodeGraph graph)
    {
        var dynamicNode = node as IPortDefinitionNode;
        var subGraphNode = node as ISubGraphPortNode;
        var definitions = dynamicNode?.GetPortDefinitions() ??
                          subGraphNode?.InnerArguments?.GetPortDefinitions();

        var orderedKeys = node.Inputs.Keys.OrderBy(name =>
        {
            if (!name.Contains('.')) return (0, 0);
            var parts = name.Split('.', 2);
            var containerProp = node.GetType().GetProperty(parts[0]);
            if (containerProp == null) return (0, 0);
            var containerInstance = containerProp.GetValue(node);
            var subProp = containerInstance?.GetType().GetProperty(parts[1]);
            return (containerProp.MetadataToken, subProp?.MetadataToken ?? 0);
        });

        foreach (var name in orderedKeys)
        {
            var port = node.Inputs[name];
            PropertyInfo? prop;
            object? editorPropertyOwner;
            if (name.Contains('.'))
            {
                var parts = name.Split('.', 2);
                var containerProp = node.GetType().GetProperty(parts[0]);
                var containerInstance = containerProp?.GetValue(node);
                prop = containerInstance?.GetType().GetProperty(parts[1]);
                editorPropertyOwner = containerInstance;
            }
            else
            {
                prop = node.GetType().GetProperty(name);
                editorPropertyOwner = node;
            }

            var definition = definitions?.FirstOrDefault(d => d.Name == name);
            var isTextPort = dynamicNode?.DynamicPortKind == DynamicPortKind.Text && definition != null;
            var isPartOfSpeechPort =
                dynamicNode?.DynamicPortKind == DynamicPortKind.PartOfSpeech && definition != null;
            var isUnicodeRangePort =
                dynamicNode?.DynamicPortKind == DynamicPortKind.UnicodeRange && definition != null;

            if (prop?.GetCustomAttribute<LegacyInputPortAttribute>() != null)
                continue;

            var portAttr = prop?.GetCustomAttribute<InputPortAttribute>();
            var portColorAttr = prop?.GetCustomAttribute<PortColorSettingAttribute>();
            var controlAttr = prop?.GetCustomAttribute<PropertyControlBaseAttribute>()
                              ?? (isUnicodeRangePort ? new UnicodeRangeStylePortControlAttribute() : null)
                              ?? (isPartOfSpeechPort ? new OptionalTextStylePortControlAttribute() : null)
                              ?? (isTextPort ? new TextPortControlAttribute() : null)
                              ?? (subGraphNode != null
                                  ? CreateSubGraphControlAttribute(port.ValueType)
                                  : null);
            var editorAttr = controlAttr == null ? ResolveEditorAttribute(prop) : null;

            yield return new PortViewModel(
                name,
                portAttr?.GetLabel() ?? definition?.GetDisplayLabel() ?? name,
                portAttr?.GetDescription() ?? (isTextPort ? TextNode.TextDescription :
                    isPartOfSpeechPort ? PartOfSpeechPortSettings.Describe(definition!) :
                    isUnicodeRangePort ? UnicodeRangePortSettings.Describe(definition!) : ""),
                portColorAttr?.Color ?? (isTextPort || isPartOfSpeechPort ? nameof(Colors.DarkOrange) :
                    subGraphNode != null ? GetSubGraphPortColor(port.ValueType) :
                    nameof(Colors.SlateGray)),
                port.ValueType,
                PortDirection.Input,
                controlAttr,
                editorAttr,
                editorPropertyOwner,
                prop,
                _graphViewModel.EditorInfo,
                graph,
                node.Id
            );
        }
    }

    private static PropertyControlBaseAttribute? CreateSubGraphControlAttribute(Type type)
    {
        if (type == typeof(float)) return new NumberPortControlAttribute();
        if (type == typeof(int)) return new NumberPortControlAttribute { Digits = 0 };
        if (type == typeof(bool)) return new BoolPortControlAttribute();
        if (type == typeof(Color)) return new ColorPortControlAttribute();
        if (type == typeof(string)) return new TextPortControlAttribute();
        return null;
    }

    private static string GetSubGraphPortColor(Type type)
    {
        if (type == typeof(ImageWrapper)) return nameof(Colors.CornflowerBlue);
        if (type == typeof(MaskWrapper) || type == typeof(OutlineWrapper)) return nameof(Colors.MediumPurple);
        if (type == typeof(BrushWrapper)) return nameof(Colors.LawnGreen);
        if (type == typeof(Color)) return nameof(Colors.Gold);
        if (type == typeof(bool)) return nameof(Colors.Green);
        return nameof(Colors.DarkOrange);
    }

    private static PropertyEditorAttribute2? ResolveEditorAttribute(PropertyInfo? prop)
    {
        if (prop == null) return null;

        var portDefsField = prop.DeclaringType?.GetField("_portDefs", BindingFlags.NonPublic | BindingFlags.Static);
        if (portDefsField?.GetValue(null) is Nodes.Effect.DynamicLoaded.PortDefinition[] portDefs)
        {
            var match = portDefs.FirstOrDefault(d => d.PropName == prop.Name);
            if (match?.EditorAttributeInstance != null)
                return match.EditorAttributeInstance;
        }

        return prop.GetCustomAttribute<PropertyEditorAttribute2>();
    }

    private IEnumerable<PortViewModel> CreateOutputPorts(NodeLogic node, NodeGraph graph)
    {
        var subGraphNode = node as ISubGraphPortNode;
        var argumentDefinitions = (node as ArgumentsNode)?.GetPortDefinitions() ??
                                  subGraphNode?.InnerReturn?.GetPortDefinitions();

        foreach (var (name, port) in node.Outputs)
        {
            var prop = node.GetType().GetProperty(name);
            var portAttr = prop?.GetCustomAttribute<OutputPortAttribute>();
            var portColorAttr = prop?.GetCustomAttribute<PortColorSettingAttribute>();
            var definition = argumentDefinitions?.FirstOrDefault(d => d.Name == name);

            yield return new PortViewModel(
                name,
                portAttr?.GetLabel() ?? definition?.GetDisplayLabel() ?? name,
                portAttr?.GetDescription() ?? "",
                portColorAttr?.Color ?? (subGraphNode != null
                    ? GetSubGraphPortColor(port.ValueType)
                    : nameof(Colors.SlateGray)),
                port.ValueType,
                PortDirection.Output,
                null,
                null,
                null,
                null,
                null,
                graph,
                node.Id
            );
        }
    }

    private IEnumerable<SubGraphViewModel> CreateSubGraphs(NodeLogic node)
    {
        foreach (var (name, subGraph) in node.SubGraphs)
        {
            var prop = node.GetType().GetProperty(name);
            var portAttr = prop?.GetCustomAttribute<SubGraphAttribute>();

            yield return new SubGraphViewModel(
                name,
                portAttr?.GetLabel() ?? name,
                portAttr?.GetDescription() ?? "",
                subGraph
            )
            {
                OpenSubGraphCommand = new RelayCommand(() =>
                {
                    ParentEditor.OpenGraph(subGraph, name, _graphViewModel.EditorInfo);
                }),
                OnGraphChangedCommand = new RelayCommand<GraphChangedEventArgs>(args =>
                {
                    // サブグラフ内のノードに関するイベントを親グラフへ流すと、親のノード一覧に混入する。
                    if (args is null or NodeAddedEventArgs or NodeRemovedEventArgs or ValueChangedEventArgs
                        or VisualStateChangedEventArgs) return;
                    _graph.OnGraphChanged(args);
                }),
                OnGraphCommitedCommand = new RelayCommand(() => _graph.Commit())
            };
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}