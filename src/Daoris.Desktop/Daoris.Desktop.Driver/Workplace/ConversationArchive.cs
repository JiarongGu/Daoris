namespace Daoris.Driver;

/// <summary>
/// The archive, judged and kept per conversation: archiving a part archives its conversation, which is archived or not as a whole, its
/// mark one fact of <see cref="ConversationFacts"/> by the conversation's id (a record's <c>line</c>, else its own id). Both doors,
/// the screen's route and the terminal's verb, call it.
/// </summary>
/// <remarks>
/// <para><b>This machine's, never the record's.</b> The record is the service's and travels; what one person keeps in their list is not
/// a fact about the work, so the record is unchanged and a teammate's may be archived too.</para>
///
/// <para><b>Archive never hides what needs the person.</b> A conversation is judged by where <see cref="SessionGroups"/> places each of
/// its parts as the archive is asked: one with a part live, one with a part waiting on you or with work to review is refused whole, and
/// the reader shows a mark that later comes to need the person in that group all the same.</para>
/// </remarks>
public sealed class ConversationArchive(string home)
{
    private readonly ConversationFacts _facts = new(home);

    /// <summary>The conversation a record is a part of.</summary>
    public static string Of(SessionRecord record) => record.Line ?? record.Id;

    /// <summary>The conversation a record is a part of.</summary>
    public static string Of(PriorSession record) => record.Line ?? record.Session;

    /// <summary>The ids of every part held here of the conversations <paramref name="ids"/> are parts of, those asked included: what a judgement must read.</summary>
    public static IReadOnlyList<string> Parts(IReadOnlyCollection<string> ids, IReadOnlyList<SessionRecord> records)
    {
        var lines = Lines(ids, records);
        return [.. ids.Concat(records.Where(record => !record.Teammate && lines.Contains(Of(record))).Select(record => record.Id)).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>Every conversation archived, by its id, and when.</summary>
    public IReadOnlyDictionary<string, DateTimeOffset> Marks() => _facts.Archives();

    public IReadOnlyDictionary<string, HelpKept> Titled() => _facts.Titled();

    /// <summary>
    /// Archive the conversations these sessions are parts of, each judged by its parts' places in <paramref name="groups"/>: a finished one
    /// is marked, keeping its first archived time; one with a part working, pending or needing you, or no record here, is refused; nothing
    /// archived writes nothing.
    /// </summary>
    /// <param name="groups">Where the reader placed the parts of the sessions asked about (<see cref="Parts"/>), as the archive is asked.</param>
    public ArchiveAnswer Archive(
        IReadOnlyCollection<string> ids, IReadOnlyList<IWorkplaceSessionRow> groups, IReadOnlyList<SessionRecord> records, DateTimeOffset now)
    {
        var placed = groups.GroupBy(group => group.Session, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var held = records.ToDictionary(record => record.Id, StringComparer.Ordinal);
        var outcomes = ids.Distinct(StringComparer.Ordinal).Select(id => Judge(id, held, records, placed)).ToList();
        var archived = outcomes.Where(outcome => outcome.Verdict == ArchiveVerdict.Archived).Select(outcome => Of(held[outcome.Session])).Distinct(StringComparer.Ordinal).ToList();
        if (archived.Count > 0) _facts.Archive(archived, now);
        return new(outcomes, Listed());
    }

    /// <summary>
    /// Take the marks of the conversations these sessions are parts of away, so each is back in the group its state puts it in. One
    /// not archived is named in the answer, never refused. Nothing is written when no mark went.
    /// </summary>
    /// <param name="records">Every record here, to find each session's conversation; a session no record names is taken as the conversation itself.</param>
    public UnarchiveAnswer Unarchive(IReadOnlyCollection<string> ids, IReadOnlyList<SessionRecord> records)
    {
        var held = records.ToDictionary(record => record.Id, StringComparer.Ordinal);
        var lineOf = ids.Distinct(StringComparer.Ordinal).ToDictionary(id => id, id => held.TryGetValue(id, out var record) ? Of(record) : id, StringComparer.Ordinal);
        var notArchived = _facts.Unarchive(lineOf.Values);
        return new(
            [.. lineOf.Where(pair => notArchived.Contains(pair.Value, StringComparer.Ordinal)).Select(pair => pair.Key)],
            Listed());
    }

    /// <summary>Take the mark of the conversation <paramref name="record"/> is a part of away, where it stands: the person wrote to it, so it stays in view.</summary>
    public void Show(SessionRecord record) => _facts.Unarchive([Of(record)]);

    /// <summary>Take the mark of the conversation <paramref name="record"/> is a part of away, where it stands.</summary>
    public void Show(PriorSession record) => _facts.Unarchive([Of(record)]);

    /// <summary>
    /// The machine log's <c>sessions.archived</c>: how many an archive took, and from which door,
    /// <see cref="PluginEvents.Screen"/> or <see cref="PluginEvents.Terminal"/>. An archive changes no work, so it is counted,
    /// never listed; one that took nothing writes nothing.
    /// </summary>
    public static void Said(MachineLog? log, int count, string door)
    {
        if (count > 0) log?.Info("sessions.archived", ("count", count), ("door", door));
    }

    /// <summary>
    /// What <c>sessions archive &lt;id&gt;…</c> says of each outcome, and its exit: 0 where every one is archived, 1 where one is
    /// kept. The alone path prints it, and <c>SESSION_ARCHIVE</c> answers it as <c>said</c> to a
    /// terminal that sent its archive over the pipe, so the two cannot drift.
    /// </summary>
    public static TreesSaid ArchiveSaid(IReadOnlyList<ArchiveOutcome> outcomes) => new(
        [
            .. outcomes.Select(outcome => outcome.Verdict switch
            {
                ArchiveVerdict.Archived => $"sessions: archived {outcome.Session}.",
                ArchiveVerdict.Live => $"sessions: kept {outcome.Session}: it is still running; stop it first.",
                ArchiveVerdict.NeedsYou => $"sessions: kept {outcome.Session}: it needs you; archive never hides what needs you.",
                _ => $"sessions: kept {outcome.Session}: no session here is {outcome.Session}.",
            }),
        ],
        [],
        outcomes.All(outcome => outcome.Verdict == ArchiveVerdict.Archived) ? 0 : 1);

    /// <summary>What <c>sessions unarchive &lt;id&gt;…</c> says of each id asked, in the order asked: one not archived is said, never refused.</summary>
    public static TreesSaid UnarchiveSaid(IEnumerable<string> ids, IReadOnlyCollection<string> notArchived) => new(
        [
            .. ids.Select(id => notArchived.Contains(id, StringComparer.Ordinal)
                ? $"sessions: {id} was not archived, so nothing changed."
                : $"sessions: unarchived {id}; it is back in the group its state puts it in."),
        ],
        [],
        0);

    private static HashSet<string> Lines(IReadOnlyCollection<string> ids, IReadOnlyList<SessionRecord> records)
    {
        var held = records.ToDictionary(record => record.Id, StringComparer.Ordinal);
        return [.. ids.Where(held.ContainsKey).Select(id => Of(held[id]))];
    }

    private static ArchiveOutcome Judge(
        string id, IReadOnlyDictionary<string, SessionRecord> held, IReadOnlyList<SessionRecord> records, IReadOnlyDictionary<string, IWorkplaceSessionRow> placed)
    {
        if (!held.TryGetValue(id, out var asked) || !placed.ContainsKey(id)) return new(id, ArchiveVerdict.Unknown);
        var line = Of(asked);
        var parts = asked.Teammate
            ? [asked]
            : records.Where(record => !record.Teammate && Of(record) == line).ToList();
        var rows = parts.Select(part => placed.GetValueOrDefault(part.Id)).OfType<IWorkplaceSessionRow>().ToList();
        if (rows.Any(row => row.Group is SessionGroup.Working or SessionGroup.Pending)) return new(id, ArchiveVerdict.Live);
        return rows.FirstOrDefault(row => row.Group == SessionGroup.NeedsYou) is { } needs
            ? new(id, ArchiveVerdict.NeedsYou) { Group = needs.Group }
            : new(id, ArchiveVerdict.Archived);
    }

    private IReadOnlyList<ArchiveMark> Listed() =>
        [.. _facts.Archives().Select(mark => new ArchiveMark(mark.Key, mark.Value)).OrderBy(mark => mark.At).ThenBy(mark => mark.Session, StringComparer.Ordinal)];
}
