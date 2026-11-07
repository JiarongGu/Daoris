using System.Reflection;

namespace Daoris.Driver;

/// <summary>One group of the tree: its sub-verbs, what it reads where none is named, and the sub-verbs a sub-verb has.</summary>
/// <param name="Takes">What the group reads where its first word is no sub-verb (a new ask's words, a role's id); null where it needs one.</param>
/// <param name="Whole">Whether the group's own command judges every word after it, its sub-verbs listed for the usage only.</param>
public sealed record TreeGroup(string Name, IReadOnlyList<string> Verbs, string? Takes = null, bool Whole = false)
{
    /// <summary>A sub-verb's own sub-verbs (<c>agent account …</c>), by the sub-verb.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Nested { get; init; } = new Dictionary<string, IReadOnlyList<string>>();
}

/// <summary>
/// The words of Daoris's one command tree: <c>daoris …</c> for everything. This program's groups are declared by the code that runs
/// each, with <see cref="CommandGroupAttribute"/>, and collected here; the doctrine commands are the CLI's rows alone. Each program hands
/// the other a word it does not own, once (<see cref="HandedVariable"/>); Loop's <c>CommandTree</c> routes by the groups.
/// </summary>
public static class CommandWords
{
    /// <summary>The one name a person and a session type.</summary>
    public const string Name = "daoris";

    /// <summary>The variable a program sets on the other when it hands words across, so a word handed once is never handed back.</summary>
    public const string HandedVariable = "DAORIS_HANDED";

    /// <summary>Every group this assembly's units declare, by name.</summary>
    public static IReadOnlyList<TreeGroup> Groups { get; } = Collect(typeof(CommandWords).Assembly);

    /// <summary>The groups an assembly's units declare, by name; a name declared twice is refused.</summary>
    public static IReadOnlyList<TreeGroup> Collect(Assembly assembly)
    {
        var marks = assembly.GetTypes().Where(type => type.IsDefined(typeof(CommandGroupAttribute), inherit: false))
            .SelectMany(type => type.GetCustomAttributes<CommandGroupAttribute>().Select(mark => (type, mark))).ToList();
        if (marks.GroupBy(each => each.mark.Name, StringComparer.Ordinal).FirstOrDefault(named => named.Count() > 1) is { } twice)
        {
            throw new InvalidOperationException(
                $"the command group `{twice.Key}` is declared twice, by {string.Join(" and ", twice.Select(each => each.type.Name))}: a group has one owner");
        }

        return [.. marks.Select(each => new TreeGroup(each.mark.Name, each.mark.Verbs, each.mark.Takes, each.mark.Whole)
            {
                Nested = each.mark.Nested.Select(line => line.Split(':', 2)).ToDictionary(
                    pair => pair[0].Trim(), pair => (IReadOnlyList<string>)pair[1].Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal),
            })
            .OrderBy(group => group.Name, StringComparer.Ordinal)];
    }

    /// <summary>
    /// The CLI an install carries, found from the desktop's program's own folder (<c>app/daoris-driver/</c>): the publish's
    /// <c>app/cli/node_modules/daoris/bin/daoris.mjs</c> (<c>CLI_PACKAGE</c> and <c>CLI_ENTRY</c>); null where none is there.
    /// </summary>
    public static string? CarriedCli(string programFolder)
    {
        var entry = Path.GetFullPath(Path.Combine(programFolder, "..", "cli", "node_modules", "daoris", "bin", "daoris.mjs"));
        return File.Exists(entry) ? entry : null;
    }

    /// <summary>What a word no group owns says where no CLI is carried beside this program to run it as a doctrine command.</summary>
    public static string NoCli(string command) =>
        $"`{command}` is no group of this program (`daoris help` names each), and the doctrine commands are the CLI's, which this program "
        + "carries none of beside it (an install's app/cli/): run the CLI's own `daoris` (the npm package) for one; nothing was done.";

    /// <summary>What a doctrine command says where the tools' Node.js cannot run it, with the tools' reason where they gave one.</summary>
    public static string NoNode(string? why) =>
        $"the doctrine commands run on Node.js 22 or later, and none runs here: {(why ?? "`node` is not on this machine's PATH").TrimEnd('.')}; nothing was done.";

    /// <summary>
    /// The words a lease and a refusal name a command by: <c>daoris</c>, its group, and its sub-verb where the second word is one;
    /// never an argument.
    /// </summary>
    public static string Named(IReadOnlyList<string> typed)
    {
        if (typed.Count == 0) return Name;
        var group = Group(typed[0]);
        return group is not null && typed.Count > 1 && group.Verbs.Contains(typed[1], StringComparer.Ordinal)
            ? $"{Name} {typed[0]} {typed[1]}"
            : $"{Name} {typed[0]}";
    }

    /// <summary>The group named <paramref name="name"/>, or null.</summary>
    public static TreeGroup? Group(string? name) => Groups.FirstOrDefault(group => group.Name == name);
}
