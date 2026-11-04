namespace Daoris.Knowledge;

/// <summary>
/// Who holds a working tree (D51): the session an open or a go-on is refused for, the sentence that names it, and where a repository's
/// main tree is. One session per working tree holds for a start and for going on alike, so both read it here.
/// </summary>
internal sealed class SessionHolders(QuestStore quests, SessionStore sessions, KnowledgeService? registry)
{
    /// <summary>
    /// The driver's code for a session that ended its turn on its own open helper ask (HELPERWAIT1; its <c>NoteCodes.EndedAwaitsHelper</c>,
    /// a twin read here by its spelling): completed, since it waits on that helper and not on the person.
    /// </summary>
    internal const string EndedAwaitsHelper = "ended.awaits-helper";

    /// <summary>Whether a finished record ended waiting on its own helper (HELPERWAIT1): completed, by that code.</summary>
    internal static bool WaitedOnHelper(Session session) =>
        session.State == SessionState.Completed && NoteParts.Carries(session.NoteParts, EndedAwaitsHelper);

    /// <summary>
    /// The session holding <paramref name="repository"/>'s <paramref name="tree"/> (D51): an active one (<see cref="SessionStore.ActiveForAsync"/>),
    /// else one that ended waiting on its own helper while its quest is still taken (HELPERWAIT1; Codex's #5 on COWORKUX1), which goes on
    /// there with the answer or parks for the person, and so keeps its tree as a park does. <paramref name="except"/> is the record moving,
    /// which holds nothing against itself.
    /// </summary>
    public async Task<Session?> HolderAsync(string repository, string? tree, CancellationToken inside, string? except = null)
    {
        if (await sessions.ActiveForAsync(repository, tree, inside).ConfigureAwait(false) is { } active) return active;
        foreach (var waiting in await sessions.CompletedWithCodeAsync(repository, tree, EndedAwaitsHelper, inside).ConfigureAwait(false))
        {
            if (string.Equals(waiting.Id, except, StringComparison.Ordinal) || !WaitedOnHelper(waiting)) continue;
            if (await quests.FindAsync(waiting.Quest!, inside).ConfigureAwait(false) is { Status: QuestStatus.Taken }) return waiting;
        }

        return null;
    }

    /// <summary>
    /// The busy sentence, phrased once for both doors (D51).
    /// </summary>
    /// <remarks>
    /// <b>It names the SESSION holding the tree, never the tree's PATH.</b> A path is machine-local
    /// material (D47 §4) and a refusal is the one surface with no strip on it: it is composed here and
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
