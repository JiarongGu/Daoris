using System.Text;

namespace Daoris.Driver;

/// <summary>
/// This machine, now (HELP1a): the agents quests, asks and the helper run on, what waits on the person, the
/// quests its strikes parked (HELP10) and the person's stop holds (SESSUX1b), what is paused (PAUSE1f-b), what each workspace
/// keeps of finished work (HIST1f-b), the plugins installed here (PLUG9) and the branches landings made (WSR5b) — each by name
/// and state, so a proposal names one this machine holds.
/// </summary>
internal sealed class HelpRoomMachineNow : IHelpRoomSection
{
    public HelpMachine Describe(HelpMachine machine, HelpMachineSources sources) => machine with
    {
        Adapter = sources.Config.Adapter,
        Intake = sources.Config.IntakeAdapter,
        Helper = sources.Config.HelperAdapter,
        Cap = sources.Config.Cap,
        // ENTRY1f2 (D161's ENTRY1f note): by the Sessions list's own rule, so the count and the list name the same sessions,
        // less Ask Daoris's own conversation, which opens here; a teammate's never reached the snapshot.
        Waiting = [.. sources.Snapshot.Active
            .Where(session => SessionGroups.WaitsOnYou(session.Id, session.State, session.Answer) && session.Repository != HelpRoom.Repository)
            .Select(session => new HelpWaitingSession(session.Id, session.Repository) { Ask = session.Ask })],
        Sessions = sources.Sessions is { } look ? Placed(look) : null,
        Asks = sources.Asks,
        Plugins = [.. sources.Plugins.Plugins.Select(entry =>
            new HelpPlugin(entry.Manifest.Id, entry.Enabled, entry.Manifest.Hooks?.Points ?? [])
            {
                Problem = entry.Problem,
                // Its kind only: the folder it came from is a path on this machine, which the room never names.
                Source = PluginSource.Read(entry.Folder).Source switch
                {
                    null => null,
                    { Package: not null } => "package",
                    { Offer: not null } => "offer",
                    _ => "folder",
                },
            })],
        Landed = [.. sources.Landed.Select(entry => new HelpLanded(entry.Repository, entry.Branch, entry.Session, entry.Pushed, entry.PullRequest)
        {
            State = entry.PullRequestState,
            AskFailed = entry.PullRequestAskFailed,
        })],
        Parked = sources.Parked,
        Held = sources.Held,
        // PAUSE1f-b: the pauses the pause and the resume write (design §2.5), asks first, each in its ids' order.
        Paused = [.. sources.Config.PausedAsks.Keys.Order(StringComparer.Ordinal).Select(ask => new PausedBy(WorkScope.Ask, ask)),
            .. sources.Config.PausedQuests.Keys.Order(StringComparer.Ordinal).Select(quest => new PausedBy(WorkScope.Quest, quest))],
        Browser = sources.Browser is { } files ? new HelpBrowser(files.Browser, files.Links, files.Extensions, files.Favorites) : null,
        History = sources.History,
        // PARALLEL1a: what the judges' facts had not read yet, which each section says where it says its part.
        NotReadYet = sources.NotReadYet,
    };

    /// <summary>
    /// The most ended sessions the room lists by id (SESSUX1h3): enough to name one to archive or delete from the newest, short
    /// enough that the room stays short for a machine with hundreds; the count says how many more.
    /// </summary>
    internal const int EndedListed = 10;

    /// <summary>
    /// This machine's sessions as the Sessions list places them (SESSUX1h3): one rule, <see cref="SessionGroups.Read"/>, less Ask
    /// Daoris's own conversations and a teammate's, which this machine does not act on; archived ones are in the list's own
    /// group, so in no count here. The ended come newest first, as the list orders them.
    /// </summary>
    private static HelpSessions Placed(SessionLook look)
    {
        var records = look.Records.GroupBy(record => record.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var rows = SessionGroups.Read(look)
            .Where(row => !row.Teammate && records.TryGetValue(row.Session, out var record) && record.Repository != HelpRoom.Repository)
            .ToList();
        int In(string group) => rows.Count(row => row.Group == group);
        var ended = rows.Where(row => row.Group == SessionGroup.Ended).ToList();
        return new HelpSessions(
            In(SessionGroup.You), In(SessionGroup.Review), In(SessionGroup.Working), In(SessionGroup.Later), ended.Count,
            [.. ended.Take(EndedListed).Select(row => new HelpEndedSession(row.Session, records[row.Session].Repository))]);
    }

    public string Render(HelpMachine machine)
    {
        var text = new StringBuilder();
        text.Append("## This machine, now\n\n");
        // PARALLEL1a (D181 point 1): the room is written at once from the facts as last judged, so a part may not be read yet.
        if (machine.NotReadYet.Any)
        {
            text.Append("- Not read yet: some of this machine was not read when this room was written. Where a line here says *not read "
                + "yet*, say so rather than guess; this room is written again as each part is read.\n");
        }

        text.Append(machine.Adapter is { Length: > 0 } adapter
            ? $"- The driver: quests run on `{adapter}`, up to {machine.Cap} sessions at once.\n"
            : "- The driver: no agent is set for quests.\n");
        text.Append(machine.Intake is { Length: > 0 } intake
            ? $"- The intake: asks are answered on `{intake}`.\n"
            : "- The intake: no agent answers asks, so an ask the declarations do not settle waits for the person.\n");
        if (machine.Helper is { Length: > 0 } helper) text.Append($"- Ask Daoris: you, on `{helper}`.\n");
        text.Append($"- {Count(machine.Waiting.Count, "session waits", "sessions wait")} on the person; "
            + $"{Count(machine.Asks, "ask waits", "asks wait")} for an answer.\n");
        // ENTRY1f2: by id and where each runs, so a go names one it was shown.
        text.Append("- Sessions waiting on the person: "
            + (machine.Waiting.Count > 0 ? string.Join(", ", machine.Waiting.Select(WaitingLine)) : "none")
            + ".\n");
        // SESSUX1h3: the Sessions list's groups, and the ended by id, so an archive or a delete names one it was shown.
        if (machine.Sessions is { } sessions)
        {
            text.Append("- This machine's sessions by the Sessions list's groups, archived ones and Ask Daoris's own not counted: "
                + $"{sessions.You} waiting on you, {sessions.Review} to review, {sessions.Working} working, "
                + $"{sessions.Later} resuming later, {sessions.Ended} ended.\n");
            var more = sessions.Ended - sessions.Listed.Count;
            text.Append("- Ended sessions, newest first: "
                + (sessions.Listed.Count > 0
                    ? string.Join(", ", sessions.Listed.Select(ended => $"`{ended.Id}` (in `{ended.Repository}`)")) + (more > 0 ? $", and {more} more" : "")
                    : "none")
                + ".\n");
        }
        else
        {
            text.Append($"- This machine's sessions by the Sessions list's groups: {NotRead(machine.NotReadYet.Sessions)}.\n");
        }

        // HELP10: by id and repository, so a retry names one the quest's page would offer Try again on.
        text.Append("- Quests parked by their failed sessions, at the driver's last look: "
            + (machine.Parked.Count > 0
                ? string.Join(", ", machine.Parked.Select(parked => $"`#{parked.Quest}` (to `{parked.Repository}`)"))
                : "none")
            + ".\n");
        // SESSUX1b: and with the session stopped, so a retry names a stop Try again would release.
        text.Append("- Quests held by the person's stop, at the driver's last look: "
            + (machine.Held.Count > 0
                ? string.Join(", ", machine.Held.Select(held => $"`#{held.Quest}` (to `{held.Repository}`, session `{held.Session}` stopped)"))
                : "none")
            + ".\n");
        // PAUSE1f-b (D132 §7.4): by scope and id, so a resume names a pause this machine keeps.
        text.Append("- Paused on this machine until resumed: "
            + (machine.Paused.Count > 0 ? string.Join(", ", machine.Paused.Select(paused => $"{paused.Word} `#{paused.Id}`")) : "none")
            + ".\n");
        // HIST1f-b (design §2.4, §6.4): what each workspace keeps, so a clear names one that keeps something.
        text.Append(HistoryLines(machine.History, machine.NotReadYet.History));
        // HELP10: as its two files hold it, so a browser proposal names a favorite kept; only where the desktop read them.
        if (machine.Browser is { } browser)
        {
            text.Append($"- Daoris's browser: {(browser.Browser == "edge" ? "your Edge" : "Daoris's own")}; links on the page open in "
                + $"{(browser.Links == "daoris" ? "Daoris's browser" : "the system's browser")}; other software's extensions "
                + $"{(browser.Extensions == "refuse" ? "refused" : "offered")}; favorites "
                + (browser.Favorites.Count > 0 ? string.Join(", ", browser.Favorites) : "none") + ".\n");
        }

        // PLUG9: by id and state, so a switch names one the catalogue holds.
        text.Append(machine.Plugins.Count > 0
            ? $"- Plugins: {string.Join(", ", machine.Plugins.Select(PluginLine))}.\n"
            : "- Plugins: none installed.\n");
        // WSR5b: by name and session, so a hand-off names one the record holds.
        text.Append(machine.Landed.Count > 0
            ? $"- Landed branches: {string.Join(", ", machine.Landed.Select(LandedLine))}.\n\n"
            : "- Landed branches: none recorded.\n\n");
        return text.ToString();
    }

    // An intake runs for its ask, whose record names it as its repository (D65 §1b).
    private static string WaitingLine(HelpWaitingSession session) =>
        session.Ask is { } ask ? $"`{session.Id}` (answering ask `#{ask}`)" : $"`{session.Id}` (in `{session.Repository}`)";

    // PLUGHOOK1c: with what its plugin last answered about its pull request, and when, so the helper reads it rather than guess
    // or ask; asking again is the person's own `trees state`.
    private static string LandedLine(HelpLanded landed) =>
        $"`{landed.Branch}` in `{landed.Repository}` (session `{landed.Session}`, "
        + (landed.Pushed ? "pushed" + (landed.PullRequest is { } pr ? $", pull request {pr}" : "") : "not pushed")
        + PullRequestWords.Row(landed.State, null, landed.AskFailed, null) + ")";

    private static string PluginLine(HelpPlugin plugin)
    {
        // PLUG9 (c): whether an update has anything to read — its kind, never the path.
        var from = plugin.Source switch
        {
            "offer" => ", installed from this install's offer",
            "folder" => ", added from a folder",
            // PLUGDIST1a: an update refuses it, and says a newer package takes its place.
            "package" => ", installed from a package",
            _ => ", no record of where it came from",
        };
        if (!plugin.Enabled) return $"`{plugin.Id}` (off{from})";
        if (plugin.Problem is { } problem) return $"`{plugin.Id}` (on, contributes nothing: {problem}{from})";
        return plugin.Points.Count > 0
            ? $"`{plugin.Id}` (on, speaks on {string.Join(", ", plugin.Points.Select(point => $"`{point}`"))}{from})"
            : $"`{plugin.Id}` (on{from})";
    }

    private static string Count(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";

    /// <summary>A part with nothing read: not judged yet by the facts (PARALLEL1a), or not read at all.</summary>
    internal static string NotRead(bool yet) => yet ? "not read yet" : "not read";

    /// <summary>
    /// What each workspace keeps of finished work (HIST1f-b), in the terminal's words (<c>daoris-driver history</c>): its closed
    /// work and sessions with their size, what a clear would take now, and how many units stay by the word that keeps them; the
    /// home's left-over files once, since every workspace's clear takes them.
    /// </summary>
    private static string HistoryLines(IReadOnlyList<HistoryReading>? readings, bool notReadYet)
    {
        const string Head = "- Finished history this machine keeps, by workspace";
        if (readings is null) return $"{Head}: {NotRead(notReadYet)}.\n";
        if (readings.Count == 0) return $"{Head}: none.\n";

        var text = new StringBuilder($"{Head}:\n");
        foreach (var reading in readings) text.Append($"  - `{reading.Workspace}`: {Keeps(reading)}.\n");
        if (readings.FirstOrDefault(reading => reading.LeftOver > 0) is { } home)
        {
            text.Append($"  - Left over from records already gone: {HistoryCommand.Count(home.LeftOver, "file")}, "
                + $"{HistoryCommand.Size(home.LeftOverBytes)}, which a workspace's clear takes.\n");
        }

        return text.ToString();
    }

    private static string Keeps(HistoryReading reading)
    {
        if (reading.Quests + reading.Asks + reading.Sessions + reading.Teammates == 0) return "none";
        var work = HistoryCommand.Join([HistoryCommand.Count(reading.Quests, "closed quest"), HistoryCommand.Count(reading.Asks, "ask")]);
        var sessions = HistoryCommand.Join([HistoryCommand.Count(reading.Sessions, "session"), HistoryCommand.Copies(reading.Teammates)]);
        var takes = HistoryCommand.Join(
        [
            HistoryCommand.Count(reading.Takes.Quests, "quest"), HistoryCommand.Count(reading.Takes.Asks, "ask"),
            HistoryCommand.Count(reading.Takes.Sessions, "session"), HistoryCommand.Copies(reading.Takes.Teammates),
        ]);
        var kept = reading.KeptBy.Values.Sum();
        return $"{(work.Length == 0 ? "no closed quest or ask" : work)}, with {(sessions.Length == 0 ? "no session" : sessions)}, "
            + $"{HistoryCommand.Size(reading.Bytes.Total + reading.Intake)}; "
            + (takes.Length > 0 ? $"a clear would take {takes}, {HistoryCommand.Size(reading.Takes.Bytes)}"
                : reading.Takes.Bytes > 0 ? $"a clear would take files alone, {HistoryCommand.Size(reading.Takes.Bytes)}"
                : "a clear would take nothing now")
            + (kept == 0 ? "" : $", and keep {kept}: " + string.Join(", ", reading.KeptBy
                .OrderByDescending(each => each.Value).ThenBy(each => each.Key, StringComparer.Ordinal)
                .Select(each => $"{each.Value} `{each.Key}`")));
    }
}

/// <summary>A plugin installed here, as the room lists it (PLUG9): by id and state, so a switch names one the catalogue holds.</summary>
/// <param name="Points">The points it speaks on; empty where it speaks on none, or is refused.</param>
public sealed record HelpPlugin(string Id, bool Enabled, IReadOnlyList<string> Points)
{
    /// <summary>Why it contributes nothing, in the catalogue's words; null when sound.</summary>
    public string? Problem { get; init; }

    /// <summary>Where it came from (PLUG9 c, PLUGDIST1a): `folder`, `offer`, `package`, or null for none recorded — never the path itself.</summary>
    public string? Source { get; init; }
}

/// <summary>A branch a landing made here, as the room lists it (WSR5b): by name and session, so a hand-off names one the record holds.</summary>
public sealed record HelpLanded(string Repository, string Branch, string Session, bool Pushed, string? PullRequest)
{
    /// <summary>What its plugin last answered about its pull request, with when (PLUGHOOK1c, D148 point 6); null where none is kept.</summary>
    public PullRequestState? State { get; init; }

    /// <summary>The latest ask about it that failed since that answer; null where none did.</summary>
    public PullRequestAskFailed? AskFailed { get; init; }
}

/// <summary>A session waiting on the person, as the room lists it (ENTRY1f2): by id and where it runs, so a go names one it was shown.</summary>
/// <param name="Repository">Where it runs, as its record says: a repository's name, or <c>ask #id</c> for an intake.</param>
public sealed record HelpWaitingSession(string Id, string Repository)
{
    /// <summary>The ask an intake answers (D65 §1b), or null for every other session.</summary>
    public string? Ask { get; init; }
}

/// <summary>This machine's sessions counted by the Sessions list's groups (SESSUX1h3), archived ones and Ask Daoris's own aside.</summary>
/// <param name="Ended">Every ended session counted, listed or not.</param>
/// <param name="Listed">The newest ended ones, at most <see cref="HelpRoomMachineNow.EndedListed"/>, by id and where each ran.</param>
public sealed record HelpSessions(int You, int Review, int Working, int Later, int Ended, IReadOnlyList<HelpEndedSession> Listed);

/// <summary>An ended session, as the room lists it (SESSUX1h3): by id and repository, so an archive or a delete names one it was shown.</summary>
public sealed record HelpEndedSession(string Id, string Repository);

/// <summary>Daoris's browser as the room says it (HELP10): which one, where links open, extensions, and the pages it keeps.</summary>
public sealed record HelpBrowser(string Browser, string Links, string Extensions, IReadOnlyList<string> Favorites);
