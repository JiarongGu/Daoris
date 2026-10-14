namespace Daoris.Knowledge;

/// <summary>What the entry's rule reads: a quest and a session by their ids, whatever holds them (SESSONE1a).</summary>
public interface ISessionEntryFacts
{
    /// <summary>The quest with this id, or null when none is held — a quest a clear forgot (HIST1b) is none.</summary>
    Task<Quest?> QuestAsync(string id, CancellationToken ct = default);

    /// <summary>The session record with this id, this machine's or a teammate's mirror; null when none is held.</summary>
    Task<Session?> SessionAsync(string id, CancellationToken ct = default);
}

/// <summary>The rule's facts as the stores hold them. A quest's <c>PublishedBy</c> names a session by the id its own machine knew it by, so a teammate's mirror, keyed <c>origin/id</c>, answers that id too.</summary>
public sealed class StoreEntryFacts(QuestStore quests, SessionStore sessions) : ISessionEntryFacts
{
    public Task<Quest?> QuestAsync(string id, CancellationToken ct = default) => quests.FindAsync(id, ct);

    public Task<Session?> SessionAsync(string id, CancellationToken ct = default) => sessions.FindOrMirrorAsync(id, ct);
}

/// <summary>
/// The session entry's key, <c>&lt;origin&gt;/&lt;repository&gt;</c> (SESSONE1a, D167 points 2 and 3, the co-working design §1.2): the line of
/// work a record belongs to, so the records that continue it read as one session. The rule lives here and nowhere else; the ledger
/// writes its answer when a session opens, every read answers it for a record that holds none, and the driver groups by what it is
/// answered (no twin of the rule).
/// </summary>
/// <remarks>
/// <para><b>The origin of a quest</b>, the first that applies: the ask that asked it (its sender is <c>ask #id</c>, a chain step's too);
/// else the origin of the session that published it, read from that session's record; else its chain parent's; else the quest itself,
/// <c>quest:&lt;id&gt;</c>. <b>Of a session</b>: an intake is the ask's, <c>ask:&lt;id&gt;/intake</c>; a helper or a reviewer is its own;
/// a session on a quest takes the quest's; a conversation handed over, or started from another's words, takes the one it continues; and
/// any other conversation is its own, <c>session:&lt;id&gt;</c>.</para>
///
/// <para><b>A fact that is gone</b> (a quest a clear forgot, a publisher's record never mirrored) passes to the next rule, and a record
/// left with none is its own entry. No ask is looked up: the quest's own sender names it, so a teammate's mirror of the quest answers as
/// this machine does, though asks are this machine's.</para>
/// </remarks>
public static class SessionEntries
{
    /// <summary>The key a record answers: the one stored, else the rule's, which nothing writes back.</summary>
    public static async Task<string> KeyAsync(Session session, ISessionEntryFacts facts, CancellationToken ct = default) =>
        session.Entry is { Length: > 0 } stored
            ? stored
            : Compose(await OriginAsync(session, facts, [], ct).ConfigureAwait(false), PartOf(session));

    /// <summary>The origin a quest's sessions are opened under — what the ledger passes <c>CreateAsync</c> for a session on it.</summary>
    public static Task<string> OriginOfQuestAsync(Quest quest, ISessionEntryFacts facts, CancellationToken ct = default) =>
        OriginOfQuestAsync(quest, facts, [], ct);

    /// <summary>The origin a session holds: that of its stored key, else the rule's. Where a conversation hands over, the one it continues.</summary>
    public static Task<string> OriginOfAsync(Session session, ISessionEntryFacts facts, CancellationToken ct = default) =>
        OriginAsync(session, facts, [], ct);

    /// <summary>A key from its two halves.</summary>
    public static string Compose(string origin, string part) => $"{origin}/{part}";

    /// <summary>The origin of an ask's intake and the part its key names: <c>ask:&lt;id&gt;/intake</c>.</summary>
    public static string AskOrigin(string askId) => $"ask:{askId}";

    /// <summary>Which half of a key is its repository: the intake's is the word, any other record's its repository.</summary>
    private static string PartOf(Session session) => session.Ask is not null ? "intake" : session.Repository;

    /// <summary>The origin a stored key holds — what comes before its first slash, where no origin has one. Null for a key that has none.</summary>
    private static string? OriginOf(string? key) => key is { Length: > 0 } && key.IndexOf('/') is > 0 and var slash ? key[..slash] : null;

    private static async Task<string> OriginAsync(Session session, ISessionEntryFacts facts, HashSet<string> seen, CancellationToken ct)
    {
        if (OriginOf(session.Entry) is { } stored) return stored;
        var own = $"session:{session.Id}";
        if (session.Ask is { } ask) return AskOrigin(ask);
        // A helper and a reviewer keep their own entry and are no part of the line they serve (design §1.2).
        if (session.Serves is not null || session.Opinion is not null) return own;
        if (!seen.Add($"s:{session.Id}")) return own;

        if (session.Quest is { } questId)
        {
            return await facts.QuestAsync(questId, ct).ConfigureAwait(false) is { } quest
                ? await OriginOfQuestAsync(quest, facts, seen, ct).ConfigureAwait(false)
                : own;
        }

        if (session.From is { } from && await facts.SessionAsync(from, ct).ConfigureAwait(false) is { } continued)
        {
            return await OriginAsync(continued, facts, seen, ct).ConfigureAwait(false);
        }

        return own;
    }

    private static async Task<string> OriginOfQuestAsync(Quest quest, ISessionEntryFacts facts, HashSet<string> seen, CancellationToken ct)
    {
        if (AskDesk.AskOf(quest.From) is { } ask) return AskOrigin(ask);
        // The quest is its own line where the rule below it finds none, and where it comes round to itself (a cycle of publishers).
        var itself = $"quest:{quest.Id}";
        if (!seen.Add($"q:{quest.Id}")) return itself;

        if (quest.PublishedBy is { } publisher
            && await facts.SessionAsync(publisher, ct).ConfigureAwait(false) is { } published
            && !seen.Contains($"s:{published.Id}"))
        {
            return await OriginAsync(published, facts, seen, ct).ConfigureAwait(false);
        }

        if (quest.Parent is { } parent && await facts.QuestAsync(parent, ct).ConfigureAwait(false) is { } above)
        {
            return await OriginOfQuestAsync(above, facts, seen, ct).ConfigureAwait(false);
        }

        return itself;
    }
}
