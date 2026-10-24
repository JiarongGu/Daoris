using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The census that holds NOBLOCK1 (D56's NOBLOCK1 note): nothing the window asks for waits on work. Every read of the driver's
/// module either reads only what is held in memory, named in <see cref="Held"/> with what it reads, or is marked
/// <c>[ByEvent]</c>, answered from what is held while its work is told as <c>DAORIS.ANSWERED</c>. A read that is neither is
/// refused, unless it is one of the <see cref="Owed"/> reads that were computed on the window's request when the rule was set,
/// which this census lets only shrink.
/// </summary>
/// <remarks>
/// A press is not counted: it answers what it did, and it leaves the window's thread at its first wait (D56's UIFREEZE1 note).
/// Moving an owed read is its <c>[ByEvent]</c> mark, its page hook through <c>askHeld</c>, its row in
/// <c>DriverModuleHeldTests.Payloads</c>, and its name taken out of <see cref="Owed"/>.
/// </remarks>
public sealed class WindowCensusTests
{
    /// <summary>The reads that answer only from memory, each with what it reads.</summary>
    private static readonly Dictionary<string, string> Held = new(StringComparer.Ordinal)
    {
        ["STATE_SLICE"] = "a pushed slice's snapshot, held by its owner",
        ["UNDER_WAY"] = "the presses under way, held by the publisher",
        ["STEWARD_ACTS"] = "the steward's live generation and this run's acts, held by the standing confirmation's judge (STEWARDROLE1b)",
    };

    /// <summary>
    /// The routes declaring no change that answered from a file, the service, git or a process when NOBLOCK1 set the rule, owed
    /// their move to <c>[ByEvent]</c> (or to <see cref="Held"/>, where a look shows they read only memory); not judged one by one
    /// then, so a name here is "not shown held", never "shown slow". None is added: a new read is held or told by event.
    /// </summary>
    private static readonly HashSet<string> Owed = new(StringComparer.Ordinal)
    {
        "ACCOUNTS", "ACROSS", "ACTORS", "ANSWER_WAITS", "ASKS_ELSEWHERE", "ASK_LANDING", "CHANNEL_READ", "CHECKOUT_BRANCH",
        "ENTRY_LANDING", "GIT_BLAME", "GIT_BRANCHES", "GIT_COMMIT", "GIT_COMPARE", "GIT_GRAPH", "GIT_HISTORY", "GIT_TRY_AGAIN",
        "HANDOFF_PLAN", "HARNESSES", "HARNESS_CANCEL", "HARNESS_INPUT", "HEAVY_MARKS", "HELPER_WAITS", "HELP_CONVERSATIONS", "HELP_PUBLISHED",
        "HELP_WIRED", "HISTORY_PLAN", "LANDING", "LINES", "NOTIFICATIONS", "NUDGE", "OPINION_GATE", "OPINION_WAITS",
        "PERMISSION_WAITS", "PERSON_CONFIRM", "PERSON_CONFIRMATIONS", "PERSON_CONFIRM_SHOWN", "PERSON_KEY", "PLUGIN", "PLUGINS", "PLUGIN_ACTIVITY",
        "PLUGIN_NEW", "PLUGIN_OPEN_FOLDER", "PLUGIN_READ", "PLUGIN_TRY", "READ_LEAVE", "RULES", "SERVICE_CONFIRMATIONS", "SESSION_DIFF",
        "SESSION_FILE", "SESSION_FILES", "SESSION_GROUPS", "SESSION_HAND_OVER_ASK", "SESSION_HELPERS", "SESSION_HISTORY", "SESSION_OPEN_FOLDER", "SESSION_OPTIONS",
        "SESSION_PARTNERS", "SESSION_PERMISSION_ANSWER", "SESSION_PROCESS", "SESSION_QUESTION", "SESSION_QUEUE", "SESSION_SEARCH", "SESSION_SPENT", "SESSION_STREAMS",
        "SESSION_SUPERVISOR", "SETUP_PLAN", "SET_SESSION_OPTION", "STARTS", "STATE", "STOP_TASK", "SWEEP_PLAN", "SYSTEM_DOWNLOADS",
        "SYSTEM_SPACE", "SYSTEM_WIRING", "TAIL_SESSION", "TOOLS_GIT", "TOOLS_PICK", "TOOLS_STOP", "TRACE", "TREES_SYNC_PLAN",
        "TREES_SYNC_SCOPE", "USAGE", "WORKFLOW_CURRENT", "WORKFLOW_LANDED", "WORKFLOW_RUN", "WORK_PLAN", "WORK_RECORDS_FOLDER", "WORK_STATE",
    };

    /// <summary>The count of <see cref="Owed"/> when the rule was set: it falls as reads move, and never rises.</summary>
    private const int OwedAtTheRule = 88;

    /// <summary>Every route of the driver's module that declares no change: a read, or an act that moves nothing the page reads.</summary>
    private static IEnumerable<string> Reads() => DriverModule.Routes.Where(route =>
        DriverModule.Declared(new IpcRequest { Id = "census", Module = DriverModule.Name, Type = route }) is null);

    [Fact]
    public void Every_read_is_held_or_told_by_event_or_owed_and_nothing_is_both()
    {
        var byEvent = DriverModule.ByEventRoutes.ToHashSet(StringComparer.Ordinal);
        var unclassed = Reads().Where(route => !Held.ContainsKey(route) && !byEvent.Contains(route) && !Owed.Contains(route))
            .Order(StringComparer.Ordinal).ToList();
        Assert.True(unclassed.Count == 0,
            $"read(s) computed on the window's request, neither held nor told by event (NOBLOCK1): {string.Join(", ", unclassed)}");

        var twice = Held.Keys.Concat(Owed).Where(byEvent.Contains).Concat(Held.Keys.Where(Owed.Contains)).ToList();
        Assert.True(twice.Count == 0, $"classed twice: {string.Join(", ", twice)}");
        Assert.True(Owed.Count <= OwedAtTheRule, $"{Owed.Count} owed, more than the {OwedAtTheRule} the rule began with: a new read is held or told by event");
    }

    [Fact]
    public void Every_name_the_census_holds_is_a_read_of_the_module()
    {
        var reads = Reads().ToHashSet(StringComparer.Ordinal);
        var stray = Held.Keys.Concat(Owed).Where(route => !reads.Contains(route)).ToList();
        Assert.True(stray.Count == 0, $"no such read, or a press: {string.Join(", ", stray)} (a moved read leaves the census)");
    }

    /// <summary>The routes the install's machine log ranked as holding the window (ui.stalled) are told by event, SESSION_WHERE first.</summary>
    [Theory]
    [InlineData("SESSION_WHERE")]
    [InlineData("HEAVY_SLOT")]
    [InlineData("SESSION_OPENINGS")]
    [InlineData("HELP_PROPOSALS")]
    [InlineData("TOOLS_LIST")]
    public void A_route_the_install_saw_hold_the_window_is_told_by_event(string route) =>
        Assert.Contains(route, DriverModule.ByEventRoutes);
}
