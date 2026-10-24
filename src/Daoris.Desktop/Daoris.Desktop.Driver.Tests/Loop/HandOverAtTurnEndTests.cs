using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MIXAGENT1c (D191: "a quest session's ask under `on` is taken at its turn's end, never mid-turn, its note written before the session
/// stops"): what a quest session's run asks at its turn's end, before its record is concluded. Where the rule or the person's yes lets
/// the ask go, the quest is held on the next agent with the agent's note and the run is told which agent, to conclude the record as
/// handed over; where it waits on the person, or is refused, nothing is taken and the run concludes as it would.
/// </summary>
public sealed class HandOverAtTurnEndTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-handask-end-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    public HandOverAtTurnEndTests()
    {
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));
        _ledger.Register("engine", Path.Combine(_home, "engine"));
        _ledger.Publish("q1", "engine");
        _ledger.Driven("s1", "q1", Path.Combine(_home, "tree-s1"), adapter: "stub");
    }

    public void Dispose()
    {
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private DriverHandOvers HandOvers(string? mode)
    {
        var config = DriverConfig.Empty with { Drivable = ["engine"], Adapter = "stub" };
        if (mode is not null) config = HandOverRules.Apply(config, new HandOverEdit { Repository = "engine", Set = new HandOverSet { Mode = mode, ModeGiven = true } });
        var adapters = AdapterSet.Built();
        var driver = new Daoris.Driver.Driver(_ledger.Client(), config, adapters, _home, harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            HandOverDoors = _ => Task.FromResult<IReadOnlyList<HandOverDoor>>([new("stub", true, null), new("codex-acp", true, "codex")]),
        };
        return new DriverHandOvers(driver.Context);
    }

    private void Asks(string id = "a1b2c3d4") =>
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Sessions, "s1" + HandOverAskFiles.AskSuffix),
            $$"""{ "id": "{{id}}", "session": "s1", "note": "ParserTests are left.", "askedBy": "agent", "at": "{{Moments.Write(T0)}}" }""");

    /// <summary>🔴 `on`: the quest is held on the next agent, the agent's note with it, before the record stops; the run is told the agent.</summary>
    [Fact]
    public async Task Under_on_the_ask_is_taken_at_the_turns_end_with_its_note()
    {
        Asks();

        var to = await HandOvers(HandOverRules.On).AtTurnEndAsync("s1", "q1", CancellationToken.None);

        Assert.Equal("codex-acp", to);
        var handed = new HandedQuests(_home).Read("q1")!;
        Assert.Equal(("codex-acp", "s1", HandOverWhy.Agent, HandOverBy.Agent, "ParserTests are left."),
            (handed.Agent, handed.From, handed.Why, handed.AskedBy, handed.Note));
        Assert.Equal(HandOverAskStates.Taken, new HandOverAskFiles(_home).ReadState("s1")!.State);
    }

    /// <summary>`ask`: nothing is taken at the turn's end; the person's yes is still to come, and with it the ask goes.</summary>
    [Fact]
    public async Task Under_ask_nothing_is_taken_until_the_persons_yes()
    {
        Asks();

        Assert.Null(await HandOvers(null).AtTurnEndAsync("s1", "q1", CancellationToken.None));
        Assert.Empty(new HandedQuests(_home).All());

        new HandOverAskFiles(_home).WriteState("s1", new HandOverAskState("a1b2c3d4", HandOverAskStates.Approved, null, "codex-acp", T0));
        Assert.Equal("codex-acp", await HandOvers(null).AtTurnEndAsync("s1", "q1", CancellationToken.None));
    }

    /// <summary>`off`, and no ask at all: nothing is taken, and an `off` ask is refused saying so.</summary>
    [Fact]
    public async Task Under_off_or_with_no_ask_nothing_is_taken()
    {
        Assert.Null(await HandOvers(HandOverRules.On).AtTurnEndAsync("s1", "q1", CancellationToken.None));

        Asks();
        Assert.Null(await HandOvers(HandOverRules.Off).AtTurnEndAsync("s1", "q1", CancellationToken.None));
        Assert.Equal((HandOverAskStates.Refused, HandOverAskWhy.Off),
            (new HandOverAskFiles(_home).ReadState("s1")!.State, new HandOverAskFiles(_home).ReadState("s1")!.Code));
        Assert.Empty(new HandedQuests(_home).All());
    }

    private DriverHandOvers HandOversOf(string? mode, params ActorChoice[] build)
    {
        var config = ActorChoices.With(DriverConfig.Empty with { Drivable = ["engine"], Adapter = "stub" }, "build", build);
        if (mode is not null) config = HandOverRules.Apply(config, new HandOverEdit { Repository = "engine", Set = new HandOverSet { Mode = mode, ModeGiven = true } });
        var adapters = AdapterSet.Built();
        var driver = new Daoris.Driver.Driver(_ledger.Client(), config, adapters, _home, harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            // The roster's order would be codex-acp then dsh; Build's own order is dsh, then codex-acp.
            HandOverDoors = _ => Task.FromResult<IReadOnlyList<HandOverDoor>>([new("stub", true, null), new("codex-acp", true, "codex"), new("dsh", true, null)]),
        };
        return new DriverHandOvers(driver.Context);
    }

    /// <summary>
    /// 🔴 MODELROLE1e (D205 §3.5(e), round 4's Opus N1): under <c>on</c> an agent's ask takes the next entry of Build's effective order on
    /// another agent, not the roster's next tool, and the quest's next start asks that entry's model (<c>handed</c>).
    /// </summary>
    [Fact]
    public async Task Under_on_the_ask_goes_to_builds_next_entry_on_another_agent_not_the_rosters()
    {
        Asks();
        var build = new ActorChoice[] { new("stub", "opus"), new("dsh", "m1"), new("codex-acp", "gpt-5") };

        var to = await HandOversOf(HandOverRules.On, build).AtTurnEndAsync("s1", "q1", CancellationToken.None);

        Assert.Equal("dsh", to);
        Assert.Equal("dsh", new HandedQuests(_home).Read("q1")!.Agent);
        var config = ActorChoices.With(DriverConfig.Empty with { Adapter = "stub" }, "build", build);
        Assert.Equal(new ActorEffective("dsh", "m1", null, "handed"), ActorChoices.Effective("build", config, handed: "dsh")[0]);
    }

    /// <summary>
    /// 🔴 MODELROLE1e (D205 §4.2, Opus B8, the row's proof): a Build whose agent's accounts are all cooling hands nothing over by itself,
    /// under <c>ask</c> and under <c>on</c> alike, since the rule governs an agent's ask and Daoris's offer is taken only on the person's press;
    /// the press names the actor's next choice and is kept whatever the rule says.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(HandOverRules.On)]
    public async Task A_cooling_build_hands_over_only_on_the_persons_press_under_ask_and_on_alike(string? mode)
    {
        AccountCooling.Cool(_home, new CoolingEntry("stub", null, T0.AddHours(3), true, "weekly", T0, "s1"), T0);
        var build = new ActorChoice[] { new("stub", "opus"), new("dsh", "m1"), new("codex-acp", "gpt-5") };
        var handOvers = HandOversOf(mode, build);

        // No ask from the agent: the standing rule hands nothing over, however the repository's mode is set.
        Assert.Null(await handOvers.AtTurnEndAsync("s1", "q1", CancellationToken.None));
        Assert.Empty(new HandedQuests(_home).All());
        Assert.Null(new HandOverBox(_home).Read("s1"));

        // The person's press, at the offer's choice (Build's next entry on another agent), is kept under either rule.
        var next = HandOverOrder.Steps("stub", [new("stub", true, null), new("codex-acp", true, "codex"), new("dsh", true, null)], ActorChoices.Effective("build", ActorChoices.With(DriverConfig.Empty with { Adapter = "stub" }, "build", build)))[0];
        Assert.Equal(("dsh", "m1", "2"), (next.Harness, next.Model, next.From));
        var pressed = await QuestHandOver.AskAsync(
            _ledger.Client(), _home, _ => new HandOverAgent([], SessionWire.Acp), "s1", next.Harness, null, HandOverWhy.Limit, HandOverBy.Person, T0);
        Assert.True(pressed.Kept, pressed.Message);
        Assert.Equal("dsh", new HandOverBox(_home).Read("s1")!.Agent);
    }

    /// <summary>🔴 A quest's session a hand-over started works its quest in its first turn: its ask then is refused, no second hand-over in a row.</summary>
    [Fact]
    public async Task A_session_a_hand_over_started_is_not_handed_over_again_at_its_first_turns_end()
    {
        _ledger.Continues("s1", "s0", HandOverWhy.Agent, HandOverBy.Agent);
        Asks();

        Assert.Null(await HandOvers(HandOverRules.On).AtTurnEndAsync("s1", "q1", CancellationToken.None));
        Assert.Equal(HandOverAskWhy.JustHanded, new HandOverAskFiles(_home).ReadState("s1")!.Code);
    }
}
