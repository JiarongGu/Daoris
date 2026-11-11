using System.Text.Json;
using static Daoris.Driver.PluginInterfaceFields;

namespace Daoris.Driver;

/// <summary>A command a plugin runs in its own process, asked at <see cref="CommandWire.Method"/>.</summary>
/// <param name="Name">What the person types after the plugin's id: lowercase letters, digits and dashes.</param>
/// <param name="About">What it does, in the plugin's words.</param>
/// <param name="Interactive">Whether it asks the person for a value, so it runs only where a person answers.</param>
/// <param name="Tool">Whether an agent may call it as a tool; never one that asks the person.</param>
/// <param name="Adopts">The git identity's API token it takes once, where one serves these hosts (<see cref="PluginTokenAdoption"/>).</param>
public sealed record PluginCommandDeclared(string Name, AuthoredText About, bool Interactive, bool Tool, PluginAdoption? Adopts = null)
{
    /// <summary>What the window shows it as: its name where unwritten.</summary>
    public AuthoredText Title { get; init; } = AuthoredText.Of(Name);

    /// <summary>Its typed arguments, in declared order.</summary>
    public IReadOnlyList<PluginCommandArg> Args { get; init; } = [];

    /// <summary>The records whose menus offer it (<see cref="PluginCommandReader.Records"/>).</summary>
    public IReadOnlyList<string> On { get; init; } = [];

    /// <summary>Whether it changes something outside the plugin, which the install plan says and an agent's tool is annotated with.</summary>
    public bool Writes { get; init; }

    /// <summary>The question the window asks once before it runs; null to ask none.</summary>
    public AuthoredText? Confirm { get; init; }
}

/// <summary>
/// A command's word that it takes a git identity's API token once: the token of an identity serving one of <paramref name="Hosts"/> (a
/// host or a domain it ends in) is sent to the command as its <paramref name="Answer"/>, and the identity keeps none after.
/// </summary>
public sealed record PluginAdoption(IReadOnlyList<string> Hosts, string Answer);

/// <summary>A plugin's private storage: a folder of its own, handed to its process alone (<see cref="PluginPrivate"/>).</summary>
/// <param name="For">What the plugin keeps there, in its words.</param>
public sealed record PluginStorageDeclared(AuthoredText For)
{
    /// <summary>The command run once before a removal deletes the folder, to sign out at its service; null for none.</summary>
    public string? Forget { get; init; }

    /// <summary>Why <see cref="Forget"/> is not run: it names no command, a refused one, or one that asks the person.</summary>
    public string? ForgetDisabled { get; init; }
}

/// <summary>An interface entry refused, alone: its interface, its name or id where it has one, and why.</summary>
public sealed record PluginInterfaceRefusal(string Interface, string? Entry, string Problem);

/// <summary>
/// The interfaces a manifest declares, each reached only as declared; none for a refused plugin. What does not read is in
/// <see cref="Refused"/>, entry by entry, and the rest is read and reached as if it were all.
/// </summary>
public sealed record PluginInterfacesDeclared(IReadOnlyList<PluginCommandDeclared> Commands, PluginStorageDeclared? Storage)
{
    public static readonly PluginInterfacesDeclared None = new([], null);

    public IReadOnlyList<PluginViewDeclared> Views { get; init; } = [];

    /// <summary>Each entry refused and why, in the manifest's order.</summary>
    public IReadOnlyList<PluginInterfaceRefusal> Refused { get; init; } = [];

    /// <summary>The interfaces it names that this build does not know, each once, skipped.</summary>
    public IReadOnlyList<string> Unknown { get; init; } = [];

    /// <summary>The interfaces' names it declares, in <see cref="PluginInterfaces.All"/>'s order.</summary>
    public IReadOnlyList<string> Names =>
    [
        .. Commands.Count > 0 ? [PluginInterfaces.Commands] : Array.Empty<string>(),
        .. Storage is null ? [] : new[] { PluginInterfaces.Storage },
        .. Views.Count > 0 ? [PluginInterfaces.Views] : Array.Empty<string>(),
    ];
}

/// <summary>
/// Reads a manifest's <c>interfaces</c>: what the plugin implements that Daoris reaches it through, besides its points. A problem
/// refuses its entry alone, never the plugin. Each is switched on or off by the person apart from the plugin (<see cref="PluginInterfaceSwitch"/>).
/// </summary>
public static class PluginInterfaces
{
    /// <summary>Commands run in the plugin's process, from a terminal, the window, or an agent where a command is a tool.</summary>
    public const string Commands = "commands";

    /// <summary>A private folder the plugin keeps its own credentials and state in.</summary>
    public const string Storage = "storage";

    /// <summary>What it shows, as trees Daoris draws on its page or in a side tab.</summary>
    public const string Views = "views";

    public static readonly IReadOnlyList<string> All = [Commands, Storage, Views];

    /// <summary>The manifest's <c>interfaces</c> from its <paramref name="root"/> object, absent being none.</summary>
    public static PluginInterfacesDeclared Read(JsonElement root, bool hooks)
    {
        if (!root.TryGetProperty("interfaces", out var declared)) return PluginInterfacesDeclared.None;
        if (declared.ValueKind != JsonValueKind.Object)
        {
            return PluginInterfacesDeclared.None with
            {
                Refused = [new("interfaces", null, $"`interfaces` must be an object naming the interfaces the plugin implements: {Listed(All)}.")],
            };
        }

        var named = declared.EnumerateObject().Select(each => each.Name).Distinct(StringComparer.Ordinal).ToList();
        var unknown = named.Where(name => !All.Contains(name, StringComparer.Ordinal)).ToList();
        var refused = new List<PluginInterfaceRefusal>();
        if (!hooks)
        {
            refused.AddRange(named.Except(unknown).Select(name => new PluginInterfaceRefusal(
                name, null, $"interface `{name}` is reached through the plugin's process, so the manifest must declare `hooks`.")));
            return PluginInterfacesDeclared.None with { Refused = refused, Unknown = unknown };
        }

        var commands = new List<PluginCommandDeclared>();
        var refusedCommands = new Dictionary<string, string>(StringComparer.Ordinal);
        if (declared.TryGetProperty(Commands, out var rows))
        {
            if (rows.ValueKind != JsonValueKind.Array) refused.Add(new(Commands, null, "`interfaces.commands` must be an array of the commands the plugin runs."));
            else foreach (var row in rows.EnumerateArray())
            {
                var read = PluginCommandReader.Read(row, commands);
                if (read.Command is { } command) commands.Add(command);
                if (read.Problem is not { } problem) continue;
                refused.Add(new(Commands, read.Name, problem));
                if (read.Name is { } name) refusedCommands.TryAdd(name, problem);
            }
        }

        PluginStorageDeclared? storage = null;
        if (declared.TryGetProperty(Storage, out var kept))
        {
            AuthoredText? keeps = null;
            if (kept.ValueKind != JsonValueKind.Object || !Words(kept, "for", PluginCommandReader.AboutMost, oneLine: false, null, out keeps) || keeps is null)
                refused.Add(new(Storage, null, "`interfaces.storage` needs a `for`: what the plugin keeps there."));
            else storage = Forgotten(new(keeps), kept, commands, refusedCommands);
        }

        var views = new List<PluginViewDeclared>();
        if (declared.TryGetProperty(Views, out var shown))
        {
            if (shown.ValueKind != JsonValueKind.Array) refused.Add(new(Views, null, "`interfaces.views` must be an array of the views the plugin shows."));
            else foreach (var row in shown.EnumerateArray())
            {
                var read = PluginViewReader.Read(row, views);
                if (read.View is { } view) views.Add(view);
                if (read.Problem is { } problem) refused.Add(new(Views, read.Id, problem));
            }
        }

        return new(commands, storage) { Views = views, Refused = refused, Unknown = unknown };
    }

    /// <summary>Storage with its <c>forget</c>: the command it names, disabled with the reason where that command cannot run alone.</summary>
    private static PluginStorageDeclared Forgotten(
        PluginStorageDeclared storage, JsonElement kept, IReadOnlyList<PluginCommandDeclared> commands, IReadOnlyDictionary<string, string> refused)
    {
        if (!kept.TryGetProperty("forget", out _)) return storage;
        var forget = Text(kept, "forget");
        if (forget is null || !PluginIds.NameShape.IsMatch(forget))
            return storage with { ForgetDisabled = "`forget` must name a command the plugin declares." };
        var disabled = commands.FirstOrDefault(command => command.Name == forget) is { } command
            ? command.Interactive ? $"`forget` names `{forget}`, which asks the person, and a removal has no one to answer it." : null
            : refused.TryGetValue(forget, out var problem) ? $"`forget` names `{forget}`, which is refused: {problem}"
            : $"`forget` names `{forget}`, which is not a command the plugin declares.";
        return storage with { Forget = forget, ForgetDisabled = disabled };
    }
}
