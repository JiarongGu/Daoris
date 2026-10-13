using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

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
    public const int CommitDigits = CommitNames.Digits;

    /// <summary>Whether a word could name a commit: at least <see cref="CommitDigits"/> hexadecimal digits and no more than a whole id.</summary>
    public static bool IsCommit(string word) => CommitNames.IsCommit(word);

    /// <summary>Whether two names of commits name the same one: either is the start of the other, the shorter long enough to name one.</summary>
    internal static bool SameCommit(string? a, string? b)
    {
        if (a is null || b is null) return false;
        var (shorter, longer) = a.Length <= b.Length ? (a, b) : (b, a);
        return shorter.Length >= CommitDigits && longer.StartsWith(shorter, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Read the stores, find what the id names, read the asks the chain names, and answer the chain (TRACE1b): the one read the
    /// terminal prints and the screen words (D50). Where nothing names the id, or several kinds do, the sentence says so.
    /// </summary>
    public static async Task<TraceRead> ReadChainAsync(TraceAsk asked, TraceSources sources, CancellationToken ct = default)
    {
        var facts = await ReadAsync(sources, ct).ConfigureAwait(false);
        var unread = TraceChains.Unread(facts);
        var (found, problem) = Resolve(asked, facts);
        if (found is null) return new TraceRead(null, problem, unread);

        var asks = new Dictionary<string, TraceAskRead>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in TraceChains.AsksNamed(found, facts))
        {
            asks[id] = await ReadAskAsync(sources.Service, id, ct).ConfigureAwait(false);
        }

        return new TraceRead(TraceChains.Of(found, facts, asks, TraceReads.Here(sources), unread), null, unread);
    }

    /// <summary>One ask, whole, from its door (<c>GET /api/asks/{id}</c>): never a failure of the trace.</summary>
    private static async Task<TraceAskRead> ReadAskAsync(ServiceClient service, string id, CancellationToken ct)
    {
        try
        {
            return new TraceAskRead(id, await service.FindAskAsync(id, ct).ConfigureAwait(false), null);
        }
        catch (Exception error) when (Unanswered(error, ct))
        {
            return new TraceAskRead(id, null, error.Message);
        }
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

        var landed = new LandedBranches(sources.Home);
        return new TraceFacts(sessions, sessionsUnread, quests, questsUnread, landed.Entries(), LandingsProblem(landed.FilePath))
        {
            AutoLandings = new AutoLandings(sources.Home).All(),
        };
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

        var found = new List<TraceFoundBy>();
        var reached = new List<TracedSession>();
        foreach (var (made, line) in evidence)
        {
            found.Add(new TraceFoundBy(TraceFoundBy.Evidence, made.Id) { Line = line });
            if (!reached.Contains(made)) reached.Add(made);
        }

        var unrecorded = new List<string>();
        foreach (var landing in landings)
        {
            var how = SameCommit(landing.Tip, commit) ? TraceFoundBy.Tip : TraceFoundBy.Pushed;
            found.Add(new TraceFoundBy(how, landing.Session) { Branch = landing.Branch });
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
                NoteParts = NotePart.Read(quest),
                Filed = Moment(quest, "filed"),
                Updated = Moment(quest, "updated"),
                Parent = Filled(quest, "parent"),
                Then = [.. Objects(quest, "then").Select(step => new QuestStepView(Text(step, "to") ?? "", Text(step, "title") ?? "", Text(step, "body") ?? ""))],
                Requirements =
                [
                    .. Objects(quest, "requirements")
                        .Where(requirement => Text(requirement, "quote") is not null)
                        .Select(requirement => new QuestRequirementView(Text(requirement, "quote")!, Text(requirement, "check") ?? "")
                        {
                            Evidence = ServiceClient.EvidenceOf(requirement),
                        }),
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
                // Why it waits and what was read of its evidence (EVID1a, D144 §5): codes and names, as the quest keeps them.
                Hold = Filled(quest, "hold"),
                Evidence = EvidenceVerdict.Read(quest),
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
