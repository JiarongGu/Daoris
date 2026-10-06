namespace Daoris.Driver;

/// <summary>What a delete did with the session asked of it (SESSUX1f, D126 §5.4).</summary>
public enum DeleteVerdict
{
    /// <summary>Its record and what this machine kept of it are gone.</summary>
    Deleted,

    /// <summary>No record here is this session.</summary>
    Unknown,

    /// <summary>A teammate's record (SYNC4): it ran on their machine, and the record is theirs.</summary>
    NotOurs,

    /// <summary>It still runs or waits: stopped first, it ends.</summary>
    Live,

    /// <summary>It served a quest, and is that work's record: archive is how it is cleared.</summary>
    ServedQuest,

    /// <summary>An ask names it as its intake, a quest was published by it, or a landing names it (<see cref="DeleteOutcome.NamedBy"/>).</summary>
    Named,

    /// <summary>Its own tree is still on this machine: discarded or cleaned up first.</summary>
    TreeHere,

    /// <summary>A workspace's remote holds it, and a session record does not travel as a deletion.</summary>
    OnRemote,
}

/// <summary>One delete's outcome, with the facts its refusal names.</summary>
/// <param name="Message">The sentence a terminal prints: the ledger's verbatim for the record's half, the driver's for this machine's.</param>
public sealed record DeleteOutcome(string Session, DeleteVerdict Verdict, string Message)
{
    /// <summary>The quest it served, or the one it published.</summary>
    public string? Quest { get; init; }

    /// <summary>The ask that names it as its intake.</summary>
    public string? Ask { get; init; }

    /// <summary>The machine a teammate's record ran on.</summary>
    public string? Machine { get; init; }

    /// <summary>The workspace whose remote holds it.</summary>
    public string? Workspace { get; init; }

    /// <summary>For <see cref="DeleteVerdict.Named"/>: <see cref="SessionDeletion.ByAsk"/>, <see cref="SessionDeletion.ByQuest"/> or <see cref="SessionDeletion.ByLanding"/>.</summary>
    public string? NamedBy { get; init; }

    /// <summary>
    /// What went, by name: <c>record</c>, then what <see cref="SessionHomeFiles"/> took (<c>conversation</c>, <c>transcript</c>,
    /// <c>files</c>, <c>harness</c>, <c>marker</c>, <c>mark</c>, <c>choice</c>, <c>spawn</c>, <c>held</c>, <c>landing</c>,
    /// <c>archived</c>), in that order.
    /// </summary>
    public IReadOnlyList<string> Removed { get; init; } = [];
}

/// <summary>
/// Deleting a conversation that served no quest (SESSUX1f, D126 §5.4), the machine's half, which both doors call: the
/// screen's <c>SESSION_DELETE</c> and the terminal's <c>sessions delete</c>.
/// </summary>
/// <remarks>
/// <para><b>The record's half is the ledger's, and it is said first.</b> The service judges whose the record is, whether it
/// still runs, whether it served a quest, what names it and whether a remote holds it; its refusal is said in its words with
/// its word, before this machine's half, so a driven session with a tree is told it served a quest rather than to discard a
/// tree it would then still not be deleted for. A teammate's record is refused from its id, which carries the machine and
/// which the ledger's doors cannot address.</para>
///
/// <para><b>This machine's half: its tree, and a landing.</b> A tree of its own still here is discarded or cleaned up first,
/// since D88's proof is the clean-up's and *Discard tree*'s; a landing that names it, standing or a trace (D113), keeps it.
/// Then the ledger deletes, judging the record again as it stands; and only after its yes are the files removed.</para>
///
/// <para><b>What goes</b> is what <see cref="SessionHomeFiles"/> takes, the helper a clear of finished history calls too
/// (HIST1c): its conversation, its transcript, its files, its conversation id, a leftover process marker, its go-on mark, its
/// choice of a new session, its spawn files, its held words, its closed automatic landing and its archive mark (§5.1; the
/// history-clearing design §2.2). <b>Never</b> a tree, a branch, its usage or the machine log, which gains
/// <c>session.deleted</c> with no word of it (§7.4).</para>
/// </remarks>
public sealed class SessionDeletion(string home)
{
    public const string ByAsk = "ask";
    public const string ByQuest = "quest";
    public const string ByLanding = "landing";

    /// <summary>
    /// The sessions among <paramref name="records"/> the ledger would delete whose tree or landing this machine still holds:
    /// what keeps <c>deletable</c> off their rows. Only those the ledger would delete are looked at.
    /// </summary>
    public IReadOnlySet<string> Kept(IEnumerable<SessionRecord> records)
    {
        var trees = new SessionTrees(home);
        return records
            .Where(record => record.Deletable && MachineHalf(record, trees) is not null)
            .Select(record => record.Id)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Delete one session, judged by both halves in order, from <paramref name="door"/> (<see cref="PluginEvents.Screen"/> or <see cref="PluginEvents.Terminal"/>).</summary>
    /// <param name="log">Where <c>session.deleted</c> is written; null writes nothing.</param>
    /// <param name="events">The loop's record of conversations, so it forgets the session too; null removes the file alone.</param>
    public async Task<DeleteOutcome> DeleteAsync(
        ServiceClient service, string id, string door, MachineLog? log, SessionEvents? events = null, CancellationToken ct = default)
    {
        var record = (await service.SessionRecordsAsync(ct).ConfigureAwait(false))
            .FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.Ordinal));
        if (record is null) return new(id, DeleteVerdict.Unknown, $"No session here is `{id}`.");

        if (record.Teammate)
        {
            var machine = id[..id.IndexOf('/')];
            return new(id, DeleteVerdict.NotOurs, $"Session `{id}` ran on `{machine}`; its record is theirs — archive it here instead.")
            {
                Machine = machine,
            };
        }

        var judged = await service.JudgeSessionDeleteAsync(id, ct).ConfigureAwait(false);
        if (!judged.Taken) return Refused(id, judged);

        if (MachineHalf(record, new SessionTrees(home)) is { } kept) return kept;

        var deleted = await service.DeleteSessionAsync(id, ct).ConfigureAwait(false);
        if (!deleted.Taken) return Refused(id, deleted);

        // HIST1c: the one helper a clear also calls, so a delete takes every file §2.2 lists, the four it once left included.
        var removed = new List<string> { "record" };
        removed.AddRange(new SessionHomeFiles(home).Remove([id], events)[id]);

        log?.Info("session.deleted", ("session", id), ("kind", KindOf(record)), ("door", door));
        return new(id, DeleteVerdict.Deleted, $"Deleted session `{id}`: its record, and its words, transcript and files on this machine.")
        {
            Removed = removed,
        };
    }

    /// <summary>
    /// This machine's half (§5.4): its own tree still here, or a landing that names it. Null when neither holds it. A tree
    /// in the repository's checkout is no tree of its own, and a tree a tidy left empty is gone.
    /// </summary>
    private static DeleteOutcome? MachineHalf(SessionRecord record, SessionTrees trees)
    {
        if (record.Tree is { } tree && Held(trees, tree) && !SessionTrees.TreeGone(tree))
        {
            return new(record.Id, DeleteVerdict.TreeHere,
                $"Session `{record.Id}`'s tree is still on this machine; discard the tree or clean it up first.");
        }

        return trees.Recorded.Landing(record.Id) is not null
            ? new(record.Id, DeleteVerdict.Named, $"A landing names session `{record.Id}`, so its record stays with that landing.")
            {
                NamedBy = ByLanding,
            }
            : null;
    }

    /// <summary>The ledger's refusal, by its word, never its sentence; the sentence is kept for a terminal to print.</summary>
    private static DeleteOutcome Refused(string id, SessionDeleteAnswer answer) => new(id, answer.Refusal switch
    {
        "not-found" => DeleteVerdict.Unknown,
        "not-ours" => DeleteVerdict.NotOurs,
        "live" => DeleteVerdict.Live,
        "served-quest" => DeleteVerdict.ServedQuest,
        "named" => DeleteVerdict.Named,
        "on-remote" => DeleteVerdict.OnRemote,
        // A word this build does not know is a newer ledger's: it said no, and its sentence says why.
        _ => DeleteVerdict.Live,
    }, answer.Message)
    {
        Quest = answer.Quest,
        Ask = answer.Ask,
        Machine = answer.Origin,
        Workspace = answer.Workspace,
        NamedBy = answer.Refusal == "named" ? answer.Ask is not null ? ByAsk : ByQuest : null,
    };

    /// <summary>The kind its open is logged by (LOG1b): Ask Daoris and an intake by their door, else the record's.</summary>
    private static string KindOf(SessionRecord record) =>
        record.Repository == HelpRoom.Repository ? SessionOpened.Help
        : record.Ask is not null ? SessionOpened.Intake
        : record.Kind;

    private static bool Held(SessionTrees trees, string tree)
    {
        try
        {
            return trees.Holds(tree);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // A path no folder could have is no tree of this home's.
            return false;
        }
    }
}
