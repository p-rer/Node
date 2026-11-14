using Node.Graph;
using Node.Graph.Attributes;
using Node.Graph.Port;
using Node.Localize;

namespace Node.Nodes.Func;

[Node(typeof(FunctionCategory), nameof(TextNode.ArgumentsNode), nameof(TextNode.ArgumentsNodeDescription),
    typeof(TextNode))]
public class ArgumentsNode : NodeLogic, IPortDefinitionNode
{
    private PortDefinition[] _portDefinitions = Array.Empty<PortDefinition>();

    public ArgumentsNode()
    {
    }

    public ArgumentsNode(params PortDefinition[] argumentPorts)
    {
        Initialize(argumentPorts);
    }

    public bool AllowCustomPorts { get; set; }

    public void Initialize(PortDefinition[] argumentPorts)
    {
        _portDefinitions = argumentPorts;
        Outputs.Clear();

        foreach (var portDef in argumentPorts)
        {
            var port = new OutputPort(this, portDef.ValueType);
            Outputs.Add(portDef.Name, port);

            if (portDef.DefaultValue == null) continue;

            if (portDef.IsCustom && ArgumentPortKinds.TryGetKind(portDef.ValueType, out var kind))
                port.SetValue(ArgumentPortKinds.ToPortValue(kind, portDef.DefaultValue));
            else
                port.SetValue(portDef.DefaultValue);
        }
    }

    public PortDefinition[] GetPortDefinitions()
    {
        return _portDefinitions;
    }

    bool IPortDefinitionNode.IsValidPortSet(PortDefinition[] definitions)
    {
        return definitions.Select(d => d.Name).Distinct().Count() == definitions.Length;
    }

    bool IPortDefinitionNode.DynamicPortsAreOutputs => true;

    DynamicPortKind IPortDefinitionNode.DynamicPortKind => DynamicPortKind.Default;

    void IPortDefinitionNode.NotifyPortDefinitionsChanged()
    {
    }

    public void InjectArguments(Dictionary<string, object?> arguments)
    {
        foreach (var (name, value) in arguments)
            if (Outputs.TryGetValue(name, out var port))
                port.SetValue(value);

        InvalidateForce();
    }

    public void InjectArgument(string name, object? value)
    {
        if (Outputs.TryGetValue(name, out var port))
        {
            port.SetValue(value);
            InvalidateForce();
        }
    }

    protected override Task Calculate()
    {
        return Task.CompletedTask;
    }
}