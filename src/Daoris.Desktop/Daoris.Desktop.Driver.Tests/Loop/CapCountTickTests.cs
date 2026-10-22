using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CAPCOUNT1 (D130's note): the cap counts what the driver starts, never a conversation the person holds. Driven over the real client
/// and the real planner, with the service's doors standing in, in-process (<see cref="StandInLedger"/>): the records the service lists
/// are read, counted once by <see cref="ActiveSessions.HoldsSlot"/>, and a look's quests and intakes take the slots they leave.
/// </summary>
public sealed class CapCountTickTests : IDisposable
{
    private static readonly TimeSpan Bound = TimeSpan.FromMinutes(2);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-capcount-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly CancellationTokenSource _closing = new(TimeSpan.FromMinutes(5));

    public CapCountTickTests()
    {
        Directory.CreateDirectory(_home);
        _ledger.Register("engine", Path.Combine(_home, "engine")).Register("tools", Path.Combine(_home, "tools-checkout"));
    }

    public void Dispose()
    {
        _closing.Cancel();
        _closing.Dispose();
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Tree(string name) => Path.Combine(_home, name);

    private static DriverConfig Config(int cap) => DriverConfig.Empty with
    {
        Drivable = ["engine", "tools"],
        Adapter = "stub",
        Cap = cap,
        PollSeconds = 1,
        Trees = ["engine"],
    };

    private (Daoris.Driver.Driver Driver, StandInRuns Runs) Driver(DriverConfig config)
    {
        var service = _ledger.Client();
        var runs = new StandInRuns(service, _ledger);
        var adapters = AdapterSet.Built();
        var driver = new Daoris.Driver.Driver(
            service, config, adapters, _home, harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            Runner = runs.Runner,
            Stops = runs.Stops,
        };
        runs.Keeping = driver.Running;
        return (driver, runs);
    }

    /// <summary>
    /// The install's own case: two Ask Daoris conversations rest between turns, and a cap of two still starts two quests at the look,
    /// where each held a slot and nothing started.
    /// </summary>
    [Fact]
    public async Task Two_idle_Ask_Daoris_conversations_take_no_slot_from_the_quests_a_look_starts()
    {
        _ledger.Working("h1", HelpRoom.Repository, Tree("room-1"));
        _ledger.Working("h2", HelpRoom.Repository, Tree("room-2"));
        _ledger.Publish("q1", "engine");
        _ledger.Publish("q2", "tools");
        var (driver, runs) = Driver(Config(cap: 2));

        var look = await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        Assert.Equal([StartVerdict.Start, StartVerdict.Start], look.Considerations.Select(c => c.Verdict));
        Assert.Equal(["q1", "q2"], runs.Started.Order());
    }

    /// <summary>A conversation the person opened in a repository takes none either, though it stays in the list its place is read from.</summary>
    [Fact]
    public async Task A_conversation_in_a_repository_takes_no_slot_and_still_holds_the_place_it_works_in()
    {
        _ledger.Working("c1", "tools", Tree("chat-tools"));
        _ledger.Publish("q1", "engine");
        _ledger.Publish("q2", "tools");
        var (driver, runs) = Driver(Config(cap: 1));

        var look = await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        // The cap of one is free for q1, and the conversation in `tools` holds that root (no tree of its own opens there), so q2 waits for it.
        Assert.Equal(StartVerdict.Start, look.Considerations.Single(c => c.Quest.Id == "q1").Verdict);
        var waits = look.Considerations.Single(c => c.Quest.Id == "q2");
        Assert.Equal(StartVerdict.RepositoryBusy, waits.Verdict);
        Assert.Contains("`c1`", waits.Reason);
        Assert.Equal(["q1"], runs.Started);
    }

    /// <summary>
    /// What the driver started still counts: a running intake, a helper, a partner (COAGENT1b: its record serves its lead and carries
    /// its piece) and a reviewer each hold the place of a quest.
    /// </summary>
    [Theory]
    [InlineData("intake")]
    [InlineData("helper")]
    [InlineData("partner")]
    [InlineData("reviewer")]
    public async Task A_running_intake_helper_or_reviewer_still_takes_a_slot_from_the_quests_a_look_starts(string kind)
    {
        _ledger.Working(
            "x1", kind == "intake" ? "ask #a1" : "tools", Tree("held"),
            ask: kind == "intake" ? "a1" : null, serves: kind is "helper" or "partner" ? "s9" : null, opinion: kind == "reviewer" ? "o1" : null,
            piece: kind == "partner" ? "pc000001" : null);
        _ledger.Publish("q1", "engine");
        var (driver, runs) = Driver(Config(cap: 1));

        var look = await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        Assert.Equal(StartVerdict.AtCapacity, Assert.Single(look.Considerations).Verdict);
        Assert.Empty(runs.Started);
    }

    /// <summary>
    /// The look's intakes take the slots the quests leave, counted the same way: an ask waits only for a place a conversation did not
    /// hold, and the line that says the cap is spent is not said for one.
    /// </summary>
    [Fact]
    public async Task An_ask_does_not_wait_for_a_slot_an_Ask_Daoris_conversation_rests_in()
    {
        _ledger.Working("h1", HelpRoom.Repository, Tree("room-1"));
        _ledger.Ask("a1");
        var (driver, _) = Driver(Config(cap: 1) with { IntakeAdapter = "stub" });

        var look = await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        Assert.DoesNotContain(look.Events, line => line.Contains("concurrency cap", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_ask_waits_for_the_slot_a_running_intake_holds()
    {
        _ledger.Working("i1", "ask #a0", Tree("intake-room"), ask: "a0");
        _ledger.Ask("a1");
        var (driver, _) = Driver(Config(cap: 1) with { IntakeAdapter = "stub" });

        var look = await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        Assert.Contains(look.Events, line => line.Contains("ask #a1 waiting: the concurrency cap (1) is spent", StringComparison.Ordinal));
    }
}
