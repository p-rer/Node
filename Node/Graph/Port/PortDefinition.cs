namespace Node.Graph.Port;

public record PortDefinition(
    string Name,
    Type ValueType,
    string Label = "",
    string Description = "",
    object? DefaultValue = null,
    bool IsCustom = false
);