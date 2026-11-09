namespace Daoris.Driver;

/// <summary>
/// A node's ports as the save and the evaluation read them: its kind's own (<see cref="INodeKind.Inputs"/>, <see cref="INodeKind.Outputs"/>),
/// which no declaration changes, then those its declaration names.
/// </summary>
public static class NodePorts
{
    /// <summary>No ports: a kind's where it declares none of its own.</summary>
    public static readonly IReadOnlyDictionary<string, string> None = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>The inputs <paramref name="node"/> takes, each with its schema.</summary>
    public static IReadOnlyDictionary<string, string> Inputs(NodeDeclaration node) => Merged(NodeKit.Of(node.Kind)?.Inputs, node.Inputs);

    /// <summary>The outputs <paramref name="node"/> carries, each with its schema.</summary>
    public static IReadOnlyDictionary<string, string> Outputs(NodeDeclaration node) => Merged(NodeKit.Of(node.Kind)?.Outputs, node.Outputs);

    private static IReadOnlyDictionary<string, string> Merged(IReadOnlyDictionary<string, string>? kind, IReadOnlyDictionary<string, string> declared)
    {
        if (kind is not { Count: > 0 }) return declared;
        var ports = new Dictionary<string, string>(kind, StringComparer.Ordinal);
        foreach (var (name, schema) in declared) ports.TryAdd(name, schema);
        return ports;
    }
}
