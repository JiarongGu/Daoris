using System.Text;

namespace Daoris.Driver;

/// <summary>
/// This machine, now (HELP1a): the agents quests, asks and the helper run on, what waits on the person, the
/// quests its strikes parked (HELP10) and the person's stop holds (SESSUX1b), the plugins installed here (PLUG9) and the
/// branches landings made (WSR5b) — each by name and state, so a proposal names one this machine holds.
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
        Browser = sources.Browser is { } files ? new HelpBrowser(files.Browser, files.Links, files.Extensions, files.Favorites) : null,
    };

    public string Render(HelpMachine machine)
    {
        var text = new StringBuilder();
        text.Append("## This machine, now\n\n");
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

public sealed partial record HelpMachine
{
    /// <summary>The harness quests run on.</summary>
    public string? Adapter { get; init; }

    /// <summary>The harness asks are answered on, or null for none (INT4b).</summary>
    public string? Intake { get; init; }

    /// <summary>The harness Ask Daoris runs on (D89).</summary>
    public string? Helper { get; init; }

    public int Cap { get; init; }

    /// <summary>
    /// The sessions waiting on the person (ENTRY1f2), as the Sessions list's <i>Waiting on you</i> holds them less Ask Daoris's
    /// own, by id, so a go names one the room was shown; the room's count is theirs.
    /// </summary>
    public IReadOnlyList<HelpWaitingSession> Waiting { get; init; } = [];

    /// <summary>How many asks wait for the person's answer.</summary>
    public int Asks { get; init; }

    /// <summary>Every plugin installed here, sound or not, in the catalogue's order (PLUG9).</summary>
    public IReadOnlyList<HelpPlugin> Plugins { get; init; } = [];

    /// <summary>The branches this machine's landings made and recorded, in the order they landed (WSR5b).</summary>
    public IReadOnlyList<HelpLanded> Landed { get; init; } = [];

    /// <summary>The quests the loop's last tick parked by their failed sessions, as the room was written (HELP10).</summary>
    public IReadOnlyList<ParkedQuest> Parked { get; init; } = [];

    /// <summary>The quests the loop's last look held by the person's stop, as the room was written (SESSUX1b).</summary>
    public IReadOnlyList<HeldQuest> Held { get; init; } = [];

    /// <summary>Daoris's browser as its files hold it (HELP10), or null where the desktop read none.</summary>
    public HelpBrowser? Browser { get; init; }
}

/// <summary>A session waiting on the person, as the room lists it (ENTRY1f2): by id and where it runs, so a go names one it was shown.</summary>
/// <param name="Repository">Where it runs, as its record says: a repository's name, or <c>ask #id</c> for an intake.</param>
public sealed record HelpWaitingSession(string Id, string Repository)
{
    /// <summary>The ask an intake answers (D65 §1b), or null for every other session.</summary>
    public string? Ask { get; init; }
}

/// <summary>Daoris's browser as the room says it (HELP10): which one, where links open, extensions, and the pages it keeps.</summary>
public sealed record HelpBrowser(string Browser, string Links, string Extensions, IReadOnlyList<string> Favorites);
