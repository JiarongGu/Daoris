namespace Daoris.Driver;

/// <summary>One command a plugin marked <c>tool</c>, as an agent calls it.</summary>
public sealed record PluginToolCommand(PluginEntry Plugin, PluginCommandDeclared Command)
{
    /// <summary>
    /// <c>&lt;plugin&gt;_&lt;command&gt;</c>: an id and a command name hold lowercase letters, digits and dashes alone, so the underscore
    /// parts them unambiguously, and the model APIs' tool names take no dot.
    /// </summary>
    public string Name => Plugin.Manifest.Id + "_" + Command.Name;

    /// <summary>The input schema an agent's call is checked against, from the command's typed arguments.</summary>
    public object Schema()
    {
        var properties = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var arg in Command.Args)
        {
            var property = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["type"] = arg.Type switch { "number" => "number", "boolean" => "boolean", _ => "string" },
                ["description"] = arg.Title.AsWritten,
            };
            if (arg.Type == "choice") property["enum"] = arg.Choices;
            properties[arg.Name] = property;
        }

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = Command.Args.Where(arg => arg.Required).Select(arg => arg.Name).ToList(),
            ["additionalProperties"] = false,
        };
    }
}

/// <summary>
/// The tools a session at a place is handed: each command marked <c>tool</c> and asking nothing of the person, of each installed plugin
/// that is on, not refused and speaks, where its <c>commands</c> and <c>tools</c> are both on at that place (<see cref="PluginSwitches"/>).
/// Read again at each list and call, so a switch moved during a session holds at its next call.
/// </summary>
public static class PluginToolList
{
    public static IReadOnlyList<PluginToolCommand> At(string home, PluginPlace place)
    {
        var tools = new List<PluginToolCommand>();
        foreach (var plugin in PluginCatalog.Load(home).Plugins)
        {
            if (!plugin.Enabled || plugin.Problem is not null || plugin.Manifest.Hooks is null) continue;
            var marked = plugin.Manifest.Interfaces.Commands.Where(command => command.Tool && !command.Interactive).ToList();
            if (marked.Count == 0) continue;
            var id = plugin.Manifest.Id;
            if (!PluginSwitches.IsOn(home, id, PluginSwitches.Commands, place) || !PluginSwitches.IsOn(home, id, PluginSwitches.Tools, place)) continue;
            tools.AddRange(marked.Select(command => new PluginToolCommand(plugin, command)));
        }

        return tools;
    }
}
