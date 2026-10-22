using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>What serves a git act (the git-auth design §3.2): an identity, the person's own sign-ins, or nothing.</summary>
public enum GitServeKind
{
    /// <summary>No pointer and no default: until GITAUTH1e, the act keeps today's ambient credentials (§6.8).</summary>
    None,

    /// <summary>The person chose <i>Your own git sign-ins</i> for this scope (§3.3).</summary>
    Own,

    /// <summary>A git identity of Daoris's.</summary>
    Identity,
}

/// <summary>
/// The answer of a resolution (§3.2): what serves the act, the identity where one does, and where the answer came from
/// (<c>repository</c>, <c>workspace</c>, <c>machine</c>, <c>default</c>), with every pointer passed over on the way and why.
/// </summary>
public sealed record GitServes(GitServeKind Kind, GitIdentity? Identity, string? Source, IReadOnlyList<string> Passed)
{
    /// <summary>Nothing serves the act.</summary>
    public static GitServes Nothing(IReadOnlyList<string> passed) => new(GitServeKind.None, null, null, passed);

    /// <summary>What served the act, as a hold keys it (§5.2): the identity's id, <c>own</c>, or <c>none</c>.</summary>
    public string Served => Kind switch
    {
        GitServeKind.Identity => Identity!.Id,
        GitServeKind.Own => GitIdentityUse.Own,
        _ => "none",
    };
}

/// <summary>
/// The pointers and their resolution (GITAUTH1c, D198 point 1; the git-auth design §3.2). The identities are one set on the machine;
/// each scope only points, and the pointers live in <c>driver.json</c> beside the access keys, holding no secret:
/// <c>gitIdentity</c> (<c>{repository: {remote: id | "own"}}</c>), <c>workspaceGitIdentity</c> (<c>{workspace: {host: id | "own"}}</c>)
/// and <c>machineGitIdentity</c> (<c>{host: "own"}</c>); a host's default is the identity's own <c>default</c>.
/// </summary>
/// <remarks>
/// <para><b>The order</b>: the repository's choice for the remote whose effective address it is; else the workspace's for the
/// host; else this machine's <c>own</c> for the host, else the host's default identity; else none.</para>
/// <para><b>Passed over, never read as none above it</b>: a pointer to an identity that is gone, whose host is not the address's,
/// or whose way is not the address's (an SSH identity cannot carry an HTTPS act), each said in <see cref="GitServes.Passed"/>.</para>
/// <para>Read by the ssh door from the files alone, taking no lease; so the base library alone.</para>
/// </remarks>
public static class GitIdentityUse
{
    /// <summary>The pointer's value for <i>Your own git sign-ins</i>.</summary>
    public const string Own = "own";

    /// <summary>The repository's pointers, by remote.</summary>
    public const string RepositoryKey = "gitIdentity";

    /// <summary>The workspace's pointers, by host.</summary>
    public const string WorkspaceKey = "workspaceGitIdentity";

    /// <summary>This machine's <c>own</c>, by host.</summary>
    public const string MachineKey = "machineGitIdentity";

    /// <summary>The pointers <c>driver.json</c> holds, read as they stand.</summary>
    public sealed record Pointers(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Repository,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Workspace,
        IReadOnlyDictionary<string, string> Machine)
    {
        /// <summary>No pointer at all.</summary>
        public static readonly Pointers None = new(
            new Dictionary<string, IReadOnlyDictionary<string, string>>(), new Dictionary<string, IReadOnlyDictionary<string, string>>(),
            new Dictionary<string, string>());

        /// <summary>How many pointers there are, for <c>git.wired</c>.</summary>
        public int Count => Repository.Values.Sum(each => each.Count) + Workspace.Values.Sum(each => each.Count) + Machine.Count;
    }

    /// <summary>The pointers in the home's <c>driver.json</c>; none where it is absent or does not read.</summary>
    public static Pointers Read(string home)
    {
        try
        {
            var path = DaorisHome.Entry(home, HomeEntry.DriverConfig);
            if (!File.Exists(path)) return Pointers.None;
            return JsonNode.Parse(AtomicFile.ReadText(path)) is JsonObject root ? Of(root) : Pointers.None;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or DriverException)
        {
            return Pointers.None;
        }
    }

    /// <summary>The pointers of a <c>driver.json</c> object already read.</summary>
    public static Pointers Of(JsonObject root)
    {
        static IReadOnlyDictionary<string, string> Flat(JsonNode? node)
        {
            var kept = new Dictionary<string, string>(StringComparer.Ordinal);
            if (node is not JsonObject map) return kept;
            foreach (var (key, value) in map)
            {
                if (value is JsonValue text && text.TryGetValue<string>(out var said) && said.Length > 0) kept[key] = said;
            }

            return kept;
        }

        static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Nested(JsonNode? node)
        {
            var kept = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
            if (node is not JsonObject map) return kept;
            foreach (var (key, value) in map) kept[key] = Flat(value);
            return kept;
        }

        var machine = Flat(root[MachineKey]).Where(each => each.Value == Own)
            .ToDictionary(each => GitHosts.Canonical(each.Key), each => each.Value, StringComparer.Ordinal);
        return new Pointers(Nested(root[RepositoryKey]), Nested(root[WorkspaceKey]), machine);
    }

    /// <summary>
    /// Resolve what serves an act (§3.2).
    /// </summary>
    /// <param name="identities">The machine's identities.</param>
    /// <param name="pointers">The pointers.</param>
    /// <param name="repository">The repository the act's working folder belongs to; null for a folder Daoris knows as none.</param>
    /// <param name="workspace">That repository's workspace; null where it has none.</param>
    /// <param name="remotes">The repository's remotes whose effective address is the act's (two or more where they share it).</param>
    /// <param name="host">The act's host, canonical (<see cref="GitHosts.Canonical"/>).</param>
    /// <param name="way">The act's way: <c>ssh</c> or <c>https</c>.</param>
    public static GitServes Resolve(
        IReadOnlyList<GitIdentity> identities, Pointers pointers, string? repository, string? workspace,
        IReadOnlyList<string> remotes, string host, string way)
    {
        var passed = new List<string>();

        GitServes? Take(string? pointer, string source, string where)
        {
            if (pointer is null) return null;
            if (pointer == Own) return new GitServes(GitServeKind.Own, null, source, passed);
            var identity = identities.FirstOrDefault(each => each.Id == pointer);
            if (identity is null) passed.Add($"{where} points at {pointer}, which is gone");
            else if (identity.Host != host) passed.Add($"{where} points at {identity.Label}, which is for {identity.Host}, not {host}");
            else if (identity.Way != way) passed.Add($"{where} points at {identity.Label}, an {identity.Way.ToUpperInvariant()} identity, for an {way.ToUpperInvariant()} address");
            else return new GitServes(GitServeKind.Identity, identity, source, passed);
            return null;
        }

        if (repository is not null && pointers.Repository.TryGetValue(repository, out var byRemote))
        {
            foreach (var remote in remotes)
            {
                if (byRemote.TryGetValue(remote, out var pointer) && Take(pointer, "repository", $"{repository}'s {remote}") is { } served) return served;
            }
        }

        if (workspace is not null && pointers.Workspace.TryGetValue(workspace, out var byHost))
        {
            var pointer = byHost.FirstOrDefault(each => GitHosts.Canonical(each.Key) == host).Value;
            if (Take(pointer, "workspace", $"workspace {workspace}") is { } served) return served;
        }

        if (pointers.Machine.ContainsKey(host)) return new GitServes(GitServeKind.Own, null, "machine", passed);

        var fallback = identities.FirstOrDefault(each => each.Default && each.Host == host && each.Way == way);
        return fallback is not null ? new GitServes(GitServeKind.Identity, fallback, "default", passed) : GitServes.Nothing(passed);
    }

    /// <summary>
    /// Write one pointer into a <c>driver.json</c> object (§8 <c>git use</c>): <paramref name="value"/> an identity's id or
    /// <see cref="Own"/>, or null to clear it; an emptied scope is removed. The caller writes the object back under the home's lease.
    /// </summary>
    public static void Set(JsonObject root, string key, string scope, string? inner, string? value)
    {
        if (key == MachineKey)
        {
            var machine = root[MachineKey] as JsonObject ?? new JsonObject();
            if (value is null) machine.Remove(scope);
            else machine[scope] = value;
            if (machine.Count == 0) root.Remove(MachineKey);
            else root[MachineKey] = machine;
            return;
        }

        var outer = root[key] as JsonObject ?? new JsonObject();
        var map = outer[scope] as JsonObject ?? new JsonObject();
        if (value is null) map.Remove(inner!);
        else map[inner!] = value;
        if (map.Count == 0) outer.Remove(scope);
        else outer[scope] = map;
        if (outer.Count == 0) root.Remove(key);
        else root[key] = outer;
    }

    /// <summary>
    /// Every scope pointing at an identity (§7.1 <i>Remove…</i> lists them before it goes): <c>repository/remote</c> and
    /// <c>workspace/host</c>, as written.
    /// </summary>
    public static IReadOnlyList<string> PointingAt(Pointers pointers, string id) =>
    [
        .. pointers.Repository.SelectMany(each => each.Value.Where(pointer => pointer.Value == id).Select(pointer => $"{each.Key}'s {pointer.Key}")),
        .. pointers.Workspace.SelectMany(each => each.Value.Where(pointer => pointer.Value == id).Select(pointer => $"workspace {each.Key}'s {pointer.Key}")),
    ];
}
