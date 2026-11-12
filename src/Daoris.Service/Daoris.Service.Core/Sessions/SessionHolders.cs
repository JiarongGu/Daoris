namespace Daoris.Knowledge;

/// <summary>
/// Who holds a working tree: the session an open or a go-on is refused for, the sentence that names it, and where a repository's
/// main tree is. One session per working tree holds for a start and for going on alike, so both read it here.
/// </summary>
internal sealed class SessionHolders(QuestStore quests, SessionStore sessions, KnowledgeService? registry)
{
    private readonly SessionPlaceLease lease = new(sessions);

    /// <summary>The writer's lease of the place at <paramref name="root"/>; see <see cref="SessionPlaceLease.HolderAsync"/>.</summary>
    public Task<Session?> PlaceHolderAsync(string department, string? root, CancellationToken inside, string? except = null) =>
        lease.HolderAsync(department, root, inside, except);

    /// <summary>Write or clear a record's mark; see <see cref="SessionPlaceLease.MarkAsync"/>.</summary>
    public Task<bool> MarkPlaceAsync(string id, IReadOnlyList<string>? paths, CancellationToken inside) => lease.MarkAsync(id, paths, inside);

    /// <summary>
    /// Whether <paramref name="tree"/> is the place itself rather than a copy of it: the registered root, or a tree unsaid. Where no
    /// root is registered a named tree stays under the tree lock, which reads an unknown conservatively.
    /// </summary>
    public static bool InPlace(string? tree, string? root) =>
        Trees.Normalize(tree) is not { } held || (Trees.Normalize(root) is { } place && string.Equals(held, place, StringComparison.Ordinal));

    /// <summary>
    /// The driver's code for a session that ended its turn on its own open helper ask (its <c>EndNoteCodes.EndedAwaitsHelper</c>,
    /// a twin read here by its spelling): completed, since it waits on that helper and not on the person.
    /// </summary>
    internal const string EndedAwaitsHelper = "ended.awaits-helper";

    /// <summary>The driver's code for a session that ended its turn on its own open check (<c>EndNoteCodes.EndedAwaitsCheck</c>), read here by its spelling.</summary>
    internal const string EndedAwaitsCheck = "ended.awaits-check";

    /// <summary>Whether a finished record ended waiting on its own helper or its own check: completed, by one of those codes.</summary>
    internal static bool WaitedOnHelper(Session session) =>
        session.State == SessionState.Completed
        && (NoteParts.Carries(session.NoteParts, EndedAwaitsHelper) || NoteParts.Carries(session.NoteParts, EndedAwaitsCheck));

    /// <summary>
    /// The session holding <paramref name="repository"/>'s <paramref name="tree"/>: an active one (<see cref="SessionStore.ActiveForAsync"/>),
    /// else one that ended waiting on its own helper while its quest is still taken, which goes on
    /// there with the answer or parks for the person, and so keeps its tree as a park does. <paramref name="except"/> is the record moving,
    /// which holds nothing against itself.
    /// </summary>
    public async Task<Session?> HolderAsync(string repository, string? tree, CancellationToken inside, string? except = null)
    {
        if (await sessions.ActiveForAsync(repository, tree, inside).ConfigureAwait(false) is { } active) return active;
        var waits = (await sessions.CompletedWithCodeAsync(repository, tree, EndedAwaitsHelper, inside).ConfigureAwait(false))
            .Concat(await sessions.CompletedWithCodeAsync(repository, tree, EndedAwaitsCheck, inside).ConfigureAwait(false));
        foreach (var waiting in waits)
        {
            if (string.Equals(waiting.Id, except, StringComparison.Ordinal) || !WaitedOnHelper(waiting)) continue;
            if (await quests.FindAsync(waiting.Quest!, inside).ConfigureAwait(false) is { Status: QuestStatus.Taken }) return waiting;
        }

        return null;
    }

    /// <summary>
    /// The busy sentence, phrased once for both doors.
    /// </summary>
    /// <remarks>
    /// <b>It names the SESSION holding the tree, never the tree's PATH.</b> A path is machine-local
    /// material and a refusal is the one surface with no strip on it: it is composed here and
    /// rendered verbatim wherever it lands. The session id is the actionable half anyway — it is what
    /// a person stops, and what the record they should read is filed under.
    /// </remarks>
    public static string Busy(string repository, Session active) =>
        $"`{repository}`'s working tree already has an active session — `{active.Id}` "
        + $"({(WaitedOnHelper(active) ? "waiting on its own helper" : Session.Spell(active.State))}{(active.Quest is null ? ", a chat" : $", quest `#{active.Quest}`")}). "
        + "One session per working tree: two agents in one tree corrupt each other's git state.";

    /// <summary>
    /// Where this repository's main tree is, as this machine's registry knows it — null when nothing
    /// is registered, which the lock reads conservatively rather than as "any tree is free".
    /// </summary>
    public async Task<string?> RootOfAsync(string repository, CancellationToken ct)
    {
        if (registry is null) return null;

        return (await registry.RegistryAsync(ct: ct).ConfigureAwait(false))
            .Named(repository)
            ?.Root;
    }
}
