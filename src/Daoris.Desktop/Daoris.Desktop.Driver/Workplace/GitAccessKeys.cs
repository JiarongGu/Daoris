using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>One git-access class as it resolves for a repository: its value and where it is set.</summary>
/// <param name="Class"><c>read</c>, <c>write</c> or <c>dangerous</c>.</param>
/// <param name="Value"><c>allow</c>, <c>ask</c>, <c>deny</c>, or <see cref="GitAccessKeys.AsToday"/> where the person set none for Write.</param>
/// <param name="Source"><c>repository</c>, <c>workspace</c>, <c>machine</c> or <c>default</c>.</param>
public sealed record GitAccessClass(string Class, string Value, string Source);

/// <summary>A repository's git access as it resolves, class by class.</summary>
public sealed record GitAccess(IReadOnlyList<GitAccessClass> Classes)
{
    /// <summary>The class named <paramref name="name"/>.</summary>
    public GitAccessClass Of(string name) => Classes.First(each => each.Class == name);

    /// <summary>Whether the person set no class at any level: Daoris's default, door by door.</summary>
    public bool Default => Classes.All(each => each.Source == GitAccessKeys.DefaultSource);
}

/// <summary>The three levels as <c>driver.json</c> keeps them: <c>gitAccess</c> by repository, <c>workspaceGitAccess</c>, <c>machineGitAccess</c>.</summary>
public sealed record GitAccessLevels(
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Repository,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Workspace,
    IReadOnlyDictionary<string, string> Machine)
{
    /// <summary>No level set.</summary>
    public static GitAccessLevels None { get; } = new(
        new Dictionary<string, IReadOnlyDictionary<string, string>>(), new Dictionary<string, IReadOnlyDictionary<string, string>>(),
        new Dictionary<string, string>());
}

/// <summary>
/// Git access as <c>driver.json</c> keeps it (10 and 12): <c>gitAccess</c>
/// (by repository), <c>workspaceGitAccess</c> and <c>machineGitAccess</c>, each <c>{read, write, dangerous}</c> with any subset set.
/// Separate keys from <c>access</c>, since a level of <c>access</c> may be <c>"full"</c>, which never touches git access. Read from the
/// file alone, so the doors (which compile this in) judge from it; edited by <see cref="GitAccessActs"/>.
/// </summary>
/// <remarks>
/// A stored shape: a class or a value this build does not know is passed over, and stays in the file as written, since an edit changes
/// only the key paths it changed. An older build reading the file passes the keys over and enforces none of them.
/// </remarks>
public static class GitAccessKeys
{
    public const string RepositoryKey = "gitAccess";
    public const string WorkspaceKey = "workspaceGitAccess";
    public const string MachineKey = "machineGitAccess";

    public const string Read = "read";
    public const string Write = "write";
    public const string Dangerous = "dangerous";

    public const string Allow = "allow";
    public const string Ask = "ask";
    public const string Deny = "deny";

    /// <summary>Write where the person set none: the landing pushes on the person's yes, a session does not push.</summary>
    public const string AsToday = "as-today";

    public const string FromRepository = "repository";
    public const string FromWorkspace = "workspace";
    public const string FromMachine = "machine";
    public const string DefaultSource = "default";

    /// <summary>The classes, in the order every door says them.</summary>
    public static readonly IReadOnlyList<string> Classes = [Read, Write, Dangerous];

    /// <summary>The values a person sets, loosest first.</summary>
    public static readonly IReadOnlyList<string> Values = [Allow, Ask, Deny];

    /// <summary>The class a spelling names, trimmed and in any case, or null.</summary>
    public static string? ClassNamed(string? spelled) =>
        Classes.FirstOrDefault(each => string.Equals(each, spelled?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The value a spelling names, trimmed and in any case, or null.</summary>
    public static string? ValueNamed(string? spelled) =>
        Values.FirstOrDefault(each => string.Equals(each, spelled?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The value Daoris's default gives <paramref name="name"/> where none is set.</summary>
    public static string DefaultOf(string name) => name switch { Read => Allow, Write => AsToday, _ => Deny };

    /// <summary>How loose a value is, for telling a widening: allow 0, ask 1, as-today 2 (sessions deny, the landing asks), deny 3.</summary>
    public static int Looseness(string value) => value switch { Allow => 0, Ask => 1, AsToday => 2, _ => 3 };

    /// <summary>The workspace name a repository in none is in.</summary>
    public static string WorkspaceName(string? workspace) => string.IsNullOrWhiteSpace(workspace) ? "default" : workspace.Trim();

    /// <summary>The levels in the home's <c>driver.json</c>; none where it is absent or does not read.</summary>
    public static GitAccessLevels Load(string home)
    {
        try
        {
            var path = DaorisHome.Entry(home, HomeEntry.DriverConfig);
            if (!File.Exists(path) || JsonNode.Parse(AtomicFile.ReadText(path)) is not JsonObject root) return GitAccessLevels.None;
            return Of(root);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or DriverException)
        {
            return GitAccessLevels.None;
        }
    }

    /// <summary>The levels in a parsed <c>driver.json</c>.</summary>
    public static GitAccessLevels Of(JsonObject root) => new(Map(root[RepositoryKey]), Map(root[WorkspaceKey]), Level(root[MachineKey]));

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Map(JsonNode? node)
    {
        var map = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (node is not JsonObject object_) return map;
        foreach (var (name, level) in object_)
        {
            var key = name.Trim();
            if (key.Length == 0 || map.ContainsKey(key)) continue;
            var read = Level(level);
            if (read.Count > 0) map[key] = read;
        }

        return map;
    }

    private static IReadOnlyDictionary<string, string> Level(JsonNode? node)
    {
        var level = new Dictionary<string, string>(StringComparer.Ordinal);
        if (node is not JsonObject object_) return level;
        foreach (var (name, value) in object_)
        {
            if (ClassNamed(name) is not { } known || value is not JsonValue text || !text.TryGetValue<string>(out var spelled) || ValueNamed(spelled) is not { } named) continue;
            level[known] = named;
        }

        return level;
    }

    /// <summary>Each class as it resolves: the repository's, else its workspace's, else this machine's, else Daoris's default.</summary>
    public static GitAccess Resolve(GitAccessLevels levels, string? repository, string? workspace)
    {
        IReadOnlyDictionary<string, string>? own = null;
        if (repository is not null) levels.Repository.TryGetValue(repository.Trim(), out own);
        levels.Workspace.TryGetValue(WorkspaceName(workspace), out var shared);
        return new([
            .. Classes.Select(name =>
                own is not null && own.TryGetValue(name, out var a) ? new GitAccessClass(name, a, FromRepository)
                : shared is not null && shared.TryGetValue(name, out var b) ? new GitAccessClass(name, b, FromWorkspace)
                : levels.Machine.TryGetValue(name, out var c) ? new GitAccessClass(name, c, FromMachine)
                : new GitAccessClass(name, DefaultOf(name), DefaultSource)),
        ]);
    }

    /// <summary>The same, from the home's file.</summary>
    public static GitAccess Resolve(string home, string? repository, string? workspace) => Resolve(Load(home), repository, workspace);
}
