using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 FOUNDATION1j part ii (the foundation design §2.4's first row and §7 row 5; D205 point 1): a continuation takes its record's role and
/// asked choice, never the machine's <c>adapter</c> key. A quest whose part ran on Build's second choice and was cut off is carried on with
/// that choice's agent and model: its start asks that agent's accounts, so where they cool it waits for their reset, never the first
/// agent's.
/// </summary>
/// <remarks>
/// Driven over the real client and the real planner, with the service's doors standing in (<see cref="StandInLedger"/>): the start's run is
/// the real one, which reaches the account walk and holds there before anything is spawned, the repository opening a tree per session and so
/// asking git nothing first. Both agents are present by a file look that asks nobody whether an account is signed in.
/// </remarks>
public sealed class ContinuedChoiceTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Now = new(DateTime.UtcNow.Date.AddHours(12), TimeSpan.Zero);

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "foundation1j-continued-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    public ContinuedChoiceTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
        _ledger.Register("engine", Path.Combine(_home, "engine"));
        foreach (var agent in new[] { "first", "second" }) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, agent, "account-1"));
        new HarnessSettings().WithDefault("first", "account-1").WithDefault("second", "account-1").Save(DaorisHome.Entry(_home, HomeEntry.Harnesses));
    }

    public void Dispose()
    {
        _ledger.Dispose();
        GitSeed.Discard(_home);
    }

    private string Command => Path.Combine(_home, "agent-here");

    private sealed class Adapter(string name, HarnessToolchain toolchain) : ISessionAdapter
    {
        public string Name => name;

        public SessionWire Wire => SessionWire.Acp;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => throw new InvalidOperationException("nothing spawns here");
    }

    private AdapterSet Adapters()
    {
        var present = new HarnessToolchain(Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true);
        return new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["first"] = new Adapter("first", present),
            ["second"] = new Adapter("second", present),
        });
    }

    /// <summary>Build's order: <c>first</c> · opus, then <c>second</c> · sonnet; the legacy <c>adapter</c> key written through as <c>first</c>.</summary>
    private static DriverConfig Config => ActorChoices.With(
        DriverConfig.Empty with { Drivable = ["engine"], Trees = ["engine"], Adapter = "first", Strikes = 3, PollSeconds = 1 },
        "build", [new ActorChoice("first", "opus"), new ActorChoice("second", "sonnet")]);

    private void Cool(string agent, DateTimeOffset until) =>
        AccountCooling.Cool(_home, new CoolingEntry(agent, "account-1", until, true, "weekly", Now, "s0"), Now);

    /// <summary>A part of quest <c>q1</c> that ran on Build's second choice, on <c>second</c> · sonnet, and was cut off with the quest taken.</summary>
    private async Task<string> CutOffOnTheSecondChoiceAsync(ServiceClient service)
    {
        _ledger.Publish("q1", "engine");
        var (id, _) = await service.OpenSessionAsync("q1", "second", null, "account-1", Path.Combine(_home, "engine"), null);
        await service.KeepActorAsync(id!, "2", new ActorEffective("second", "sonnet", null, "2").Asked);
        await service.AdvanceAsync(id!, "working");
        _ledger.Move("q1", "Taken");
        await service.AdvanceAsync(id!, "failed", note: "the agent's turn failed with the quest still taken.");
        return id!;
    }

    [Fact]
    public async Task A_part_on_Build_s_second_choice_is_carried_on_with_that_choice_and_waits_for_its_agent_s_reset_never_the_first_s()
    {
        using var service = _ledger.Client();
        await CutOffOnTheSecondChoiceAsync(service);
        // Each agent's one account cools, to its own reset: the hold names whose account the carry-on asked.
        Cool("first", Now.AddHours(2));
        Cool("second", Now.AddHours(5));
        var adapters = Adapters();
        var roster = new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)) { Clock = () => Now, Zone = Zone };

        var look = await new Daoris.Driver.Driver(service, Config, adapters, _home, harnesses: roster).TickAsync().WaitAsync(Poll.Patience);

        var sitting = Assert.Single(look.Considerations);
        Assert.Equal(StartVerdict.Blocked, sitting.Verdict);
        var wait = Assert.Single(look.Waits);
        Assert.Equal(("second", "second", "account-1", Now.AddHours(5)), (wait.Adapter, wait.Agent, wait.Account, wait.Until));
        Assert.Single(_ledger.Sessions);
    }

    /// <summary>
    /// The foundation design §2.4's fourth row: a reviewer's same-agent press reads the agent the work ran on, else the agent its record's
    /// choice was asked of, else its role's first entry; never the machine's <c>adapter</c> key. A named reviewer and the rule's are as before.
    /// </summary>
    [Fact]
    public void A_same_agent_press_reads_the_record_s_agent_never_the_machine_s_adapter()
    {
        var rule = new OpinionRule(["codex-acp"], [OpinionRules.Landing]);
        var press = new OpinionRequest(Now, "s1", OpinionRules.Landing) { SameAgent = true };
        var ran = new PriorSession("s1", null, "completed") { Adapter = "second", Asked = new ActorEffective("second", "sonnet", null, "2").Asked };

        Assert.Equal(["second"], OpinionGateLook.Reviewers(press, ran, rule, Config));
        Assert.Equal(["second"], OpinionGateLook.Reviewers(press, ran with { Adapter = null }, rule, Config));
        Assert.Equal(["first"], OpinionGateLook.Reviewers(press, null, rule, Config with { Adapter = "piped" }));
        Assert.Equal(["dsh"], OpinionGateLook.Reviewers(press with { SameAgent = false, Reviewer = "dsh" }, ran, rule, Config));
        Assert.Equal(["codex-acp"], OpinionGateLook.Reviewers(null, ran, rule, Config));
    }
}
