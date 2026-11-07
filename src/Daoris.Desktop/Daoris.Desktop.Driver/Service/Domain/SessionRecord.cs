using System.Globalization;
using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// A session record as the service answers it, closed ones included: what <see cref="SessionGroups"/> places, read
/// from <c>/api/sessions?includeClosed=true</c>. Never what a session printed.
/// </summary>
/// <param name="Id">Its id. A teammate's record comes down with the sync keyed <c>origin/id</c>.</param>
/// <param name="Repository">Where it ran: a repository's name, <c>ask #a</c> for an intake, <c>daoris:help</c> for Ask Daoris.</param>
/// <param name="State">The record's state in its public spelling (<c>awaiting-person</c>, <c>stood-down</c>).</param>
public sealed record SessionRecord(string Id, string Repository, string State)
{
    public string? Actor { get; init; }
    public string? Choice { get; init; }
    public ActorAsked? Asked { get; init; }
    public JsonElement? Override { get; init; }
    public ActorRan? Ran { get; init; }
    public ActorChosen? Chosen { get; init; }
    /// <summary>The quest it serves, or null for a conversation's or an intake's.</summary>
    public string? Quest { get; init; }

    /// <summary>The quest a chat took through its own connector, its <c>Quest</c> staying null; null for every other record.</summary>
    public string? TookQuest { get; init; }

    /// <summary><c>driven</c> or <c>chat</c>, as the record says.</summary>
    public string Kind { get; init; } = "driven";

    /// <summary>The adapter it ran on, as the record says; what marks a Codex child as seen by its calls.</summary>
    public string? Adapter { get; init; }

    /// <summary>The tree it held, or null: a teammate's record names none over loopback that is ours to look at.</summary>
    public string? Tree { get; init; }

    /// <summary>The ask an intake answers, or null.</summary>
    public string? Ask { get; init; }

    public DateTimeOffset Created { get; init; }

    /// <summary>When the record last moved: when a parked session began to wait, or when an ended one ended.</summary>
    public DateTimeOffset Updated { get; init; }

    /// <summary>
    /// The words waiting on the record, or null. The service answers them to this machine only, so a teammate's
    /// record carries none.
    /// </summary>
    public IReadOnlyList<SaidWordView>? Said { get; init; }

    /// <summary>
    /// A park the person answered (it stays parked with their word kept): the same session goes on at the driver's
    /// next look, so nothing about it waits on the person any more.
    /// </summary>
    public bool Answered => State == "awaiting-person" && SaidWordView.AnyPerson(Said);

    /// <summary>
    /// Whether the ledger would delete this record: the record's half, as the service answers it per
    /// record. Whether this machine still holds its tree or a landing of it is the driver's half (<see cref="SessionDeletion"/>).
    /// </summary>
    public bool Deletable { get; init; }

    /// <summary>
    /// The commit its tree stood at when the spawn began, or null: what an abandon judges a tree's commits from.
    /// The service answers it to this machine only, beside the tree.
    /// </summary>
    public string? BaseCommit { get; init; }

    /// <summary>
    /// Whether it took its own quest through its own connector: a quest this machine took with no record that
    /// says so is taken here outside Daoris.
    /// </summary>
    public bool Took { get; init; }

    /// <summary>The workspace its record is filed under, the default one where an older host answers none.</summary>
    public string Workspace { get; init; } = RemoteTarget.DefaultWorkspace;

    /// <summary>
    /// Its evidence bundle, as written at its end: the commits it landed and what is read of its done's
    /// evidence, the commit by its full id (<see cref="EvidenceCheck.DonesCommit"/>). Null where none is written.
    /// </summary>
    public string? Evidence { get; init; }

    /// <summary>
    /// The line of work the service answers for it: <c>&lt;origin&gt;/&lt;repository&gt;</c>, an opaque key the
    /// parts of one session share. Null from an older host.
    /// </summary>
    public string? Entry { get; init; }

    /// <summary>The session it continues (its <c>from</c>: a hand-over, or a conversation started from another's words), or null.</summary>
    public string? From { get; init; }

    /// <summary>
    /// Why it continues <see cref="From"/>, one of <see cref="HandOverWhy"/>: what the
    /// <i>Parts</i> strip says a part began by. Null with no link, and from an older host.
    /// </summary>
    public string? FromWhy { get; init; }

    /// <summary>The part its line ends at, as the service walks it; null from an older host or where the walk finds none.</summary>
    public string? Head { get; init; }

    /// <summary>
    /// The access mode an Ask Daoris record ran under, as the service answers it to this machine; null for
    /// every other record and from an older host, which <see cref="HelpModes.Recorded"/> reads as restricted.
    /// </summary>
    public string? HelpMode { get; init; }

    /// <summary>
    /// The session it serves (<c>serves</c>): a helper's or a partner's lead, as the service answers it to this machine;
    /// null for every other record, from an older host and for a teammate's record.
    /// </summary>
    public string? Serves { get; init; }

    /// <summary>The second opinion a reviewer's record reads for (where <i>At work</i> lists it by the line it serves); null for every other.</summary>
    public string? Opinion { get; init; }

    /// <summary>
    /// The piece a partner's record writes, the ask's id, kept only beside <see cref="Serves"/>;
    /// null for every other record. What the landing's eligibility refuses at every door.
    /// </summary>
    public string? Piece { get; init; }

    /// <summary>Whether it is a partner's record: one carrying a piece, which lands only through its lead.</summary>
    public bool Partner => Piece is not null;

    /// <summary>
    /// Whether it ended waiting on its own helper: completed by <see cref="EndNoteCodes.EndedAwaitsHelper"/>, read by its
    /// note's code, never its English. False for every other record and from an older host.
    /// </summary>
    public bool AwaitsHelper { get; init; }

    /// <summary>
    /// Whether an account's limit made its failure: the ledger's <c>limit</c> flag, which a row says as
    /// <see cref="ShownState.Limited"/>. False from an older host.
    /// </summary>
    public bool Limit { get; init; }

    /// <summary>
    /// The key it is grouped by: the service's, else the record its own line of work, as the service derives a record whose
    /// facts are gone (<c>session:&lt;id&gt;/&lt;repository&gt;</c>), so an older host still lists every record.
    /// </summary>
    public string EntryKey => Entry ?? $"session:{Id}/{Repository}";

    /// <summary>A record that comes down from the team: its process is on another machine, and nothing here reaches it.</summary>
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

