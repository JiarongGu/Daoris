using System.Globalization;

namespace Daoris.Driver;

/// <summary>What a terminal asked of finished history (HIST1d): a reading, or a clear's list, or with <see cref="Yes"/> its press.</summary>
/// <param name="Scope">What the words name: a workspace, one quest's work, one quest's failed sessions, or one ask's work.</param>
/// <param name="Id">The workspace, quest or ask named, without its <c>#</c>; null for a reading of every workspace a record names.</param>
public sealed record HistoryAsk(HistoryScope Scope, string? Id)
{
    /// <summary>A clear: its list, or with <see cref="Yes"/> its press. Without it, the reading (<c>history [--workspace]</c>).</summary>
    public bool Clear { get; init; }

    /// <summary>The second press, which clears what the list holds now, each unit judged again.</summary>
    public bool Yes { get; init; }

    /// <summary>The reading as <c>HISTORY_PLAN</c>'s answer, field for field.</summary>
    public bool Json { get; init; }
}

/// <summary>
/// <c>daoris-driver history</c>, <c>history clear --workspace</c>, <c>quest clear [--failed]</c> and <c>ask --clear</c> (HIST1d,
/// D153 point 7; the history-clearing design §6.2, D50): the terminal's door to <see cref="HistoryClearing"/>, which the screen's
/// <c>HISTORY_PLAN</c> and <c>HISTORY_CLEAR</c> call too, so a machine with no screen clears as the window does. In the library,
/// so its words are held by a test.
/// </summary>
/// <remarks>
/// <para><b>The reading</b> says, per workspace, what the home keeps of its finished work and what a clear would take, the units
/// kept by reason with the line that frees each, and its conversations that served no quest; then the home's left-over files and
/// machine log once. With no workspace it reads every workspace a record names, renamed or unregistered ones included (§2.4).
/// <c>--json</c> prints <c>HISTORY_PLAN</c>'s answer field for field, from the one projection both doors serialize
/// (<see cref="HistoryAnswers"/>).</para>
///
/// <para><b>Without <c>--yes</c> a clear is the first press</b>: it prints what it would take and what it keeps, each kept unit
/// in its keep's own sentence (the service's for the records' half, the driver's for this machine's), and changes nothing. With
/// it, it lists again and sends what may go, each unit judged again by both halves, at the door <c>terminal</c>, as
/// <c>trees clean --yes</c> does. Nothing to clear is information, never a refusal (D48 §6).</para>
///
/// <para>Exit codes are the family's: 0 done, listed or nothing to do · 1 a unit named was kept: one quest, one ask or one
/// quest's failed sessions, on its list or at its press, or no such quest or ask here; a workspace's units are not named one by
/// one, so one it keeps is information · 2 could not: a file the disk would not let go of, the usage, or a service that did not
/// answer (the host says its sentence).</para>
/// </remarks>
public static class HistoryCommand
{
    public const string Usage =
        """
        usage: daoris-driver history [--workspace <name>] [--json]
               daoris-driver history clear --workspace <name> [--yes]
               daoris-driver quest clear <id> [--failed] [--yes]  ·  daoris-driver ask --clear <id> [--yes]
        """;

    /// <summary>Whether the words after <c>quest</c> or <c>ask</c> name a clear, rightly or not: <c>clear</c>, or <c>--clear</c>.</summary>
    public static bool Asks(WorkScope scope, IReadOnlyList<string> args) =>
        args.Count > 0 && args[0] == (scope == WorkScope.Ask ? "--clear" : "clear");

    /// <summary>
    /// What the words after <c>history</c> ask, or null with what is wrong with them: <c>[--workspace &lt;name&gt;] [--json]</c>, or
    /// <c>clear --workspace &lt;name&gt; [--yes]</c>, each word at most once and in any order.
    /// </summary>
    public static HistoryAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        var clear = args is ["clear", ..];
        string? workspace = null;
        bool json = false, yes = false;
        for (var at = clear ? 1 : 0; at < args.Count; at++)
        {
            switch (args[at])
            {
                case "--workspace" when workspace is null && at + 1 < args.Count && WorkspaceName(args[at + 1]) is { } named:
                    workspace = named;
                    at++;
                    break;
                case "--workspace":
                    problem = "`--workspace` takes one workspace's name.";
                    return null;
                case "--json" when !clear && !json:
                    json = true;
                    break;
                case "--yes" when clear && !yes:
                    yes = true;
                    break;
                default:
                    problem = clear
                        ? "`history clear` takes `--workspace <name>`, then `--yes` to clear what its list holds."
                        : $"`{args[at]}` is not a word `history` takes.";
                    return null;
            }
        }

        if (clear && workspace is null)
        {
            problem = "`history clear` takes `--workspace <name>`: a clear names its workspace.";
            return null;
        }

        return new HistoryAsk(HistoryScope.Workspace, workspace) { Clear = clear, Yes = yes, Json = json };
    }

    /// <summary>
    /// What the words after <c>quest</c> or <c>ask</c> ask, or null with what is wrong with them: <c>clear &lt;id&gt; [--failed]
    /// [--yes]</c>, or <c>--clear &lt;id&gt; [--yes]</c>.
    /// </summary>
    public static HistoryAsk? Read(WorkScope scope, IReadOnlyList<string> args, out string? problem)
    {
        problem = scope == WorkScope.Ask
            ? "`--clear` takes one ask's id, then `--yes` to clear what its list holds."
            : "`clear` takes one quest's id, `--failed` for its failed sessions alone, then `--yes` to clear what its list holds.";
        if (!Asks(scope, args) || args.Count < 2 || Id(args[1]) is not { } id) return null;

        bool yes = false, failed = false;
        for (var at = 2; at < args.Count; at++)
        {
            switch (args[at])
            {
                case "--yes" when !yes:
                    yes = true;
                    break;
                case "--failed" when scope == WorkScope.Quest && !failed:
                    failed = true;
                    break;
                default:
                    return null;
            }
        }

        problem = null;
        var named = scope == WorkScope.Ask ? HistoryScope.Ask : failed ? HistoryScope.Failed : HistoryScope.Quest;
        return new HistoryAsk(named, id) { Clear = true, Yes = yes };
    }

    /// <exception cref="DriverException">The service did not answer, or has no history door: the host says it, exit 2.</exception>
    public static async Task<int> RunAsync(HistoryAsk ask, HistoryWorld world, TextWriter output, CancellationToken ct = default)
    {
        if (!ask.Clear) return await ReadAsync(ask, world, output, ct).ConfigureAwait(false);

        var plan = await HistoryClearing.PlanAsync(world, ask.Scope, ask.Id ?? "", ct).ConfigureAwait(false);
        return ask.Scope == HistoryScope.Workspace
            ? await WorkspaceAsync(ask, plan, world, output, ct).ConfigureAwait(false)
            : await OneAsync(ask, plan, world, output, ct).ConfigureAwait(false);
    }

    // ——— The reading (§2.4).

    private static async Task<int> ReadAsync(HistoryAsk ask, HistoryWorld world, TextWriter output, CancellationToken ct)
    {
        IReadOnlyList<string> workspaces = ask.Id is { } named
            ? [RemoteTarget.Workspace(named)]
            : await WorkspacesAsync(world.Service, ct).ConfigureAwait(false);
        var plans = new List<HistoryPlan>();
        foreach (var workspace in workspaces)
        {
            plans.Add(await HistoryClearing.PlanAsync(world, HistoryScope.Workspace, workspace, ct).ConfigureAwait(false));
        }

        if (ask.Json)
        {
            output.WriteLine(ask.Id is null
                ? HistoryAnswers.Json(new HistoryWorkspacesAnswer([.. plans.Select(HistoryAnswers.Plan)]))
                : HistoryAnswers.Json(HistoryAnswers.Plan(plans[0])));
            return 0;
        }

        if (plans.Count == 0)
        {
            Write(output, ["nothing finished is kept on this machine: no record names a workspace."]);
            return 0;
        }

        foreach (var plan in plans) Write(output, Lines(plan));
        Write(output, [Home(plans[0].Reading!)]);
        return 0;
    }

    /// <summary>
    /// Every workspace a record names, a session's, a quest's or an ask's, each once and by name, so one no page shows any more
    /// (renamed, or no longer registered) is read too (§2.4).
    /// </summary>
    private static async Task<IReadOnlyList<string>> WorkspacesAsync(ServiceClient service, CancellationToken ct)
    {
        var records = await service.SessionRecordsAsync(ct).ConfigureAwait(false);
        var quests = await service.EveryQuestAsync(ct).ConfigureAwait(false);
        var asks = await service.EveryAskAsync(ct).ConfigureAwait(false);
        return
        [
            .. records.Select(record => record.Workspace)
                .Concat(quests.Select(quest => quest.Workspace))
                .Concat(asks.Select(each => each.Workspace))
                .Select(RemoteTarget.Workspace)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase),
        ];
    }

    /// <summary>What a terminal prints of one workspace's reading: what it keeps, what a clear would take, and what stays and why.</summary>
    private static List<string> Lines(HistoryPlan plan)
    {
        var reading = plan.Reading!;
        var workspace = reading.Workspace;
        var lines = new List<string>();
        if (reading.Quests + reading.Asks + reading.Sessions + reading.Teammates == 0)
        {
            lines.Add($"{workspace} keeps no finished work on this machine.");
        }
        else
        {
            var work = Join([Count(reading.Quests, "closed quest"), Count(reading.Asks, "ask")]);
            var sessions = Join([Count(reading.Sessions, "session"), Copies(reading.Teammates)]);
            lines.Add($"{workspace} keeps {work} on this machine, with {(sessions.Length == 0 ? "no session" : sessions)}: "
                      + $"{Size(reading.Bytes.Total + reading.Intake)}.");
        }

        var disk = Join(
        [
            Of(reading.Bytes.Conversations, "conversations"), Of(reading.Bytes.Transcripts, "transcripts"), Of(reading.Bytes.Files, "files"),
            Of(reading.Bytes.Kept, "kept files"), Of(reading.Bytes.Other, "the small files beside them"),
            reading.Intake > 0 ? $"{Size(reading.Intake)} in the intake's room" : null,
        ]);
        if (disk.Length > 0) lines.Add($"  on the disk: {disk}.");

        var takes = Join(
        [
            Count(reading.Takes.Quests, "quest"), Count(reading.Takes.Asks, "ask"), Count(reading.Takes.Sessions, "session"),
            Copies(reading.Takes.Teammates),
            reading.LeftOver > 0 ? $"the home's {Count(reading.LeftOver, "left-over file")}" : null,
            TakesRoom(plan) ? "the intake's room" : null,
        ]);
        lines.Add(takes.Length == 0
            ? "  a clear would take nothing now."
            : $"  a clear would take {takes}, {Size(reading.Takes.Bytes)}: `{Door(HistoryScope.Workspace, workspace)}` lists it first.");

        foreach (var (word, count) in reading.KeptBy.OrderByDescending(each => each.Value).ThenBy(each => each.Key, StringComparer.Ordinal))
        {
            lines.Add($"  {count.ToString(CultureInfo.InvariantCulture)} kept: {Because(word)}");
        }

        if (reading.Conversations > 0)
        {
            lines.Add($"  {Count(reading.Conversations, "conversation that served no quest", "conversations that served no quest")}, "
                      + $"{Size(reading.ConversationBytes)}: a clear never takes one; `daoris-driver sessions delete <id>` does.");
        }

        return lines;
    }

    /// <summary>The home as a whole, said once: its left-over files, which a workspace's clear takes, and its log, which none does.</summary>
    private static string Home(HistoryReading reading) =>
        (reading.LeftOver == 0
            ? "the home as a whole: nothing left over from records already gone; "
            : $"the home as a whole: {Count(reading.LeftOver, "file")} left over from records already gone, {Size(reading.LeftOverBytes)}, "
              + "which a workspace's clear takes; ")
        + $"the machine log holds {Size(reading.Log)}, which a clear never touches.";

    /// <summary>Why units are kept, by the word that keeps them, with the line that frees them where one does (§5).</summary>
    private static string Because(string word) => word switch
    {
        HistoryWords.Open => "it is still open or taken: its record is work in progress.",
        HistoryWords.Asked => "an ask asked it; `daoris-driver ask --clear <id>` clears the ask with every quest it became.",
        HistoryWords.Live => "a session still runs, or its work is still being landed; `daoris-driver sessions stop <id>` stops one.",
        HistoryWords.NeedsYou => "something of it waits on you; `daoris-driver sessions --group you` lists the sessions that do.",
        HistoryWords.Awaited => "open work waits on its answer or builds on it.",
        HistoryWords.TreeHere => "a session's tree is still here; `daoris-driver trees clean` removes it once its work has landed, or "
                                 + "`daoris-driver trees remove <session> --force` discards it.",
        HistoryWords.LandingStands => "a landing's branch still stands; `daoris-driver trees clean` removes it once it has merged.",
        HistoryWords.Unpushed => "its last moves have not reached the remote; `daoris-driver sync`, then clear it.",
        HistoryWords.NotOurs => "a teammate's record, which is theirs.",
        HistoryWords.Unknown => "this machine no longer holds it.",
        _ => $"the service keeps it (`{word}`).",
    };

    // ——— One quest's work, one quest's failed sessions, one ask's work.

    private static async Task<int> OneAsync(HistoryAsk ask, HistoryPlan plan, HistoryWorld world, TextWriter output, CancellationToken ct)
    {
        var named = Named(ask.Scope, plan.Id);
        if (plan.Units is not [var unit])
        {
            Write(output, [$"nothing of {named} is on this machine to clear."]);
            return 1;
        }

        if (unit.Keep is { Word: HistoryWords.Unknown } unknown)
        {
            Write(output, [unknown.Message]);
            return 1;
        }

        if (unit.Keep is { } keep)
        {
            Write(output, [$"{named} stays on this machine: {keep.Message}", .. Pieces(unit)]);
            return 1;
        }

        if (!Takes(unit))
        {
            Write(output, [Nothing(ask.Scope, plan.Id), .. Pieces(unit)]);
            return 0;
        }

        if (!ask.Yes)
        {
            var stays = ask.Scope == HistoryScope.Failed ? $" #{plan.Id} and its other sessions stay." : "";
            Write(output,
            [
                $"clearing {named} would take {Contents(unit)}, with what this machine kept of them ({Size(unit.Bytes.Total)}): their "
                + $"words, transcripts and files.{stays} Nothing brings it back; `{Door(ask.Scope, plan.Id)}` clears it.",
                .. Remote(unit.Forgotten.Count, unit.Workspace),
                .. Pieces(unit),
            ]);
            return 0;
        }

        var outcome = await HistoryClearing.ClearAsync(world, ask.Scope, plan.Id, [unit.Name], PluginEvents.Terminal, ct).ConfigureAwait(false);
        if (outcome.Cleared is [var cleared])
        {
            var stays = ask.Scope == HistoryScope.Failed ? $"; #{plan.Id} and its other sessions stay" : "";
            Write(output,
            [
                $"cleared {named} from this machine: {Contents(cleared)}, {Size(outcome.Bytes)}{stays}.",
                .. Remote(outcome.Forgotten, cleared.Workspace),
                .. Pieces(cleared),
                .. Failed(outcome.Failed),
            ]);
            return outcome.Failed > 0 ? 2 : 0;
        }

        var stayed = outcome.Changed.FirstOrDefault()?.Keep;
        Write(output, [$"kept {named}, which changed since the list: {stayed?.Message ?? "the service did not clear it."}"]);
        return 1;
    }

    private static string Nothing(HistoryScope scope, string id) => scope == HistoryScope.Failed
        ? $"#{id} has no failed session of this machine's to clear."
        : $"nothing of {Named(scope, id)} is left to clear on this machine.";

    // ——— A workspace's finished history.

    private static async Task<int> WorkspaceAsync(HistoryAsk ask, HistoryPlan plan, HistoryWorld world, TextWriter output, CancellationToken ct)
    {
        var workspace = plan.Id;
        var reading = plan.Reading!;
        var going = plan.Units.Where(unit => unit.Clearable && Takes(unit)).ToList();
        var kept = plan.Units.Where(unit => !unit.Clearable).ToList();
        var room = TakesRoom(plan);
        var keeps = kept.Select(unit => $"  keeps {Name(unit)}: {unit.Keep!.Message}").Concat(plan.Units.SelectMany(Pieces)).ToList();

        if (going.Count == 0 && reading.LeftOver == 0 && !room)
        {
            Write(output, [$"nothing of {workspace}'s finished history may be cleared now{(ask.Yes ? ", so nothing changed" : "")}.", .. keeps]);
            return 0;
        }

        if (!ask.Yes)
        {
            var takes = Join(
            [
                Count(reading.Takes.Quests, "quest"), Count(reading.Takes.Asks, "ask"), Count(reading.Takes.Sessions, "session"),
                Copies(reading.Takes.Teammates), reading.LeftOver > 0 ? Count(reading.LeftOver, "left-over file") : null,
                room ? "the intake's room" : null,
            ]);
            var lines = new List<string>
            {
                $"clearing {workspace}'s finished history would take {takes}, {Size(reading.Takes.Bytes)}, and keep "
                + $"{kept.Count.ToString(CultureInfo.InvariantCulture)}; `{Door(HistoryScope.Workspace, workspace)} --yes` clears what this "
                + "list holds. Nothing brings it back.",
            };
            lines.AddRange(going.Select(unit => $"  takes {Name(unit)}: {Contents(unit)}, {Size(unit.Bytes.Total)}."));
            if (reading.LeftOver > 0)
            {
                lines.Add($"  takes {Count(reading.LeftOver, "file")} left over from records already gone, {Size(reading.LeftOverBytes)}.");
            }

            if (room) lines.Add($"  takes the intake's room, {Size(reading.Intake)}: no ask of {workspace} is kept.");
            lines.AddRange(keeps);
            var forgotten = going.Sum(unit => unit.Forgotten.Count);
            if (forgotten > 0)
            {
                lines.Add($"  the remote for {workspace} keeps the team's copy of {forgotten.ToString(CultureInfo.InvariantCulture)} of these "
                          + $"quests; this machine will not fetch {(forgotten == 1 ? "it" : "them")} again.");
            }

            Write(output, lines);
            return 0;
        }

        var outcome = await HistoryClearing.ClearAsync(
                world, HistoryScope.Workspace, workspace, [.. plan.Units.Where(unit => unit.Clearable).Select(unit => unit.Name)], PluginEvents.Terminal, ct)
            .ConfigureAwait(false);
        var went = Join(
        [
            Count(outcome.Quests, "quest"), Count(outcome.Asks, "ask"), Count(outcome.Sessions, "session"), Copies(outcome.Teammates),
            outcome.LeftOver > 0 ? Count(outcome.LeftOver, "left-over file") : null, outcome.Intake ? "the intake's room" : null,
        ]);
        var listed = outcome.Listed > 0
            ? $"{outcome.Cleared.Count.ToString(CultureInfo.InvariantCulture)} of {outcome.Listed.ToString(CultureInfo.InvariantCulture)} listed, "
            : "";
        var changes = outcome.Changed.Count switch
        {
            0 => ".",
            1 => "; 1 changed since the list and was kept.",
            var n => $"; {n.ToString(CultureInfo.InvariantCulture)} changed since the list and were kept.",
        };
        var said = new List<string>
        {
            outcome.Took
                ? $"cleared {workspace}'s finished history from this machine: {listed}{(went.Length == 0 ? "nothing" : went)}, {Size(outcome.Bytes)}{changes}"
                : $"nothing of {workspace}'s finished history was cleared{changes}",
        };
        if (outcome.Forgotten > 0)
        {
            said.Add(outcome.Forgotten == 1
                ? $"  1 of the quests was forgotten here; the remote for {workspace} keeps the team's copy, and this machine will not fetch it again."
                : $"  {outcome.Forgotten.ToString(CultureInfo.InvariantCulture)} of the quests were forgotten here; the remote for {workspace} "
                  + "keeps the team's copy, and this machine will not fetch them again.");
        }

        said.AddRange(outcome.Changed.Select(unit => $"  kept {Name(unit)}, which changed since the list: {unit.Keep!.Message}"));
        // What the list kept stays, said as the list said it, so the press's lines account for every unit.
        said.AddRange(keeps);
        said.AddRange(Failed(outcome.Failed));
        Write(output, said);
        return outcome.Failed > 0 ? 2 : 0;
    }

    /// <summary>
    /// Whether a workspace's reading counts its intake's room in what a clear would take: its takes are the clearable units', the
    /// left-over files' and the room's where no ask of the workspace is kept (<see cref="HistoryReading.Takes"/>), so what is left
    /// once the units' and the left-over files' are taken away is the room or nothing.
    /// </summary>
    private static bool TakesRoom(HistoryPlan plan) =>
        plan.Reading is { Intake: > 0 } reading
        && reading.Takes.Bytes - reading.LeftOverBytes - plan.Units.Where(unit => unit.Clearable).Sum(unit => unit.Bytes.Total) >= reading.Intake;

    // ——— The words.

    /// <summary>Whether a unit takes anything: a failed-sessions unit of a quest with none of this machine's takes nothing.</summary>
    private static bool Takes(HistoryUnitPlan unit) => unit.Quests.Count + unit.Asks.Count + unit.Sessions.Count + unit.Teammates.Count > 0;

    /// <summary>What a unit takes, counted: <c>1 ask, 3 quests and 5 sessions</c>.</summary>
    private static string Contents(HistoryUnitPlan unit) => Join(
    [
        Count(unit.Asks.Count, "ask"), Count(unit.Quests.Count, "quest"), Count(unit.Sessions.Count, "session"), Copies(unit.Teammates.Count),
    ]);

    /// <summary>The pieces a unit lists and keeps while it goes, each in its keep's sentence: a teammate's failed session.</summary>
    private static IEnumerable<string> Pieces(HistoryUnitPlan unit) =>
        unit.Kept.Select(piece => $"  keeps {piece.Session ?? (piece.Quest is { } quest ? $"#{quest}" : Name(unit))}: {piece.Message}");

    /// <summary>The team's copy a remote keeps of a quest forgotten here (§3.2), in the clear's ask's words (§6.1).</summary>
    private static IEnumerable<string> Remote(int forgotten, string? workspace) => forgotten > 0
        ? [$"  the remote for {RemoteTarget.Workspace(workspace)} keeps the team's copy; this machine will not fetch it again."]
        : [];

    private static IEnumerable<string> Failed(int failed) => failed > 0
        ? [$"  {Count(failed, "file")} could not be removed and {(failed == 1 ? "is" : "are")} left over; the next clear of a workspace takes "
           + $"{(failed == 1 ? "it" : "them")}."]
        : [];

    /// <summary>A unit as a person reads it: <c>#q1</c>, <c>ask #a1</c>, <c>#q1's failed sessions</c>.</summary>
    private static string Name(HistoryUnitPlan unit) => unit.Kind switch
    {
        HistoryKinds.Ask => $"ask #{unit.Id}",
        HistoryKinds.Failed => $"#{unit.Id}'s failed sessions",
        _ => $"#{unit.Id}",
    };

    private static string Named(HistoryScope scope, string id) => scope switch
    {
        HistoryScope.Ask => $"ask #{id}",
        HistoryScope.Failed => $"#{id}'s failed sessions",
        HistoryScope.Workspace => $"{id}'s finished history",
        _ => $"#{id}",
    };

    /// <summary>The line that clears a scope, without its <c>--yes</c> for a workspace's (the reading names it to list first).</summary>
    private static string Door(HistoryScope scope, string id) => scope switch
    {
        HistoryScope.Ask => $"daoris-driver ask --clear {id} --yes",
        HistoryScope.Failed => $"daoris-driver quest clear {id} --failed --yes",
        HistoryScope.Workspace => $"daoris-driver history clear --workspace {id}",
        _ => $"daoris-driver quest clear {id} --yes",
    };

    private static string? Count(int count, string noun, string? nouns = null) => count switch
    {
        0 => null,
        1 => $"1 {noun}",
        _ => $"{count.ToString(CultureInfo.InvariantCulture)} {nouns ?? noun + "s"}",
    };

    private static string? Copies(int count) => Count(count, "teammate's copy", "teammate's copies");

    private static string? Of(long bytes, string what) => bytes > 0 ? $"{Size(bytes)} of {what}" : null;

    /// <summary>The parts said, in English: <c>a</c>, <c>a and b</c>, <c>a, b and c</c>; empty for none.</summary>
    private static string Join(IEnumerable<string?> parts)
    {
        var said = parts.OfType<string>().Where(part => part.Length > 0).ToList();
        return said.Count switch
        {
            0 => "",
            1 => said[0],
            _ => $"{string.Join(", ", said.Take(said.Count - 1))} and {said[^1]}",
        };
    }

    /// <summary>A size as a person reads it, in bytes, KB, MB or GB of 1024.</summary>
    public static string Size(long bytes) => bytes switch
    {
        1 => "1 byte",
        < 1024 => $"{bytes.ToString(CultureInfo.InvariantCulture)} bytes",
        < 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0:0.#} KB"),
        < 1024L * 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024):0.#} MB"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024 * 1024):0.##} GB"),
    };

    /// <summary>An id as a person writes it, without its <c>#</c>, or null where it is none or a flag.</summary>
    private static string? Id(string word) =>
        !word.StartsWith('-') && word.Trim().TrimStart('#').Trim() is { Length: > 0 } id ? id : null;

    /// <summary>A workspace's name, or null where it is none or a flag.</summary>
    private static string? WorkspaceName(string word) => !word.StartsWith('-') && word.Trim() is { Length: > 0 } name ? name : null;

    /// <summary>The first line under the binary's name, as its other one-line answers are; the rest as written, indented.</summary>
    private static void Write(TextWriter output, IReadOnlyList<string> lines)
    {
        for (var at = 0; at < lines.Count; at++) output.WriteLine(at == 0 ? $"daoris-driver: {lines[at]}" : lines[at]);
    }
}
