using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MODELROLE1c (D205 §4.1, §4.5): a new start walks Build's effective order choice by choice, each choice's accounts by the walk
/// before the next. An entry that cannot run here at all is passed by its code (<c>unavailable</c>, <c>no-connector</c>), a choice
/// whose every account is cooling is passed with the walk's own reason, and the first that can run is taken; with none, the wait's
/// sentence names each choice and the soonest cool-off rides beside it. An order of one is today's walk exactly.
/// </summary>
/// <remarks>
/// Nothing here starts a process or asks git: the walk is asked directly, and each agent is present by a file look that asks nobody
/// whether an account is signed in (<see cref="HarnessToolchain.ProbeByPresence"/>), as <c>RefusedByTheHarnessTests</c>' are.
/// </remarks>
public sealed class BuildWalkTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Now = new(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "modelrole1c-walk-" + Guid.NewGuid().ToString("N")[..8]);

    public BuildWalkTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
        foreach (var agent in new[] { "cool", "fine", "piped" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, agent, "account-1"));
        }

        new HarnessSettings().WithDefault("cool", "account-1").WithDefault("fine", "account-1").WithDefault("piped", "account-1").Save(Settings);
    }

    public void Dispose()
    {
        _log?.Dispose();
        GitSeed.Discard(_home);
    }

    private MachineLog? _log;

    private string Settings => DaorisHome.Entry(_home, HomeEntry.Harnesses);

    private string Command => Path.Combine(_home, "agent-here");

    private sealed class Adapter(string name, HarnessToolchain toolchain, SessionWire wire = SessionWire.Acp) : ISessionAdapter
    {
        public string Name => name;

        public SessionWire Wire => wire;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => throw new InvalidOperationException("nothing spawns here");
    }

    private Daoris.Driver.Driver Driver(params ActorChoice[] build)
    {
        var present = new HarnessToolchain(Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true);
        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["cool"] = new Adapter("cool", present),
            ["fine"] = new Adapter("fine", present),
            ["piped"] = new Adapter("piped", present, SessionWire.Pipe),
        });
        var roster = new HarnessRoster(adapters, Settings) { Clock = () => Now, Zone = Zone };
        var config = ActorChoices.With(DriverConfig.Empty with { Adapter = "cool", Drivable = ["engine"] }, "build", build);
        return new Daoris.Driver.Driver(new ServiceClient("http://127.0.0.1:9", null), config, adapters, _home, harnesses: roster) { Log = _log };
    }

    private static string Said(JsonElement line) => $"{line.GetProperty("choice").GetString()}:{line.GetProperty("agent").GetString()}:{line.GetProperty("code").GetString()}";

    /// <summary>The machine log's lines of one event so far, each as the data its line holds.</summary>
    private List<JsonElement> Logged(string @event)
    {
        var lines = StubFile.Lines(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Logs), "2026-10-24.driver.jsonl"));
        return [.. lines.Select(line => JsonDocument.Parse(line).RootElement).Where(line => line.GetProperty("event").GetString() == @event)
            .Select(line => line.GetProperty("data"))];
    }

    private static Consideration Start => new(new QuestView("q1", "platform", "engine", "Split the loader", "Two jobs.", "Open"), StartVerdict.Start, "", Workspace: "default");

    private void Cooling(string agent) =>
        AccountCooling.Cool(_home, new CoolingEntry(agent, "account-1", Now.AddHours(2), true, "weekly", Now, "s0"), Now);

    [Fact]
    public async Task An_entry_not_here_and_a_cooling_one_are_passed_and_the_first_that_can_run_is_taken()
    {
        Cooling("cool");
        var driver = Driver(new("gone"), new("cool", "opus", "high"), new("fine", "sonnet"));

        var (selection, chosen) = await driver.WalkBuildAsync(Start, [new RepoView("engine", true, null)], CancellationToken.None);

        Assert.True(selection.Allowed, selection.Refusal);
        Assert.Equal(new ActorEffective("fine", "sonnet", null, "3"), chosen);
        Assert.Equal(new ActorAsked("fine", new("entry", "sonnet", true), new("account-default", null, false)), chosen!.Asked);
    }

    [Fact]
    public async Task The_order_is_a_preference_so_a_free_first_choice_is_taken_before_a_later_one()
    {
        var driver = Driver(new("cool", "opus"), new("fine"));

        var (selection, chosen) = await driver.WalkBuildAsync(Start, [new RepoView("engine", true, null)], CancellationToken.None);

        Assert.True(selection.Allowed, selection.Refusal);
        Assert.Equal("1", chosen!.From);
        Assert.Equal("cool", chosen.Agent);
    }

    [Fact]
    public async Task With_no_choice_that_can_run_the_start_waits_naming_each_and_the_soonest_cool_off()
    {
        Cooling("cool");
        Cooling("fine");
        var driver = Driver(new("gone"), new("cool", "opus"), new("fine"));

        var (selection, chosen) = await driver.WalkBuildAsync(Start, [new RepoView("engine", true, null)], CancellationToken.None);

        Assert.Null(chosen);
        Assert.False(selection.Allowed);
        Assert.StartsWith("Build waits: none of its choices can run now.", selection.Refusal, StringComparison.Ordinal);
        Assert.Contains("choice 1, gone · account default: `gone` is not an agent on this machine", selection.Refusal, StringComparison.Ordinal);
        Assert.Contains("choice 2, cool · opus:", selection.Refusal, StringComparison.Ordinal);
        Assert.Contains("choice 3, fine · account default:", selection.Refusal, StringComparison.Ordinal);
        Assert.Equal(Now.AddHours(2), selection.Cooling!.Until);
    }

    [Fact]
    public async Task A_pipe_entry_is_passed_where_the_repository_has_not_adopted_and_the_door_is_the_protocol_s_where_any_entry_is()
    {
        var driver = Driver(new("piped"), new("fine"));

        var (unadopted, taken) = await driver.WalkBuildAsync(Start, [new RepoView("engine", false, "D:/engine")], CancellationToken.None);
        Assert.True(unadopted.Allowed, unadopted.Refusal);
        Assert.Equal("fine", taken!.Agent);

        var (adopted, first) = await driver.WalkBuildAsync(Start, [new RepoView("engine", true, null)], CancellationToken.None);
        Assert.True(adopted.Allowed, adopted.Refusal);
        Assert.Equal("piped", first!.Agent);
    }

    /// <summary>`cool` given a second account, both in its list, so a reading can pass one and leave the other (D205 §3.3).</summary>
    private void TwoAccounts()
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "cool", "account-2"));
        HarnessSettings.Load(Settings).WithRotation("cool", ["account-1", "account-2"]).Save(Settings);
    }

    private void Refuses(string agent, string account, string model) =>
        Assert.Null(AgentOffers.Refused(_home, agent, null, account, model, DateTimeOffset.UtcNow));

    /// <summary>
    /// MODELROLE1c2's proof (D205 §3.3, §13's per-account walk): a first choice naming a model one account refused is run on the other
    /// account, the refusing account passed for this start and never cooled.
    /// </summary>
    [Fact]
    public async Task A_first_choice_one_account_does_not_offer_runs_on_the_other_account()
    {
        TwoAccounts();
        Refuses("cool", "account-1", "opus");
        var driver = Driver(new("cool", "opus"), new("fine", "sonnet"));

        var walked = await driver.WalkBuildPassesAsync(Start, [new RepoView("engine", true, null)], CancellationToken.None);

        Assert.True(walked.Selection.Allowed, walked.Selection.Refusal);
        Assert.Equal("1", walked.Chosen!.From);
        Assert.Equal("account-2", walked.Selection.Profile);
        Assert.Empty(walked.Passes);
        Assert.DoesNotContain(AccountCooling.Read(_home, Now), entry => entry.Account == "account-1");
    }

    /// <summary>
    /// MODELROLE1c2's proof (D205 §3.3, §4.1): with neither account offering the first choice's model, the start runs its second, the first
    /// passed `not-offered`; another model of the same agent is judged apart.
    /// </summary>
    [Fact]
    public async Task With_neither_account_offering_the_first_choice_the_second_runs_and_the_pass_says_not_offered()
    {
        TwoAccounts();
        Refuses("cool", "account-1", "opus");
        Refuses("cool", "account-2", "opus");
        var driver = Driver(new("cool", "opus"), new("cool", "sonnet"));

        var walked = await driver.WalkBuildPassesAsync(Start, [new RepoView("engine", true, null)], CancellationToken.None);

        Assert.True(walked.Selection.Allowed, walked.Selection.Refusal);
        Assert.Equal(new ActorEffective("cool", "sonnet", null, "2"), walked.Chosen);
        var pass = Assert.Single(walked.Passes);
        Assert.Equal(("1", AgentOffers.NotOffered), (pass.Entry.From, pass.Code));
    }

    /// <summary>
    /// D205 §3.3's re-ask at the open, the refusal passed as if known before the spawn, taking no count: a later choice of the same agent
    /// on the same account is set on the open session; any other answer (here another account of the refused choice) ends the record.
    /// </summary>
    [Fact]
    public async Task The_re_ask_answers_a_same_account_move_only_where_the_walk_takes_the_same_agent_on_the_same_account()
    {
        var driver = Driver(new("cool", "opus"), new("cool", "sonnet"));
        var first = new ActorEffective("cool", "opus", null, "1");
        var onOne = new HarnessSelection(null, "account-1");

        var moved = await driver.ReaskAsync(Start, [new RepoView("engine", true, null)], first, onOne, CancellationToken.None);
        Assert.Equal(("2", "sonnet"), (moved!.Value.Choice, moved.Value.Asked.NamedModel));

        TwoAccounts();
        Assert.Null(await driver.ReaskAsync(Start, [new RepoView("engine", true, null)], first, onOne, CancellationToken.None));
    }

    /// <summary>
    /// LOGCOLLECTION1: a quest held at every look is told once, then again only when what was passed or why changes: the install wrote
    /// 10,488 `actor.passed` lines in a day for one held quest. The passes the walk took are `actor.chose`'s words, not its sentences.
    /// </summary>
    [Fact]
    public async Task A_pass_is_logged_when_it_is_new_or_changes_and_not_at_every_unchanged_look()
    {
        _log = new MachineLog(_home, "driver", () => Now);
        Cooling("cool");
        Cooling("fine");
        var driver = Driver(new("gone"), new("cool", "opus"), new("fine"));
        var registry = new[] { new RepoView("engine", true, null) };
        var other = Start with { Quest = new QuestView("q2", "platform", "engine", "Split the loader", "Two jobs.", "Open") };

        async Task<int> Look(Consideration start) =>
            (await driver.WalkBuildPassesAsync(start, registry, CancellationToken.None)).Passes.Count;

        Assert.Equal(3, await Look(Start));
        Assert.Equal(["1:gone:unavailable", "2:cool:cooling", "3:fine:cooling"], Logged("actor.passed").Select(line => Said(line)));
        Assert.Equal(3, await Look(Start));
        Assert.Equal(3, await Look(Start));
        Assert.Equal(3, Logged("actor.passed").Count);

        // Another quest held the same way is its own first look.
        Assert.Equal(3, await Look(other));
        Assert.Equal(6, Logged("actor.passed").Count);

        // The cool-offs lift: `cool` is taken, `gone` still passed as before, and the line for it says the walk's words.
        File.Delete(AccountCooling.PathOf(_home));
        Assert.Equal(1, await Look(Start));
        Assert.Equal(6, Logged("actor.passed").Count);
        var chose = Assert.Single(Logged("actor.chose"));
        Assert.Equal(["1:gone:unavailable"], chose.GetProperty("passed").EnumerateArray().Select(word => word.GetString()));

        // `cool` and `fine` cool again: those two are new against the look before, `gone` is not.
        Cooling("cool");
        Cooling("fine");
        Assert.Equal(3, await Look(Start));
        Assert.Equal(["2:cool:cooling", "3:fine:cooling"], Logged("actor.passed").Skip(6).Select(line => Said(line)));
        Assert.Equal(3, await Look(Start));
        Assert.Equal(8, Logged("actor.passed").Count);
    }

    /// <summary>A re-ask at an open takes no count of its own, so it writes no pass and leaves the quest's last look as it was.</summary>
    [Fact]
    public async Task A_re_ask_writes_no_pass_and_does_not_change_what_the_last_look_remembers()
    {
        _log = new MachineLog(_home, "driver", () => Now);
        Cooling("cool");
        var driver = Driver(new("cool", "opus"), new("cool", "sonnet"), new("fine"));
        var registry = new[] { new RepoView("engine", true, null) };
        await driver.WalkBuildPassesAsync(Start, registry, CancellationToken.None);
        var written = Logged("actor.passed").Count;
        Assert.Equal(2, written);

        await driver.ReaskAsync(Start, registry, new ActorEffective("fine", null, null, "3"), new HarnessSelection(null, "account-1"), CancellationToken.None);
        await driver.WalkBuildPassesAsync(Start, registry, CancellationToken.None);

        Assert.Equal(written, Logged("actor.passed").Count);
    }

    [Fact]
    public async Task An_order_of_one_is_today_s_walk_with_its_own_refusal()
    {
        Cooling("cool");
        var driver = Driver(new ActorChoice("cool"));

        var (selection, chosen) = await driver.WalkBuildAsync(Start, [new RepoView("engine", true, null)], CancellationToken.None);

        Assert.False(selection.Allowed);
        Assert.DoesNotContain("Build waits", selection.Refusal, StringComparison.Ordinal);
        Assert.Equal("cool", chosen!.Agent);
    }
}
