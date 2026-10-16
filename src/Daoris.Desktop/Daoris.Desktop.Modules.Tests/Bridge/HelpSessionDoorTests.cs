using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// SESSUX1h2c: a session proposal's Apply is Sessions' own doors, the archive marks the <c>SESSION_ARCHIVE</c> route writes and
/// the delete <c>SESSION_DELETE</c> runs, each judged again by where the list places the session as it goes, and each coming
/// back as its sentence. The service is stood in over a loopback socket and nothing starts a process: the fast half (MOD8).
/// </summary>
public sealed class HelpSessionDoorTests : DriverModuleBridge
{
    [Theory]
    [InlineData("session", "archive", true)]
    [InlineData("session", "unarchive", true)]
    [InlineData("delete", "session", true)]
    [InlineData("delete", "quest", false)]
    [InlineData("go", "session", false)]
    public void Sessions_are_read_for_a_session_proposal_and_a_sessions_delete(string kind, string door, bool read)
    {
        var proposal = new HelpProposal("p1", kind, door, "done1", null, null, null, "why", "h1", "proposed");

        Assert.Equal(read, HelpRoutes.JudgedAgainstSessions([proposal]));
    }

    [Fact]
    public async Task An_archive_marks_what_ended_and_says_what_the_list_kept()
    {
        using var ledger = Ledger();
        var doors = await DoorsAsync(ledger);

        var (ok, message) = await doors.ArchiveSessionsAsync(["done1", "running1"], true, CancellationToken.None);

        Assert.True(ok);
        Assert.Contains("Archived 1 session", message);
        Assert.Contains("`done1`", message);
        Assert.Contains("`running1`", message);
        Assert.Equal(["done1"], new SessionArchive(Home).Marks().Keys);
    }

    [Fact]
    public async Task An_archive_that_marks_nothing_is_not_applied()
    {
        using var ledger = Ledger();
        var doors = await DoorsAsync(ledger);

        var (ok, message) = await doors.ArchiveSessionsAsync(["running1"], true, CancellationToken.None);

        Assert.False(ok);
        Assert.Contains("`running1`", message);
        Assert.False(File.Exists(DaorisHome.Entry(Home, HomeEntry.Sessions,"archived.json")));
    }

    [Fact]
    public async Task An_unarchive_takes_the_marks_away_and_says_one_that_was_not_archived()
    {
        using var ledger = Ledger();
        var doors = await DoorsAsync(ledger);
        await doors.ArchiveSessionsAsync(["done1"], true, CancellationToken.None);

        var (ok, message) = await doors.ArchiveSessionsAsync(["done1", "failed1"], false, CancellationToken.None);

        Assert.True(ok);
        Assert.Contains("Unarchived 1 session", message);
        Assert.Contains("`failed1`", message);
        Assert.Empty(new SessionArchive(Home).Marks());
    }

    [Fact]
    public async Task A_delete_goes_through_the_sessions_own_delete_and_says_what_went()
    {
        using var ledger = Ledger();
        ledger.Serve("/api/sessions/done1/deletable", Encoding.UTF8.GetBytes("""{"message":"ok"}"""));
        ledger.Serve("/api/sessions/done1", Encoding.UTF8.GetBytes("""{"message":"deleted"}"""));
        var doors = await DoorsAsync(ledger);

        var (ok, message) = await doors.DeleteSessionAsync("done1", CancellationToken.None);

        Assert.True(ok);
        Assert.StartsWith("Deleted session `done1`", message);
    }

    [Fact]
    public async Task A_delete_the_ledger_refuses_is_not_applied_in_its_words()
    {
        using var ledger = Ledger();
        ledger.Serve("/api/sessions/failed1/deletable", Encoding.UTF8.GetBytes("""{"error":"Session `failed1` worked on `#q1`; archive it instead.","refusal":"served-quest","quest":"q1"}"""));
        var doors = await DoorsAsync(ledger);

        var (ok, message) = await doors.DeleteSessionAsync("failed1", CancellationToken.None);

        Assert.False(ok);
        Assert.Contains("q1", message);
    }

    [Fact]
    public async Task A_delete_before_the_driver_is_up_is_the_cold_start_sentence()
    {
        var refused = await Assert.ThrowsAnyAsync<Exception>(
            () => Module().HelpDoors(null).DeleteSessionAsync("done1", CancellationToken.None));

        Assert.Contains("still coming up", refused.Message);
    }

    private async Task<IHelpDoors> DoorsAsync(LoopbackHost ledger)
    {
        (DriverConfig.Empty with { Drivable = ["engine"] }).Save(DriverConfigPath);
        var loop = Loop();
        var service = new ServiceClient(ledger.Address, null);
        await loop.ComeUpAsync(service);
        return new DriverModule(Bus, loop).HelpDoors(service);
    }

    /// <summary>The service's doors a look at the sessions reads: `engine` drivable, one ended session, one running, one failed.</summary>
    private LoopbackHost Ledger()
    {
        var ledger = new LoopbackHost();
        var root = Path.Combine(Home, "checkouts", "engine").Replace('\\', '/');
        static string Session(string id, string state, string quest, int minute) =>
            $$"""{"id":"{{id}}","quest":"{{quest}}","repository":"engine","adapter":"claude-code","state":"{{state}}","kind":"driven","created":"2026-10-02T09:{{minute:00}}:00Z","updated":"2026-10-02T09:{{minute + 1:00}}:00Z"}""";
        static string Quest(string id, string status) =>
            $$"""{"id":"{{id}}","from":"game","to":"engine","title":"The work of #{{id}}","body":"A body.","status":"{{status}}"}""";
        var records = $"[{Session("failed1", "failed", "q1", 0)},{Session("running1", "working", "q3", 40)},{Session("done1", "completed", "q4", 50)}]";
        ledger.Serve("/api/sessions?includeClosed=true", Encoding.UTF8.GetBytes(records));
        ledger.Serve("/api/sessions", Encoding.UTF8.GetBytes($"[{Session("running1", "working", "q3", 40)}]"));
        ledger.Serve("/api/quests?includeClosed=true", Encoding.UTF8.GetBytes($"[{Quest("q1", "Open")},{Quest("q3", "Taken")},{Quest("q4", "Done")}]"));
        ledger.Serve("/api/quests", Encoding.UTF8.GetBytes($"[{Quest("q1", "Open")},{Quest("q3", "Taken")}]"));
        ledger.Serve("/api/registry", Encoding.UTF8.GetBytes(
            $$"""[{"repository":"engine","adopted":true,"registered":true,"root":"{{root}}","workspace":"aurora"}]"""));
        return ledger;
    }
}
