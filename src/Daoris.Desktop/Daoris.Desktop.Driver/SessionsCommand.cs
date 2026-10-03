using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What <c>daoris-driver sessions</c> was asked (D126 §7.1; MSG1e's <c>say</c>, D137 §5.2; MSG1g's <c>go-on-new</c>).</summary>
/// <param name="Verb">
/// <c>list</c>, <c>stop</c>, <c>finish</c>, <c>decline</c>, <c>archive</c>, <c>unarchive</c>, <c>delete</c>, <c>say</c> or
/// <c>go-on-new</c>.
/// </param>
public sealed record SessionsAsk(string Verb)
{
    /// <summary>The sessions a verb names.</summary>
    public IReadOnlyList<string> Ids { get; init; } = [];

    /// <summary>The listing's one group, one of <see cref="SessionGroup"/>.</summary>
    public string? Group { get; init; }

    /// <summary>The listing's one repository.</summary>
    public string? Repository { get; init; }

    /// <summary>The listing as the screen's answer.</summary>
    public bool Json { get; init; }

    /// <summary>A finish's note or a decline's reason.</summary>
    public string? Note { get; init; }

    /// <summary><c>archive --ended</c>: every session in Ended, listed first.</summary>
    public bool Ended { get; init; }

    /// <summary><c>archive --ended --yes</c>: archive what the list holds.</summary>
    public bool Yes { get; init; }

    /// <summary>A say's words (MSG1e), joined by a space as `answer` joins them.</summary>
    public string? Text { get; init; }

    /// <summary>A say's files, each as the person named its path.</summary>
    public IReadOnlyList<string> Files { get; init; } = [];
}

/// <summary>
/// What <c>sessions</c> reads and touches: the service, the home's choices and files, this machine's process markers and
/// the machine log.
/// </summary>
/// <param name="Door">The configured agent's wire, which the planner's verdicts are read by (D70).</param>
/// <param name="Log">Where the archive's and the delete's lines go, as the terminal's door; null writes none.</param>
public sealed record SessionsWorld(ServiceClient Service, string Home, DriverConfig Config, SessionWire Door, MachineLog? Log)
{
    /// <summary>The markers every driver on the home leaves for a live process: whether something here runs a session.</summary>
    public SessionProcesses Processes { get; init; } = new(Path.Combine(Home, "sessions"));

    /// <summary>How long a request is waited for: ten seconds (§7.1).</summary>
    public TimeSpan Wait { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>How often the record is read while a request waits.</summary>
    public TimeSpan Poll { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Whether a driver loop holds this home (DRV8a, <see cref="DriverLock.HeldBy"/>): where none does, nothing would take a
    /// say up, so the verb keeps the words on the record itself (MSG1e, D137 §5.2).
    /// </summary>
    public Func<bool> LoopRuns { get; init; } = () => DriverLock.HeldBy(Home) is not null;

    /// <summary>
    /// The cool-off a start on an adapter would read for an account, or null where it is ready (MSG1g's <c>go-on-new</c>): read
    /// from this home's cool-offs by the account's owner, as the roster reads them, plugins' harnesses included.
    /// </summary>
    public Func<string?, string?, CoolingEntry?>? CoolingOf { get; init; }
}

/// <summary>
/// <c>daoris-driver sessions</c> (SESSUX1g, D126 §7.1): Sessions' terminal door. It lists this machine's sessions by what
/// they need, from <see cref="SessionGroups"/>, the one reader <c>SESSION_GROUPS</c> hands the page; stops, finishes and
/// declines a session; and archives, unarchives and deletes through the owners the screen's routes call. In the library,
/// so its words are held by a test.
/// </summary>
/// <remarks>
/// <para><b>Stop, finish and decline reach a session another process runs through a request its loop honours</b>
/// (<see cref="SessionRequests"/>): the verb writes it, waits up to ten seconds for the record to move, and says what
/// happened, withdrawing a request nothing took. Where nothing on this machine runs the session, a parked one is moved by
/// the ledger directly and any other is ended as the screen's stop ends an orphan.</para>
///
/// <para><b>Say is what the screen's box says</b> (MSG1e, D137 §5.2, D50): the words reach the loop that runs the session
/// through the same requests, a say of their own that the loop answers beside it; a loop keeps words to a session nothing here
/// runs on its record. Where no loop drives the home, a driven session's words are kept on its record for the next loop, and
/// a conversation's are refused, since nothing here could open it. What never goes on is judged first, by
/// <see cref="WordsNever"/>, before anything is asked. One line says where the words stand.</para>
///
/// <para>Exit codes are the family's: 0 done, listed, or the words taken or held · 1 refused, a policy answer · 2 could not,
/// the usage among them.</para>
/// </remarks>
public static class SessionsCommand
{
    public const string Usage =
        """
        usage: daoris-driver sessions [--group you|review|working|later|ended|archived] [--repository <name>] [--json]
               daoris-driver sessions stop <id>  ·  sessions finish <id> [--note "…"]  ·  sessions decline <id> --reason "…"
               daoris-driver sessions archive <id>… | --ended [--yes]  ·  sessions unarchive <id>…  ·  sessions delete <id>
               daoris-driver sessions say <id> "…" [--file <path>]…
               daoris-driver sessions go-on-new <id>
        """;

    /// <summary>The fields of a row in <c>--json</c>, in order: <c>SESSION_GROUPS</c>' row, field for field.</summary>
    public static IReadOnlyList<string> JsonFields { get; } =
        ["session", "group", "shown", "archived", "teammate", "strikes", "awaits", "awaitsOf", "work", "holdsQuest", "pausedBy", "deletable"];

    /// <summary>What the words ask, or null with what is wrong with them.</summary>
    public static SessionsAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        switch (args)
        {
            case ["stop", var id] when Id(id):
                return new SessionsAsk("stop") { Ids = [id] };
            case ["stop", ..]:
                problem = "`stop` takes one session's id.";
                return null;
            case ["finish", var id] when Id(id):
                return new SessionsAsk("finish") { Ids = [id] };
            case ["finish", var id, "--note", var note] when Id(id) && note.Trim().Length > 0:
                return new SessionsAsk("finish") { Ids = [id], Note = note.Trim() };
            case ["finish", ..]:
                problem = "`finish` takes one session's id, and `--note` the words its record keeps.";
                return null;
            case ["decline", var id, "--reason", var reason] when Id(id) && reason.Trim().Length > 0:
                return new SessionsAsk("decline") { Ids = [id], Note = reason.Trim() };
            case ["decline", ..]:
                problem = "`decline` takes one session's id and `--reason`: the reason is the part whoever reads its record can act on.";
                return null;
            case ["archive", "--ended"]:
                return new SessionsAsk("archive") { Ended = true };
            case ["archive", "--ended", "--yes"]:
                return new SessionsAsk("archive") { Ended = true, Yes = true };
            case ["archive", _, ..] when args.Skip(1).All(Id):
                return new SessionsAsk("archive") { Ids = [.. args.Skip(1).Distinct(StringComparer.Ordinal)] };
            case ["archive", ..]:
                problem = "`archive` takes sessions' ids, or `--ended` and then `--yes`.";
                return null;
            case ["unarchive", _, ..] when args.Skip(1).All(Id):
                return new SessionsAsk("unarchive") { Ids = [.. args.Skip(1).Distinct(StringComparer.Ordinal)] };
            case ["unarchive", ..]:
                problem = "`unarchive` takes sessions' ids.";
                return null;
            case ["delete", var id] when Id(id):
                return new SessionsAsk("delete") { Ids = [id] };
            case ["delete", ..]:
                problem = "`delete` takes one session's id.";
                return null;
            case ["say", ..]:
                return ReadSay(args, out problem);
            case ["go-on-new", var id] when Id(id):
                return new SessionsAsk("go-on-new") { Ids = [id] };
            case ["go-on-new", ..]:
                problem = "`go-on-new` takes one session's id.";
                return null;
        }

        var ask = new SessionsAsk("list");
        for (var at = 0; at < args.Count; at++)
        {
            switch (args[at])
            {
                case "--json":
                    ask = ask with { Json = true };
                    break;
                case "--group" when at + 1 < args.Count && SessionGroup.Order.Contains(args[at + 1]):
                    ask = ask with { Group = args[++at] };
                    break;
                case "--group":
                    problem = $"`--group` takes one of {string.Join(", ", SessionGroup.Order)}.";
                    return null;
                case "--repository" when at + 1 < args.Count && Id(args[at + 1]):
                    ask = ask with { Repository = args[++at] };
                    break;
                case "--repository":
                    problem = "`--repository` takes a repository's name.";
                    return null;
                default:
                    problem = $"`{args[at]}` is not a word `sessions` takes.";
                    return null;
            }
        }

        return ask;
    }

    /// <summary><c>say &lt;id&gt; "…" [--file &lt;path&gt;]…</c>: the words after the id joined by a space, and each path after its flag.</summary>
    private static SessionsAsk? ReadSay(IReadOnlyList<string> args, out string? problem)
    {
        problem = "`say` takes one session's id, then your words, and `--file <path>` for each file you give with them.";
        if (args.Count < 2 || !Id(args[1])) return null;

        var words = new List<string>();
        var files = new List<string>();
        for (var at = 2; at < args.Count; at++)
        {
            if (args[at] != "--file")
            {
                words.Add(args[at]);
                continue;
            }

            if (at + 1 >= args.Count || args[at + 1].Trim().Length == 0) return null;
            files.Add(args[++at]);
        }

        var text = string.Join(' ', words).Trim();
        if (text.Length == 0) return null;

        problem = null;
        return new SessionsAsk("say") { Ids = [args[1]], Text = text, Files = files };
    }

    public static async Task<int> RunAsync(SessionsAsk ask, SessionsWorld world, TextWriter output, CancellationToken ct = default) =>
        ask.Verb switch
        {
            "say" => await SayAsync(world, ask, output, ct).ConfigureAwait(false),
            "go-on-new" => await GoOnNewAsync(world, ask.Ids[0], output, ct).ConfigureAwait(false),
            "stop" => await StopAsync(world, ask.Ids[0], output, ct).ConfigureAwait(false),
            "finish" or "decline" => await ResolveAsync(world, ask, output, ct).ConfigureAwait(false),
            "archive" when ask.Ended => await ArchiveEndedAsync(world, ask.Yes, output, ct).ConfigureAwait(false),
            "archive" => await ArchiveAsync(world, ask.Ids, output, ct).ConfigureAwait(false),
            "unarchive" => Unarchive(world, ask.Ids, output),
            "delete" => await DeleteAsync(world, ask.Ids[0], output, ct).ConfigureAwait(false),
            _ => await ListAsync(world, ask, output, ct).ConfigureAwait(false),
        };

    /// <summary>The written form of one listing, as <c>SESSION_GROUPS</c> answers it: <c>{"sessions": [...]}</c>.</summary>
    public static string Json(IEnumerable<SessionGrouping> rows)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("sessions");
            foreach (var row in rows)
            {
                writer.WriteStartObject();
                writer.WriteString("session", row.Session);
                writer.WriteString("group", row.Group);
                writer.WriteString("shown", row.Shown);
                writer.WriteBoolean("archived", row.Archived);
                writer.WriteBoolean("teammate", row.Teammate);
                if (row.Strikes is { } strikes) writer.WriteNumber("strikes", strikes); else writer.WriteNull("strikes");
                writer.WriteString("awaits", row.Awaits);
                writer.WriteString("awaitsOf", row.AwaitsOf);
                if (row.Work is { } work)
                {
                    writer.WriteStartObject("work");
                    if (work.Commits is { } commits) writer.WriteNumber("commits", commits); else writer.WriteNull("commits");
                    if (work.Uncommitted is { } uncommitted) writer.WriteNumber("uncommitted", uncommitted); else writer.WriteNull("uncommitted");
                    writer.WriteEndObject();
                }
                else
                {
                    writer.WriteNull("work");
                }

                writer.WriteBoolean("holdsQuest", row.HoldsQuest);
                if (row.PausedBy is { } pause)
                {
                    writer.WriteStartObject("pausedBy");
                    writer.WriteString("scope", pause.Word);
                    writer.WriteString("id", pause.Id);
                    writer.WriteEndObject();
                }
                else
                {
                    writer.WriteNull("pausedBy");
                }

                writer.WriteBoolean("deletable", row.Deletable);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n");
    }

    // ——— The listing.

    private static async Task<int> ListAsync(SessionsWorld world, SessionsAsk ask, TextWriter output, CancellationToken ct)
    {
        var look = await SessionGroups.LookAsync(world.Service, world.Config, world.Door, lastLook: null, world.Home, ct: ct).ConfigureAwait(false);
        var records = look.Records.GroupBy(record => record.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var rows = SessionGroups.Read(look)
            .Where(row => ask.Group is null || row.Group == ask.Group)
            .Where(row => ask.Repository is null
                || (records.TryGetValue(row.Session, out var record) && string.Equals(record.Repository, ask.Repository, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (ask.Json)
        {
            output.WriteLine(Json(rows));
            return 0;
        }

        if (rows.Count == 0)
        {
            output.WriteLine($"sessions: {Nothing(ask)}.");
            return 0;
        }

        var quests = look.Quests.GroupBy(quest => quest.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var openings = new SessionEvents(Path.Combine(world.Home, "sessions"))
            .Openings(rows.Select(row => row.Session).Where(SessionEvents.IsId));
        var now = DateTimeOffset.UtcNow;
        foreach (var group in rows.GroupBy(row => row.Group))
        {
            var each = group.ToList();
            output.WriteLine($"{GroupName(group.Key)} ({each.Count})");
            foreach (var row in each)
            {
                var record = records[row.Session];
                var line = string.Join(" · ", new[] { Title(record, quests, openings), Span(record, now) }.Concat(Facts(row, record)));
                output.WriteLine($"  {row.Session}  {Word(row.Shown)}  {record.Repository} — {line}");
            }
        }

        return 0;
    }

    private static string Nothing(SessionsAsk ask) => (ask.Group, ask.Repository) switch
    {
        (SessionGroup.You, _) => "nothing waits on you",
        (SessionGroup.Review, _) => "nothing to review",
        (SessionGroup.Working, _) => "nothing is working",
        (SessionGroup.Later, _) => "nothing resumes later",
        (SessionGroup.Ended, _) => "nothing has ended",
        (SessionGroup.Archived, _) => "nothing is archived",
        (_, { } repository) => $"no session of {repository} on this machine",
        _ => "no session on this machine",
    };

    private static string GroupName(string group) => group switch
    {
        SessionGroup.You => "Waiting on you",
        SessionGroup.Review => "To review",
        SessionGroup.Working => "Working",
        SessionGroup.Later => "Resumes later",
        SessionGroup.Ended => "Ended",
        _ => "Archived",
    };

    /// <summary>The word a row shows, as the page says it in English: the record's state, or the reader's own word.</summary>
    private static string Word(string shown) => shown switch
    {
        "awaiting-person" => "waiting on you",
        "stood-down" => "stood down",
        ShownState.AwaitingReply => "awaiting reply",
        _ => shown,
    };

    /// <summary>What a session is for: its quest's title, a conversation's opening line, Ask Daoris, an ask's intake.</summary>
    private static string Title(SessionRecord record, IReadOnlyDictionary<string, QuestView> quests, IReadOnlyDictionary<string, string> openings) =>
        record.Quest is { } quest ? quests.TryGetValue(quest, out var view) ? view.Title : $"#{quest}"
        : record.Repository == HelpRoom.Repository ? "Ask Daoris"
        : record.Ask is { } ask ? $"Intake for ask #{ask}"
        : openings.TryGetValue(record.Id, out var opening) ? opening
        : $"a conversation in {record.Repository}";

    /// <summary>How long: a live session since it began, an ended one from its start to its end.</summary>
    private static string Span(SessionRecord record, DateTimeOffset now)
    {
        var to = record.Live || record.Updated == DateTimeOffset.MinValue ? now : record.Updated;
        var span = record.Created == DateTimeOffset.MinValue || to < record.Created ? TimeSpan.Zero : to - record.Created;
        return span.TotalMinutes < 1 ? $"{(int)span.TotalSeconds}s"
            : span.TotalHours < 1 ? $"{(int)span.TotalMinutes}m"
            : span.TotalDays < 1 ? $"{(int)span.TotalHours}h {span.Minutes:00}m"
            : $"{(int)span.TotalDays}d {span.Hours}h";
    }

    /// <summary>What a row's second line says on the screen (D126 §2.2), in the terminal's words.</summary>
    private static IEnumerable<string> Facts(SessionGrouping row, SessionRecord record)
    {
        if (row.Teammate) yield return $"on {record.Id[..record.Id.IndexOf('/')]}";
        if (row.Shown == ShownState.Parked)
        {
            yield return row.Strikes is { } strikes
                ? $"after {strikes} failed session{(strikes == 1 ? "" : "s")}"
                : "after its failed sessions";
        }

        if (row.Shown == ShownState.AwaitingReply && row.Awaits is { } awaits)
        {
            yield return row.AwaitsOf is { } of ? $"waits on #{awaits}, asked of {of}" : $"waits on #{awaits}";
        }

        if (row.Shown == ShownState.Answered) yield return "your answer goes on at the driver's next look";
        if (row.Group == SessionGroup.Review && row.Work is { } work)
        {
            yield return work.Commits is > 0 and var commits ? $"{commits} commit{(commits == 1 ? "" : "s")} to review"
                : work.Commits == 0 && work.Uncommitted > 0 ? "uncommitted changes"
                : "work to review";
        }

        if (row.HoldsQuest && record.Quest is { } quest) yield return $"held here until you try again: daoris driver retry {quest} --session {row.Session}";
        // PAUSE1b (D132 §6.1): a pause, never a hold, in its words: Resume is the press that moves it.
        if (row.PausedBy is { } pause)
        {
            yield return pause.Scope == WorkScope.Ask
                ? $"paused with ask #{pause.Id}; Resume carries it on: {pause.Door}"
                : $"paused with quest #{pause.Id}; Resume carries it on: {pause.Door}";
        }
        if (row.Archived && row.Group != SessionGroup.Archived) yield return "archived";
    }

    // ——— Stop, finish and decline.

    private static async Task<int> StopAsync(SessionsWorld world, string id, TextWriter output, CancellationToken ct)
    {
        var record = await RecordAsync(world, id, output, ct).ConfigureAwait(false);
        if (record is null) return 1;
        if (!record.Live)
        {
            output.WriteLine($"sessions: {id} has already ended ({Word(record.State)}).");
            return 1;
        }

        var parked = record.State == "awaiting-person";
        if (world.Processes.AliveOnThisMachine(id))
        {
            return await AskAsync(world, record, new SessionRequest(id, SessionMove.Stop, DateTimeOffset.UtcNow) { Parked = parked }, "stopped", output, ct)
                .ConfigureAwait(false);
        }

        if (parked)
        {
            try
            {
                await SessionMoves.ResolveAsync(_ => false, world.Service, id, "stopped", note: null, ct).ConfigureAwait(false);
            }
            catch (DriverException refused)
            {
                output.WriteLine($"sessions: {refused.Message}");
                return 1;
            }

            output.WriteLine($"sessions: stopped {id}: it waited on you with no process left, so the ledger ended it unanswered.");
            Held(record, output);
            return 0;
        }

        var answer = await SessionMoves.StopAsync(world.Processes, world.Service, id, ct).ConfigureAwait(false);
        if (answer.Orphan)
        {
            output.WriteLine($"sessions: stopped {id}: nothing on this machine ran it any more, so its record is ended as your stop.");
            Held(record, output);
            return 0;
        }

        output.WriteLine(answer.Elsewhere
            ? $"sessions: another Daoris process on this machine began running {id} as it was asked; ask again."
            : $"sessions: {id} is {Word(record.State)}, and nothing on this machine runs it yet to stop.");
        return answer.Elsewhere ? 2 : 1;
    }

    private static async Task<int> ResolveAsync(SessionsWorld world, SessionsAsk ask, TextWriter output, CancellationToken ct)
    {
        var id = ask.Ids[0];
        var record = await RecordAsync(world, id, output, ct).ConfigureAwait(false);
        if (record is null) return 1;
        if (record.Ask is { } asked)
        {
            output.WriteLine($"sessions: {id} is an intake; answer its ask #{asked} instead: publish it or close it.");
            return 1;
        }

        if (record.State != "awaiting-person")
        {
            output.WriteLine($"sessions: {id} is {Word(record.State)}, not waiting on you; finish and decline answer a session that waits on you.");
            return 1;
        }

        var (move, state, past) = ask.Verb == "finish"
            ? (SessionMove.Finish, "completed", "finished")
            : (SessionMove.Decline, "declined", "declined");
        if (world.Processes.AliveOnThisMachine(id))
        {
            return await AskAsync(world, record, new SessionRequest(id, move, DateTimeOffset.UtcNow) { Note = ask.Note, Parked = true }, past, output, ct)
                .ConfigureAwait(false);
        }

        try
        {
            var said = await SessionMoves.ResolveAsync(_ => false, world.Service, id, state, ask.Note, ct).ConfigureAwait(false);
            output.WriteLine($"sessions: {past} {id}: {said}");
            return 0;
        }
        catch (DriverException refused)
        {
            output.WriteLine($"sessions: {refused.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Ask the loop that runs a session, and wait for its record to move (§7.1 rules 1–3): what happened is said, and a
    /// request nothing took in time is withdrawn so no loop acts on it after the person was told.
    /// </summary>
    private static async Task<int> AskAsync(
        SessionsWorld world, SessionRecord record, SessionRequest request, string past, TextWriter output, CancellationToken ct)
    {
        var requests = new SessionRequests(world.Home);
        requests.Write(request);
        var waited = Stopwatch.StartNew();
        while (waited.Elapsed < world.Wait)
        {
            await Task.Delay(world.Poll, ct).ConfigureAwait(false);
            var now = (await world.Service.SessionRecordsAsync(ct).ConfigureAwait(false))
                .FirstOrDefault(each => string.Equals(each.Id, record.Id, StringComparison.Ordinal));
            if (now is not null && now.State == record.State) continue;

            output.WriteLine($"sessions: {past} {record.Id} — the loop that runs it took the request, and its record says {Word(now?.State ?? "gone")}.");
            if (request.Move == SessionMove.Stop) Held(record, output);
            return 0;
        }

        var seconds = (int)Math.Round(world.Wait.TotalSeconds);
        output.WriteLine(requests.Withdraw(record.Id)
            ? $"sessions: nothing that runs {record.Id} took the request within {seconds} seconds, so it was withdrawn: it runs in "
              + "another Daoris process on this machine that takes no requests (a terminal's chat stops with its own Ctrl+C)."
            : $"sessions: the loop that runs {record.Id} took the request, and its record had not moved after {seconds} seconds; "
              + "`daoris-driver sessions` says where it stands.");
        return 2;
    }

    /// <summary>A stopped driven session's quest is held here until the person tries again (SESSUX1b): said with its door.</summary>
    private static void Held(SessionRecord record, TextWriter output)
    {
        if (record.Quest is { } quest)
        {
            output.WriteLine($"  its quest #{quest} is held here until you try again: daoris driver retry {quest} --session {record.Id}");
        }
    }

    /// <summary>This machine's record of one session, or null with why it is not one this machine can act on.</summary>
    private static async Task<SessionRecord?> RecordAsync(SessionsWorld world, string id, TextWriter output, CancellationToken ct)
    {
        var record = (await world.Service.SessionRecordsAsync(ct).ConfigureAwait(false))
            .FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.Ordinal));
        if (record is null)
        {
            output.WriteLine($"sessions: no session here is {id}.");
            return null;
        }

        if (record.Teammate)
        {
            output.WriteLine($"sessions: {id} runs on {id[..id.IndexOf('/')]}; nothing this machine sends reaches its process.");
            return null;
        }

        return record;
    }

    // ——— Say (MSG1e, D137 §5.2): what the screen's box says to a session.

    private static async Task<int> SayAsync(SessionsWorld world, SessionsAsk ask, TextWriter output, CancellationToken ct)
    {
        var id = ask.Ids[0];
        var text = ask.Text ?? "";
        var waited = Stopwatch.StartNew();

        // Read here, on the machine that has them, as `ask --file` reads them.
        var uploads = new List<ChatUpload>();
        foreach (var path in ask.Files)
        {
            var full = Path.GetFullPath(path);
            if (!File.Exists(full))
            {
                output.WriteLine($"sessions: `{path}` is not a file on this machine.");
                return 2;
            }

            uploads.Add(new ChatUpload(Path.GetFileName(full), await File.ReadAllBytesAsync(full, ct).ConfigureAwait(false)));
        }

        // What never goes on is judged from the record before anything is asked (D137 §2.2), by the one table.
        var json = await world.Service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
        var record = SessionRecords.Parse(json).FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.Ordinal));
        if (record is null)
        {
            output.WriteLine($"sessions: {Refusal(WordsNever.NotFound, id, null, null, null)}");
            return 1;
        }

        var last = WordsNever.LastHere(json, record.Quest);
        if (WordsNever.Judge(record, last) is { } never)
        {
            output.WriteLine($"sessions: {Refusal(never, id, record, last, null)}");
            return 1;
        }

        // Kept as the box keeps what is attached (CONV4c), so the names the request and the record carry name files that exist.
        IReadOnlyList<string> files;
        try
        {
            files = uploads.Count > 0 ? [.. ChatFiles.Keep(world.Home, id, uploads).Select(file => file.Name)] : [];
        }
        catch (DriverException refused)
        {
            output.WriteLine($"sessions: {refused.Message}");
            return 2;
        }

        var runs = world.Processes.AliveOnThisMachine(id);
        if (!runs && !world.LoopRuns()) return await KeepAsync(world, record, text, files, loop: false, output, ct).ConfigureAwait(false);

        var (held, taken) = await AskLoopAsync(world, record, text, files, waited, ct).ConfigureAwait(false);
        if (held is not null) return await SaidAsync(world, record, held, waited, output, ct).ConfigureAwait(false);
        if (taken)
        {
            output.WriteLine(
                $"sessions: the loop that runs {id} took your words, and had not said where they stand after {Seconds(world)} seconds; "
                + "`daoris-driver sessions` says where it stands.");
            return 2;
        }

        if (runs)
        {
            output.WriteLine(
                $"sessions: nothing that runs {id} took your words within {Seconds(world)} seconds, so they were withdrawn: it runs in "
                + "another Daoris process on this machine that takes no requests (a terminal's chat takes words on its own input).");
            return 2;
        }

        // A loop holds the home and took nothing in time: one still coming up, or a build from before this door.
        return await KeepAsync(world, record, text, files, loop: true, output, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// A say asked of the loops on this home and waited for, until its answer or the wait's end (§5.2). A session winding up is
    /// asked again until its record has ended, since the say door keeps nothing for a record still running. Null and not taken
    /// where nothing took it, which is withdrawn so no loop acts on it after the person was told.
    /// </summary>
    private static async Task<(WordsHeld? Held, bool Taken)> AskLoopAsync(
        SessionsWorld world, SessionRecord record, string text, IReadOnlyList<string> files, Stopwatch waited, CancellationToken ct)
    {
        var requests = new SessionRequests(world.Home);
        WordsHeld? winding = null;
        while (true)
        {
            var request = new SessionRequest(record.Id, SessionMove.Say, DateTimeOffset.UtcNow)
            {
                Text = text, Files = files, Key = SessionRequests.NewKey(),
            };
            requests.Write(request);

            WordsHeld? held = null;
            while (held is null && waited.Elapsed < world.Wait)
            {
                await Task.Delay(world.Poll, ct).ConfigureAwait(false);
                held = requests.AnswerOf(request);
            }

            if (held is null)
            {
                return requests.Withdraw(request)
                    ? (winding, winding is not null)
                    : (requests.AnswerOf(request) ?? winding, true);
            }

            if (held.Why != WordsHeld.Running || waited.Elapsed >= world.Wait) return (held, true);
            winding = held;
        }
    }

    /// <summary>Where the words stand, in one line, from the loop's answer (§5.2); words kept on the record are followed.</summary>
    private static async Task<int> SaidAsync(
        SessionsWorld world, SessionRecord record, WordsHeld held, Stopwatch waited, TextWriter output, CancellationToken ct)
    {
        switch (held)
        {
            case { Sent: true, Reaches: "resume" }:
                return await GoesOnAsync(world, record, held.Word, waited, output, ct).ConfigureAwait(false);
            case { Sent: true, Reaches: "next-step" }:
                output.WriteLine("sessions: held: it reads this at its next step.");
                return 0;
            case { Sent: true, Reaches: "turn-end" }:
                output.WriteLine("sessions: held: it reads this when its turn ends.");
                return 0;
            case { Sent: true }:
                output.WriteLine(record.Quest is null
                    ? "sessions: sent: it takes this as its next turn."
                    : "sessions: held: it reads this once its turn opens.");
                return 0;
            case { Why: WordsHeld.Running }:
                output.WriteLine(
                    $"sessions: {record.Id} was still winding up after {Seconds(world)} seconds, and nothing here holds words for it "
                    + "until it ends; say it again once it has ended.");
                return 2;
            case { Why: WordsHeld.Unreached }:
                output.WriteLine(
                    $"sessions: the desktop runs {record.Id}, a conversation, and hands it only the words typed in its box; write in "
                    + "its box there.");
                return 2;
            case { Why: { } why }:
                output.WriteLine($"sessions: {Refusal(why, record.Id, record, null, held.Message)}");
                return 1;
            default:
                output.WriteLine($"sessions: {held.Message ?? "the loop that took your words could not say where they went."}");
                return 2;
        }
    }

    /// <summary>
    /// Words kept on the record, followed until the wait's end (§5.2, D137 §3.1): taken by the same session, gone to a new one,
    /// judged unable to go on, or still held, with what holds them.
    /// </summary>
    private static async Task<int> GoesOnAsync(
        SessionsWorld world, SessionRecord record, string? word, Stopwatch waited, TextWriter output, CancellationToken ct)
    {
        var events = new SessionEvents(Path.Combine(world.Home, "sessions"));
        var gone = false;
        while (word is not null)
        {
            // The driver's note on these words, where it wrote one (MSG1b, MSG1d): where they went, or why they cannot go on.
            var note = events.Page(record.Id, limit: SessionEvents.MaxPage).Events
                .LastOrDefault(each => each.Kind == SessionEventKind.Note && each.Words?.Contains(word, StringComparer.Ordinal) == true);
            if (note is { To: { } to })
            {
                output.WriteLine($"sessions: went to session `{to}`, because {Because(note)}.");
                return 0;
            }

            if (note is not null)
            {
                output.WriteLine($"sessions: kept, but it cannot go on in this session, because {Because(note)}.");
                output.WriteLine($"  start a conversation with them instead: daoris-driver chat --repository {record.Repository}");
                return 1;
            }

            var json = await world.Service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
            gone = !Waiting(json, record.Id).Contains(word, StringComparer.Ordinal);
            var now = SessionRecords.Parse(json).FirstOrDefault(each => string.Equals(each.Id, record.Id, StringComparison.Ordinal));
            if (gone && now is { Live: true })
            {
                output.WriteLine("sessions: going on: the same session took it.");
                return 0;
            }

            if (waited.Elapsed >= world.Wait) break;
            await Task.Delay(world.Poll, ct).ConfigureAwait(false);
        }

        output.WriteLine(gone
            ? "sessions: taken: the session took it, and has moved since; `daoris-driver sessions` says where it stands."
            : $"sessions: {await HeldAsync(world, record, ct).ConfigureAwait(false)}");
        return 0;
    }

    /// <summary>
    /// Kept words not taken within the wait: held for the driver's next look, or what holds them, by the planner's own verdict on
    /// the record's quest, since whatever holds a start holds a reopen (D137 §2.2). A conversation goes on as it opens again.
    /// </summary>
    private static async Task<string> HeldAsync(SessionsWorld world, SessionRecord record, CancellationToken ct)
    {
        const string Next = "held: the same session goes on with this at the driver's next look.";
        if (record.Quest is not { } quest) return "held: the same conversation goes on with this as it opens again.";

        Consideration? verdict;
        try
        {
            verdict = (await SessionGroups.VerdictsAsync(world.Service, world.Config, world.Door, lastLook: null, ct).ConfigureAwait(false))
                .FirstOrDefault(each => string.Equals(each.Quest.Id, quest, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or JsonException)
        {
            return Next;
        }

        return verdict switch
        {
            null or { Verdict: StartVerdict.Start } => Next,
            { Verdict: StartVerdict.Paused, PausedBy: { } pause } => $"held: it goes on with this once you resume its work: {pause.Door}",
            { Verdict: StartVerdict.Held } =>
                $"held: it goes on with this once {verdict.Quest.To} is no longer held: daoris driver resume {verdict.Quest.To}",
            { Verdict: StartVerdict.AtCapacity } => "held: it goes on with this when a running session here ends.",
            _ => $"held: it waits: {verdict.Reason}",
        };
    }

    /// <summary>
    /// Words the verb keeps on the record itself (§5.2). Where no loop drives the home, a driven session's, for the next loop to
    /// take up, shown in its conversation as the terminal's, since nothing else writes that record now; where a loop holds the
    /// home but took nothing in time, the same, unshown, since that loop's record of it may be open. A conversation goes on only
    /// through a loop's chat runner, so where none runs nothing here could open it.
    /// </summary>
    private static async Task<int> KeepAsync(
        SessionsWorld world, SessionRecord record, string text, IReadOnlyList<string> files, bool loop, TextWriter output, CancellationToken ct)
    {
        if (record.Quest is null)
        {
            output.WriteLine(loop
                ? $"sessions: the driver on this machine took nothing within {Seconds(world)} seconds, so your words were withdrawn; say them again in a moment."
                : $"sessions: {record.Id} is a conversation, and no driver runs on this machine to open it again: start the desktop, "
                  + "or `daoris-driver drive`, then say it again.");
            return loop ? 2 : 1;
        }

        var said = await world.Service.SayAsync(record.Id, text, files, ct).ConfigureAwait(false);
        if (said.Kept)
        {
            if (!loop && said.Word is { } kept)
            {
                new SessionEvents(Path.Combine(world.Home, "sessions")).Keep(record.Id, LoopWords.Shown(kept, RequestDoor.Terminal), null);
            }

            output.WriteLine(loop
                ? "sessions: held: the same session goes on with this at the driver's next look."
                : "sessions: held: the same session goes on with this when a driver next runs on this machine.");
            return 0;
        }

        switch (said.Refusal)
        {
            case "running":
                output.WriteLine(
                    $"sessions: {record.Id} is still {Word(record.State)}, and nothing on this machine runs it: a driver's next start "
                    + "ends it, and then it can go on; say it again then.");
                return 2;
            case { } refusal:
                output.WriteLine($"sessions: {Refusal(WordsNever.Code(refusal), record.Id, record, null, said.Message)}");
                return 1;
            default:
                output.WriteLine($"sessions: {said.Message}");
                return 2;
        }
    }

    /// <summary>What never goes on, by its code, in the terminal's words (D137 §2.2, §5.1); any other code, the service's sentence.</summary>
    private static string Refusal(string code, string id, SessionRecord? record, string? last, string? message) => code switch
    {
        WordsNever.NotFound => $"no session here is {id}.",
        WordsNever.Teammate =>
            $"{id} ran on {(id.Contains('/') ? id[..id.IndexOf('/')] : "another machine")}, where its conversation is; nothing said here reaches it.",
        WordsNever.Help => $"{id} is Ask Daoris's own conversation, which goes on nowhere: its panel opens a new one.",
        WordsNever.Intake => $"{id} is an intake; answer its ask #{record?.Ask} instead: publish it or close it.",
        WordsNever.StoodDown => $"it stood down: #{record?.Quest} is someone else's, so it has nothing to go on with.",
        WordsNever.Superseded =>
            $"#{record?.Quest} went on in a later session here, {last}; say it to that one: daoris-driver sessions say {last} \"…\"",
        _ => message ?? $"{id} did not take your words ({code}).",
    };

    /// <summary>A driver's note's reason, as its line says it after <c>because</c>; its code where the line says none.</summary>
    private static string Because(SessionEvent note)
    {
        var text = note.Text ?? "";
        var at = text.IndexOf("because ", StringComparison.Ordinal);
        return at >= 0 ? text[(at + "because ".Length)..].TrimEnd().TrimEnd('.') : note.Why ?? "it could not";
    }

    /// <summary>The ids of the words waiting on one record, as the service answers its <c>said</c>; none where it answers none.</summary>
    private static IReadOnlyList<string> Waiting(string json, string id)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array) return [];
        foreach (var session in document.RootElement.EnumerateArray())
        {
            if (session.ValueKind != JsonValueKind.Object
                || !session.TryGetProperty("id", out var named) || named.ValueKind != JsonValueKind.String || named.GetString() != id)
            {
                continue;
            }

            return session.TryGetProperty("said", out var said) && said.ValueKind == JsonValueKind.Array
                ? [.. said.EnumerateArray()
                    .Where(each => each.ValueKind == JsonValueKind.Object && each.TryGetProperty("id", out var word) && word.ValueKind == JsonValueKind.String)
                    .Select(each => each.GetProperty("id").GetString()!)]
                : [];
        }

        return [];
    }

    private static int Seconds(SessionsWorld world) => (int)Math.Round(world.Wait.TotalSeconds);

    // ——— Go on in a new session (MSG1g, D137 §2.2): the screen's press, at a terminal.

    /// <summary>
    /// Words a resume holds while their account cools go on in a new session at the driver's next look (<see cref="GoOnNew"/>):
    /// one line, exit 0 where the choice is kept, 1 where it is refused, 2 where it could not be kept.
    /// </summary>
    private static async Task<int> GoOnNewAsync(SessionsWorld world, string id, TextWriter output, CancellationToken ct)
    {
        var answer = await GoOnNew.AskAsync(
                world.Service, world.Home, world.CoolingOf ?? CoolingOn(world.Home), id, DateTimeOffset.UtcNow, ct)
            .ConfigureAwait(false);
        output.WriteLine($"sessions: {answer.Message}");
        return answer.Sent ? 0 : answer.Why is null ? 2 : 1;
    }

    /// <summary>The home's cool-offs as a start reads them: by the account's owner, the build's harnesses and the plugins' (D64).</summary>
    private static Func<string?, string?, CoolingEntry?> CoolingOn(string home)
    {
        var built = AdapterSet.Built();
        var adapters = built.WithPlugins(PluginCatalog.Load(home, built.Names));
        var roster = new HarnessRoster(adapters, Path.Combine(home, "harnesses.json"));
        return (adapter, profile) => roster.CoolingOf(adapter ?? "", profile);
    }

    // ——— Archive, unarchive and delete: the screen's owners.

    private static async Task<int> ArchiveAsync(SessionsWorld world, IReadOnlyList<string> ids, TextWriter output, CancellationToken ct)
    {
        var look = await SessionGroups.LookAsync(world.Service, world.Config, world.Door, lastLook: null, world.Home, ids, ct).ConfigureAwait(false);
        var answer = new SessionArchive(world.Home).Archive(
            ids, SessionGroups.Read(look, ids), [.. look.Records.Select(record => record.Id)], DateTimeOffset.UtcNow);
        foreach (var outcome in answer.Outcomes)
        {
            output.WriteLine(outcome.Verdict switch
            {
                ArchiveVerdict.Archived => $"sessions: archived {outcome.Session}.",
                ArchiveVerdict.Live => $"sessions: kept {outcome.Session}: it is still running; stop it first.",
                ArchiveVerdict.NeedsYou when outcome.Group == SessionGroup.Review =>
                    $"sessions: kept {outcome.Session}: it has work to review; archive never hides what needs you.",
                ArchiveVerdict.NeedsYou => $"sessions: kept {outcome.Session}: it is waiting on you; archive never hides what needs you.",
                _ => $"sessions: kept {outcome.Session}: no session here is {outcome.Session}.",
            });
        }

        var archived = answer.Outcomes.Count(outcome => outcome.Verdict == ArchiveVerdict.Archived);
        SessionArchive.Said(world.Log, archived, PluginEvents.Terminal);
        return archived == answer.Outcomes.Count ? 0 : 1;
    }

    /// <summary>§5.3 at a terminal: the list first, what it would take and what stays; <c>--yes</c> archives what it holds, each judged again.</summary>
    private static async Task<int> ArchiveEndedAsync(SessionsWorld world, bool yes, TextWriter output, CancellationToken ct)
    {
        var look = await SessionGroups.LookAsync(world.Service, world.Config, world.Door, lastLook: null, world.Home, ct: ct).ConfigureAwait(false);
        var rows = SessionGroups.Read(look);
        var ended = rows.Where(row => row.Group == SessionGroup.Ended && !row.Archived).Select(row => row.Session).ToList();
        if (ended.Count == 0)
        {
            output.WriteLine("sessions: nothing that ended is left to archive.");
            return 0;
        }

        if (!yes)
        {
            output.WriteLine($"sessions: would archive {Sessions(ended.Count)} that ended:");
            foreach (var id in ended) output.WriteLine($"  {id}");
            var (you, review) = (rows.Count(row => row.Group == SessionGroup.You), rows.Count(row => row.Group == SessionGroup.Review));
            if (you > 0 || review > 0)
            {
                output.WriteLine("sessions: kept in the list: " + (you > 0 && review > 0 ? $"{you} waiting on you, {review} to review."
                    : you > 0 ? $"{you} waiting on you." : $"{review} to review."));
            }

            output.WriteLine("sessions: `--yes` archives them, each judged again as it goes.");
            return 0;
        }

        var answer = new SessionArchive(world.Home).Archive(ended, rows, [.. look.Records.Select(record => record.Id)], DateTimeOffset.UtcNow);
        var archived = answer.Outcomes.Count(outcome => outcome.Verdict == ArchiveVerdict.Archived);
        SessionArchive.Said(world.Log, archived, PluginEvents.Terminal);
        output.WriteLine(archived == ended.Count
            ? $"sessions: archived {Sessions(archived)} that ended; `--group archived` lists them, and `unarchive` brings one back."
            : $"sessions: archived {archived} of {ended.Count}; the rest changed since the list.");
        return 0;
    }

    private static int Unarchive(SessionsWorld world, IReadOnlyList<string> ids, TextWriter output)
    {
        var back = new SessionArchive(world.Home).Unarchive(ids);
        foreach (var id in ids)
        {
            output.WriteLine(back.NotArchived.Contains(id, StringComparer.Ordinal)
                ? $"sessions: {id} was not archived, so nothing changed."
                : $"sessions: unarchived {id}; it is back in the group its state puts it in.");
        }

        return 0;
    }

    private static async Task<int> DeleteAsync(SessionsWorld world, string id, TextWriter output, CancellationToken ct)
    {
        var outcome = await new SessionDeletion(world.Home).DeleteAsync(world.Service, id, PluginEvents.Terminal, world.Log, ct: ct).ConfigureAwait(false);
        output.WriteLine($"sessions: {outcome.Message}");
        return outcome.Verdict == DeleteVerdict.Deleted ? 0 : 1;
    }

    private static string Sessions(int count) => $"{count.ToString(CultureInfo.InvariantCulture)} session{(count == 1 ? "" : "s")}";

    /// <summary>A word that can be a session's id or a repository's name: not a flag, not blank.</summary>
    private static bool Id(string word) => word.Trim().Length > 0 && !word.StartsWith('-');
}
