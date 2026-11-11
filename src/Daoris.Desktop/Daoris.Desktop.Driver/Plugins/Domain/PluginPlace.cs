namespace Daoris.Driver;

/// <summary>
/// Where a plugin is asked or reached: a repository in its workspace, a workspace, or the machine, named by the setup tree's keys
/// (<c>repository:&lt;id&gt;</c>, <c>workspace:&lt;name&gt;</c>, <c>machine</c>).
/// </summary>
public sealed record PluginPlace(string? Repository = null, string? Workspace = null)
{
    public const string MachineKey = "machine";

    private const string RepositoryPrefix = "repository:";

    private const string WorkspacePrefix = "workspace:";

    public static PluginPlace Machine { get; } = new();

    /// <summary>The narrowest key: the repository's, else the workspace's, else the machine's.</summary>
    public string Key => Repository is { Length: > 0 } repository ? RepositoryPrefix + repository
        : Workspace is { Length: > 0 } workspace ? WorkspacePrefix + workspace
        : MachineKey;

    /// <summary>The keys a switch is read at, narrow to wide, ending at the machine.</summary>
    public IReadOnlyList<string> Chain
    {
        get
        {
            var chain = new List<string>(3);
            if (Repository is { Length: > 0 } repository) chain.Add(RepositoryPrefix + repository);
            if (Workspace is { Length: > 0 } workspace) chain.Add(WorkspacePrefix + workspace);
            chain.Add(MachineKey);
            return chain;
        }
    }

    /// <summary>A place's key as the person writes it, the machine where none is written, or null where it names no place.</summary>
    public static PluginPlace? FromKey(string? key) => key switch
    {
        null or MachineKey => Machine,
        _ when key.StartsWith(RepositoryPrefix, StringComparison.Ordinal) && key.Length > RepositoryPrefix.Length
            => new(Repository: key[RepositoryPrefix.Length..]),
        _ when key.StartsWith(WorkspacePrefix, StringComparison.Ordinal) && key.Length > WorkspacePrefix.Length
            => new(Workspace: key[WorkspacePrefix.Length..]),
        _ => null,
    };
}
