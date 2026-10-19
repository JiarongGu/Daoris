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
///
/// <para><b>One session knows its circle without a row: an intake</b> (D65 §1b). Its room is under the
/// home, never registered, and the circle is its ASK's — so the host names it here, from the ask, and
/// the room's path is never asked.</para>
///
/// <para><b>One session is BOUND to its circle: Ask Daoris in a workspace's room</b> (ASKSCOPE1c, D158's ASKSCOPE1 note point 5).
/// Its record keeps the workspace it was started in, and the driver hands that to its connector; what it asks is answered from
/// that workspace alone, and naming another, or <c>all</c>, is refused (<see cref="Bound"/>).</para>
/// </remarks>
public sealed class AmbientWorkspace(string workingDirectory, string? circle = null)
{
    /// <summary>
    /// The variable the driver names a conversation's bound workspace in (ASKSCOPE1c). 🔴 A twin: the driver's
    /// <c>HelpCircle.Variable</c> spells the same name, and each side's test holds the spelling.
    /// </summary>
    public const string BoundVariable = "DAORIS_SESSION_WORKSPACE";

    /// <summary>
    /// The workspace this session is bound to by its record (ASKSCOPE1c): every answer is that workspace's, whatever a call names.
    /// Null for every other session, which answers from its own circle unless a call deliberately names another.
    /// </summary>
    public string? Bound { get; init; }

    /// <summary>The workspace <paramref name="variable"/> binds a session to, trimmed; null where it names none.</summary>
    public static string? BoundFrom(Func<string, string?> variable) =>
        variable(BoundVariable) is { } named && !string.IsNullOrWhiteSpace(named) ? named.Trim() : null;

    /// <summary>
    /// The workspace bound to this session, or the circle named for it, or the workspace of the repository this directory sits
    /// in, or null when it sits in none.
    /// </summary>
    public async Task<string?> ResolveAsync(KnowledgeService service, CancellationToken ct = default)
    {
        if (Bound is { Length: > 0 } bound) return bound;
        if (circle is { Length: > 0 }) return circle;

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
