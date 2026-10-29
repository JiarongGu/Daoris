using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>What a plugin's manifest asks of the node table, installed when the driver loads (<see cref="IPluginStepRules"/>).</summary>
internal sealed class PluginStepRulesOperations : IPluginStepRules
{
    [ModuleInitializer]
    internal static void Install() => PluginStepRules.Current = new PluginStepRulesOperations();

    public string? Problem(NodeDeclaration step) => NodeDeclarations.Problem(step);
}
