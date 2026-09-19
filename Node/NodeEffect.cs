using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Newtonsoft.Json;
using Node.Editor.ViewModel;
using Node.Graph;
using Node.Graph.Events;
using Node.Graph.Port;
using Node.Graph.Snapshot;
using Node.Localize;
using Node.Nodes.Func;
using Node.ValueTypes;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;
using AlphaMode = Vortice.DCommon.AlphaMode;

namespace Node;

[VideoEffect(nameof(TextUi.Node), [VideoEffectCategories.Filtering], ["Node"], IsAviUtlSupported = false,
    ResourceType = typeof(TextUi))]
public sealed class NodeEffect : VideoEffectBase
{
    private readonly Dispatcher _uiDispatcher = Application.Current.Dispatcher;

    internal readonly Lock ProcessorInitializationLock = new();
    private GraphSnapshot _graph = new();

    private Guid _graphId = Guid.NewGuid();

    private volatile GraphSnapshot? _pendingExternalUpdate;

    public NodeEffect()
    {
        GraphLibraryViewModel.Current?.Subscribe(_graphId, this);
    }

    public override string Label => TextUi.Node;

    [Display(Name = nameof(TextUi.NodeEditor), GroupName = nameof(TextUi.Node),
        ResourceType = typeof(TextUi))]
    [OpenNodeEditor]
    public Guid GraphId
    {
        get => _graphId;
        set
        {
            var oldId = _graphId;
            Set(ref _graphId, value);
            if (oldId == value) return;

            var library = GraphLibraryViewModel.Current;
            library?.Unsubscribe(oldId, this);
            library?.Subscribe(value, this);
        }
    }

    public GraphSnapshot Graph
    {
        get => _graph;
        set
        {
            Set(ref _graph, value);
            ApplyToInternalGraph(value);
            GraphLibraryViewModel.Current?.PropagateChange(GraphId, this, value);
        }
    }

    public GraphSnapshot InternalGraphSnapshot
    {
        get => _graph;
        set
        {
            Set(ref _graph, value, nameof(Graph));
            GraphLibraryViewModel.Current?.PropagateChange(GraphId, this, value);
        }
    }

    [JsonIgnore]
    internal NodeGraph? InternalGraph
    {
        get;
        set
        {
            if (field != null)
                UnSubscribeChildUndoRedoable(field.PreviewNotifier);
            field = value;
            if (field != null)
                SubscribeChildUndoRedoable(field.PreviewNotifier);
        }
    }

    private void ApplyToInternalGraph(GraphSnapshot value)
    {
        if (InternalGraph is null) return;
        var tempGraph = Serializer.Restore(value);
        lock (InternalGraph.GraphLock)
        {
            InternalGraph.UpdateGraph(tempGraph);
        }

        InvokeGraphUpdated();
    }

    internal void ApplyExternalGraphUpdate(GraphSnapshot snapshot)
    {
        _graph = snapshot;
        _pendingExternalUpdate = snapshot;
    }

    internal bool ApplyPendingExternalUpdateLocked()
    {
        var pending = _pendingExternalUpdate;
        if (pending is null || InternalGraph is null) return false;

        _pendingExternalUpdate = null;
        var tempGraph = Serializer.Restore(pending);
        InternalGraph.UpdateGraph(tempGraph);
        return true;
    }

    public void SwitchGraph(Guid newGraphId)
    {
        if (GraphId == newGraphId) return;

        var library = GraphLibraryViewModel.Current;
        var snapshot = library != null && library.TryGetSnapshot(newGraphId, out var s)
            ? s
            : new GraphSnapshot();

        GraphId = newGraphId;
        Graph = snapshot;
    }

    public event EventHandler? GraphUpdated;

    internal void InvokeGraphUpdated()
    {
        if (_uiDispatcher.CheckAccess())
            GraphUpdated?.Invoke(this, EventArgs.Empty);
        else
            _uiDispatcher.BeginInvoke(() => GraphUpdated?.Invoke(this, EventArgs.Empty));
    }

    protected override IEnumerable<IAnimatable> GetAnimatables()
    {
        return [];
    }

    public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex,
        ExoOutputDescription exoOutputDescription)
    {
        return [""];
    }

    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
    {
        return new Processor(devices, this);
    }

    private static bool IsFileSelectorProperty(PropertyInfo prop)
    {
        return Attribute.IsDefined(prop, typeof(FileSelectorAttribute)) ||
               prop.GetCustomAttributes(true)
                   .Any(attr => attr is PropertyEditorAttribute2 &&
                                attr.GetType().Name.Contains("FileSelector", StringComparison.Ordinal));
    }

    private static IEnumerable<(InputPort Port, string Path)> EnumerateFilePathPorts(NodeGraph graph)
    {
        foreach (var node in graph.Nodes.Values)
        {
            foreach (var entry in EnumerateFilePathPorts(node))
                yield return entry;

            foreach (var entry in node.SubGraphs.Values.SelectMany(EnumerateFilePathPorts))
                yield return entry;
        }
    }

    private static IEnumerable<(InputPort Port, string Path)> EnumerateFilePathPorts(NodeLogic node)
    {
        foreach (var prop in node.GetType().GetProperties())
        {
            if (IsFileSelectorProperty(prop))
            {
                if (node.Inputs.TryGetValue(prop.Name, out var port) &&
                    port.LocalValue is string { Length: > 0 } path)
                    yield return (port, path);
                continue;
            }

            if (!typeof(InputsContainer).IsAssignableFrom(prop.PropertyType)) continue;
            if (prop.GetValue(node) is not InputsContainer container) continue;

            foreach (var subProp in container.GetType().GetProperties())
            {
                if (!IsFileSelectorProperty(subProp)) continue;
                var key = $"{prop.Name}.{subProp.Name}";
                if (!node.Inputs.TryGetValue(key, out var port)) continue;
                if (port.LocalValue is string { Length: > 0 } path)
                    yield return (port, path);
            }
        }
    }

    public override IEnumerable<string> GetFiles()
    {
        if (InternalGraph is not null)
            return EnumerateFilePathPorts(InternalGraph)
                .Select(p => p.Path)
                .Distinct()
                .ToList();

        try
        {
            var tempGraph = Serializer.Restore(Graph);
            return EnumerateFilePathPorts(tempGraph)
                .Select(p => p.Path)
                .Distinct()
                .ToList();
        }
        catch (Exception)
        {
            return base.GetFiles();
        }
    }

    public override void ReplaceFile(string from, string to)
    {
        if (InternalGraph is null)
        {
            try
            {
                var tempGraph = Serializer.Restore(Graph);
                var savedMatches = EnumerateFilePathPorts(tempGraph)
                    .Where(p => p.Path == from)
                    .ToList();

                if (savedMatches.Count == 0)
                    return;

                foreach (var match in savedMatches)
                    match.Port.SetValue(to);

                Graph = Serializer.Create(tempGraph);
            }
            catch (Exception)
            {
                base.ReplaceFile(from, to);
            }

            return;
        }

        var matches = EnumerateFilePathPorts(InternalGraph)
            .Where(p => p.Path == from)
            .ToList();

        if (matches.Count == 0)
            return;

        InternalGraph.BeginEdit();
        try
        {
            foreach (var match in matches)
                match.Port.SetValue(to);
        }
        finally
        {
            InternalGraph.EndEdit();
        }
    }
}

public sealed class Processor : IVideoEffectProcessor
{
    private readonly IGraphicsDevicesAndContext _devices;
    private readonly NodeEffect _nodeEffect;
    private ID2D1Image? _affineOutput;

    private AffineTransform2D? _affineTransform;
    private ID2D1Bitmap1? _blankBitmap;

    private ID2D1Image? _currentInputImage;

    private bool _hasError;

    private volatile ArgumentsNode _inputNode = null!;
    private bool _isEvaluating;
    private volatile Lock _lock;
    private ID2D1Image? _outputImage;
    private volatile ReturnNode _outputNode = null!;

    public Processor(IGraphicsDevicesAndContext devices, NodeEffect effect)
    {
        _devices = devices;
        _nodeEffect = effect;

        var graph = InitializeGraph();
        _lock = graph.GraphLock;

        CreateBlankBitmap();

        _nodeEffect.GraphUpdated += OnGraphUpdated;
    }

    public ID2D1Image Output => _outputImage ?? _blankBitmap!;

    public DrawDescription Update(EffectDescription effectDescription)
    {
        lock (_lock)
        {
            if (_nodeEffect.ApplyPendingExternalUpdateLocked())
            {
                _inputNode = _nodeEffect.InternalGraph!.Nodes.Values.OfType<ArgumentsNode>().FirstOrDefault()
                             ?? _inputNode;
                _outputNode = _nodeEffect.InternalGraph.Nodes.Values.OfType<ReturnNode>().FirstOrDefault()
                              ?? _outputNode;
            }

            try
            {
                if (_isEvaluating)
                    return effectDescription.DrawDescription;

                _isEvaluating = true;

                if (_nodeEffect.InternalGraph == null! || _inputNode == null! || _outputNode == null!)
                    return effectDescription.DrawDescription;

                // 評価開始
                var context = new EvaluationContext(_devices, effectDescription);

                _inputNode.InjectArguments(new Dictionary<string, object?>
                {
                    ["InputImage"] = new ImageWrapper { Image = _currentInputImage },
                    ["FrameIndex"] = effectDescription.ItemPosition.Frame
                });

                var outputDict = _outputNode.ExtractReturns(context).GetAwaiter().GetResult();
                var outputImage = (outputDict["OutputImage"] as ImageWrapper)?.Image;

                if (outputImage == null || outputImage.NativePointer == IntPtr.Zero)
                    throw new InvalidOperationException(TextUi.OutputImageIsNull);
                _devices.DeviceContext.GetImageLocalBounds(outputImage);

                _outputImage = outputImage;
                _hasError = false;

                ApplyAffineTransform(outputImage);

                return effectDescription.DrawDescription;
            }
            catch (Exception ex)
            {
                if (!_hasError) Debug.WriteLine($"[Processor] Error: {ex.Message}");

                _hasError = true;

                SetBlankImage();
                ApplyAffineTransform(_blankBitmap!);

                return effectDescription.DrawDescription;
            }
            finally
            {
                _isEvaluating = false;
            }
        }
    }

    public void SetInput(ID2D1Image? input)
    {
        lock (_lock)
        {
            _currentInputImage = input;
        }
    }

    public void ClearInput()
    {
        lock (_lock)
        {
            _currentInputImage = null;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_nodeEffect.InternalGraph != null!)
            {
                _nodeEffect.InternalGraph.Committed -= OnGraphCommitted;
                _nodeEffect.InternalGraph.GraphChanged -= OnGraphChangedForBroadcast;
            }

            _nodeEffect.GraphUpdated -= OnGraphUpdated;

            if (_nodeEffect.InternalGraph != null)
                foreach (var node in _nodeEffect.InternalGraph.Nodes.Values)
                    node.Dispose();

            ClearInput();

            _affineTransform?.SetInput(0, null, true);
            _affineTransform?.Dispose();
            _affineTransform = null;

            _affineOutput?.Dispose();
            _affineOutput = null;
            _outputImage = null;

            _blankBitmap?.Dispose();
            _blankBitmap = null;
        }
    }

    /// <summary>
    ///     グラフの初期化。
    /// </summary>
    private NodeGraph InitializeGraph()
    {
        lock (_nodeEffect.ProcessorInitializationLock)
        {
            if (_nodeEffect.InternalGraph is { } existingGraph)
            {
                _inputNode = existingGraph.Nodes.Values.OfType<ArgumentsNode>().First();
                _outputNode = existingGraph.Nodes.Values.OfType<ReturnNode>().First();

                existingGraph.Committed -= OnGraphCommitted;
                existingGraph.Committed += OnGraphCommitted;
                existingGraph.GraphChanged -= OnGraphChangedForBroadcast;
                existingGraph.GraphChanged += OnGraphChangedForBroadcast;

                return existingGraph;
            }

            NodeGraph graph;

            if (_nodeEffect.Graph.Nodes.Count > 0)
            {
                graph = Serializer.Restore(_nodeEffect.Graph);
                _nodeEffect.InternalGraph = graph;

                _inputNode = graph.Nodes.Values.OfType<ArgumentsNode>().FirstOrDefault() ??
                             new ArgumentsNode(
                                 new PortDefinition("InputImage", typeof(ImageWrapper)),
                                 new PortDefinition("FrameIndex", typeof(int))
                             )
                             {
                                 Id = Guid.NewGuid()
                             };
                _outputNode = graph.Nodes.Values.OfType<ReturnNode>().FirstOrDefault() ??
                              new ReturnNode(
                                  new PortDefinition("OutputImage", typeof(ImageWrapper))
                              )
                              {
                                  Id = Guid.NewGuid()
                              };
            }
            else
            {
                graph = new NodeGraph();
                _nodeEffect.InternalGraph = graph;

                _inputNode = new ArgumentsNode(
                    new PortDefinition("InputImage", typeof(ImageWrapper)),
                    new PortDefinition("FrameIndex", typeof(int))
                )
                {
                    Id = Guid.NewGuid()
                };

                _outputNode = new ReturnNode(
                    new PortDefinition("OutputImage", typeof(ImageWrapper))
                )
                {
                    Id = Guid.NewGuid()
                };

                graph.AddNode(_inputNode);
                graph.AddNode(_outputNode);

                graph.SetVisualState(_inputNode.Id, 100, 100);
                graph.SetVisualState(_outputNode.Id, 500, 100);

                graph.Connect(_inputNode.Id, "InputImage", _outputNode.Id, "OutputImage");

                _nodeEffect.Graph = Serializer.Create(graph);

                _inputNode = graph.Nodes.Values.OfType<ArgumentsNode>().First();
                _outputNode = graph.Nodes.Values.OfType<ReturnNode>().First();
            }

            graph.Committed -= OnGraphCommitted;
            graph.Committed += OnGraphCommitted;
            graph.GraphChanged -= OnGraphChangedForBroadcast;
            graph.GraphChanged += OnGraphChangedForBroadcast;

            _nodeEffect.InvokeGraphUpdated();

            return graph;
        }
    }

    private void OnGraphUpdated(object? sender, EventArgs e)
    {
        var currentLock = _lock;
        lock (currentLock)
        {
            if (_nodeEffect.InternalGraph == null!) return;

            _nodeEffect.InternalGraph.Committed -= OnGraphCommitted;
            _nodeEffect.InternalGraph.Committed += OnGraphCommitted;
            _nodeEffect.InternalGraph.GraphChanged -= OnGraphChangedForBroadcast;
            _nodeEffect.InternalGraph.GraphChanged += OnGraphChangedForBroadcast;

            _inputNode = _nodeEffect.InternalGraph.Nodes.Values.OfType<ArgumentsNode>().FirstOrDefault()
                         ?? _inputNode;
            _outputNode = _nodeEffect.InternalGraph.Nodes.Values.OfType<ReturnNode>().FirstOrDefault()
                          ?? _outputNode;

            _lock = _nodeEffect.InternalGraph.GraphLock;
        }
    }

    private void OnGraphCommitted(object? sender, CommittedEventArgs e)
    {
        try
        {
            if (_nodeEffect.InternalGraph == null!) return;

            GraphSnapshot snapshot;
            lock (_lock)
            {
                _nodeEffect.InternalGraph.InvalidateAll();
                snapshot = Serializer.Create(_nodeEffect.InternalGraph);
            }

            _nodeEffect.InternalGraphSnapshot = snapshot;
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception.Message);
        }
    }

    private void OnGraphChangedForBroadcast(object? sender, GraphChangedEventArgs e)
    {
        try
        {
            if (_nodeEffect.InternalGraph == null!) return;

            GraphSnapshot snapshot;
            lock (_lock)
            {
                snapshot = Serializer.Create(_nodeEffect.InternalGraph);
            }

            GraphLibraryViewModel.Current?.PropagateChange(_nodeEffect.GraphId, _nodeEffect, snapshot);
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception.Message);
        }
    }

    private void CreateBlankBitmap()
    {
        var bitmapProperties = new BitmapProperties1(
            new PixelFormat(Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied),
            96,
            96,
            BitmapOptions.Target
        );

        _blankBitmap = _devices.DeviceContext.CreateBitmap(
            new SizeI(1, 1),
            IntPtr.Zero,
            0,
            bitmapProperties
        );
    }

    private void SetBlankImage()
    {
        if (_blankBitmap == null) CreateBlankBitmap();

        var deviceContext = _devices.DeviceContext;
        var previousTarget = deviceContext.Target;
        deviceContext.Target = _blankBitmap;
        deviceContext.BeginDraw();
        deviceContext.Clear(new Color(0, 0, 0, 0));
        deviceContext.EndDraw();
        deviceContext.Target = previousTarget;

        _outputImage = _blankBitmap;
    }

    private void ApplyAffineTransform(ID2D1Image input)
    {
        _affineTransform ??= new AffineTransform2D(_devices.DeviceContext)
        {
            BorderMode = BorderMode.Soft,
            TransformMatrix = Matrix3x2.Identity
        };

        _affineTransform.SetInput(0, input, true);

        _affineOutput?.Dispose();
        _affineOutput = _affineTransform.Output;
        _outputImage = _affineOutput;
    }
}