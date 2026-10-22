using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What <c>daoris-driver sessions</c> was asked (D126 §7.1; MSG1e's <c>say</c>, D137 §5.2; MSG1g's <c>go-on-new</c>; MSG1f3's
/// <c>start-from</c>).
/// </summary>
/// <param name="Verb">
/// <c>list</c>, <c>stop</c>, <c>finish</c>, <c>decline</c>, <c>archive</c>, <c>unarchive</c>, <c>delete</c>, <c>say</c>,
/// <c>take-back</c>, <c>reword</c>, <c>go-on-new</c> or <c>start-from</c>.
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

    /// <summary>A say's words (MSG1e), joined by a space as `answer` joins them; a reword's new words (SAIDBACK1a).</summary>
    public string? Text { get; init; }

    /// <summary>A take-back's or a reword's word (SAIDBACK1a): the id it waits under on the record; null lists them.</summary>
    public string? Word { get; init; }

    /// <summary>A say's files, each as the person named its path.</summary>
    public IReadOnlyList<string> Files { get; init; } = [];

    /// <summary>A say's <c>--now</c> (STEER2c): the terminal's twin of the page's *Send now*, which <c>SESSION_INPUT</c> carries as <c>now: true</c>.</summary>
    public bool Now { get; init; }

    /// <summary>
    /// A say's <c>--level</c> (COWORK1a): <c>ordinary</c>, <c>urgent</c> or <c>now</c>, which <c>SESSION_INPUT</c> carries as
    /// <c>level</c>; <c>--now</c> is <c>now</c>. Null where none was asked, the person's default, urgent.
    /// </summary>
    public string? Level { get; init; }

    /// <summary>A hand-over's agent (MIXAGENT1a-i): the adapter the conversation goes on with.</summary>
    public string? Agent { get; init; }

    /// <summary>A hand-over's account, or null for the one the roster picks.</summary>
    public string? Account { get; init; }
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
    public SessionProcesses Processes { get; init; } = new(DaorisHome.Entry(Home, HomeEntry.SessionPids));

    /// <summary>How a delete removes a file from the home: the disk, or a test's that refuses a path as a held file does (SESSDEL1).</summary>
    public Action<string, bool> Remover { get; init; } = SessionHomeFiles.FromDisk;

    /// <summary>
    /// Where the running Daoris is looked for (RPCOPEN1, RPC1e, the terminal RPC design §7.2): the listing is its <c>SESSION_GROUPS</c>
    /// where it answers, in the same words. Null looks for none, as a test of the alone path does.
    /// </summary>
    public InstanceWorld? Instance { get; init; }

    /// <summary>How long a request is waited for unless a world says otherwise: ten seconds (§7.1).</summary>
    public static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(10);

    /// <summary>How long a request is waited for: <see cref="DefaultWait"/>.</summary>
    public TimeSpan Wait { get; init; } = DefaultWait;

    /// <summary>How often the record is read while a request waits.</summary>
    public TimeSpan Poll { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// The clock <see cref="Wait"/> and <see cref="Answer"/> are read by: the machine's, or a test's that stands still until the
    /// loop has taken the words (FLAKE5), so the wait a test means is not the load's.
    /// </summary>
    public TimeProvider Clock { get; init; } = TimeProvider.System;

    /// <summary>
    /// How long the answer of a loop that took a say is waited for once <see cref="Wait"/> is over (FLAKE3): that loop's own bound
    /// on a say, its client's timeout, since a loop that took one always answers it, a failure included.
    /// </summary>
    public TimeSpan Answer { get; init; } = DriverHttp.Timeout;

    /// <summary>
    /// Whether a driver loop holds this home (DRV8a, <see cref="HomeLease.HeldBy"/>): where none does, nothing would take a
    /// say up, so the verb keeps the words on the record itself (MSG1e, D137 §5.2). A verb's or a migration's hold is no loop
    /// (HOMELEASE1): only an instance's counts, and this verb's own lease among them never does.
    /// </summary>
    public Func<bool> LoopRuns { get; init; } = () => HomeLease.HeldBy(Home) is { IsInstance: true };

    /// <summary>
    /// Told why a held word's line fell back to "at the driver's next look" instead of naming what holds it (TESTWAIT5): the
    /// failure's kind, or that the planner gave no verdict. A test reads the cause; the person reads the same line.
    /// </summary>
    public Action<string>? Fell { get; init; }

    /// <summary>
    /// The cool-off a start on an adapter would read for an account, or null where it is ready (MSG1g's <c>go-on-new</c>): read
    /// from this home's cool-offs by the account's owner, as the roster reads them, plugins' harnesses included.
    /// </summary>
    public Func<string?, string?, CoolingEntry?>? CoolingOf { get; init; }

    /// <summary>
    /// The terminal's own conversation, opened with the words a session could not go on with (MSG1f3's <c>start-from</c>, D50):
    /// the host's chat door, which runs it through the chat runner's one act the screen's route calls and answers its exit.
    /// Null where nothing here can hold a conversation.
    /// </summary>
    public Func<string, CancellationToken, Task<int>>? StartFrom { get; init; }

    /// <summary>
    /// The terminal's own door for a conversation handed to another agent (MIXAGENT1a-i, D163, D50): the host's chat door, which
    /// runs the chat runner's one hand-over, as <see cref="StartFrom"/> runs its start, and answers its exit. Takes the session,
    /// the agent and the account, or null for the roster's pick. Null where nothing here can hold a conversation.
    /// </summary>
    public Func<string, string, string?, CancellationToken, Task<int>>? HandOver { get; init; }

    /// <summary>
    /// The keeping stop (COWORK1a, <c>sessions stop &lt;id&gt; --turn</c>): stop the session's turn so what waits goes next, through the
    /// Daoris whose loop holds its turns, answering a <see cref="KeepingStops"/> code. Null where nothing here holds them: the verb
    /// alone, which sends it to the running Daoris instead.
    /// </summary>
    public Func<string, CancellationToken, Task<string>>? StopTurn { get; init; }

    /// <summary>
    /// The agents a quest's session may go on with (MIXAGENT1a-ii, D163 point 1): each one's accounts and door, as the loop's
    /// roster reads them for a start. Null reads this build's adapters and the home's plugins.
    /// </summary>
    public Func<string, HandOverAgent?>? Drives { get; init; }
}

/// <summary>
/// What the keeping stop came to (COWORK1a, the message-urgency design §3.2): <c>CANCEL_TURN</c>'s answer to a terminal's
/// <c>sessions stop &lt;id&gt; --turn</c>, which the verb words.
/// </summary>
public static class KeepingStops
{
    /// <summary>The turn was asked to stop, keeping the session: what waits goes next.</summary>
    public const string Stopped = "stopped";

    /// <summary>No turn ran, or nothing waited for its end: nothing was stopped, since ending the work is the session's own stop.</summary>
    public const string Nothing = "nothing";

    /// <summary>The door has no stop that keeps the session (the native door's run): nothing was stopped.</summary>
    public const string NoInterrupt = "no-interrupt";

    /// <summary>Nothing here runs the session.</summary>
    public const string NotHere = "not-here";
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
/// <para><b>Start-from is the screen's *Start a conversation with these words*</b> (MSG1f3, D137 §2.2, D50): a conversation
/// in this terminal, as <c>chat</c> opens one, its first message the words a session could not go on with, which then leave
/// that session naming it, through the chat runner's own act (<see cref="ChatRunner.StartFromAsync"/>).</para>
///
/// <para>Exit codes are the family's: 0 done, listed, or the words taken or held · 1 refused, a policy answer · 2 could not,
/// the usage among them.</para>
/// </remarks>
public static class SessionsCommand
{
    public const string Usage =
        """
        usage: daoris-driver sessions [--group you|review|working|later|ended|archived] [--repository <name>] [--json]
               daoris-driver sessions stop <id> [--turn]  ·  sessions finish <id> [--note "…"]  ·  sessions decline <id> --reason "…"
               daoris-driver sessions archive <id>… | --ended [--yes]  ·  sessions unarchive <id>…  ·  sessions delete <id>
               daoris-driver sessions say <id> "…" [--file <path>]… [--level ordinary|urgent|now] [--now]
               daoris-driver sessions take-back <id> [<word>]  ·  sessions reword <id> <word> "…"
               daoris-driver sessions go-on-new <id>
               daoris-driver sessions start-from <id>
               daoris-driver sessions hand-over <id> --agent <adapter> [--account <name>]
               daoris-driver sessions hand-over <id> --approve|--decline
               daoris-driver sessions helper <id> "<question>" [--agent <name>]
               daoris-driver sessions helper --approve|--decline <ask>
        """;

    /// <summary>The fields of a row in <c>--json</c>, in order: <c>SESSION_GROUPS</c>' row, field for field.</summary>
    public static IReadOnlyList<string> JsonFields { get; } =
        ["session", "group", "shown", "archived", "teammate", "strikes", "awaits", "awaitsOf", "work", "holdsQuest", "pausedBy", "holds", "deletable",
            "lands", "discards", "calling"];

    /// <summary>The fields of an entry in <c>--json</c>'s <c>entries</c>, in order: <c>SESSION_GROUPS</c>' entry, field for field (SESSONE1b).</summary>
    public static IReadOnlyList<string> JsonEntryFields { get; } = ["entry", "origin", "repository", "group", "leading", "parts"];

    /// <summary>The fields of one of an entry's parts, in order.</summary>
    public static IReadOnlyList<string> JsonPartFields { get; } = ["session", "quest", "shown", "group", "began", "after", "askedBy"];

    /// <summary>What the words ask, or null with what is wrong with them.</summary>
    public static SessionsAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        switch (args)
        {
            case ["stop", var id] when Id(id):
                return new SessionsAsk("stop") { Ids = [id] };
            case ["stop", var id, "--turn"] when Id(id):
                return new SessionsAsk("stop-turn") { Ids = [id] };
            case ["stop", ..]:
                problem = "`stop` takes one session's id, and `--turn` to stop only its turn so what waits goes next.";
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
            case ["take-back", var id] when Id(id):
                return new SessionsAsk("take-back") { Ids = [id] };
            case ["take-back", var id, var word] when Id(id) && Id(word):
                return new SessionsAsk("take-back") { Ids = [id], Word = word.Trim() };
            case ["take-back", ..]:
                problem = "`take-back` takes one session's id, then the word to take back; with no word it lists those waiting. "
                    + "To put new words in its place, `reword <id> <word> \"…\"`.";
                return null;
            case ["reword", var id, var word, _, ..] when Id(id) && Id(word) && string.Join(' ', args.Skip(3)).Trim().Length > 0:
                return new SessionsAsk("reword") { Ids = [id], Word = word.Trim(), Text = string.Join(' ', args.Skip(3)).Trim() };
            case ["reword", ..]:
                problem = "`reword` takes one session's id, the word to reword, then the words to put in its place.";
                return null;
            case ["go-on-new", var id] when Id(id):
                return new SessionsAsk("go-on-new") { Ids = [id] };
            case ["go-on-new", ..]:
                problem = "`go-on-new` takes one session's id.";
                return null;
            case ["start-from", var id] when Id(id):
                return new SessionsAsk("start-from") { Ids = [id] };
            case ["start-from", ..]:
                problem = "`start-from` takes one session's id.";
                return null;
            case ["hand-over", var id, "--agent", var agent] when Id(id) && Id(agent):
                return new SessionsAsk("hand-over") { Ids = [id], Agent = agent };
            case ["hand-over", var id, "--agent", var agent, "--account", var account] when Id(id) && Id(agent) && Id(account):
                return new SessionsAsk("hand-over") { Ids = [id], Agent = agent, Account = account };
            // MIXAGENT1c: the person's yes or no to the agent's own ask to hand its session over.
            case ["hand-over", var id, "--approve"] when Id(id):
                return new SessionsAsk("hand-over-approve") { Ids = [id] };
            case ["hand-over", var id, "--decline"] when Id(id):
                return new SessionsAsk("hand-over-decline") { Ids = [id] };
            case ["hand-over", ..]:
                problem = "`hand-over` takes one session's id, `--agent <adapter>` and, where one is wanted, `--account <name>`; "
                    + "or `--approve` or `--decline` for its agent's own ask to hand it over.";
                return null;
            case ["helper", ..]:
                return SessionsHelperCommand.Read(args, out problem);
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

    /// <summary>
    /// <c>say &lt;id&gt; "…" [--file &lt;path&gt;]… [--level ordinary|urgent|now] [--now]</c>: the words after the id joined by a space,
    /// each path after its flag, and the level after its own (COWORK1a); <c>--now</c> is <c>--level now</c> (STEER2c).
    /// </summary>
    private static SessionsAsk? ReadSay(IReadOnlyList<string> args, out string? problem)
    {
        problem = "`say` takes one session's id, then your words, `--file <path>` for each file you give with them, and "
            + "`--level ordinary|urgent|now` for when it reads them (`--now` is `--level now`).";
        if (args.Count < 2 || !Id(args[1])) return null;

        var words = new List<string>();
        var files = new List<string>();
        string? level = null;
        for (var at = 2; at < args.Count; at++)
        {
            if (args[at] == "--now")
            {
                level = MessageLevels.Now;
                continue;
            }

            if (args[at] == "--level")
            {
                if (at + 1 >= args.Count || MessageLevels.Read(args[at + 1]) is null)
                {
                    problem = "`--level` is when it reads your words: ordinary, urgent or now.";
                    return null;
                }

                level = args[++at];
                continue;
            }

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
        return new SessionsAsk("say") { Ids = [args[1]], Text = text, Files = files, Level = level, Now = level == MessageLevels.Now };
    }

    public static async Task<int> RunAsync(SessionsAsk ask, SessionsWorld world, TextWriter output, CancellationToken ct = default) =>
        ask.Verb switch
        {
            "say" => await SayThroughAsync(world, ask, output, ct).ConfigureAwait(false),
            "take-back" or "reword" when ask.Word is not null => await ThroughAsync(
                world, PersonPresses.SessionQueueEdit, QueueEditBody(ask), () => TakeBackAsync(world, ask, output, ct), output, ct).ConfigureAwait(false),
            "take-back" or "reword" => await TakeBackAsync(world, ask, output, ct).ConfigureAwait(false),
            "go-on-new" => await ThroughAsync(
                world, PersonPresses.SessionGoOnNew, Body(("id", ask.Ids[0])), () => GoOnNewAsync(world, ask.Ids[0], output, ct), output, ct).ConfigureAwait(false),
            "start-from" => await ThroughAsync(
                world, PersonPresses.SessionStartFrom, Body(("id", ask.Ids[0])), () => StartFromAsync(world, ask.Ids[0], output, ct), output, ct).ConfigureAwait(false),
            "hand-over" => await ThroughAsync(
                world, PersonPresses.SessionHandOver, Body(("id", ask.Ids[0]), ("agent", ask.Agent), ("account", ask.Account)),
                () => HandOverAsync(world, ask, output, ct), output, ct).ConfigureAwait(false),
            "hand-over-approve" => await ThroughAsync(
                world, PersonPresses.SessionHandOverApprove, Body(("session", ask.Ids[0])),
                () => HandOverAskWord.RunAsync(world, ask, output), output, ct).ConfigureAwait(false),
            "hand-over-decline" => await ThroughAsync(
                world, PersonPresses.SessionHandOverDecline, Body(("session", ask.Ids[0])),
                () => HandOverAskWord.RunAsync(world, ask, output), output, ct).ConfigureAwait(false),
            "helper" => await ThroughAsync(
                world, PersonPresses.SessionHelperAsk, Body(("id", ask.Ids[0]), ("question", ask.Text), ("agent", ask.Agent)),
                () => SessionsHelperCommand.RunAsync(world, ask, output, ct), output, ct).ConfigureAwait(false),
            "helper-approve" => await ThroughAsync(
                world, PersonPresses.SessionHelperApprove, Body(("ask", ask.Ids[0])),
                () => SessionsHelperCommand.RunWordAsync(world, ask, output), output, ct).ConfigureAwait(false),
            "helper-decline" => await ThroughAsync(
                world, PersonPresses.SessionHelperDecline, Body(("ask", ask.Ids[0])),
                () => SessionsHelperCommand.RunWordAsync(world, ask, output), output, ct).ConfigureAwait(false),
            "stop" => await ThroughAsync(
                world, PersonPresses.SessionStop, Body(("id", ask.Ids[0])), () => StopAsync(world, ask.Ids[0], output, ct), output, ct).ConfigureAwait(false),
            "stop-turn" => await ThroughAsync(
                world, PersonPresses.SessionStopTurn, Body(("id", ask.Ids[0]), ("keep", true)), () => StopTurnAsync(world, ask.Ids[0], output, ct), output, ct)
                .ConfigureAwait(false),
            "finish" or "decline" => await ThroughAsync(
                world, PersonPresses.SessionResolve,
                Body(("id", ask.Ids[0]), ("state", ask.Verb == "finish" ? "completed" : "declined"), ("note", ask.Note)),
                () => ResolveAsync(world, ask, output, ct), output, ct).ConfigureAwait(false),
            "archive" when ask.Ended => await ArchiveEndedAsync(world, ask.Yes, output, ct).ConfigureAwait(false),
            "archive" => await ArchiveAsync(world, ask.Ids, output, ct).ConfigureAwait(false),
            "unarchive" => await UnarchiveAsync(world, ask.Ids, output, ct).ConfigureAwait(false),
            "delete" => await ThroughAsync(
                world, PersonPresses.SessionDelete, Body(("id", ask.Ids[0])), () => DeleteAsync(world, ask.Ids[0], output, ct), output, ct).ConfigureAwait(false),
            _ => await ListAsync(world, ask, output, ct).ConfigureAwait(false),
        };

    // ——— The person's presses through the running Daoris (RPC1d, the terminal RPC design §7.2): each is sent to the instance, which
    // shows it in its window and runs it once on the person's Confirm, the verb saying it waits and then the lines the route answers
    // (`said`, the alone path's own words); alone where no instance runs, as before. A verb that holds a request-folder wait keeps
    // that wait on its alone path, and the instance runs the same path itself.

    /// <summary>A press's payload: the route's own fields beside <c>said: true</c>, which asks for the verb's lines; an absent value is left out.</summary>
    private static Dictionary<string, object?> Body(params (string Name, object? Value)[] fields)
    {
        var body = new Dictionary<string, object?>();
        foreach (var (name, value) in fields)
        {
            if (value is not null) body[name] = value;
        }

        body["said"] = true;
        return body;
    }

    /// <summary>A take-back's or a reword's payload: the record's word under its session, the new words only for a reword.</summary>
    private static Dictionary<string, object?> QueueEditBody(SessionsAsk ask) =>
        Body(("session", ask.Ids[0]), ("id", ask.Word), ("text", ask.Verb == "reword" ? ask.Text : null));

    private static async Task<int> ThroughAsync(
        SessionsWorld world, InstanceRoute route, object payload, Func<Task<int>> alone, TextWriter output, CancellationToken ct) =>
        world.Instance is { } instance
            ? await InstanceDoor.PressAsync(route, payload, alone, TreesSaid.Words(output, instance.Error, "sessions"), instance, ct).ConfigureAwait(false)
            : await alone().ConfigureAwait(false);

    /// <summary>
    /// <c>say</c> through the instance: a file named is judged here first, where the person named it, as the alone path judges it; its
    /// full path is sent, which the Daoris on this machine reads as <see cref="SayAsync"/> does.
    /// </summary>
    private static async Task<int> SayThroughAsync(SessionsWorld world, SessionsAsk ask, TextWriter output, CancellationToken ct)
    {
        if (world.Instance is null) return await SayAsync(world, ask, output, ct).ConfigureAwait(false);

        var paths = new List<string>();
        foreach (var path in ask.Files)
        {
            var full = Path.GetFullPath(path);
            if (!File.Exists(full))
            {
                output.WriteLine($"sessions: `{path}` is not a file on this machine.");
                return 2;
            }

            paths.Add(full);
        }

        return await ThroughAsync(
            world, PersonPresses.SessionSay,
            Body(("id", ask.Ids[0]), ("text", ask.Text ?? ""), ("paths", paths.Count > 0 ? paths : null), ("now", ask.Now ? true : null), ("level", ask.Level)),
            () => SayAsync(world, ask, output, ct), output, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The written form of one listing, as <c>SESSION_GROUPS</c> answers it: <c>{"sessions": [...]}</c>, and with
    /// <paramref name="entries"/> the entries beside the rows (SESSONE1b), which stay what they were.
    /// </summary>
    public static string Json(IEnumerable<SessionGrouping> rows, IEnumerable<SessionEntry>? entries = null)
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
                    writer.WriteString("branch", work.Branch);
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

                // MSG1f2 (D137 §3.2): what holds the person's words on a record that resumes later.
                if (row.Holds is { } holds)
                {
                    writer.WriteStartObject("holds");
                    writer.WriteString("why", holds.Why);
                    writer.WriteString("reason", holds.Reason);
                    writer.WriteString("repository", holds.Repository);
                    if (holds.Until is { } until) writer.WriteString("until", until); else writer.WriteNull("until");
                    writer.WriteEndObject();
                }
                else
                {
                    writer.WriteNull("holds");
                }

                writer.WriteBoolean("deletable", row.Deletable);
                // LAND4: what its own tree offers to land, whatever group it rests in.
                if (row.Lands is { } lands)
                {
                    writer.WriteStartObject("lands");
                    writer.WriteString("branch", lands.Branch);
                    writer.WriteString("tree", lands.Tree);
                    writer.WriteNumber("commits", lands.Commits);
                    if (lands.Uncommitted is { } paths) writer.WriteNumber("uncommitted", paths); else writer.WriteNull("uncommitted");
                    writer.WriteEndObject();
                }
                else
                {
                    writer.WriteNull("lands");
                }

                // SQUASHTIDY1b: where the line holds its commits by content, its discard instead, with what the page says.
                if (row.Discards is { } discards)
                {
                    writer.WriteStartObject("discards");
                    writer.WriteString("branch", discards.Branch);
                    writer.WriteString("tree", discards.Tree);
                    writer.WriteString("says", discards.Says);
                    writer.WriteString("keeps", discards.Work.Keeps);
                    writer.WriteString("keptAt", discards.KeptAt);
                    writer.WriteEndObject();
                }
                else
                {
                    writer.WriteNull("discards");
                }

                // WAITSAY1b: the call a working session's agent is inside, where the process that read the rows heard one; AUTONOMY1b3b:
                // whether it waits on the person's answer to its permission request.
                if (row.Calling is { } call)
                {
                    writer.WriteStartObject("calling");
                    writer.WriteString("title", call.Title);
                    writer.WriteString("since", call.Since);
                    writer.WriteBoolean("waiting", call.Waiting);
                    writer.WriteEndObject();
                }
                else
                {
                    writer.WriteNull("calling");
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            if (entries is not null)
            {
                writer.WriteStartArray("entries");
                foreach (var entry in entries)
                {
                    writer.WriteStartObject();
                    writer.WriteString("entry", entry.Key);
                    writer.WriteString("origin", entry.Origin);
                    writer.WriteString("repository", entry.Repository);
                    writer.WriteString("group", entry.Group);
                    writer.WriteString("leading", entry.Leading);
                    writer.WriteStartArray("parts");
                    foreach (var part in entry.Parts)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("session", part.Session);
                        writer.WriteString("quest", part.Quest);
                        writer.WriteString("shown", part.Shown);
                        writer.WriteString("group", part.Group);
                        writer.WriteString("began", part.Began);
                        writer.WriteString("after", part.After);
                        writer.WriteString("askedBy", part.AskedBy);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n");
    }

    // ——— The listing.

    private static async Task<int> ListAsync(SessionsWorld world, SessionsAsk ask, TextWriter output, CancellationToken ct)
    {
        // RPCOPEN1: the running Daoris's own look where it answers, its lines made by the function below; this process's look otherwise.
        if (world.Instance is { } instance && await InstanceDoor.TryReadAsync(
                OpenRoutes.SessionGroups, new { group = ask.Group, repository = ask.Repository, json = ask.Json, said = true },
                TreesSaid.ReadWords(output, instance.Error), instance, ct).ConfigureAwait(false) is { } viaInstance)
        {
            return viaInstance;
        }

        // A fresh plan, which sees no cool-off: the look reads the home's for words resumed on an account (MSG1f3).
        var look = await SessionGroups.LookAsync(world.Service, world.Config, world.Door, lastLook: null, world.Home, ct: ct, coolingOf: world.CoolingOf)
            .ConfigureAwait(false);
        foreach (var line in ListLines(look, ask, world.Home, DateTimeOffset.UtcNow)) output.WriteLine(line);
        return 0;
    }

    /// <summary>
    /// The listing's lines from one look (RPCOPEN1, the terminal RPC design §7.3): what <c>sessions</c> prints, which the instance's
    /// <c>SESSION_GROUPS</c> answers as <c>said</c> from its own look, so the verb that asks it prints the same words. The <c>--json</c>
    /// form is one element, whole.
    /// </summary>
    public static IReadOnlyList<string> ListLines(SessionLook look, SessionsAsk ask, string home, DateTimeOffset now)
    {
        var output = new List<string>();
        var records = look.Records.GroupBy(record => record.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var rows = SessionGroups.Read(look)
            .Where(row => ask.Group is null || row.Group == ask.Group)
            .Where(row => ask.Repository is null
                || (records.TryGetValue(row.Session, out var record) && string.Equals(record.Repository, ask.Repository, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        // SESSONE1b (D167): by entry, the rows kept for the page and for --json. An entry is in the group, and the repository, it is.
        var entries = SessionGroups.ReadEntries(look)
            .Where(entry => ask.Group is null || entry.Group == ask.Group)
            .Where(entry => ask.Repository is null || string.Equals(entry.Repository, ask.Repository, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (ask.Json)
        {
            output.Add(Json(rows, entries));
            return output;
        }

        if (entries.Count == 0)
        {
            output.Add($"sessions: {Nothing(ask)}.");
            return output;
        }

        var quests = look.Quests.GroupBy(quest => quest.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var openings = new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions))
            .Openings(entries.Select(entry => entry.Leading).Where(SessionEventText.IsId));
        var placed = SessionGroups.Read(look).GroupBy(row => row.Session, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var group in entries.GroupBy(entry => entry.Group))
        {
            var each = group.ToList();
            output.Add($"{GroupName(group.Key)} ({each.Count})");
            foreach (var entry in each)
            {
                var record = records[entry.Leading];
                var row = placed[entry.Leading];
                var line = string.Join(" · ", new[] { Title(record, quests, openings), Span(record, now) }.Concat(Facts(row, record))
                    .Concat(entry.Parts.Count > 1 ? [$"{entry.Parts.Count} parts"] : []));
                output.Add($"  {entry.Leading}  {Word(row.Shown)}  {record.Repository} — {line}");
                if (entry.Parts.Count < 2) continue;
                foreach (var part in entry.Parts)
                {
                    output.Add($"      {part.Session}  {Word(part.Shown)}  {(part.Quest is { } quest ? $"#{quest}" : "conversation")} · {Began(part)}");
                }
            }
        }

        return output;
    }

    /// <summary>How a part began, in the terminal's words (D167): what it followed or who asked; a word this build does not know, opened.</summary>
    internal static string Began(EntryPart part) => part.Began switch
    {
        PartBegan.Retried => $"retried after {part.After}",
        PartBegan.Resumed => $"resumed after {part.After}",
        PartBegan.CarriedOn => part.After is { } after ? $"carried on after {after}" : "carried on",
        PartBegan.HandedOver => $"handed over from {part.After}",
        // SESSCTX1b: a part moved on from one whose own conversation was lost, as the page's `work.parts.began.moved` says it.
        PartBegan.Moved => $"moved on from {part.After}: its own conversation was lost",
        PartBegan.Asked => part.AskedBy is { } by ? $"asked by {by}" : "asked by a sibling",
        _ => "opened",
    };

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
        ShownState.GoingOn => "going on",
        _ => shown,
    };

    /// <summary>What a session is for: its quest's name (SESSUX1j), a conversation's opening line, Ask Daoris, an ask's intake.</summary>
    private static string Title(SessionRecord record, IReadOnlyDictionary<string, QuestView> quests, IReadOnlyDictionary<string, string> openings) =>
        record.Quest is { } quest ? quests.TryGetValue(quest, out var view) ? view.Name : $"#{quest}"
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
    internal static IEnumerable<string> Facts(SessionGrouping row, SessionRecord record)
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

        // LAND4: commits its own tree holds that no branch of the person's does, whatever its ending, with the door that lands them.
        if (row.Lands is { } lands)
        {
            yield return $"{lands.Commits} commit{(lands.Commits == 1 ? "" : "s")} on {lands.Branch} in its tree {lands.Tree}, not landed: "
                + $"daoris-driver trees land {row.Session}";
        }

        // SQUASHTIDY1b: commits the line holds by content are never offered to land again; the door that discards the tree is,
        // with the ref its commits stay at, or why the tree stays.
        if (row.Discards is { Work: var held } discards)
        {
            yield return held.Keeps is { } keeps
                ? $"{held.Held.Said}, in its tree {discards.Tree}: daoris-driver trees remove {row.Session} discards it, keeping its "
                  + $"commits at `{keeps}`"
                : $"{held.Held.Said}, in its tree {discards.Tree}, which stays: {held.Stays}";
        }

        if (row.HoldsQuest && record.Quest is { } quest) yield return $"held here until you try again: daoris driver retry {quest} --session {row.Session}";
        // PAUSE1b (D132 §6.1): a pause, never a hold, in its words: Resume is the press that moves it.
        if (row.PausedBy is { } pause)
        {
            yield return pause.Scope == WorkScope.Ask
                ? $"paused with ask #{pause.Id}; Resume carries it on: {pause.Door}"
                : $"paused with quest #{pause.Id}; Resume carries it on: {pause.Door}";
        }

        // MSG1f2 (D137 §3.2): what holds the person's words, in the words `sessions say` prints for them; a pause's is the line above.
        if (row.Holds is { Why: not WordsHold.Paused } holds) yield return $"held: {holds.Sentence}";
        if (row.Archived && row.Group != SessionGroup.Archived) yield return "archived";
    }

    // ——— Stop, finish and decline.

    /// <summary>
    /// <c>stop &lt;id&gt; --turn</c>, the keeping stop (COWORK1a, the message-urgency design §3.2, §8.6): the session's turn stopped so
    /// what waits goes next, through the Daoris whose loop holds its turns, and what it came to said. Only that Daoris holds a turn,
    /// so alone, with no seam, the verb could not: exit 2. A door with no stop that keeps the session, or no turn to stop, is a
    /// policy answer, exit 1; a door that carries only text refuses in the driver's words.
    /// </summary>
    private static async Task<int> StopTurnAsync(SessionsWorld world, string id, TextWriter output, CancellationToken ct)
    {
        if (world.StopTurn is not { } stopTurn)
        {
            output.WriteLine(
                $"sessions: the Daoris that runs {id} holds its turns and did not answer here; stop its turn in Daoris's window, beside the words that wait.");
            return 2;
        }

        string came;
        try
        {
            came = await stopTurn(id, ct).ConfigureAwait(false);
        }
        catch (DriverException refused)
        {
            output.WriteLine($"sessions: {refused.Message}");
            return 1;
        }

        output.WriteLine(came switch
        {
            KeepingStops.Stopped => $"sessions: stopping {id}'s turn: what waits goes next, and what it has done stays.",
            KeepingStops.NoInterrupt =>
                $"sessions: {id}'s door cannot stop a turn and keep the session, so nothing was stopped: what waits goes when its turn ends, "
                + $"and `daoris-driver sessions stop {id}` ends the session.",
            KeepingStops.NotHere => $"sessions: nothing here runs {id}, so it has no turn to stop.",
            _ => $"sessions: {id} has no turn running with words waiting for its end, so nothing was stopped.",
        });
        return came == KeepingStops.Stopped ? 0 : 1;
    }

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
        int exit;
        if (world.Processes.AliveOnThisMachine(id))
        {
            exit = await AskAsync(world, record, new SessionRequest(id, move, DateTimeOffset.UtcNow) { Note = ask.Note, Parked = true }, past, output, ct)
                .ConfigureAwait(false);
        }
        else
        {
            try
            {
                var said = await SessionMoves.ResolveAsync(_ => false, world.Service, id, state, ask.Note, ct).ConfigureAwait(false);
                output.WriteLine($"sessions: {past} {id}: {said}");
                exit = 0;
            }
            catch (DriverException refused)
            {
                output.WriteLine($"sessions: {refused.Message}");
                return 1;
            }
        }

        if (exit == 0 && move == SessionMove.Finish) await StillOpenAsync(world, record, output, ct).ConfigureAwait(false);
        return exit;
    }

    /// <summary>
    /// A finish at a checkpoint leaves its quest as it was (QUESTCLOSE1, D126's note): where that is still open or taken, said
    /// right after it with the door that closes it as the person's, since nothing here carries a finished session's quest on.
    /// A quest that cannot be read says nothing: the finish stood, and <c>quest done</c> answers for itself.
    /// </summary>
    private static async Task StillOpenAsync(SessionsWorld world, SessionRecord record, TextWriter output, CancellationToken ct)
    {
        if (record.Quest is not { } id) return;
        QuestView? quest;
        try
        {
            quest = await world.Service.FindQuestAsync(id, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            return;
        }

        if (quest?.Status is "Open" or "Taken")
        {
            output.WriteLine(
                $"  its quest #{quest.Id} is still {quest.Status.ToLowerInvariant()}: daoris-driver quest done {quest.Id} [--note \"…\"] "
                + "marks it done as yours.");
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
        var waited = Waited.From(world);
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
        var waited = Waited.From(world);

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

            uploads.Add(new ChatUpload(Path.GetFileName(full), await File.ReadAllBytesAsync(full, ct).ConfigureAwait(false))); // read-shares: the person's own file
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
        var next = WordsNever.ContinuedIn(json, id);
        if (WordsNever.Judge(record, last, next) is { } never)
        {
            output.WriteLine($"sessions: {Refusal(never, id, record, last, null, next)}");
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

        var (held, taken) = await AskLoopAsync(world, record, text, files, waited, ask, ct).ConfigureAwait(false);
        if (held is not null) return await SaidAsync(world, record, held, waited, ask.Level, output, ct).ConfigureAwait(false);
        if (taken)
        {
            output.WriteLine(
                $"sessions: the loop that runs {id} took your words, and gave no answer of where they stand; "
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
    /// A say asked of the loops on this home and waited for, until its answer or the wait's end (§5.2); one a loop took by then
    /// is waited for until that loop answers it (FLAKE3). A session winding up is asked again until its record has ended, since
    /// the say door keeps nothing for a record still running. Null and not taken where nothing took it, which is withdrawn so
    /// no loop acts on it after the person was told.
    /// </summary>
    private static async Task<(WordsHeld? Held, bool Taken)> AskLoopAsync(
        SessionsWorld world, SessionRecord record, string text, IReadOnlyList<string> files, Waited waited, SessionsAsk ask, CancellationToken ct)
    {
        var requests = new SessionRequests(world.Home);
        WordsHeld? winding = null;
        while (true)
        {
            // The level beside `now`, which a say at now still writes for a loop from before COWORK1a.
            var request = new SessionRequest(record.Id, SessionMove.Say, DateTimeOffset.UtcNow)
            {
                Text = text, Files = files, Key = SessionRequests.NewKey(), Now = MessageLevels.Asked(ask.Level, ask.Now) == MessageLevel.Now,
                Level = ask.Level,
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
                if (requests.Withdraw(request)) return (winding, winding is not null);

                // 🔴 Taken (FLAKE3): the loop that took it answers it whatever became of the words, a failure included, so that
                // answer is waited for rather than the clock read. Read once at the wait's end, a loop under load was said to
                // have taken the words and not said where they stood while it was writing its answer.
                return (await AnsweredAsync(world, requests, request, ct).ConfigureAwait(false) ?? winding, true);
            }

            if (held.Why != WordsHeld.Running || waited.Elapsed >= world.Wait) return (held, true);
            winding = held;
        }
    }

    /// <summary>
    /// The answer of a loop that took a say (FLAKE3), waited for while a loop holds the home and within that loop's own bound on
    /// a say (<see cref="SessionsWorld.Answer"/>); null where none came, as from a loop that went away with the words.
    /// </summary>
    private static async Task<WordsHeld?> AnsweredAsync(
        SessionsWorld world, SessionRequests requests, SessionRequest request, CancellationToken ct)
    {
        var answering = Waited.From(world);
        while (true)
        {
            if (requests.AnswerOf(request) is { } held) return held;
            // Read once more as it gives up, for an answer written as its loop let go of the home.
            if (answering.Elapsed >= world.Answer || !world.LoopRuns()) return requests.AnswerOf(request);
            await Task.Delay(world.Poll, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Where the words stand, in one line, from the loop's answer (§5.2); words kept on the record are followed. The level asked
    /// (COWORK1a) is named where the door could not honour it, with the reason (the message-urgency design §8.6).
    /// </summary>
    private static async Task<int> SaidAsync(
        SessionsWorld world, SessionRecord record, WordsHeld held, Waited waited, string? level, TextWriter output, CancellationToken ct)
    {
        switch (held)
        {
            // STEER2c: what the door did with words at now, in the terminal's words (D136's 2026-10-16 note). No door's turn is
            // stopped for them (D188 point 3): its turn stopping retired with COWORK1a.
            case { Sent: true, Now: true, Reaches: "now" }:
                output.WriteLine("sessions: sent now: it took this at once, cutting its step in flight.");
                return 0;
            case { Sent: true, Why: SendNowWhy.NoInterrupt or SendNowWhy.Opening or SendNowWhy.Behind }:
                var nextStep = held.Reaches == "next-step";
                var reason = held.Why switch
                {
                    SendNowWhy.NoInterrupt when nextStep => "its agent cannot be steered mid-step",
                    SendNowWhy.NoInterrupt => "its door takes words only when its turn ends",
                    SendNowWhy.Opening => "its first prompt has not left yet",
                    _ => "words ahead of these are still on their way",
                };
                output.WriteLine($"sessions: not sent now: {reason}; it reads this {(nextStep ? "at its next step" : "when its turn ends")}.");
                return 0;
            case { Sent: true, Reaches: "turn-end" } when level == MessageLevels.Urgent:
                output.WriteLine("sessions: held, not urgent: its door takes words only when its turn ends; it reads this then.");
                return 0;
            case { Sent: true, Now: true, Reaches: not "resume" }:
                output.WriteLine("sessions: sent now: it takes this as its next turn.");
                return 0;
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
        SessionsWorld world, SessionRecord record, string? word, Waited waited, TextWriter output, CancellationToken ct)
    {
        var events = new SessionEvents(DaorisHome.Entry(world.Home, HomeEntry.Sessions));
        var gone = false;
        while (word is not null)
        {
            // The driver's note on these words, where it wrote one (MSG1b, MSG1d): where they went, or why they cannot go on.
            var note = events.Page(record.Id, limit: SessionEventText.MaxPage).Events
                .LastOrDefault(each => each.Kind == SessionEventKind.Note && each.Words?.Contains(word, StringComparer.Ordinal) == true);
            if (note is { To: { } to })
            {
                output.WriteLine($"sessions: went to session `{to}`, because {Because(note)}.");
                return 0;
            }

            if (note is not null)
            {
                output.WriteLine($"sessions: kept, but it cannot go on in this session, because {Because(note)}.");
                // MSG1f3: the terminal's *Start a conversation with these words*, which takes them off this session.
                output.WriteLine($"  start a conversation with them instead: daoris-driver sessions start-from {record.Id}");
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
            world.Fell?.Invoke($"the look failed: {error.GetType().Name}: {error.Message}");
            return Next;
        }

        if (verdict is null)
        {
            world.Fell?.Invoke($"the planner gave no verdict for quest {quest}");
            return Next;
        }

        // The listing's line for words that wait (MSG1f2): one sentence for both doors.
        if (WordsHold.Of(verdict, wait: null) is not { } holds)
        {
            world.Fell?.Invoke($"the planner's verdict for quest {quest} holds nothing: {verdict.Verdict}");
            return Next;
        }

        return $"held: {holds.Sentence}";
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
                : WordsNever.IsHelp(record)
                    // ASKHIST1: Ask Daoris's conversation goes on in the window, whose panel holds it.
                    ? $"sessions: {record.Id} is Ask Daoris's conversation, which goes on in the window: start Daoris, then say it again."
                    : $"sessions: {record.Id} is a conversation, and no driver runs on this machine to open it again: start the desktop, "
                      + "or `daoris-driver drive`, then say it again.");
            return loop ? 2 : 1;
        }

        var said = await world.Service.SayAsync(record.Id, text, files, ct).ConfigureAwait(false);
        if (said.Kept)
        {
            if (!loop && said.Word is { } kept)
            {
                new SessionEvents(DaorisHome.Entry(world.Home, HomeEntry.Sessions)).Keep(record.Id, LoopWords.Shown(kept, RequestDoor.Terminal), null);
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
    /// <param name="next">The conversation a conversation went on in (HANDWORDS1), where it was read; else its code says the sentence it came with.</param>
    private static string Refusal(string code, string id, SessionRecord? record, string? last, string? message, string? next = null) => code switch
    {
        WordsNever.Continued when next is not null => $"{WordsNever.WentOn(id, next).TrimEnd('.')}: daoris-driver sessions say {next} \"…\"",
        WordsNever.NotFound => $"no session here is {id}.",
        WordsNever.Teammate =>
            $"{id} ran on {(id.Contains('/') ? id[..id.IndexOf('/')] : "another machine")}, where its conversation is; nothing said here reaches it.",
        // Only a window from before ASKHIST1 answers it: Ask Daoris's conversations go on in themselves since.
        WordsNever.Help => $"{id} is Ask Daoris's own conversation, which the Daoris running here does not go on with: its panel opens a new one.",
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

    // ——— Take back and reword (SAIDBACK1a, D137 §2.4): words waiting on a record, before a session took them.

    /// <summary>
    /// One word waiting on a parked or ended record, taken back or reworded through the service's take-back door, which judges
    /// it; with no word named, the words waiting listed under their ids. Where no loop drives the home, the record's
    /// conversation settles the word it showed waiting with the withdrawn line (QUEUEEDIT1a's) and shows a reword's new words
    /// as the terminal's, as <see cref="KeepAsync"/> shows words it keeps; where a loop does, its record of the conversation may
    /// be open, so nothing is written there. Exit 0 done or listed, 1 refused (<c>taken</c> among them), 2 could not.
    /// </summary>
    private static async Task<int> TakeBackAsync(SessionsWorld world, SessionsAsk ask, TextWriter output, CancellationToken ct)
    {
        var id = ask.Ids[0];
        if (ask.Word is not { } word)
        {
            if (await world.Service.RecordAsync(id, ct).ConfigureAwait(false) is not { } record)
            {
                output.WriteLine($"sessions: no session of this machine's is {id}.");
                return 1;
            }

            var waiting = record.Waiting.Where(each => each.Id is not null).ToList();
            if (waiting.Count == 0)
            {
                output.WriteLine($"sessions: no words wait on {id}.");
                return 0;
            }

            output.WriteLine(
                $"sessions: {waiting.Count} word{(waiting.Count == 1 ? " waits" : "s wait")} on {id}; take one back with "
                + $"`daoris-driver sessions take-back {id} <word>`:");
            foreach (var each in waiting) output.WriteLine($"  {each.Id}  {OneLine(each.Text)}");
            return 0;
        }

        var text = ask.Verb == "reword" ? ask.Text : null;
        if (world.LoopRuns())
        {
            // SAIDBACK1c: the loop that holds the home writes the note into its own record of the conversation, which a page may
            // have open; so it is asked through the request folder, as a say is. One that takes none in time is withdrawn, and
            // the service judges it here, with nothing written.
            var held = await AskLoopTakeBackAsync(world, id, word, text, ct).ConfigureAwait(false);
            if (held is not null)
            {
                output.WriteLine($"sessions: {held.Message}");
                return held.Sent ? 0 : held.Why is null ? 2 : 1;
            }
        }

        var answer = await world.Service.TakeBackAsync(id, word, text, ct).ConfigureAwait(false);
        if (answer.Done && !world.LoopRuns())
        {
            var events = new SessionEvents(DaorisHome.Entry(world.Home, HomeEntry.Sessions));
            events.Keep(id, ChatTurns.Withdrawn(word), null);
            if (answer.Word is { } kept) events.Keep(id, LoopWords.Shown(kept, RequestDoor.Terminal), null);
        }

        output.WriteLine($"sessions: {answer.Message}");
        return answer.Done ? 0 : answer.Why is null ? 2 : 1;

        // A word's text on the listing's one line: its lines joined by a space, cut after a hundred characters.
        static string OneLine(string text)
        {
            var line = string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            return line.Length <= 100 ? line : line[..99] + "…";
        }
    }

    /// <summary>
    /// A take-back asked of the loops on this home and waited for (SAIDBACK1c), as <see cref="AskLoopAsync"/> waits for a say:
    /// the loop's answer, or null where none took it in time, when it is withdrawn so no loop acts on it after the verb went to
    /// the service itself. A loop that took it is waited for until it answers, within <see cref="SessionsWorld.Answer"/>, since the
    /// word is gone by then and asking the service again would only say it is no longer there.
    /// </summary>
    private static async Task<WordsHeld?> AskLoopTakeBackAsync(
        SessionsWorld world, string id, string word, string? text, CancellationToken ct)
    {
        var requests = new SessionRequests(world.Home);
        var request = new SessionRequest(id, SessionMove.TakeBack, DateTimeOffset.UtcNow)
        {
            Text = text, Word = word, Key = SessionRequests.NewKey(),
        };
        requests.Write(request);

        var waited = Waited.From(world);
        WordsHeld? held = null;
        while (held is null && waited.Elapsed < world.Wait)
        {
            await Task.Delay(world.Poll, ct).ConfigureAwait(false);
            held = requests.AnswerOf(request);
        }

        if (held is null && !requests.Withdraw(request)) held = await AnsweredAsync(world, requests, request, ct).ConfigureAwait(false);
        return held is null ? null : held with { Message = held.Message ?? (held.Sent ? $"took word `{word}` back." : $"word `{word}` was not taken back.") };
    }

    // ——— Go on in a new session (MSG1g, D137 §2.2): the screen's press, at a terminal.

    /// <summary>
    /// Words a resume holds while their account cools go on in a new session at the driver's next look (<see cref="GoOnNew"/>):
    /// one line, exit 0 where the choice is kept, 1 where it is refused, 2 where it could not be kept.
    /// </summary>
    private static async Task<int> GoOnNewAsync(SessionsWorld world, string id, TextWriter output, CancellationToken ct)
    {
        var answer = await GoOnNew.AskAsync(
                world.Service, world.Home, world.CoolingOf ?? SessionGroups.CoolingOn(world.Home), id, DateTimeOffset.UtcNow, ct)
            .ConfigureAwait(false);
        output.WriteLine($"sessions: {answer.Message}");
        return answer.Sent ? 0 : answer.Why is null ? 2 : 1;
    }

    // ——— Start a conversation with these words (MSG1f3, D137 §2.2, D50): the screen's press, at a terminal.

    /// <summary>
    /// The terminal's own conversation, opened by the host's chat door with the words a session could not go on with; its exit
    /// is the conversation's. A world with no door to open one has nothing to start it in.
    /// </summary>
    private static async Task<int> StartFromAsync(SessionsWorld world, string id, TextWriter output, CancellationToken ct)
    {
        if (world.StartFrom is { } open) return await open(id, ct).ConfigureAwait(false);

        output.WriteLine($"sessions: no conversation can be opened from here; run `daoris-driver sessions start-from {id}` at a terminal.");
        return 2;
    }

    /// <summary>
    /// What the chat runner's start-from came to, in one line (<see cref="StartFrom.RunAsync"/>'s sentence): null where a
    /// conversation opened, which then runs on in this terminal, its words taken or left where they were; else the exit, 1, a
    /// refusal by its code or a start the runner refused, as <c>chat</c>'s refusals are.
    /// </summary>
    public static int? Started(StartedFrom started, TextWriter output)
    {
        output.WriteLine($"sessions: {started.Message}");
        return started.SessionId is null ? 1 : null;
    }

    // ——— Hand a conversation over (MIXAGENT1a-i, D163, D50): the page's press follows with MIXAGENT1b.

    /// <summary>
    /// A conversation goes on with another agent: opened by the host's chat door, as start-from's is, since a conversation lives in
    /// the shell's chat runner; its exit is the door's. A world with no door has nothing to hand it to.
    /// </summary>
    private static async Task<int> HandOverAsync(SessionsWorld world, SessionsAsk ask, TextWriter output, CancellationToken ct)
    {
        // A quest's own session (MIXAGENT1a-ii, D163 point 1) is no conversation: it opens none here. Judged and kept as a box the
        // driver's next look takes, which stops it and carries its quest on in the same tree with the agent named.
        var record = (await world.Service.SessionRecordsAsync(ct).ConfigureAwait(false))
            .FirstOrDefault(each => string.Equals(each.Id, ask.Ids[0], StringComparison.Ordinal));
        if (record is not null && QuestHandOver.IsQuestSession(record))
        {
            var roster = world.Drives ?? DrivesBy(world.Home);
            var answer = await QuestHandOver.AskAsync(
                world.Service, world.Home, roster, ask.Ids[0], ask.Agent!, ask.Account, HandOverWhy.Press, HandOverBy.Person,
                DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
            output.WriteLine($"sessions: {answer.Message}");
            if (answer.Kept && !world.LoopRuns()) output.WriteLine("sessions: no driver is running on this home now: its look takes it when one does.");
            return answer.Kept ? 0 : 1;
        }

        if (world.HandOver is { } open) return await open(ask.Ids[0], ask.Agent!, ask.Account, ct).ConfigureAwait(false);

        output.WriteLine($"sessions: nothing here can hand a conversation over; run `daoris-driver sessions hand-over {ask.Ids[0]} --agent {ask.Agent}` at a terminal.");
        return 2;
    }

    /// <summary>This build's adapters with the home's plugins, as the loop lays them for a look (D64): what a terminal with no roster of its own judges by.</summary>
    private static Func<string, HandOverAgent?> DrivesBy(string home)
    {
        var built = AdapterSet.Built();
        return QuestHandOver.Roster(built.WithPlugins(PluginCatalog.Load(home, built.Names)), home);
    }

    /// <summary>
    /// What the chat runner's hand-over came to, in one line: null where the new conversation opened, which then runs on in this
    /// terminal; else the exit, 0 where the ask is kept for the conversation's turn to end, 1 where it is refused.
    /// </summary>
    public static int? HandedOver(HandedOver handed, TextWriter output)
    {
        output.WriteLine($"sessions: {handed.Message}");
        return handed.SessionId is not null ? null : handed.Kept ? 0 : 1;
    }

    // ——— Archive, unarchive and delete: the screen's owners.

    /// <summary>
    /// <c>archive &lt;id&gt;…</c>: the running Daoris's <c>SESSION_ARCHIVE</c> where one runs, a person's press its window confirms (RPC1c),
    /// said in the words its answer carries; this process's own archive where none runs. Both say <see cref="SessionArchive.ArchiveSaid"/>.
    /// </summary>
    private static async Task<int> ArchiveAsync(SessionsWorld world, IReadOnlyList<string> ids, TextWriter output, CancellationToken ct) =>
        world.Instance is { } instance
            ? await InstanceDoor.PressAsync(
                PersonPresses.SessionArchive, new { ids, archived = true, said = true }, () => ArchiveAloneAsync(world, ids, output, ct),
                TreesSaid.Words(output, instance.Error, "sessions"), instance, ct).ConfigureAwait(false)
            : await ArchiveAloneAsync(world, ids, output, ct).ConfigureAwait(false);

    private static async Task<int> ArchiveAloneAsync(SessionsWorld world, IReadOnlyList<string> ids, TextWriter output, CancellationToken ct)
    {
        var look = await SessionGroups.LookAsync(world.Service, world.Config, world.Door, lastLook: null, world.Home, ids, ct).ConfigureAwait(false);
        var answer = new SessionArchive(world.Home).Archive(
            ids, SessionGroups.Read(look, ids), [.. look.Records.Select(record => record.Id)], DateTimeOffset.UtcNow);
        var said = SessionArchive.ArchiveSaid(answer.Outcomes);
        said.Print(output, output);
        SessionArchive.Said(world.Log, answer.Outcomes.Count(outcome => outcome.Verdict == ArchiveVerdict.Archived), PluginEvents.Terminal);
        return said.Code;
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

    /// <summary><c>unarchive &lt;id&gt;…</c>: as <see cref="ArchiveAsync"/>, the same route with <c>archived: false</c> (RPC1c).</summary>
    private static async Task<int> UnarchiveAsync(SessionsWorld world, IReadOnlyList<string> ids, TextWriter output, CancellationToken ct) =>
        world.Instance is { } instance
            ? await InstanceDoor.PressAsync(
                PersonPresses.SessionArchive, new { ids, archived = false, said = true }, () => Task.FromResult(Unarchive(world, ids, output)),
                TreesSaid.Words(output, instance.Error, "sessions"), instance, ct).ConfigureAwait(false)
            : Unarchive(world, ids, output);

    private static int Unarchive(SessionsWorld world, IReadOnlyList<string> ids, TextWriter output)
    {
        var back = new SessionArchive(world.Home).Unarchive(ids);
        var said = SessionArchive.UnarchiveSaid(ids, back.NotArchived);
        said.Print(output, output);
        return said.Code;
    }

    /// <summary>The screen's owner at the terminal's door: 0 deleted, 1 refused, 2 deleted with a file the disk kept (SESSDEL1), as a clear's is.</summary>
    private static async Task<int> DeleteAsync(SessionsWorld world, string id, TextWriter output, CancellationToken ct)
    {
        var outcome = await new SessionDeletion(world.Home, world.Remover)
            .DeleteAsync(world.Service, id, PluginEvents.Terminal, world.Log, ct: ct).ConfigureAwait(false);
        output.WriteLine($"sessions: {outcome.Message}");
        return outcome.Verdict != DeleteVerdict.Deleted ? 1 : outcome.Stayed.Count > 0 ? 2 : 0;
    }

    private static string Sessions(int count) => $"{count.ToString(CultureInfo.InvariantCulture)} session{(count == 1 ? "" : "s")}";

    /// <summary>A word that can be a session's id or a repository's name: not a flag, not blank.</summary>
    private static bool Id(string word) => word.Trim().Length > 0 && !word.StartsWith('-');

    /// <summary>
    /// How long a verb has waited, read off <see cref="SessionsWorld.Clock"/> (FLAKE5): the machine's, or a test's that counts
    /// from the moment the loop took the words rather than from a loaded machine's late start.
    /// </summary>
    private readonly record struct Waited(TimeProvider Clock, long Since)
    {
        public static Waited From(SessionsWorld world) => new(world.Clock, world.Clock.GetTimestamp());

        public TimeSpan Elapsed => Clock.GetElapsedTime(Since);
    }
}
