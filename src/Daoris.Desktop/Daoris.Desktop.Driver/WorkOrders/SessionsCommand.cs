namespace Daoris.Driver;

/// <summary>
/// <c>daoris session</c>: Sessions' terminal door. It lists this machine's sessions by what they need, from
/// <see cref="SessionGroups"/>, the one reader <c>SESSION_GROUPS</c> hands the page; stops, finishes and declines a session;
/// and archives, unarchives and deletes through the owners the screen's routes call. Each verb group is its own type under
/// <c>Sessions/</c>; this keeps the usage, the dispatch and the doors callers use.
/// </summary>
/// <remarks>
/// <para><b>Stop, finish and decline reach a session another process runs through a request its loop honours</b>
/// (<see cref="SessionRequests"/>): the verb writes it, waits up to ten seconds for the record to move, and says what
/// happened, withdrawing a request nothing took. Where nothing on this machine runs the session, a parked one is moved by
/// the ledger directly and any other is ended as the screen's stop ends an orphan.</para>
///
/// <para><b>Say is what the screen's box says</b>: the words reach the loop that runs the session
/// through the same requests, a say of their own that the loop answers beside it; a loop keeps words to a session nothing here
/// runs on its record. Where no loop drives the home, a driven session's words are kept on its record for the next loop, and
/// a conversation's are refused, since nothing here could open it. What never goes on is judged first, by
/// <see cref="WordsNever"/>, before anything is asked. One line says where the words stand.</para>
///
/// <para><b>Start-from is the screen's *Start a conversation with these words*</b>: a conversation
/// in this terminal, as <c>chat</c> opens one, its first message the words a session could not go on with, which then leave
/// that session naming it, through the chat runner's own act (<see cref="ChatRunner.StartFromAsync"/>).</para>
///
/// <para>Exit codes are the family's: 0 done, listed, or the words taken or held · 1 refused, a policy answer · 2 could not,
/// the usage among them.</para>
/// </remarks>
[CommandGroup("session", "list", "stop", "finish", "decline", "archive", "unarchive", "delete", "say", "take-back", "reword",
    "start-from", "hand-over", "helper", "pass", "partner", "handback", "opinion", "supervise", "at-work", Nested = ["opinion: ask show stop anyway myself"])]
public static class SessionsCommand
{
    public const string Usage =
        """
        usage: daoris session [--group you|review|working|later|ended|archived] [--repository <name>] [--json]
               daoris session stop <id> [--turn]  ·  session finish <id> [--note "…"]  ·  session decline <id> --reason "…"
               daoris session archive <id>… | --ended [--yes]  ·  session unarchive <id>…  ·  session delete <id>
               daoris session say <id> "…" [--file <path>]… [--level ordinary|urgent|now]
               daoris session take-back <id> [<word>]  ·  session reword <id> <word> "…"
               daoris session start-from <id>
               daoris session hand-over <id> [--agent <adapter> [--account <name>]]
               daoris session hand-over <id> --approve|--decline
               daoris session helper <id> "<question>" [--agent <name>] [--after <ask>] [--to-me]
               daoris session helper --approve|--decline <ask>
               daoris session pass <ask>
               daoris session partner <id> "<piece>" --agent <name> [--paths <path>…]
               daoris session partner <id> --list  ·  session partner <piece> --stop
               daoris session supervise [<id>]  ·  session supervise <id> --question <qid> --generation <g> --choice <label>|--leave <bound> (--reason "…" --source <where>)…
               daoris session handback <session|quest> [--json]
        """;

    /// <summary>The fields of a row in <c>--json</c>, in order: <c>SESSION_GROUPS</c>' row, field for field.</summary>
    public static IReadOnlyList<string> JsonFields { get; } =
        ["session", "group", "shown", "archived", "teammate", "strikes", "awaits", "awaitsOf", "work", "holdsQuest", "pausedBy", "holds", "deletable",
            "lands", "discards", "calling"];

    /// <summary>The fields of an entry in <c>--json</c>'s <c>entries</c>, in order: <c>SESSION_GROUPS</c>' entry, field for field.</summary>
    public static IReadOnlyList<string> JsonEntryFields { get; } = ["entry", "origin", "repository", "group", "leading", "head", "opens", "parts"];

    /// <summary>The fields of one of an entry's parts, in order.</summary>
    public static IReadOnlyList<string> JsonPartFields { get; } = ["session", "quest", "shown", "group", "began", "after", "askedBy", "head"];

    /// <summary>What the words ask, or null with what is wrong with them.</summary>
    public static SessionsAsk? Read(IReadOnlyList<string> args, out string? problem) => SessionsReading.Read(args, out problem);

    public static async Task<int> RunAsync(SessionsAsk ask, SessionsWorld world, TextWriter output, CancellationToken ct = default) =>
        ask.Verb switch
        {
            "say" => await SessionsSaying.SayThroughAsync(world, ask, output, ct).ConfigureAwait(false),
            "take-back" or "reword" when ask.Word is not null => await SessionsPresses.ThroughAsync(
                world, PersonPresses.SessionQueueEdit, SessionsPresses.QueueEditBody(ask), () => SessionsQueueEdit.TakeBackAsync(world, ask, output, ct), output, ct)
                .ConfigureAwait(false),
            "take-back" or "reword" => await SessionsQueueEdit.TakeBackAsync(world, ask, output, ct).ConfigureAwait(false),
            // A conversation is held, never run here (RPC1g3, D238): the host's door sends the route with its hold.
            "start-from" => await SessionsGoingOn.StartFromAsync(world, ask.Ids[0], output, ct).ConfigureAwait(false),
            "hand-over" when !string.IsNullOrWhiteSpace(ask.Agent) => await SessionsGoingOn.HandOverToAsync(world, ask, output, ct).ConfigureAwait(false),
            "hand-over" => await SessionsPresses.ThroughAsync(
                world, PersonPresses.SessionHandOver, SessionsPresses.Body(("id", ask.Ids[0]), ("agent", ask.Agent), ("account", ask.Account)),
                () => SessionsGoingOn.HandOverAsync(world, ask, output, ct), output, ct).ConfigureAwait(false),
            "hand-over-approve" => await SessionsPresses.ThroughAsync(
                world, PersonPresses.SessionHandOverApprove, SessionsPresses.Body(("session", ask.Ids[0])),
                () => HandOverAskWord.RunAsync(world.Home, ask.Ids, ask.Verb, output), output, ct).ConfigureAwait(false),
            "hand-over-decline" => await SessionsPresses.ThroughAsync(
                world, PersonPresses.SessionHandOverDecline, SessionsPresses.Body(("session", ask.Ids[0])),
                () => HandOverAskWord.RunAsync(world.Home, ask.Ids, ask.Verb, output), output, ct).ConfigureAwait(false),
            "helper" => await SessionsPresses.ThroughAsync(
                world, PersonPresses.SessionHelperAsk,
                SessionsPresses.Body(("id", ask.Ids[0]), ("question", ask.Text), ("agent", ask.Agent), ("after", ask.After), ("toPerson", ask.ToMe ? true : null)),
                () => SessionsHelperCommand.RunAsync(world, ask, output, ct), output, ct).ConfigureAwait(false),
            // The press names the session that asked beside the ask; an ask this machine holds none of is the judge's to refuse, alone.
            "pass" when SessionsHelperCommand.AskerOf(world.Home, ask.Ids[0]) is { } asker => await SessionsPresses.ThroughAsync(
                world, PersonPresses.SessionHelperPass, SessionsPresses.Body(("ask", ask.Ids[0]), ("session", asker)),
                () => SessionsHelperCommand.RunPassAsync(world, ask, output), output, ct).ConfigureAwait(false),
            "pass" => await SessionsHelperCommand.RunPassAsync(world, ask, output).ConfigureAwait(false),
            "helper-approve" => await SessionsPresses.ThroughAsync(
                world, PersonPresses.SessionHelperApprove, SessionsPresses.Body(("ask", ask.Ids[0])),
                () => SessionsHelperCommand.RunWordAsync(world, ask, output), output, ct).ConfigureAwait(false),
            "helper-decline" => await SessionsPresses.ThroughAsync(
                world, PersonPresses.SessionHelperDecline, SessionsPresses.Body(("ask", ask.Ids[0])),
                () => SessionsHelperCommand.RunWordAsync(world, ask, output), output, ct).ConfigureAwait(false),
            "partner" => await SessionsPresses.ThroughAsync(
                world, PersonPresses.SessionPartnerAsk,
                SessionsPresses.Body(("id", ask.Ids[0]), ("piece", ask.Text), ("agent", ask.Agent), ("paths", ask.Paths.Count > 0 ? ask.Paths : null)),
                () => SessionsPartnerCommand.RunAsync(world, ask, output, ct), output, ct).ConfigureAwait(false),
            "at-work" => await AtWorkVerb.RunAsync(world, ask, output, ct).ConfigureAwait(false),
            "partner-list" => await SessionsPartnerCommand.ListAsync(world, ask.Ids[0], output).ConfigureAwait(false),
            // A file for the look to act on, written the same with or without a running Daoris.
            "partner-discard" => await SessionsPartnerCommand.ActAsync(world, ask.Ids[0], PartnerPersonActs.Discard, output).ConfigureAwait(false),
            "partner-hand-over" => await SessionsPartnerCommand.ActAsync(world, ask.Ids[0], PartnerPersonActs.HandOver, output).ConfigureAwait(false),
            // A read, so no press and never through the running Daoris.
            "handback" => await SessionsHandback.RunAsync(world, ask, output, ct).ConfigureAwait(false),
            // The supervisor's holder's verb, through the running Daoris alone.
            "supervise-list" or "supervise-print" or "supervise-answer" or "supervise-leave" or "supervise-course" => await SessionsSuperviseCommand.RunAsync(world, ask, output, ct).ConfigureAwait(false),
            // A piece's stop is its partner's record's stop, the same press: the driver reads it as the person's and keeps the work.
            "partner-stop" => SessionsPartnerCommand.Working(world, ask.Ids[0], output) is { } partner
                ? await SessionsPresses.ThroughAsync(
                    world, PersonPresses.SessionStop, SessionsPresses.Body(("id", partner)), () => SessionsEnding.StopAsync(world, partner, output, ct), output, ct)
                    .ConfigureAwait(false)
                : 1,
            "stop" => await SessionsPresses.ThroughAsync(
                world, PersonPresses.SessionStop, SessionsPresses.Body(("id", ask.Ids[0])),
                () => SessionsEnding.StopAsync(world, ask.Ids[0], output, ct), output, ct).ConfigureAwait(false),
            "stop-turn" => await SessionsPresses.ThroughAsync(
                world, PersonPresses.SessionStopTurn, SessionsPresses.Body(("id", ask.Ids[0]), ("keep", true)),
                () => SessionsEnding.StopTurnAsync(world, ask.Ids[0], output, ct), output, ct).ConfigureAwait(false),
            "finish" or "decline" => await SessionsPresses.ThroughAsync(
                world, PersonPresses.SessionResolve,
                SessionsPresses.Body(("id", ask.Ids[0]), ("state", ask.Verb == "finish" ? "completed" : "declined"), ("note", ask.Note)),
                () => SessionsEnding.ResolveAsync(world, ask, output, ct), output, ct).ConfigureAwait(false),
            "archive" when ask.Ended => await SessionsArchiving.ArchiveEndedAsync(world, ask.Yes, output, ct).ConfigureAwait(false),
            "archive" => await SessionsArchiving.ArchiveAsync(world, ask.Ids, output, ct).ConfigureAwait(false),
            "unarchive" => await SessionsArchiving.UnarchiveAsync(world, ask.Ids, output, ct).ConfigureAwait(false),
            "delete" => await SessionsPresses.ThroughAsync(
                world, PersonPresses.SessionDelete, SessionsPresses.Body(("id", ask.Ids[0])), () => SessionsArchiving.DeleteAsync(world, ask.Ids[0], output, ct), output, ct)
                .ConfigureAwait(false),
            _ => await SessionsListing.ListAsync(world, ask, output, ct).ConfigureAwait(false),
        };

    /// <summary>The written form of one listing, as <c>SESSION_GROUPS</c> answers it (see <see cref="SessionsJson.Json"/>).</summary>
    public static string Json(IEnumerable<SessionGrouping> rows, IEnumerable<SessionEntry>? entries = null) => SessionsJson.Json(rows, entries);

    /// <summary>The listing's lines from one look (see <see cref="SessionsListing.ListLines"/>).</summary>
    public static IReadOnlyList<string> ListLines(SessionLook look, SessionsAsk ask, string home, DateTimeOffset now) =>
        SessionsListing.ListLines(look, ask, home, now);

    /// <summary>How a part began, in the terminal's words.</summary>
    internal static string Began(EntryPart part) => SessionsListing.Began(part);

    /// <summary>What a row's second line says on the screen, in the terminal's words.</summary>
    internal static IEnumerable<string> Facts(SessionGrouping row, SessionRecord record) => SessionsListing.Facts(row, record);

    /// <summary>What the chat runner's start-from came to, in one line (see <see cref="SessionsGoingOn.Started"/>).</summary>
    public static int? Started(StartedFrom started, TextWriter output) => SessionsGoingOn.Started(started, output);

    /// <summary>This build's adapters with the home's plugins, as the loop lays them for a look.</summary>
    public static Func<string, HandOverAgent?> DrivesBy(string home) => SessionsGoingOn.DrivesBy(home);

    /// <summary>What the chat runner's hand-over came to, in one line (see <see cref="SessionsGoingOn.HandedOver"/>).</summary>
    public static int? HandedOver(HandedOver handed, TextWriter output) => SessionsGoingOn.HandedOver(handed, output);
}
