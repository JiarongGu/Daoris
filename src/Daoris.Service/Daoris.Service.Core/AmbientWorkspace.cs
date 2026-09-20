namespace Daoris.Knowledge;

/// <summary>
/// Which workspace a session is speaking for, resolved from where it is running.
/// </summary>
/// <remarks>
/// <para><b>The default scope is the session's own circle</b> (D48 §4). An agent asking "has anyone
/// solved this" means its own family — answering with every family the machine can see is the
/// disclosure the workspace boundary exists to prevent, and it happens on the very first call a
/// session makes.</para>
///
/// <para><b>Resolved from the registry by path, never from the tree.</b> Membership is wiring (D48 §2):
/// nothing in a repository says which workspace it belongs to, and nothing should — that is what makes
/// a fork, a mirror and a private copy work. So the answer is a registry row found by matching the
/// working directory against the roots the machine knows.</para>
///
/// <para><b>No match is null, and null means "say so".</b> A session running somewhere unregistered has
/// no circle to default to, and quietly picking one would be a guess presented as a scope. The caller
/// spans everything and reports that it did — the D24 shape: name the tier that answered.</para>
/// </remarks>
public sealed class AmbientWorkspace(string workingDirectory)
{
    /// <summary>Where this process was started — for a stdio host, the repository that spawned it.</summary>
    public static AmbientWorkspace Here() => new(Directory.GetCurrentDirectory());

    /// <summary>The workspace of the repository this directory sits in, or null when it sits in none.</summary>
    public async Task<string?> ResolveAsync(KnowledgeService service, CancellationToken ct = default)
    {
        var registry = await service.RegistryAsync(ct: ct).ConfigureAwait(false);
        return Containing(registry, workingDirectory)?.InWorkspace;
    }

    /// <summary>
    /// The registered repository this path is inside, longest root first.
    /// </summary>
    /// <remarks>
    /// Longest wins because a checkout inside a checkout is a real layout, and the inner one is the
    /// repository the session is actually in. The comparison is on path SEGMENTS — a prefix test would
    /// place a session in `…/game` when it is running in `…/game-tools`, which is a whole different
    /// circle reached by a string that merely started the same way.
    /// </remarks>
    internal static Registration? Containing(IReadOnlyList<Registration> registry, string path)
    {
        var target = Normalize(path);
        if (target.Length == 0) return null;

        return registry
            .Where(r => !string.IsNullOrWhiteSpace(r.Root))
            .Select(r => (Registration: r, Root: Normalize(r.Root!)))
            .Where(candidate => candidate.Root.Length > 0 && Inside(target, candidate.Root))
            .OrderByDescending(candidate => candidate.Root.Length)
            .Select(candidate => candidate.Registration)
            .FirstOrDefault();
    }

    /// <summary>Whether a path is the root itself or beneath it — a full segment beneath it.</summary>
    private static bool Inside(string path, string root) =>
        path.Equals(root, Comparison) || path.StartsWith(root + '/', Comparison);

    /// <summary>
    /// One spelling: forward slashes, no trailing separator. Paths arrive from a registration written
    /// on this machine and from the process's own working directory, and on Windows those two disagree
    /// about the separator often enough that comparing them raw is a coin toss.
    /// </summary>
    private static string Normalize(string path) =>
        path.Replace('\\', '/').TrimEnd('/');

    /// <summary>
    /// Case-insensitive, because Windows and macOS are — and a case-sensitive miss here is silent: the
    /// session simply gets an unscoped answer with no indication that its own root was right there.
    /// </summary>
    private const StringComparison Comparison = StringComparison.OrdinalIgnoreCase;
}
