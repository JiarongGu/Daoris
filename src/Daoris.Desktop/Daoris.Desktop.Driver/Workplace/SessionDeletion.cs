namespace Daoris.Driver;

/// <summary>
/// Deleting a conversation that served no quest, the machine's half, which both doors call: the
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
/// since that proof is the clean-up's and *Discard tree*'s; a landing that names it, standing or a trace, keeps it.
/// Then the ledger deletes, judging the record again as it stands; and only after its yes are the files removed.</para>
///
/// <para><b>What goes</b> is what <see cref="SessionHomeFiles"/> takes, the helper a clear of finished history calls too:
/// its conversation, its transcript, its files, its conversation id, a leftover process marker, its go-on mark, its
/// choice of a new session, its spawn files, its held words, its closed automatic landing and its archive mark. <b>Never</b> a tree, a branch, its usage or the machine log, which gains
/// <c>session.deleted</c> with no word of it.</para>
///
/// <para><b>What the disk would not let go of stays, and is said</b>: by name in <see cref="DeleteOutcome.Stayed"/>,
/// and in the sentence, as a clear says what it left over. The record is gone all the same.</para>
/// </remarks>
/// <param name="remover">The disk, which a test hands one that refuses a path as a held file does.</param>
public sealed class SessionDeletion(string home, Action<string, bool>? remover = null)
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
        ServiceClient service, string id, string door, MachineLog? log, ISessionEventRemoval? events = null, CancellationToken ct = default)
    {
        var records = await service.SessionRecordsAsync(ct).ConfigureAwait(false);
        var record = records.FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.Ordinal));
        if (record is null) return new(id, DeleteVerdict.Unknown, $"No session here is `{id}`.");

        if (record.Teammate)
        {
            var machine = id[..id.IndexOf('/')];
            return new(id, DeleteVerdict.NotOurs, $"Session `{id}` ran on `{machine}`; its record is theirs — archive it here instead.")
            {
                Machine = machine,
            };
        }

        // A conversation is deleted whole: every part goes, each judged first, and one refused keeps the rest.
        var parts = Parts(record, records);
        var trees = new SessionTrees(home);
        foreach (var part in parts)
        {
            var judged = await service.JudgeSessionDeleteAsync(part.Id, ct).ConfigureAwait(false);
            if (!judged.Taken) return Refused(part.Id, judged) with { Message = Whole(judged.Message, part.Id, id, parts.Count) };
            if (MachineHalf(part, trees) is { } kept) return kept with { Message = Whole(kept.Message, part.Id, id, parts.Count) };
        }

        foreach (var part in parts)
        {
            var deleted = await service.DeleteSessionAsync(part.Id, ct).ConfigureAwait(false);
            if (!deleted.Taken) return Refused(part.Id, deleted) with { Message = Whole(deleted.Message, part.Id, id, parts.Count) };
        }

        // The one helper a clear also calls, so a delete takes every file a clear takes.
        var ids = parts.Select(part => part.Id).ToList();
        var files = new SessionHomeFiles(home, remover).Remove(ids, events, [ConversationArchive.Of(record)]);
        var stayed = ids.SelectMany(each => files.Stayed[each]).Distinct(StringComparer.Ordinal).ToList();

        log?.Info("session.deleted", ("session", id), ("kind", KindOf(record)), ("door", door), ("parts", parts.Count));
        return new(id, DeleteVerdict.Deleted, stayed.Count == 0
            ? parts.Count == 1
                ? $"Deleted session `{id}`: its record, and its words, transcript and files on this machine."
                : $"Deleted session `{id}` and the {parts.Count - 1} other part{(parts.Count == 2 ? "" : "s")} of its conversation: their records, and their words, transcripts and files on this machine."
            : LeftOver(id, stayed))
        {
            Removed = ["record", .. ids.SelectMany(each => files.Went[each]).Distinct(StringComparer.Ordinal)],
            Stayed = stayed,
        };
    }

    /// <summary>
    /// What a delete of <paramref name="record"/> takes: every part of its conversation held here, oldest first, so the person is told how
    /// many go before they press (<see cref="Parts(string, IReadOnlyList{SessionRecord})"/>).
    /// </summary>
    public static IReadOnlyList<SessionRecord> Parts(SessionRecord record, IReadOnlyList<SessionRecord> records)
    {
        var line = ConversationArchive.Of(record);
        var held = records.Where(each => !each.Teammate && ConversationArchive.Of(each) == line)
            .OrderBy(each => each.Created).ThenBy(each => each.Id, StringComparer.Ordinal).ToList();
        return held.Count > 0 && !record.Teammate ? held : [record];
    }

    /// <summary>The refusal of one part, said as the conversation's: which part, of how many, and that nothing was deleted.</summary>
    private static string Whole(string said, string part, string asked, int count) =>
        count == 1 ? said : $"Session `{asked}` is one of {count} parts of its conversation, deleted whole; part `{part}` is kept: {said} Nothing was deleted.";

    /// <summary>
    /// A delete whose files the disk did not all let go of: which stayed, by the words for each name, and that they are
    /// left over, as a clear says of its own; the next clear of a workspace is promised only where it takes every one of them.
    /// </summary>
    private static string LeftOver(string id, IReadOnlyList<string> stayed)
    {
        var said = stayed.Select(Said).ToList();
        var plural = said.Count > 1 || said[0].Plural;
        var list = said.Count == 1 ? said[0].Words : $"{string.Join(", ", said.SkipLast(1).Select(each => each.Words))} and {said[^1].Words}";
        var taken = stayed.All(SessionHomeFiles.TakenWhenLeftOver)
            ? $"; the next clear of a workspace takes {(plural ? "them" : "it")}"
            : "";
        return $"Deleted session `{id}`: its record, and what this machine kept of it but {list}, which the disk would not let go of "
               + $"and {(plural ? "are" : "is")} left over{taken}.";
    }

    /// <summary>A name of what the home keeps of a session, as a person reads it, and whether it reads as more than one.</summary>
    private static (string Words, bool Plural) Said(string name) => name switch
    {
        SessionHomeFiles.Conversation => ("its words", true),
        SessionHomeFiles.Transcript => ("its transcript", false),
        SessionHomeFiles.Files => ("its files", true),
        SessionHomeFiles.Binding => ("its agent's conversation id", false),
        SessionHomeFiles.Marker => ("its process marker", false),
        SessionHomeFiles.Mark => ("its go-on mark", false),
        SessionHomeFiles.Choice => ("its choice of a new session", false),
        SessionHomeFiles.Spawn => ("its spawn files", true),
        SessionHomeFiles.Help => ("what Ask Daoris kept of it", false),
        SessionHomeFiles.Held => ("its held words", true),
        SessionHomeFiles.Landing => ("its closed automatic landing", false),
        SessionHomeFiles.Facts => ("its name, pin and archive mark", true),
        // A name a later inventory adds is said as it is named.
        _ => ($"its {name}", false),
    };

    /// <summary>
    /// This machine's half: its own tree still here, or a landing that names it. Null when neither holds it. A tree
    /// in the repository's checkout is no tree of its own, and a tree a tidy left empty is gone.
    /// </summary>
    private static DeleteOutcome? MachineHalf(SessionRecord record, SessionTrees trees)
    {
        if (record.Tree is { } tree && Held(trees, tree) && !TreeFolders.TreeGone(tree))
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

    /// <summary>The kind its open is logged by: Ask Daoris and an intake by their door, else the record's.</summary>
    private static string KindOf(SessionRecord record) =>
        record.Repository == KnowledgeRoomIdentity.Repository ? SessionOpened.Help
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
