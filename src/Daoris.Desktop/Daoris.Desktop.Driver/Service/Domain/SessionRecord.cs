using System.Globalization;
using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// A session record as the service answers it (D46), closed ones included: what <see cref="SessionGroups"/> places, read
/// from <c>/api/sessions?includeClosed=true</c>. Never what a session printed.
/// </summary>
/// <param name="Id">Its id. A teammate's record came down with the sync keyed <c>origin/id</c> (SYNC4).</param>
/// <param name="Repository">Where it ran: a repository's name, <c>ask #a</c> for an intake, <c>daoris:help</c> for Ask Daoris.</param>
/// <param name="State">The record's state in its public spelling (<c>awaiting-person</c>, <c>stood-down</c>).</param>
public sealed record SessionRecord(string Id, string Repository, string State)
{
    public string? Actor { get; init; }
    public string? Choice { get; init; }
    public ActorAsked? Asked { get; init; }
    public JsonElement? Override { get; init; }
    public ActorRan? Ran { get; init; }
    /// <summary>The quest it serves, or null for a conversation's or an intake's.</summary>
    public string? Quest { get; init; }

    /// <summary><c>driven</c> or <c>chat</c>, as the record says.</summary>
    public string Kind { get; init; } = "driven";

    /// <summary>The tree it held (D51), or null: a teammate's record names none over loopback that is ours to look at.</summary>
    public string? Tree { get; init; }

    /// <summary>The ask an intake answers (D65 §1b), or null.</summary>
    public string? Ask { get; init; }

    public DateTimeOffset Created { get; init; }

    /// <summary>When the record last moved: when a parked session began to wait, or when an ended one ended.</summary>
    public DateTimeOffset Updated { get; init; }

    /// <summary>
    /// The person's answer to a park (STANDDOWN2), or null. The service answers it to this machine only, so a teammate's
    /// record carries none.
    /// </summary>
    public string? Answer { get; init; }

    /// <summary>
    /// A park the person answered (ANSWER1b keeps it parked with the answer set): the same session goes on at the driver's
    /// next look (D131), so nothing about it waits on the person any more.
    /// </summary>
    public bool Answered => State == "awaiting-person" && Answer is not null;

    /// <summary>
    /// Whether the ledger would delete this record (SESSUX1f, D126 §5.4): the record's half, as the service answers it per
    /// record. Whether this machine still holds its tree or a landing of it is the driver's half (<see cref="SessionDeletion"/>).
    /// </summary>
    public bool Deletable { get; init; }

    /// <summary>
    /// The commit its tree stood at when the spawn began (SURF6), or null: what an abandon judges a tree's commits from
    /// (PAUSE1d, D132 §3.3). The service answers it to this machine only, beside the tree.
    /// </summary>
    public string? BaseCommit { get; init; }

    /// <summary>
    /// Whether it took its own quest through its own connector (STANDDOWN2): a quest this machine took with no record that
    /// says so was taken here outside Daoris (PAUSE1d, D132 §3.2).
    /// </summary>
    public bool Took { get; init; }

    /// <summary>The workspace its record is filed under (D48), the default one where a host from before answers none (HIST1c's reading).</summary>
    public string Workspace { get; init; } = RemoteTarget.DefaultWorkspace;

    /// <summary>
    /// Its evidence bundle (D46 §4), as written at its end: the commits it landed and, since EVID1b, what was read of its done's
    /// evidence, the commit by its full id (<see cref="EvidenceCheck.DonesCommit"/>). Null where none was written.
    /// </summary>
    public string? Evidence { get; init; }

    /// <summary>
    /// The line of work the service answers for it (SESSONE1a, D167): <c>&lt;origin&gt;/&lt;repository&gt;</c>, an opaque key the
    /// parts of one session share. Null from a host before the key.
    /// </summary>
    public string? Entry { get; init; }

    /// <summary>The session it continues (D163's <c>from</c>: a hand-over, or a conversation started from another's words), or null.</summary>
    public string? From { get; init; }

    /// <summary>
    /// Why it continues <see cref="From"/>, one of <see cref="HandOverWhy"/> (SESSCTX1b, the session-context design §7): what the
    /// <i>Parts</i> strip says a part began by. Null with no link, and from a host before the field.
    /// </summary>
    public string? FromWhy { get; init; }

    /// <summary>
    /// The access mode an Ask Daoris record ran under (HELPMODE1a2, D183 point 3), as the service answers it to this machine; null for
    /// every other record and from a host before the field, which <see cref="HelpModes.Recorded"/> reads as restricted.
    /// </summary>
    public string? HelpMode { get; init; }

    /// <summary>
    /// The session it serves (MIXAGENT2a-i's <c>serves</c>): a helper's or a partner's lead, as the service answers it to this machine;
    /// null for every other record, from a host before the field and for a teammate's record.
    /// </summary>
    public string? Serves { get; init; }

    /// <summary>
    /// The piece a partner's record writes, the ask's id (COAGENT1b, the co-agent design §7.3), kept only beside <see cref="Serves"/>;
    /// null for every other record. What the landing's eligibility refuses at every door (§2.4).
    /// </summary>
    public string? Piece { get; init; }

    /// <summary>Whether it is a partner's record: one carrying a piece, which lands only through its lead (D199 point 3).</summary>
    public bool Partner => Piece is not null;

    /// <summary>
    /// Whether it ended waiting on its own helper (HELPERWAIT1): completed by <see cref="NoteCodes.EndedAwaitsHelper"/>, read by its
    /// note's code, never its English. False for every other record and from a host before parts.
    /// </summary>
    public bool AwaitsHelper { get; init; }

    /// <summary>
    /// Whether an account's limit made its failure (LIMITRESUME1d, D206 §3.1): the ledger's <c>limit</c> flag, which a row says as
    /// <see cref="ShownState.Limited"/>. False from a host before the field.
    /// </summary>
    public bool Limit { get; init; }

    /// <summary>
    /// The key it is grouped by: the service's, else the record its own line of work, as the service derives a record whose
    /// facts are gone (<c>session:&lt;id&gt;/&lt;repository&gt;</c>, SESSONE1b), so a host from before still lists every record.
    /// </summary>
    public string EntryKey => Entry ?? $"session:{Id}/{Repository}";

    /// <summary>A record that came down from the team (SYNC4): its process is on another machine, and nothing here reaches it.</summary>
    public bool Teammate => Id.Contains('/');

    /// <summary>
    /// Whether it still runs or waits: anything that is not one of the five endings. A state this build does not know is
    /// counted live, so nothing archives a session a newer host says is still going.
    /// </summary>
    public bool Live => !Endings.Contains(State);

    private static readonly HashSet<string> Endings = new(StringComparer.Ordinal)
    {
        "completed", "declined", "failed", "stopped", "stood-down",
    };
}

