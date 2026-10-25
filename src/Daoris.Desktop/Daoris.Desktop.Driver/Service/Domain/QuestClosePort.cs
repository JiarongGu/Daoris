namespace Daoris.Driver;

/// <summary>One quest a close names (FABRIC-CLOSE1b), with the submission it judged: what the close door checks is still current.</summary>
public sealed record QuestCloseItem(string Id, string Machine, long Sequence);

/// <summary>What the close door answered (<see cref="QuestCloseAnswer.Outcome"/>).</summary>
public static class QuestCloseOutcomes
{
    /// <summary>Every quest named is Done by this close: made now, or made already under the same key.</summary>
    public const string Closed = "closed";

    /// <summary>A check refused it, now or when it was first sent under this key, or the key went with another close: nothing was written.</summary>
    public const string Refused = "refused";

    /// <summary>
    /// The refusal's code for a key the door never took before it was asked what became of it (FABRIC-CLOSE1u): that close never ran, and the
    /// key is withdrawn, so it never runs. Its close is judged again on what is there now, under a new key.
    /// </summary>
    public const string Withdrawn = "withdrawn";

    /// <summary>
    /// The checks the close door names a refusal by (the service's <c>QuestCloseCodes</c>): only a refusal naming one of these is the door's
    /// own, and settles the close (FABRIC-CLOSE1l, the review's finding 7). Any other answer leaves the close unknown. 🔴 Twins: a code the
    /// door adds is added here, or its refusal leaves the close unknown; FABRIC-CLOSE1v added the door's scope checks, a quest of another
    /// repository, of another chain, or bound to another graph.
    /// </summary>
    public static IReadOnlySet<string> Codes { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "malformed", "not-found", "not-bound", "not-held", "closed", "stale", "held", "unsealed", "key-reused", "refused", Withdrawn,
        "other-repository", "not-in-chain", "other-graph",
    };
}

/// <summary>The close door's answer (FABRIC-CLOSE1a's <c>POST /api/quests/{id}/close</c>), as the driver reads it.</summary>
/// <param name="Outcome">One of <see cref="QuestCloseOutcomes"/>.</param>
/// <param name="Code">A refusal's check, as the door names it (<c>stale</c>, <c>held</c>, <c>key-reused</c>, …); null otherwise.</param>
/// <param name="Message">The door's sentence, verbatim.</param>
/// <param name="Quests">The quests named, as they now stand, where the door answered them.</param>
public sealed record QuestCloseAnswer(string Outcome, string? Code, string Message, IReadOnlyList<QuestView> Quests);

/// <summary>The close door, as the closer calls it (FABRIC-CLOSE1b): a port, so the fence is proved without a host.</summary>
public interface IQuestClosePort
{
    /// <exception cref="HttpRequestException">The host did not answer: the close's outcome is unknown, and its door is asked what became of it.</exception>
    Task<QuestCloseAnswer> CloseQuestsAsync(string quest, string key, string token, IReadOnlyList<QuestCloseItem> quests, CancellationToken ct);

    /// <summary>
    /// What became of a close sent with <paramref name="key"/> (FABRIC-CLOSE1u), answered from the door's own record, executing nothing: its
    /// first outcome where the door took the key (closed, or its refusal), else refused <see cref="QuestCloseOutcomes.Withdrawn"/>, the key
    /// withdrawn in the same transaction so a send of it still on its way is refused too.
    /// </summary>
    /// <exception cref="HttpRequestException">The host did not answer: the close stays unknown, and is asked after again.</exception>
    Task<QuestCloseAnswer> CloseOutcomeAsync(string quest, string key, string token, IReadOnlyList<QuestCloseItem> quests, CancellationToken ct);
}

/// <summary>The service's two doors a bind reads and writes (FABRIC-CLOSE1b): the handshake, and the bind itself. A port, so a bind is proved without a host.</summary>
public interface IClosureBindPort
{
    /// <summary>Whether the local host knows a closure graph's operations (<c>bound</c>, <c>submitted</c>) and doors; false from a host before them.</summary>
    Task<bool> KnowsClosureAsync(CancellationToken ct);

    /// <summary>
    /// Whether the local host judges a bind in a workspace synced through a remote by the remote's handshake (FABRIC-CLOSE1i), refusing it
    /// where a machine of it has not declared it knows a bound quest; false from a host before it, which is asked no such bind.
    /// </summary>
    Task<bool> KnowsRemoteClosureAsync(CancellationToken ct) => Task.FromResult(false);

    /// <summary>Names <paramref name="graph"/> for the take of <paramref name="id"/> by <paramref name="session"/>, whose take alone binds it (FABRIC-CLOSE1o).</summary>
    Task<(bool Ok, string Message)> BindQuestAsync(string id, string graph, string session, CancellationToken ct);
}
