using System.Globalization;
using System.Text.Json;

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

/// <summary>The service's session records, closed ones included (SESSUX1a): asked of its door, and read.</summary>
public static class SessionRecords
{
    /// <summary>The door the records are read by: every record, closed ones included, as the page reads them.</summary>
    public const string Door = "/api/sessions?includeClosed=true";

    /// <summary>Every record the answer holds, in its order; one with no id is skipped.</summary>
    public static IReadOnlyList<SessionRecord> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var records = new List<SessionRecord>();
        if (document.RootElement.ValueKind != JsonValueKind.Array) return records;
        foreach (var session in document.RootElement.EnumerateArray())
        {
            if (session.ValueKind != JsonValueKind.Object || Text(session, "id") is not { Length: > 0 } id) continue;
            records.Add(new SessionRecord(id, Text(session, "repository") ?? "", Text(session, "state") ?? "")
            {
                Quest = Text(session, "quest") is { Length: > 0 } quest ? quest : null,
                Kind = Text(session, "kind") ?? "driven",
                Tree = Text(session, "tree") is { Length: > 0 } tree ? tree : null,
                Ask = Text(session, "ask"),
                Created = Time(session, "created"),
                Updated = Time(session, "updated"),
                // The service keeps a blank answer as its own words, so an empty one is none.
                Answer = Text(session, "answer") is { Length: > 0 } answer ? answer : null,
                // A host older than the field says nothing, and nothing is no delete (SESSUX1f).
                Deletable = session.TryGetProperty("deletable", out var deletable) && deletable.ValueKind == JsonValueKind.True,
                BaseCommit = Text(session, "baseCommit") is { Length: > 0 } baseCommit ? baseCommit : null,
                // Absent is no take of its own: a host older than STANDDOWN2, or a teammate's record (PAUSE1d).
                Took = session.TryGetProperty("took", out var took) && took.ValueKind == JsonValueKind.True,
            });
        }

        return records;
    }

    /// <summary>
    /// The records as the service answers them, over a client of this reader's own. A refusal is the driver's sentence
    /// (<see cref="DriverException"/>), as every read through <see cref="DriverHttp"/> says it.
    /// </summary>
    /// <remarks>
    /// <see cref="ServiceClient"/> reads these records for the strikes and each quest's last run and keeps them to itself;
    /// its file is another branch's while this one is built (TOOL4d), so the reader asks the same door with the same key
    /// rather than growing that class. Moving it onto the client later changes no answer.
    /// </remarks>
    /// <param name="handler">The test seam, as <see cref="DriverHttp.Client"/>'s; production passes none.</param>
    public static async Task<string> ReadAsync(
        string baseUrl, string? key, HttpMessageHandler? handler = null, CancellationToken ct = default)
    {
        using var http = DriverHttp.Client(key, handler);
        return await DriverHttp.GetAsync(http, baseUrl.TrimEnd('/') + Door, ct).ConfigureAwait(false);
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static DateTimeOffset Time(JsonElement element, string name) =>
        DateTimeOffset.TryParse(Text(element, name), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
            ? at
            : DateTimeOffset.MinValue;
}

/// <summary>
/// What one session's own tree holds that no branch of the person's does, by D88's proof: its unlanded commits and its
/// uncommitted paths. Null in either is git unable to say.
/// </summary>
public sealed record TreeWork(int? Commits, int? Uncommitted)
{
    /// <summary>
    /// Whether there is work nobody has accepted yet. Only a proven zero is none: a count git could not give is kept, as
    /// the clean-up keeps what it cannot clear (D88).
    /// </summary>
    public bool Holds => Commits != 0 || Uncommitted != 0;
}

/// <summary>
/// The groups a session is listed in by state (D126 §2.1), in the order the person acts on them, and Archived: the
/// public spelling the page and <c>daoris-driver sessions --group</c> share.
/// </summary>
public static class SessionGroup
{
    /// <summary>Waiting on you: only the person's press moves it.</summary>
    public const string You = "you";

    /// <summary>To review: an ended session whose own tree holds work no branch of the person's holds.</summary>
    public const string Review = "review";

    /// <summary>Working: queued, starting, working, and a live chat between turns.</summary>
    public const string Working = "working";

    /// <summary>Resumes later: it moves by itself when what it waits on arrives.</summary>
    public const string Later = "later";

    /// <summary>Ended: a record.</summary>
    public const string Ended = "ended";

    /// <summary>Archived: out of the way on this machine, kept whole (§5.2).</summary>
    public const string Archived = "archived";

    /// <summary>The groups in the order a list shows them.</summary>
    public static IReadOnlyList<string> Order { get; } = [You, Review, Working, Later, Ended, Archived];
}

/// <summary>
/// The words a row shows that are not a record state (D126 §2.2): each a fact about the session's quest, or for an
/// answered park about its record (ANSWER1c), derived here and never written back to the record, so there is no new
/// session state.
/// </summary>
public static class ShownState
{
    /// <summary>The last session here of a quest parked on its failed sessions (DRV6).</summary>
    public const string Parked = "parked";

    /// <summary>The last session here of a quest taken and waiting on a question asked of another repository (D79).</summary>
    public const string AwaitingReply = "awaiting-reply";

    /// <summary>
    /// A park the person answered, still parked until the driver's next look takes it up (ANSWER1c, D131): the one word
    /// derived from the record rather than its quest, since its state still says it waits on the person.
    /// </summary>
    public const string Answered = "answered";

    /// <summary>
    /// An ended record the person's words wait on, whose run the planner starts (MSG1f2, D137 §3.2): the same session goes
    /// on with them, so it is working, as an answered park is.
    /// </summary>
    public const string GoingOn = "going-on";
}

/// <summary>
/// What holds the person's words on an ended record that goes on with them by itself once it lifts (MSG1f2, D137 §3.2): the
/// planner's own verdict on its quest, since whatever holds a start holds a reopen (§2.2), by a code the page words, with
/// the planner's sentence beside it.
/// </summary>
/// <param name="Why">One of the codes below.</param>
/// <param name="Reason">The planner's sentence, or the spawn's hold's: the line where the page has none of its own.</param>
public sealed record WordsHold(string Why, string Reason)
{
    /// <summary>The work's pause (D132): <i>Resume</i> moves it, and the row's line already names whose.</summary>
    public const string Paused = "paused";

    /// <summary>The person holds its repository (D46 §3).</summary>
    public const string Hold = "hold";

    /// <summary>The concurrency cap is spent: a reopen goes first once a running session here ends.</summary>
    public const string Cap = "cap";

    /// <summary>Its account is cooling (TOOL4g; MSG1g's resume on its own account): the words go at its reset.</summary>
    public const string Cooling = "cooling";

    /// <summary>A session is active in the tree or the repository it goes back into (D51, PAR1).</summary>
    public const string Busy = "busy";

    /// <summary>Anything else the planner or the spawn holds it by, said in its own sentence.</summary>
    public const string Waits = "waits";

    /// <summary>The repository the person holds, for <see cref="Hold"/>.</summary>
    public string? Repository { get; init; }

    /// <summary>When the cool-off ends, for <see cref="Cooling"/>.</summary>
    public DateTimeOffset? Until { get; init; }

    /// <summary>Whose pause, for <see cref="Paused"/>.</summary>
    public PausedBy? PausedBy { get; init; }

    /// <summary>
    /// What holds the words, by the planner's verdict on the record's quest and the cool-off the look held its start on;
    /// null where the verdict starts it, which is going on.
    /// </summary>
    /// <param name="wait">The look's wait naming this quest (TOOL4g), or null.</param>
    public static WordsHold? Of(Consideration verdict, AccountWait? wait) => verdict.Verdict switch
    {
        StartVerdict.Start => null,
        StartVerdict.Paused => new(Paused, verdict.Reason) { PausedBy = verdict.PausedBy },
        StartVerdict.Held => new(Hold, verdict.Reason) { Repository = verdict.Quest.To },
        StartVerdict.AtCapacity => new(Cap, verdict.Reason),
        StartVerdict.Blocked when wait is not null => new(Cooling, verdict.Reason) { Until = wait.Until },
        StartVerdict.RepositoryBusy => new(Busy, verdict.Reason),
        _ => new(Waits, verdict.Reason),
    };

    /// <summary>
    /// What it says in the terminal's words, after <c>held: </c>: the same lines <c>sessions say</c> prints for words that wait
    /// (MSG1e), so the listing and the verb agree.
    /// </summary>
    public string Sentence => this switch
    {
        { Why: Paused, PausedBy: { } pause } => $"it goes on with this once you resume its work: {pause.Door}",
        { Why: Hold, Repository: { } repository } =>
            $"it goes on with this once {repository} is no longer held: daoris driver resume {repository}",
        { Why: Cap } => "it goes on with this when a running session here ends.",
        _ => $"it waits: {Reason}",
    };
}

/// <summary>Where one session is listed, and what its row's second line says (D126 §2.2, §2.4).</summary>
/// <param name="Group">One of <see cref="SessionGroup"/>.</param>
/// <param name="Shown">The record's state, or one of <see cref="ShownState"/>.</param>
public sealed record SessionGrouping(string Session, string Group, string Shown)
{
    /// <summary>Whether this machine's archive mark stands. A session that needs the person is shown in its group whatever the mark says.</summary>
    public bool Archived { get; init; }

    /// <summary>A teammate's record (SYNC4): read here, and acted on from its own machine.</summary>
    public bool Teammate { get; init; }

    /// <summary>
    /// For a parked session: how many sessions failed since the quest's last *Try again*, as the planner counted to park it.
    /// For a live one of this machine's (UX7c): how many failed before it, the same count, null where none did.
    /// </summary>
    public int? Strikes { get; init; }

    /// <summary>For a session awaiting reply: the quest its quest waits on.</summary>
    public string? Awaits { get; init; }

    /// <summary>And the repository that quest was asked of, where the service still lists it.</summary>
    public string? AwaitsOf { get; init; }

    /// <summary>For a session to review: what its own tree holds.</summary>
    public TreeWork? Work { get; init; }

    /// <summary>
    /// Whether this session's stop holds its quest here (SESSUX1b, D126 §2.2): the person stopped it, it is its quest's
    /// last session here, and nothing starts that quest on this machine until they choose *Try again*.
    /// </summary>
    public bool HoldsQuest { get; init; }

    /// <summary>
    /// Whose pause holds this session's quest (PAUSE1b, D132 §6.1), where it is its quest's last session here: its line says
    /// *paused with ask `#a`; Resume carries it on*, and *Resume* stands where *Try again* would. Null otherwise.
    /// </summary>
    public PausedBy? PausedBy { get; init; }

    /// <summary>
    /// For an ended record the person's words wait on that resumes later (MSG1f2, D137 §3.2): what holds them, which its line
    /// says. Null otherwise, and for one that goes on (<see cref="ShownState.GoingOn"/>).
    /// </summary>
    public WordsHold? Holds { get; init; }

    /// <summary>
    /// Whether *Delete…* would be taken (SESSUX1f, D126 §5.4): the ledger would delete its record, and this machine holds
    /// neither its tree nor a landing of it. The page offers the act only here, D95's way.
    /// </summary>
    public bool Deletable { get; init; }
}

/// <summary>
/// A quest the planner parked on its failed sessions here (DRV6, <see cref="StartVerdict.Exhausted"/>), with what its park
/// is said by (SESSUX1i, D126 §4.6, §4.7): Overview's *What needs you* row and the park's notice.
/// </summary>
/// <param name="Quest">Its id.</param>
/// <param name="Repository">The repository it is addressed to.</param>
public sealed record QuestPark(string Quest, string Repository)
{
    /// <summary>Its last session here, which failed or was cut off; null where the records show no session of this machine's on it.</summary>
    public string? Session { get; init; }

    /// <summary>How many sessions failed since its last *Try again*, as the planner counted to park it; null where the records no longer say.</summary>
    public int? Strikes { get; init; }

    /// <summary>What its last session's record said about its end: the last failure's note. Null is a state, not a gap.</summary>
    public string? Note { get; init; }

    /// <summary>That note's lines by code (LANG1a), beside it; null where the note is, or the record is from before parts.</summary>
    public IReadOnlyList<NotePart>? NoteParts { get; init; }

    /// <summary>When its last session ended: what the park has waited since. Null where that record is not listed.</summary>
    public DateTimeOffset? Since { get; init; }
}

/// <summary>
/// Everything one look at the sessions reads (D126 §2.4): the records, the quests and the planner's verdicts, each quest's
/// last session here and its strikes as the planner's own readers derive them, the trees' judgement and the marks.
/// </summary>
public sealed record SessionLook(
    IReadOnlyList<SessionRecord> Records, IReadOnlyList<QuestView> Quests, IReadOnlyList<Consideration> Considered)
{
    /// <summary>The session this machine last ran on each quest (D79): <see cref="ServiceClient.ReadLastRun"/>, the planner's own reading.</summary>
    public IReadOnlyDictionary<string, PriorSession> LastRun { get; init; } =
        new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Each quest's failed sessions since its last *Try again* (DRV6, RETRY1), counted as the planner counts them.</summary>
    public IReadOnlyDictionary<string, int> Strikes { get; init; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>What each judged tree holds, by <see cref="SessionGroups.Normal"/>; a tree not here was not judged, and holds nothing to review.</summary>
    public IReadOnlyDictionary<string, TreeWork> Trees { get; init; } = new Dictionary<string, TreeWork>(StringComparer.OrdinalIgnoreCase);

    /// <summary>This machine's archive marks (§5.2), by session.</summary>
    public IReadOnlyDictionary<string, DateTimeOffset> Archived { get; init; } = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);

    /// <summary>
    /// The sessions whose tree or landing this machine still holds (SESSUX1f, <see cref="SessionDeletion.Kept"/>): what
    /// keeps a record the ledger would delete from being deleted here.
    /// </summary>
    public IReadOnlySet<string> Kept { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// The words each record could not go on with (MSG1b's marks, <see cref="GoOnMarks"/>), by session: a record whose every
    /// waiting word is marked waits on nothing, as the planner leaves it (MSG1f2).
    /// </summary>
    public IReadOnlyDictionary<string, GoOnMark> Unable { get; init; } = new Dictionary<string, GoOnMark>(StringComparer.Ordinal);

    /// <summary>
    /// The starts the loop's last look held on a cooling account (TOOL4g, D125 §4): what a record whose words such a start
    /// holds is said with, its reset (MSG1f2). None where no loop has looked, since a fresh plan cannot see a cool-off.
    /// </summary>
    public IReadOnlyList<AccountWait> Waits { get; init; } = [];

    /// <summary>
    /// A look from the records as the service answered them: parsed, and each quest's last run and strikes read by the
    /// planner's own readers, never a second copy of either rule.
    /// </summary>
    /// <param name="forgivenAt">RETRY1's mark for a quest (<see cref="DriverConfig.ForgivenAt"/>): the planner counts strikes from it.</param>
    public static SessionLook From(
        string recordsJson, IReadOnlyList<QuestView> quests, IReadOnlyList<Consideration> considered, Func<string, int> forgivenAt) =>
        new(SessionRecords.Parse(recordsJson), quests, considered)
        {
            LastRun = ServiceClient.ReadLastRun(recordsJson),
            Strikes = ServiceClient.ReadStrikes(recordsJson)
                .ToDictionary(pair => pair.Key, pair => pair.Value - forgivenAt(pair.Key), StringComparer.OrdinalIgnoreCase),
        };
}

/// <summary>
/// The one reader of a session's group (SESSUX1a, D126 §2.4): which of the five groups each session is in, or Archived,
/// the word its row shows, and what its second line says. <c>SESSION_GROUPS</c> hands it to the page and the terminal's
/// <c>sessions</c> prints it, so the two cannot disagree.
/// </summary>
/// <remarks>
/// <para><b>Pure.</b> <see cref="Read"/> decides from a <see cref="SessionLook"/> and touches nothing; what it reads is
/// gathered by the door that asks (<see cref="JudgeAsync"/>, <see cref="VerdictsAsync"/>), so the table of cases holds
/// the rule without a process.</para>
///
/// <para><b>A session is in the first group it qualifies for</b>: Waiting on you, To review, Working, Resumes later,
/// Ended. Parked and awaiting reply are the planner's verdicts on the session's quest (<see cref="StartVerdict.Exhausted"/>,
/// <see cref="StartVerdict.Waiting"/>), read for the quest's last session here only; nothing here counts strikes or
/// decides a wait a second way.</para>
///
/// <para><b>To review is the tree's work, once nothing will go back into the tree.</b> Only the newest session on a tree
/// stands for it; a tree a live session holds is in use, as D88's proof keeps a tree in use; and a tree whose quest is
/// still taken and considered by the planner is the one its carry-on or resume goes back into (D79, D80), so its asker
/// rests in Resumes later or Ended. A quest the person's stop holds goes back into nothing until they release it
/// (<see cref="StartVerdict.Stopped"/>, SESSUX1b), so the stop's work is to review. Read literally, the order would put every awaiting reply with a commit in To review
/// and offer a review of a tree a carry-on is writing.</para>
///
/// <para><b>A teammate's record is grouped by its state only.</b> Its park waits on them, its tree is on their machine,
/// and the planner's verdicts and the strikes are this machine's (D47 §6, as the park notification already reads it).</para>
///
/// <para><b>Archive never hides what needs the person</b>: a mark moves a session to Archived from Resumes later or
/// Ended only; one waiting on you, to review or still live is shown in its group with its mark said.</para>
/// </remarks>
public static class SessionGroups
{
    /// <summary>Every session's place, in the order a list shows them; with <paramref name="only"/>, those sessions' alone.</summary>
    /// <param name="only">The sessions asked about. The rest still decide theirs: a tree is one session's to review.</param>
    public static IReadOnlyList<SessionGrouping> Read(SessionLook look, IReadOnlyCollection<string>? only = null)
    {
        var facts = new Facts(look);
        var placed = look.Records
            .Where(record => only is null || only.Contains(record.Id, StringComparer.Ordinal))
            .Select(record => (Record: record, Row: facts.Place(record)))
            .ToList();

        return [.. SessionGroup.Order.SelectMany(group => Ordered(group, placed.Where(each => each.Row.Group == group))).Select(each => each.Row)];
    }

    /// <summary>
    /// The quests the planner parked on their failed sessions, each with its last session here, its number, that session's
    /// note and when it ended (SESSUX1i, D126 §4.6, §4.7), in the planner's order.
    /// </summary>
    /// <remarks>
    /// Read from the facts a *parked* row is placed by (the planner's verdict, the last run, the strikes), so Overview's row,
    /// the park's notice and the list's row name one session and count one number. A person's stop is no park: it holds
    /// its quest by <see cref="StartVerdict.Stopped"/>, which the person caused (§4.7).
    /// </remarks>
    public static IReadOnlyList<QuestPark> Parks(SessionLook look)
    {
        var records = look.Records
            .GroupBy(record => record.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        return [.. look.Considered
            .Where(consideration => consideration.Verdict == StartVerdict.Exhausted)
            .DistinctBy(consideration => consideration.Quest.Id, StringComparer.OrdinalIgnoreCase)
            .Select(consideration =>
            {
                var quest = consideration.Quest.Id;
                var last = look.LastRun.TryGetValue(quest, out var run) ? run : null;
                var ended = last is not null && records.TryGetValue(last.Session, out var record) && record.Updated != DateTimeOffset.MinValue
                    ? record.Updated
                    : (DateTimeOffset?)null;
                return new QuestPark(quest, consideration.Quest.To)
                {
                    Session = last?.Session,
                    Strikes = look.Strikes.TryGetValue(quest, out var strikes) ? strikes : null,
                    Note = string.IsNullOrWhiteSpace(last?.Note) ? null : last.Note.Trim(),
                    NoteParts = string.IsNullOrWhiteSpace(last?.Note) ? null : last.NoteParts,
                    Since = ended,
                };
            })];
    }

    /// <summary>
    /// The trees <see cref="Read"/> would put to review if they held work: an ended session's own tree, this home's, the
    /// newest session on it, held by nothing live and gone back into by no quest. Only these are worth a git walk.
    /// </summary>
    /// <param name="held">Whether a path is a tree this home opened (<see cref="SessionTrees.Holds"/>): the only kind looked at.</param>
    public static IReadOnlyList<string> TreesToJudge(
        SessionLook look, Func<string, bool> held, IReadOnlyCollection<string>? only = null)
    {
        var facts = new Facts(look);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var trees = new List<string>();
        foreach (var record in look.Records)
        {
            if (only is not null && !only.Contains(record.Id, StringComparer.Ordinal)) continue;
            if (facts.ReviewableTree(record) is not { } tree || !seen.Add(Normal(tree)) || !Held(held, tree)) continue;
            trees.Add(tree);
        }

        return trees;
    }

    /// <summary>The look with each tree <see cref="TreesToJudge"/> names judged once (D88's proof): a tree the judge cannot speak for holds nothing to review.</summary>
    /// <param name="judge"><see cref="SessionTrees.WorkAsync"/>, the git walk; a test hands in its own.</param>
    public static async Task<SessionLook> JudgeAsync(
        SessionLook look, Func<string, bool> held, Func<string, CancellationToken, Task<TreeWork?>> judge,
        IReadOnlyCollection<string>? only = null, CancellationToken ct = default)
    {
        var judged = new Dictionary<string, TreeWork>(look.Trees, StringComparer.OrdinalIgnoreCase);
        foreach (var tree in TreesToJudge(look, held, only))
        {
            if (await judge(tree, ct).ConfigureAwait(false) is { } work) judged[Normal(tree)] = work;
        }

        return look with { Trees = judged };
    }

    /// <summary>
    /// The planner's verdict on each quest, never a second copy of its rules: the loop's last look where a loop has
    /// looked, and the planner over a fresh snapshot where none has (a terminal, or a desktop waiting on another driver's
    /// lock, DRV8a).
    /// </summary>
    /// <param name="door">The configured adapter's wire, which decides whether a repository that never adopted can be driven (D70).</param>
    public static async Task<IReadOnlyList<Consideration>> VerdictsAsync(
        ServiceClient service, DriverConfig config, SessionWire door, IReadOnlyList<Consideration>? lastLook, CancellationToken ct = default) =>
        // A fresh plan reads the pauses as the loop's look does (PAUSE1b), so the list and a tick say one verdict.
        lastLook ?? Planner.Plan(
            await PausedWork.LookAsync(service, config, await service.SnapshotAsync(ct).ConfigureAwait(false), ct).ConfigureAwait(false),
            config, door);

    /// <summary>
    /// One look at the sessions, gathered for the reader (D126 §2.4): the records, every quest, the planner's verdicts, the
    /// trees judged, the archive marks and what this machine still holds of each (SESSUX1f). The screen's
    /// <c>SESSION_GROUPS</c> and the terminal's <c>sessions</c> both gather here, so they cannot disagree.
    /// </summary>
    /// <param name="lastLook">The loop's last look, where a loop has looked; null plans over a fresh snapshot.</param>
    public static async Task<SessionLook> LookAsync(
        ServiceClient service, DriverConfig config, SessionWire door, IReadOnlyList<Consideration>? lastLook, string home,
        IReadOnlyCollection<string>? only = null, CancellationToken ct = default)
    {
        var records = await service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
        var quests = await service.EveryQuestAsync(ct).ConfigureAwait(false);
        var considered = await VerdictsAsync(service, config, door, lastLook, ct).ConfigureAwait(false);
        var trees = new SessionTrees(home);
        var look = SessionLook.From(records, quests, considered, config.ForgivenAt) with
        {
            Archived = new SessionArchive(home).Marks(),
        };
        // What each quest's last session here could not go on with (MSG1f2), read as the planner's snapshot reads it.
        look = look with { Unable = new GoOnMarks(home).For(look.LastRun.Values) };
        look = look with { Kept = new SessionDeletion(home).Kept(look.Records) };
        return await JudgeAsync(look, trees.Holds, trees.WorkAsync, only, ct).ConfigureAwait(false);
    }

    /// <summary>A tree path as one tree: separators and a trailing one aside, compared without case, as Windows sees it.</summary>
    public static string Normal(string tree) => tree.Replace('\\', '/').TrimEnd('/');

    private static bool Held(Func<string, bool> held, string tree)
    {
        try
        {
            return held(tree);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // A path no folder could have is no tree of this home's.
            return false;
        }
    }

    /// <summary>
    /// A group's rows in its order (D126 §2.1): waiting and resuming oldest first, the longest wait first; to review and
    /// working by repository then start, so rows do not reshuffle as their states move; ended and archived newest first.
    /// </summary>
    private static IEnumerable<(SessionRecord Record, SessionGrouping Row)> Ordered(
        string group, IEnumerable<(SessionRecord Record, SessionGrouping Row)> rows) => group switch
        {
            SessionGroup.You or SessionGroup.Later => rows
                .OrderBy(each => each.Record.Updated).ThenBy(each => each.Record.Id, StringComparer.Ordinal),
            SessionGroup.Review or SessionGroup.Working => rows
                .OrderBy(each => each.Record.Repository, StringComparer.OrdinalIgnoreCase)
                .ThenBy(each => each.Record.Created).ThenBy(each => each.Record.Id, StringComparer.Ordinal),
            _ => rows.OrderByDescending(each => each.Record.Updated).ThenBy(each => each.Record.Id, StringComparer.Ordinal),
        };

    /// <summary>What every session's place is read against, gathered once per look.</summary>
    private sealed class Facts
    {
        private readonly SessionLook _look;
        private readonly Dictionary<string, Consideration> _verdicts;
        private readonly Dictionary<string, QuestView> _quests;
        private readonly HashSet<string> _liveTrees;
        private readonly Dictionary<string, string> _newestOnTree;

        public Facts(SessionLook look)
        {
            _look = look;
            _verdicts = look.Considered
                .GroupBy(consideration => consideration.Quest.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            _quests = look.Quests
                .GroupBy(quest => quest.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var ours = look.Records.Where(record => !record.Teammate && record.Tree is not null).ToList();
            _liveTrees = new HashSet<string>(
                ours.Where(record => record.Live).Select(record => Normal(record.Tree!)), StringComparer.OrdinalIgnoreCase);
            _newestOnTree = ours
                .GroupBy(record => Normal(record.Tree!), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderByDescending(record => record.Created).ThenByDescending(record => record.Id, StringComparer.Ordinal).First().Id,
                    StringComparer.OrdinalIgnoreCase);
        }

        public SessionGrouping Place(SessionRecord record)
        {
            var archived = _look.Archived.ContainsKey(record.Id);
            var row = new SessionGrouping(record.Id, SessionGroup.Ended, record.State)
            {
                Archived = archived,
                Teammate = record.Teammate,
                // SESSUX1f (D126 §5.4): the ledger's half, as the service answered it, and this machine's.
                Deletable = record.Deletable && !record.Live && !record.Teammate && !_look.Kept.Contains(record.Id),
            };

            if (record.Live)
            {
                // UX7c (D152, the UX7 design §5.2): a live session of a quest whose earlier sessions here failed is another try,
                // and its head says how many failed before it, counted as the planner counts them, from the last *Try again*.
                if (!record.Teammate && record.Quest is { } quest && _look.Strikes.TryGetValue(quest, out var failed) && failed > 0)
                {
                    row = row with { Strikes = failed };
                }

                // ANSWER1c (D131): an answered park goes on at the driver's next look, so it is working, never waiting on you.
                if (record.Answered) return row with { Group = SessionGroup.Working, Shown = ShownState.Answered };

                // A teammate's park waits on them: nothing this window sends reaches its process (D47 §6).
                return row with { Group = record.State == "awaiting-person" && !record.Teammate ? SessionGroup.You : SessionGroup.Working };
            }

            if (record.Teammate) return Rest(row);

            var verdict = VerdictOnLast(record);
            // SESSUX1b (D126 §2.2): its line says the stop holds its quest, in whichever group it rests; and PAUSE1b (D132
            // §6.1), that a pause does, since a pause is the reason before a stop.
            row = row with { HoldsQuest = verdict?.Verdict == StartVerdict.Stopped, PausedBy = verdict?.PausedBy };

            // MSG1f2 (D137 §3.2): the person's words wait on it, so it goes on with them, working, where the planner starts
            // it; else it resumes later, its line naming what holds them, the planner's own verdict. Before parked and to
            // review: words written to it are the person's Try again, and its tree is what it goes on in.
            if (verdict is not null && WordsWait(record))
            {
                if (WordsHold.Of(verdict, WaitOn(verdict.Quest.Id)) is not { } holds)
                {
                    return row with { Group = SessionGroup.Working, Shown = ShownState.GoingOn };
                }

                return Rest(row with { Group = SessionGroup.Later, Holds = holds });
            }

            if (verdict?.Verdict == StartVerdict.Exhausted)
            {
                return row with
                {
                    Group = SessionGroup.You,
                    Shown = ShownState.Parked,
                    Strikes = _look.Strikes.TryGetValue(record.Quest!, out var strikes) ? strikes : null,
                };
            }

            if (ReviewableTree(record) is { } tree && _look.Trees.TryGetValue(Normal(tree), out var work) && work.Holds)
            {
                return row with { Group = SessionGroup.Review, Work = work };
            }

            if (verdict is { Verdict: StartVerdict.Waiting, Quest.Awaits: { Length: > 0 } awaits })
            {
                return Rest(row with
                {
                    Group = SessionGroup.Later,
                    Shown = ShownState.AwaitingReply,
                    Awaits = awaits,
                    AwaitsOf = _quests.TryGetValue(awaits, out var question) ? question.To : null,
                });
            }

            return Rest(row);

            SessionGrouping Rest(SessionGrouping resting) => archived ? resting with { Group = SessionGroup.Archived } : resting;
        }

        /// <summary>
        /// The tree an ended session of this machine's would be reviewed in, were there work in it: its own, the newest
        /// session on it, held by nothing live, its quest not parked and not going back into it. Null for any other.
        /// </summary>
        public string? ReviewableTree(SessionRecord record)
        {
            if (record.Live || record.Teammate || record.Tree is not { } tree) return null;
            var key = Normal(tree);
            if (!_newestOnTree.TryGetValue(key, out var newest) || newest != record.Id || _liveTrees.Contains(key)) return null;

            // Parked comes first, and a quest the planner still considers while it is taken goes back into this
            // session's tree: its carry-on (D80) or its resume (D79), whatever holds that start for now. Bar the person's
            // stop (SESSUX1b): nothing goes back into its tree until they release it, so its work is theirs to review. And bar
            // a pause (PAUSE1b, D132 §6.1): nothing goes back in until Resume, and the person paused the work to look at it.
            return VerdictOnLast(record) is { } verdict
                   && verdict.Verdict is not (StartVerdict.Stopped or StartVerdict.Paused)
                   && (verdict.Verdict == StartVerdict.Exhausted || verdict.Quest.Status == "Taken")
                ? null
                : tree;
        }

        /// <summary>
        /// Whether the person's words wait on this record for it to go on with (MSG1b, D137 §2.2): its quest's last session
        /// here, with words kept on it, and not every one a word it was already found unable to go on with (its marks).
        /// </summary>
        private bool WordsWait(SessionRecord record) =>
            record.Quest is { } quest
            && _look.LastRun.TryGetValue(quest, out var last)
            && string.Equals(last.Session, record.Id, StringComparison.Ordinal)
            && last.WordsWaiting
            && !GoOnMarks.Judged(last, _look.Unable.GetValueOrDefault(record.Id));

        /// <summary>The cool-off the look held this quest's start on (TOOL4g), or null.</summary>
        private AccountWait? WaitOn(string quest) =>
            _look.Waits.FirstOrDefault(wait => wait.Quests.Contains(quest, StringComparer.OrdinalIgnoreCase));

        /// <summary>The planner's verdict on this session's quest, where this session is that quest's last here; null otherwise.</summary>
        private Consideration? VerdictOnLast(SessionRecord record) =>
            record.Quest is { } quest
            && _look.LastRun.TryGetValue(quest, out var last)
            && string.Equals(last.Session, record.Id, StringComparison.Ordinal)
            && _verdicts.TryGetValue(quest, out var verdict)
                ? verdict
                : null;
    }
}

/// <summary>
/// The considerations the loop's last look made, whole (SESSUX1a): the planner's verdicts <see cref="SessionGroups"/>
/// reads, so the list says what the loop decided rather than a second plan of its own.
/// </summary>
/// <remarks>Kept as <see cref="ParkedQuests"/> is, and replaced whole each look, so nothing older than the last look is read.</remarks>
public sealed class LastLook
{
    private volatile Looked? _looked;

    /// <summary>One look's verdicts and the cool-offs it held starts on, replaced together.</summary>
    private sealed record Looked(IReadOnlyList<Consideration> Considered, IReadOnlyList<AccountWait> Waits);

    /// <summary>What the last look considered, in its order; null before any look, which is when a fresh plan is read instead.</summary>
    public IReadOnlyList<Consideration>? Latest => _looked?.Considered;

    /// <summary>
    /// The starts the last look held on a cooling account (TOOL4g): a record whose words such a start holds says its reset
    /// (MSG1f2). None before any look.
    /// </summary>
    public IReadOnlyList<AccountWait> Waits => _looked?.Waits ?? [];

    /// <param name="waits">The look's waits (<see cref="TickReport.Waits"/>); none where it held nothing on an account.</param>
    public void Record(IEnumerable<Consideration> considered, IEnumerable<AccountWait>? waits = null) =>
        _looked = new Looked([.. considered], [.. waits ?? []]);
}
