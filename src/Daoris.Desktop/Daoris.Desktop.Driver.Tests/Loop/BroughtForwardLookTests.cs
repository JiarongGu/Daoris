using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SYNCMERGE1b (D178 points 3 and 8; the sync design's amendment): a session whose take lost is stood down with its tree kept (D68 §5), and
/// once the person brings its work forward, a look carries it on in that tree. Once, across a restart, from the records alone: the session
/// carried on is opened after the press, so neither its own end nor a new driver reading the same records carries the stand-down on again.
/// A bring-forward that lost again carries nothing.
/// </summary>
/// <remarks>
/// Driven over the real client and the real planner, with the service's doors and each start's run standing in, in-process, as
/// <see cref="SessionsOutliveTheirLookTests"/> drives them. What the carry-on's start does with its tree is
/// <c>StartInSyncTickTests</c>' and <c>GoOnInSyncTests</c>' to hold.
/// </remarks>
public sealed class BroughtForwardLookTests : IDisposable
{
    /// <summary>How long anything here may take: every wait is on a signal, so the bound elapses only on a real failure.</summary>
    private static readonly TimeSpan Bound = TimeSpan.FromMinutes(2);

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "brought-forward", Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    // Every look rides this, so a test that fails never leaves a stand-in session waiting.
    private readonly CancellationTokenSource _closing = new(TimeSpan.FromMinutes(5));

    public BroughtForwardLookTests()
    {
        Directory.CreateDirectory(_home);
        _ledger.Register("engine", Path.Combine(_home, "engine")).Register("tools", DaorisHome.Entry(_home, HomeEntry.ToolVersions));
    }

    public void Dispose()
    {
        _closing.Cancel();
        _closing.Dispose();
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// 🔴 The row's own case. s1's take lost and the driver stood it down; nothing goes on while the claim reads lost. The person brings it
    /// forward, and a driver started afresh carries s1 on, once: not again while that session runs, nor once it stood down in its turn, nor
    /// after a second restart.
    /// </summary>
    [Fact]
    public async Task A_stand_down_brought_forward_is_carried_on_once_across_a_restart()
    {
        using var sync = new RemoteSyncSet([]);
        _ledger.Publish("q1", "engine");
        var (first, runs) = Driver(sync);
        await first.TickAsync(_closing.Token).WaitAsync(Bound);
        _ledger.Claim = "lost";
        await first.TickAsync(_closing.Token).WaitAsync(Bound);
        await Poll.Until(() => State("s1") == "stood-down", () => $"s1 is {State("s1")}", Bound);
        var lostLook = await first.TickAsync(_closing.Token).WaitAsync(Bound);

        // A driver started afresh (its own runs), so what carries s1 on is the records alone.
        _ledger.BringForward();
        var (restarted, carrying) = Driver(sync);
        var carried = await restarted.TickAsync(_closing.Token).WaitAsync(Bound);
        Assert.Equal(["q1"], carrying.Started);
        var running = await restarted.TickAsync(_closing.Token).WaitAsync(Bound);
        // As the driver stops a take that lost: s2 stands down in its turn, the claim still held by the press.
        await Poll.Until(() => carrying.Stops("s2", Daoris.Driver.Driver.LostClaimNoted), () => "s2 is not yet running", Bound);
        await Poll.Until(() => State("s2") == "stood-down", () => $"s2 is {State("s2")}", Bound);
        var after = await restarted.TickAsync(_closing.Token).WaitAsync(Bound);
        var (again, more) = Driver(sync);
        var afterRestart = await again.TickAsync(_closing.Token).WaitAsync(Bound);

        Assert.DoesNotContain(lostLook.Considerations, c => c.Verdict == StartVerdict.Start);
        Assert.Equal(["q1"], runs.Started);
        var start = Assert.Single(carried.Considerations);
        Assert.Equal((StartVerdict.Start, "s1"), (start.Verdict, start.Resumes?.Session));
        Assert.Equal("held", start.BroughtForward?.Standing);
        Assert.StartsWith("carrying on in `engine` — you brought the work of session `s1` forward", start.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(running.Considerations, c => c.Verdict == StartVerdict.Start);
        Assert.DoesNotContain(after.Considerations, c => c.Verdict == StartVerdict.Start);
        Assert.Empty(afterRestart.Considerations);
        Assert.Empty(more.Started);
        Assert.Equal(["s1", "s2"], _ledger.SessionsFor("q1").Select(s => s["id"]!.GetValue<string>()));
    }

    /// <summary>A bring-forward that lost again (D178 point 3): nothing goes on, whatever looks; the conflict page says the second loss.</summary>
    [Fact]
    public async Task A_bring_forward_that_lost_again_carries_nothing()
    {
        using var sync = new RemoteSyncSet([]);
        _ledger.Publish("q1", "engine");
        var (driver, runs) = Driver(sync);
        await driver.TickAsync(_closing.Token).WaitAsync(Bound);
        _ledger.Claim = "lost";
        await driver.TickAsync(_closing.Token).WaitAsync(Bound);
        await Poll.Until(() => State("s1") == "stood-down", () => $"s1 is {State("s1")}", Bound);

        _ledger.BringForward("lost");
        var look = await driver.TickAsync(_closing.Token).WaitAsync(Bound);
        var (restarted, more) = Driver(sync);
        var afterRestart = await restarted.TickAsync(_closing.Token).WaitAsync(Bound);

        Assert.Empty(look.Considerations);
        Assert.Empty(afterRestart.Considerations);
        Assert.Equal(["q1"], runs.Started);
        Assert.Empty(more.Started);
        Assert.Single(_ledger.SessionsFor("q1"));
    }

    /// <summary>
    /// What the carried-on session is told of the one before it (D80's carry-on): its take lost and the person brought its work forward, so
    /// the quest is this machine's again, never the stop's words that its work would double someone else's; offline, that the fresh take may
    /// still lose (D178 point 8).
    /// </summary>
    [Fact]
    public void The_carried_on_session_is_told_the_press_and_offline_that_the_take_may_still_lose()
    {
        var held = Daoris.Driver.Driver.BroughtForwardTold(new ForwardTake("desk", 7, DateTimeOffset.UnixEpoch, "held"));
        var unconfirmed = Daoris.Driver.Driver.BroughtForwardTold(new ForwardTake("desk", 7, DateTimeOffset.UnixEpoch, "unconfirmed"));

        Assert.Contains("the person brought that work forward", held);
        Assert.EndsWith("the remote confirmed this machine's fresh take, so the quest is yours.", held);
        Assert.EndsWith(
            "this machine's fresh take is not yet confirmed by the remote, which may still refuse it: until then the quest is yours, and if it "
            + "is refused, this session is stopped as the one before it was.",
            unconfirmed);
        Assert.DoesNotContain("double someone else's", held + unconfirmed);
    }

    private string State(string session) => _ledger.Session(session)["state"]!.GetValue<string>();

    private (Daoris.Driver.Driver Driver, StandInRuns Runs) Driver(RemoteSyncSet sync)
    {
        var service = _ledger.Client();
        var runs = new StandInRuns(service, _ledger);
        var adapters = AdapterSet.Built();
        var driver = new Daoris.Driver.Driver(
            service, Config(), adapters, _home, sync: sync, harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            Runner = runs.Runner,
            Stops = runs.Stops,
        };
        runs.Keeping = driver.Running;
        return (driver, runs);
    }

    private static DriverConfig Config() => DriverConfig.Empty with
    {
        Drivable = ["engine", "tools"],
        Adapter = "stub",
        Cap = 3,
        PollSeconds = 1,
        Trees = ["engine"],
    };
}
