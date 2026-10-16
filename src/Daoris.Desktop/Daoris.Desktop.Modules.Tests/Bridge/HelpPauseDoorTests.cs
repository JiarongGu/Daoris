using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// PAUSE1f-c: a pause proposal's Apply is the work's own doors, the pause <c>WORK_PAUSE</c> makes and the resume
/// <c>WORK_RESUME</c> lifts, each judged again as it goes and coming back as its sentence; the facts the judge reads are asked
/// for only when one is pending. The service is stood in over a loopback socket and nothing starts a process: the fast half (MOD8).
/// </summary>
public sealed class HelpPauseDoorTests : DriverModuleBridge
{
    [Theory]
    [InlineData("pause", "pause", true)]
    [InlineData("pause", "resume", true)]
    [InlineData("session", "archive", false)]
    [InlineData("delete", "quest", false)]
    [InlineData("go", "quests", false)]
    public void The_work_is_read_for_a_pause_proposal_only(string kind, string door, bool read)
    {
        var proposal = new HelpProposal("p1", kind, door, "a1", null, null, null, "why", "h1", "proposed");

        Assert.Equal(read, HelpRoutes.JudgedAgainstWork([proposal]));
        Assert.False(HelpRoutes.JudgedAgainstWork([]));
    }

    [Fact]
    public async Task A_pause_stops_what_runs_and_says_so_in_the_pages_sentence()
    {
        using var ledger = Ledger();
        var doors = await DoorsAsync(ledger);

        var (ok, message) = await doors.PauseWorkAsync(WorkScope.Ask, "a1", CancellationToken.None);

        Assert.True(ok);
        Assert.Contains("w0rk1ng0", message);
        Assert.Equal("w0rk1ng0", DriverConfig.Load(DriverConfigPath).PausedAsk("a1")!.Stopped["q2"]);
    }

    [Fact]
    public async Task A_pause_of_work_this_machine_does_not_have_is_not_applied()
    {
        using var ledger = Ledger();
        var doors = await DoorsAsync(ledger);

        var (ok, message) = await doors.PauseWorkAsync(WorkScope.Quest, "q99", CancellationToken.None);

        Assert.False(ok);
        Assert.Contains("q99", message);
        Assert.False(File.Exists(DriverConfigPath) && DriverConfig.Load(DriverConfigPath).PausedQuest("q99") is not null);
    }

    [Fact]
    public async Task A_resume_lifts_the_pause_and_says_what_it_released()
    {
        using var ledger = Ledger();
        (DriverConfig.Empty with { Drivable = ["engine"] })
            .WithPausedAsk("a1", new WorkPause(DateTimeOffset.UtcNow, new Dictionary<string, string> { ["q2"] = "w0rk1ng0" }))
            .Save(DriverConfigPath);
        var doors = await DoorsAsync(ledger);

        var (ok, message) = await doors.ResumeWorkAsync(WorkScope.Ask, "a1", CancellationToken.None);

        Assert.True(ok);
        Assert.Contains("w0rk1ng0", message);
        Assert.Null(DriverConfig.Load(DriverConfigPath).PausedAsk("a1"));
    }

    [Fact]
    public async Task A_resume_of_work_not_paused_is_said_and_applied()
    {
        using var ledger = Ledger();
        var doors = await DoorsAsync(ledger);

        var (ok, _) = await doors.ResumeWorkAsync(WorkScope.Ask, "a1", CancellationToken.None);

        Assert.True(ok);
    }

    [Fact]
    public async Task A_resume_of_an_ask_this_machine_does_not_have_is_not_applied()
    {
        using var ledger = Ledger();
        var doors = await DoorsAsync(ledger);

        var (ok, message) = await doors.ResumeWorkAsync(WorkScope.Ask, "zz", CancellationToken.None);

        Assert.False(ok);
        Assert.Contains("zz", message);
    }

    [Fact]
    public async Task The_doors_before_the_driver_is_up_are_the_cold_start_sentence()
    {
        var refused = await Assert.ThrowsAnyAsync<Exception>(
            () => Module().HelpDoors(null).PauseWorkAsync(WorkScope.Ask, "a1", CancellationToken.None));

        Assert.Contains("still coming up", refused.Message);
    }

    /// <summary>
    /// The route end to end: the judge is given the work (<c>Work</c>) and the wiring, so the card's Apply pauses what it
    /// judged, and the file keeps the pause the driver then reads.
    /// </summary>
    [Fact]
    public async Task Apply_of_a_pause_proposal_judges_by_the_work_and_pauses_it()
    {
        using var ledger = Ledger();
        (DriverConfig.Empty with { Drivable = ["engine"] }).Save(DriverConfigPath);
        var module = await UpAsync(ledger);
        var folder = HelpProposals.FolderOf(Home);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "p1a2b3c4.json"),
            """{"id":"p1a2b3c4","kind":"pause","door":"pause","target":"#a1","why":"hold it","state":"proposed","by":{"session":"h1"}}""");

        var answer = await AnswerAsync(module, "HELP_APPLY", new { id = "p1a2b3c4" });

        Assert.True(answer.GetProperty("applied").GetBoolean());
        Assert.Equal("w0rk1ng0", DriverConfig.Load(DriverConfigPath).PausedAsk("a1")!.Stopped["q2"]);
    }

    private async Task<DriverModule> UpAsync(LoopbackHost ledger)
    {
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient(ledger.Address, null));
        return new DriverModule(Bus, loop);
    }

    private async Task<IHelpDoors> DoorsAsync(LoopbackHost ledger)
    {
        var loop = Loop();
        var service = new ServiceClient(ledger.Address, null);
        await loop.ComeUpAsync(service);
        return new DriverModule(Bus, loop).HelpDoors(service);
    }

    private string TreeOf(string name) => DaorisHome.Entry(Home, HomeEntry.Trees,"aurora", "engine", name).Replace('\\', '/');

    /// <summary>Ask <c>a1</c>'s work: <c>q1</c> open; <c>q2</c> taken, worked by a session nothing here runs; <c>q3</c> taken, its session waiting on you.</summary>
    private LoopbackHost Ledger()
    {
        var ledger = new LoopbackHost();
        string Session(string id, string state, string quest, int minute, string? tree = null) =>
            $$"""{"id":"{{id}}","quest":"{{quest}}","repository":"engine","adapter":"claude-code","state":"{{state}}","kind":"driven",{{(tree is null ? "" : $"\"tree\":\"{tree}\",")}}"created":"2026-10-03T09:{{minute:00}}:00Z","updated":"2026-10-03T09:{{minute + 1:00}}:00Z"}""";
        string Quest(string id, string status) =>
            $$"""{"id":"{{id}}","from":"ask #a1","to":"engine","title":"The work of #{{id}}","body":"A body.","status":"{{status}}"}""";
        var quests = $"[{Quest("q1", "Open")},{Quest("q2", "Taken")},{Quest("q3", "Taken")}]";
        var working = Session("w0rk1ng0", "working", "q2", 20);
        var parked = Session("p4rk3d00", "awaiting-person", "q3", 30);
        ledger.Serve("/api/quests?includeClosed=true", Bytes(quests));
        ledger.Serve("/api/quests", Bytes(quests));
        ledger.Serve("/api/sessions?includeClosed=true", Bytes($"[{Session("earl1er0", "failed", "q2", 10, TreeOf("s-1a2b3c4d"))},{working},{parked}]"));
        ledger.Serve("/api/sessions", Bytes($"[{working},{parked}]"));
        ledger.Serve("/api/sessions/w0rk1ng0/state", Bytes("""{"session":{"id":"w0rk1ng0","state":"stopped"},"message":"Session is now stopped."}"""));
        ledger.Serve("/api/asks/a1", Bytes("""{"id":"a1","workspace":"aurora","sentence":"Add the note field","state":"Published","tier":"named"}"""));
        ledger.Serve("/api/registry", Bytes($$"""[{"repository":"engine","adopted":true,"registered":true,"root":"{{Home.Replace('\\', '/')}}/engine","workspace":"aurora"}]"""));
        ledger.Serve("/api/quests/q2/claim", Bytes("""{"quest":"q2","claim":"held"}"""));
        ledger.Serve("/api/quests/q3/claim", Bytes("""{"quest":"q3","claim":"none"}"""));
        return ledger;
    }

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
}
