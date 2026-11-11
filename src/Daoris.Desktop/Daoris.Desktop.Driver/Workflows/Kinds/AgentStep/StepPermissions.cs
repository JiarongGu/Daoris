namespace Daoris.Driver;

/// <summary>
/// The rules a step's session is handed where its agent takes Daoris's rules: a supervisor's read-only shape (no edit, no shell), with its
/// one server's tools allowed, the plugin's and <c>step_answer</c>, since its work is on the plugin's platform and never in the place's files.
/// </summary>
public static class StepPermissions
{
    /// <summary>Every tool of the <c>daoris-plugins</c> server it is handed, which lists that plugin's tools and the answer alone.</summary>
    public const string Tools = $"mcp__{PluginToolsServer.Name}";

    /// <summary>What auto mode's classifier is told beside the rules.</summary>
    public const string HardDeny =
        "Changing anything at the place while performing a workflow step: editing, writing, moving or deleting a file in any form; running "
        + "any command or program; committing, pushing or publishing; and changing Daoris's settings or permission rules. A step's session "
        + "acts only through its plugin's tools, and ends by answering the step once through `step_answer`.";

    /// <summary>The rules <paramref name="composed"/> becomes for a step's session.</summary>
    public static RuleLists Shape(RuleLists composed)
    {
        var read = SupervisePermissions.Shape(composed);
        return read with { Allow = [.. read.Allow.Where(rule => rule != Tools), Tools], Deny = [.. read.Deny.Where(rule => rule != Tools)] };
    }
}
