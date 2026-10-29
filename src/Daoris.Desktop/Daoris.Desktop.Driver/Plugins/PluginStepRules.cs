namespace Daoris.Driver;

/// <summary>
/// What a plugin's manifest asks of the workflows' node table, which owns the authority by kind: whether a step it declares is a sound node
/// (FABRIC-UNIT1d, §D4's <i>validated before saved</i>). Workflows installs it when the driver loads and answers exactly as the table did
/// before it was a port, so a step refused is refused by the same code.
/// </summary>
internal interface IPluginStepRules
{
    /// <summary>What does not hold of <paramref name="step"/>, by its field, or null: the node table's own checks.</summary>
    string? Problem(NodeDeclaration step);
}

/// <summary>The required Workflows operation, resolved only where a manifest's step is read.</summary>
internal static class PluginStepRules
{
    private static IPluginStepRules? _current;

    internal static IPluginStepRules Current
    {
        get => _current ?? throw new InvalidOperationException("no plugin step rules are installed: Workflows installs them when the driver loads");
        set => _current = value;
    }
}
