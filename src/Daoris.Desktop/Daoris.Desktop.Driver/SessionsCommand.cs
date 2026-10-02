using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What <c>daoris-driver sessions</c> was asked (D126 §7.1).</summary>
/// <param name="Verb"><c>list</c>, <c>stop</c>, <c>finish</c>, <c>decline</c>, <c>archive</c>, <c>unarchive</c> or <c>delete</c>.</param>
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
/// <para>Exit codes are the family's: 0 done, or listed · 1 refused, a policy answer · 2 could not, the usage among
/// them.</para>
/// </remarks>
public static class SessionsCommand
{
    public const string Usage =
        """
        usage: daoris-driver sessions [--group you|review|working|later|ended|archived] [--repository <name>] [--json]
               daoris-driver sessions stop <id>  ·  sessions finish <id> [--note "…"]  ·  sessions decline <id> --reason "…"
               daoris-driver sessions archive <id>… | --ended [--yes]  ·  sessions unarchive <id>…  ·  sessions delete <id>
        """;

    /// <summary>The fields of a row in <c>--json</c>, in order: <c>SESSION_GROUPS</c>' row, field for field.</summary>
    public static IReadOnlyList<string> JsonFields { get; } =
        ["session", "group", "shown", "archived", "teammate", "strikes", "awaits", "awaitsOf", "work", "holdsQuest", "deletable"];

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

    public static async Task<int> RunAsync(SessionsAsk ask, SessionsWorld world, TextWriter output, CancellationToken ct = default) =>
        ask.Verb switch
        {
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
