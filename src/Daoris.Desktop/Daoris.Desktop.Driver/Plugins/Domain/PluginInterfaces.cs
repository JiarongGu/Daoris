using System.Text.Json;

namespace Daoris.Driver;

/// <summary>A command a plugin runs in its own process, asked at <see cref="CommandWire.Method"/>.</summary>
/// <param name="Name">What the person types after the plugin's id: lowercase letters, digits and dashes.</param>
/// <param name="About">What it does, in the plugin's sentence.</param>
/// <param name="Interactive">Whether it asks the person for a value, so it runs only where a person answers.</param>
/// <param name="Tool">Whether an agent may call it as a tool; never one that asks the person.</param>
/// <param name="Adopts">The git identity's API token it takes once, where one serves these hosts (<see cref="PluginTokenAdoption"/>).</param>
public sealed record PluginCommandDeclared(string Name, string About, bool Interactive, bool Tool, PluginAdoption? Adopts = null);

/// <summary>
/// A command's word that it takes a git identity's API token once: the token of an identity serving one of <paramref name="Hosts"/> (a
/// host or a domain it ends in) is sent to the command as its <paramref name="Answer"/>, and the identity keeps none after.
/// </summary>
public sealed record PluginAdoption(IReadOnlyList<string> Hosts, string Answer);

/// <summary>A plugin's private storage: a folder of its own, handed to its process alone (<see cref="PluginPrivate"/>).</summary>
/// <param name="For">What the plugin keeps there, in its sentence.</param>
public sealed record PluginStorageDeclared(string For);

/// <summary>The interfaces a manifest declares, each reached only as declared; none for a refused plugin.</summary>
public sealed record PluginInterfacesDeclared(IReadOnlyList<PluginCommandDeclared> Commands, PluginStorageDeclared? Storage)
{
    public static readonly PluginInterfacesDeclared None = new([], null);

    /// <summary>The interfaces' names it declares, in <see cref="PluginInterfaces.All"/>'s order.</summary>
    public IReadOnlyList<string> Names =>
        [.. Commands.Count > 0 ? [PluginInterfaces.Commands] : Array.Empty<string>(), .. Storage is null ? [] : new[] { PluginInterfaces.Storage }];
}

/// <summary>A manifest's <c>interfaces</c> as read: the interfaces, or the first problem, which refuses the plugin.</summary>
public sealed record PluginInterfacesRead(PluginInterfacesDeclared Interfaces, string? Problem);

/// <summary>
/// Reads a manifest's <c>interfaces</c>: what the plugin implements that Daoris reaches it through, besides its points. Each is switched
/// on or off by the person apart from the plugin (<see cref="PluginInterfaceSwitch"/>).
/// </summary>
public static class PluginInterfaces
{
    /// <summary>Commands run in the plugin's process, from a terminal, the window, or an agent where a command is a tool.</summary>
    public const string Commands = "commands";

    /// <summary>A private folder the plugin keeps its own credentials and state in.</summary>
    public const string Storage = "storage";

    public static readonly IReadOnlyList<string> All = [Commands, Storage];

    /// <summary>The manifest's <c>interfaces</c> from its <paramref name="root"/> object, absent being none.</summary>
    public static PluginInterfacesRead Read(JsonElement root, bool hooks)
    {
        if (!root.TryGetProperty("interfaces", out var declared)) return new(PluginInterfacesDeclared.None, null);
        if (declared.ValueKind != JsonValueKind.Object)
            return Refused("`interfaces` must be an object naming the interfaces the plugin implements: `commands`, `storage`.");
        foreach (var named in declared.EnumerateObject())
        {
            if (!All.Contains(named.Name, StringComparer.Ordinal))
                return Refused($"interface `{named.Name}` is not one this build knows: `commands`, `storage`.");
            if (!hooks)
                return Refused($"interface `{named.Name}` is reached through the plugin's process, so the manifest must declare `hooks`.");
        }

        var commands = new List<PluginCommandDeclared>();
        if (declared.TryGetProperty(Commands, out var rows))
        {
            if (rows.ValueKind != JsonValueKind.Array) return Refused("`interfaces.commands` must be an array of the commands the plugin runs.");
            foreach (var row in rows.EnumerateArray())
            {
                var name = Text(row, "name");
                if (name is null || !PluginIds.NameShape.IsMatch(name))
                    return Refused("a declared command needs a `name` — lowercase letters, digits and dashes, at most 40.");
                if (commands.Any(command => command.Name == name)) return Refused($"command `{name}` is declared twice.");
                if (Text(row, "about") is not { } about) return Refused($"command `{name}` needs an `about`: what it does, in a sentence.");
                if (Flag(row, "interactive") is not { } interactive)
                    return Refused($"command `{name}`'s `interactive` must be true or false: whether it asks the person.");
                if (Flag(row, "tool") is not { } tool) return Refused($"command `{name}`'s `tool` must be true or false: whether an agent may call it.");
                if (tool && interactive) return Refused($"command `{name}` asks the person, so it is never an agent's tool.");
                PluginAdoption? adopts = null;
                if (row.TryGetProperty("adopts", out var adopted))
                {
                    var hosts = adopted.ValueKind == JsonValueKind.Object && adopted.TryGetProperty("hosts", out var named) && named.ValueKind == JsonValueKind.Array
                        ? named.EnumerateArray().Select(host => host.ValueKind == JsonValueKind.String ? host.GetString()!.Trim().ToLowerInvariant() : "").ToList()
                        : [];
                    if (hosts.Count == 0 || hosts.Any(host => host.Length == 0) || Text(adopted, "answer") is not { } answer || !PluginIds.NameShape.IsMatch(answer))
                        return Refused($"command `{name}`'s `adopts` must name its `hosts` and the `answer` the token is sent as.");
                    if (commands.Any(command => command.Adopts is not null)) return Refused("only one command may adopt a git identity's API token.");
                    adopts = new(hosts, answer);
                }

                commands.Add(new(name, about, interactive, tool, adopts));
            }
        }

        PluginStorageDeclared? storage = null;
        if (declared.TryGetProperty(Storage, out var kept))
        {
            if (Text(kept, "for") is not { } keeps) return Refused("`interfaces.storage` needs a `for`: what the plugin keeps there.");
            storage = new(keeps);
        }

        return new(new(commands, storage), null);
    }

    private static PluginInterfacesRead Refused(string problem) => new(PluginInterfacesDeclared.None, problem);

    private static string? Text(JsonElement row, string name) =>
        row.ValueKind == JsonValueKind.Object && row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        && value.GetString()!.Trim() is { Length: > 0 } text
            ? text
            : null;

    /// <summary>A flag as written, false where absent; null where it is not a flag.</summary>
    private static bool? Flag(JsonElement row, string name) =>
        !row.TryGetProperty(name, out var value) ? false
        : value.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => null };
}
