using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A session record as a trace reads it (TRACE1, D143): every field the service answers this machine about where a change
/// came from, read from <c>/api/sessions?includeClosed=true</c>. <see cref="SessionRecord"/> keeps what the list groups by;
/// this keeps what ran the work, which no other reader needs.
/// </summary>
/// <param name="Id">Its id. A teammate's record came down with the sync keyed <c>origin/id</c> (SYNC4).</param>
/// <param name="Repository">Where it ran: a repository's name, <c>ask #a</c> for an intake, <c>daoris:help</c> for Ask Daoris.</param>
public sealed record TracedSession(string Id, string Repository, string State)
{
    public string? Quest { get; init; }

    /// <summary><c>driven</c> or <c>chat</c>, as the record says.</summary>
    public string Kind { get; init; } = "driven";

    public string? Adapter { get; init; }

    /// <summary>The tool's version the record opened on (D49 §4), or null where it names none.</summary>
    public string? HarnessVersion { get; init; }

    /// <summary>The account it ran as, or null: the tool's own sign-in on this machine's record, unsaid on a teammate's.</summary>
    public string? Profile { get; init; }

    public string? Tree { get; init; }

    /// <summary>The commit its tree stood at when the spawn began (SURF6).</summary>
    public string? BaseCommit { get; init; }

    /// <summary>The ask an intake answers (D65 §1b), or null.</summary>
    public string? Ask { get; init; }

    public string? Note { get; init; }

    /// <summary>The commits the driver read off its tree at its end (D46 §4), as written: <c>commits landed:</c> and a line each.</summary>
    public string? Evidence { get; init; }

    /// <summary>The person's answer to its park (STANDDOWN2), or null.</summary>
    public string? Answer { get; init; }

    /// <summary>When it opened, or null where the record's moment does not read.</summary>
    public DateTimeOffset? Created { get; init; }

    public DateTimeOffset? Updated { get; init; }

    /// <summary>Whether it took its quest through its own connector (STANDDOWN2).</summary>
    public bool Took { get; init; }

    /// <summary>A stop that was not the person's (D104).</summary>
    public bool Interrupted { get; init; }

    /// <summary>A failure an account's limit made (TOOL4c).</summary>
    public bool Limit { get; init; }

    /// <summary>A record that came down from the team (SYNC4): it ran on another machine, which keeps what is machine-local.</summary>
    public bool Teammate => Id.Contains('/');

    /// <summary>
    /// The commits its evidence names, each its abbreviation and its whole line, in the order written: the lines after the
    /// heading <see cref="WorkingTree.CommitsSinceAsync"/> writes, each beginning with git's abbreviation. None where it says
    /// none landed, could not be read, or there is no evidence.
    /// </summary>
    public IReadOnlyList<(string Sha, string Line)> Commits =>
        Evidence is not { } evidence
            ? []
            : [.. evidence.ReplaceLineEndings("\n").Split('\n').Skip(1)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .Select(line => (Sha: line.Split(' ', 2)[0].ToLowerInvariant(), Line: line))
                .Where(commit => Trace.IsCommit(commit.Sha))];
}

/// <summary>How a quest's done answered one requirement (DRIFT1d, D133 §4), as the service answers it.</summary>
public sealed record TracedAnswer(int Requirement, string? Met, string? Departed, string? Quote);

/// <summary>
/// A quest as a trace reads it (TRACE1): the state its operations replay to, as <c>/api/quests?includeClosed=true</c>
/// answers it, with what <see cref="QuestView"/> leaves out because no planner needs it: its answers, its yes, its moments.
/// </summary>
public sealed record TracedQuest(string Id, string From, string To, string Title, string Status)
{
    public IReadOnlyList<string> Lanes { get; init; } = [];

    public string? Note { get; init; }

    public DateTimeOffset? Filed { get; init; }

    public DateTimeOffset? Updated { get; init; }

    /// <summary>The quest whose close published this one (D65 §4), or null.</summary>
    public string? Parent { get; init; }

    public IReadOnlyList<QuestStepView> Then { get; init; } = [];

    public IReadOnlyList<QuestRequirementView> Requirements { get; init; } = [];

    public IReadOnlyList<TracedAnswer> Answers { get; init; } = [];

    public bool Held { get; init; }

    /// <summary>When the person said yes to a departure (DRIFT1d), or null.</summary>
    public DateTimeOffset? Accepted { get; init; }

    /// <summary>The session whose connector published it (SESS1), or null.</summary>
    public string? PublishedBy { get; init; }

    /// <summary>The quest its taker waits on (D79), or null.</summary>
    public string? Awaits { get; init; }

    /// <summary>Whom it asks as a person reads it (the service's <c>QuestAddress.Spell</c>).</summary>
    public string Address => Lanes.Count == 0 ? To : $"{To}:{string.Join('+', Lanes)}";
}

/// <summary>
/// What a trace read from each store (TRACE1, D143), each with why it could not be where it could not: the service's
/// session records and quests, and this machine's landings. An ask is read when the chain names one, and each session's
/// events and rules file when the chain reaches it.
/// </summary>
internal sealed record TraceFacts(
    IReadOnlyList<TracedSession> Sessions, string? SessionsUnread,
    IReadOnlyList<TracedQuest> Quests, string? QuestsUnread,
    IReadOnlyList<LandedBranch> Landings, string? LandingsUnread);

/// <summary>What an id was found to name, before the chain is read: the sessions and quests it starts from, and how each was found.</summary>
/// <param name="Kind">The kind it names, one of <see cref="TraceEntry"/>.</param>
/// <param name="Found">How it was found, a line each, for a commit: which evidence and which landing named it.</param>
/// <param name="Sessions">The sessions the chain is read through in full.</param>
/// <param name="Quests">The quests the chain is read through, by id, in order.</param>
/// <param name="Unrecorded">Sessions a landing names that no record on the service does.</param>
internal sealed record TraceFound(
    string Kind, string Id, IReadOnlyList<string> Found, IReadOnlyList<TracedSession> Sessions, IReadOnlyList<string> Quests,
    IReadOnlyList<string> Unrecorded);

/// <summary>
/// One read from a commit, a session or a quest back to its ask (TRACE1, D143): the reads and the resolution. Nothing here
/// writes, anywhere: every request to the service is a <c>GET</c>, and every file is opened to read.
/// </summary>
/// <remarks>
/// <para><b>Each link from the store that keeps it.</b> The person's words and go-aheads from the ask (D133, D135); the
/// quest, its requirements and answers from the quest as its operations replay; each session's agent, account, harness, tree
/// and base commit from its record; its start's notes and its instruction from its events (D76); its rules from the file it
/// was handed while that file exists; a branch landing from <c>landings.json</c> (WSR5) and the person's acceptance from its
/// events (D100); the standing answer from <c>driver.json</c> (D135 §3). Nothing is derived from what is current where the
/// store kept no moment.</para>
///
/// <para><b>What it cannot reach</b>, said where it would have been: the quest's operations themselves (a local host answers the
/// state they replay to, and no door here reads one quest's); a merge into the line's own commit, which the acceptance does not
/// name; an ended session's rules, whose file goes with its run; and a teammate's machine-local half.</para>
/// </remarks>
public static class Trace
{
    /// <summary>The fewest hexadecimal digits a commit is named by: git's own floor for an abbreviation.</summary>
    public const int CommitDigits = 4;

    /// <summary>Whether a word could name a commit: at least <see cref="CommitDigits"/> hexadecimal digits and no more than a whole id.</summary>
    public static bool IsCommit(string word) =>
        word.Length is >= CommitDigits and <= 64 && word.All(Uri.IsHexDigit);

    /// <summary>Whether two names of commits name the same one: either is the start of the other, the shorter long enough to name one.</summary>
    internal static bool SameCommit(string? a, string? b)
    {
        if (a is null || b is null) return false;
        var (shorter, longer) = a.Length <= b.Length ? (a, b) : (b, a);
        return shorter.Length >= CommitDigits && longer.StartsWith(shorter, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Each store's facts, every read independent, so one that does not answer costs its links and not the others.</summary>
    internal static async Task<TraceFacts> ReadAsync(TraceSources sources, CancellationToken ct)
    {
        IReadOnlyList<TracedSession> sessions = [];
        string? sessionsUnread = null;
        try
        {
            sessions = ParseSessions(await sources.Service.SessionRecordsJsonAsync(ct).ConfigureAwait(false));
        }
        catch (Exception error) when (Unanswered(error, ct))
        {
            sessionsUnread = error.Message;
        }

        IReadOnlyList<TracedQuest> quests = [];
        string? questsUnread = null;
        try
        {
            quests = ParseQuests(await sources.Service.QuestsJsonAsync(ct).ConfigureAwait(false));
        }
        catch (Exception error) when (Unanswered(error, ct))
        {
            questsUnread = error.Message;
        }

        var landed = new LandedBranches(sources.Home);        return new TraceFacts(sessions, sessionsUnread, quests, questsUnread, landed.Entries(), LandingsProblem(landed.FilePath));
    }

    /// <summary>
    /// What an id names in what was read: a session by its record's id, a quest by its id, a commit by the evidence and the
    /// landings that name it. Several kinds named, or none, is a sentence instead.
    /// </summary>
    internal static (TraceFound? Found, string? Problem) Resolve(TraceAsk asked, TraceFacts facts)
    {
        var id = asked.Id.Trim().TrimStart('#');
        bool Asks(string kind) => asked.Kind is null || asked.Kind == kind;

        var session = Asks(TraceEntry.Session)
            ? facts.Sessions.FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.OrdinalIgnoreCase))
            : null;
        var quest = Asks(TraceEntry.Quest)
            ? facts.Quests.FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.OrdinalIgnoreCase))
            : null;
        var commit = id.ToLowerInvariant();
        var evidence = Asks(TraceEntry.Commit) && IsCommit(commit)
            ? facts.Sessions.SelectMany(each => each.Commits.Where(made => SameCommit(made.Sha, commit)).Select(made => (Session: each, made.Line))).ToList()
            : [];
        var landings = Asks(TraceEntry.Commit) && IsCommit(commit)
            ? facts.Landings.Where(each => SameCommit(each.Tip, commit) || SameCommit(each.PushedTip, commit)).ToList()
            : [];

        var named = new List<string>();
        if (session is not null) named.Add($"session {session.Id}");
        if (quest is not null) named.Add($"quest #{quest.Id}");
        if (evidence.Count > 0 || landings.Count > 0) named.Add($"commit {commit}");

        if (named.Count == 0)
        {
            var what = asked.Kind switch
            {
                TraceEntry.Session => "no session record",
                TraceEntry.Quest => "no quest",
                TraceEntry.Commit => "no session's evidence and no landing",
                _ => "no session record, no quest, no session's evidence and no landing",
            };
            return (null, $"nothing here names `{id}`: {what} names it.");
        }

        if (named.Count > 1)
        {
            var forms = named.Select(each => $"`daoris-driver trace {each.Split(' ')[0]} {id}`");
            return (null, $"`{id}` names more than one thing here: {string.Join(" and ", named)}. Name the one to read: "
                + $"{string.Join(", or ", forms)}.");
        }

        if (session is not null)
        {
            return (new TraceFound(TraceEntry.Session, session.Id, [], [session], session.Quest is { } on ? [on] : [], []), null);
        }

        if (quest is not null)
        {
            var on = facts.Sessions
                .Where(each => string.Equals(each.Quest, quest.Id, StringComparison.OrdinalIgnoreCase))
                .OrderBy(each => each.Created ?? DateTimeOffset.MaxValue)
                .ToList();
            return (new TraceFound(TraceEntry.Quest, quest.Id, [], on, [quest.Id], []), null);
        }

        var found = new List<string>();
        var reached = new List<TracedSession>();
        foreach (var (made, line) in evidence)
        {
            found.Add($"found in session {made.Id}'s evidence: {line}");
            if (!reached.Contains(made)) reached.Add(made);
        }

        var unrecorded = new List<string>();
        foreach (var landing in landings)
        {
            var at = SameCommit(landing.Tip, commit) ? "the tip of" : "the commit a plugin pushed of";
            found.Add($"found as {at} branch {landing.Branch}, which session {landing.Session}'s landing made");
            var made = facts.Sessions.FirstOrDefault(each => string.Equals(each.Id, landing.Session, StringComparison.OrdinalIgnoreCase));
            if (made is null)
            {
                if (!unrecorded.Contains(landing.Session, StringComparer.OrdinalIgnoreCase)) unrecorded.Add(landing.Session);
            }
            else if (!reached.Contains(made))
            {
                reached.Add(made);
            }
        }

        reached = [.. reached.OrderBy(each => each.Created ?? DateTimeOffset.MaxValue)];
        IReadOnlyList<string> onQuests =
        [
            .. reached.Select(each => each.Quest)
                .Concat(landings.Where(each => unrecorded.Contains(each.Session, StringComparer.OrdinalIgnoreCase)).Select(each => each.Quest))
                .OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];
        return (new TraceFound(TraceEntry.Commit, commit, found, reached, onQuests, unrecorded), null);
    }

    /// <summary>Every session record the answer holds, in its order; one with no id is passed over, as the list's reader passes it.</summary>
    internal static IReadOnlyList<TracedSession> ParseSessions(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array) return [];
        var sessions = new List<TracedSession>();
        foreach (var session in document.RootElement.EnumerateArray())
        {
            if (session.ValueKind != JsonValueKind.Object || Text(session, "id") is not { Length: > 0 } id) continue;
            sessions.Add(new TracedSession(id, Text(session, "repository") ?? "", Text(session, "state") ?? "")
            {
                Quest = Filled(session, "quest"),
                Kind = Text(session, "kind") ?? "driven",
                Adapter = Filled(session, "adapter"),
                HarnessVersion = Filled(session, "harnessVersion"),
                Profile = Filled(session, "profile"),
                Tree = Filled(session, "tree"),
                BaseCommit = Filled(session, "baseCommit"),
                Ask = Filled(session, "ask"),
                Note = Filled(session, "note"),
                Evidence = Filled(session, "evidence"),
                Answer = Filled(session, "answer"),
                Created = Moment(session, "created"),
                Updated = Moment(session, "updated"),
                Took = Flag(session, "took"),
                Interrupted = Flag(session, "interrupted"),
                Limit = Flag(session, "limit"),
            });
        }

        return sessions;
    }

    /// <summary>Every quest the answer holds, in its order; one with no id is passed over.</summary>
    internal static IReadOnlyList<TracedQuest> ParseQuests(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array) return [];
        var quests = new List<TracedQuest>();
        foreach (var quest in document.RootElement.EnumerateArray())
        {
            if (quest.ValueKind != JsonValueKind.Object || Text(quest, "id") is not { Length: > 0 } id) continue;
            quests.Add(new TracedQuest(id, Text(quest, "from") ?? "", Text(quest, "to") ?? "", Text(quest, "title") ?? "", Text(quest, "status") ?? "")
            {
                Lanes = Strings(quest, "lanes"),
                Note = Filled(quest, "note"),
                Filed = Moment(quest, "filed"),
                Updated = Moment(quest, "updated"),
                Parent = Filled(quest, "parent"),
                Then = [.. Objects(quest, "then").Select(step => new QuestStepView(Text(step, "to") ?? "", Text(step, "title") ?? "", Text(step, "body") ?? ""))],
                Requirements =
                [
                    .. Objects(quest, "requirements")
                        .Where(requirement => Text(requirement, "quote") is not null)
                        .Select(requirement => new QuestRequirementView(Text(requirement, "quote")!, Text(requirement, "check") ?? "")),
                ],
                Answers =
                [
                    .. Objects(quest, "answers")
                        .Select(answer => (Answer: answer, Number: answer.TryGetProperty("requirement", out var number)
                            && number.ValueKind == JsonValueKind.Number && number.TryGetInt32(out var n) ? n : (int?)null))
                        .Where(each => each.Number is not null)
                        .Select(each => new TracedAnswer(
                            each.Number!.Value, Filled(each.Answer, "met"), Filled(each.Answer, "departed"), Filled(each.Answer, "quote"))),
                ],
                Held = Flag(quest, "held"),
                Accepted = Moment(quest, "accepted"),
                PublishedBy = Filled(quest, "publishedBy"),
                Awaits = Filled(quest, "awaits"),
            });
        }

        return quests;
    }

    /// <summary>Why <c>landings.json</c> does not read, or null where it reads or is not there: a file that does not read is no record, and is said.</summary>
    private static string? LandingsProblem(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var _ = JsonDocument.Parse(File.ReadAllText(path));
            return null;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return error.Message;
        }
    }

    /// <summary>A read the service did not answer: unreachable, refused, unparsable, or a client's own time-out, never the person's cancel.</summary>
    private static bool Unanswered(Exception error, CancellationToken ct) =>
        error is DriverException or HttpRequestException or JsonException or InvalidOperationException
        || (error is OperationCanceledException && !ct.IsCancellationRequested);

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Text with something in it, or null: an empty string on the wire says as little as none.</summary>
    private static string? Filled(JsonElement element, string name) => Text(element, name) is { Length: > 0 } text ? text : null;

    private static bool Flag(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static DateTimeOffset? Moment(JsonElement element, string name) =>
        DateTimeOffset.TryParse(Text(element, name), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at) ? at : null;

    private static IReadOnlyList<JsonElement> Objects(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object)]
            : [];

    private static IReadOnlyList<string> Strings(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!)]
            : [];
}
