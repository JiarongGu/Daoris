using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1j part i (the foundation design §2.4, D212 point 4): one start plan. A start of a work walks its seeded role's effective order
/// choice by choice, D197's account walk inside each choice, every entry passed in one vocabulary, and answers a settled selection whose one
/// reservation the roster took at the selection: a probe takes none, a look's starts spread across accounts as each is chosen, and the
/// re-ask at the open takes no second one.
/// </summary>
/// <remarks>
/// Nothing here starts a process or asks git: each agent is present by a file look that asks nobody whether an account is signed in
/// (<see cref="HarnessToolchain.ProbeByPresence"/>), as <c>BuildWalkTests</c>' are. The clock is the day the test runs, which names the
/// machine log's file.
/// </remarks>
public sealed class StartPlanTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Now = new(DateTime.UtcNow.Date.AddHours(12), TimeSpan.Zero);

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "foundation1j-plan-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly AdapterSet _adapters;

    private readonly HarnessRoster _roster;

    private readonly PassMemory _memory = new();

    private MachineLog? _log;

    public StartPlanTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
        foreach (var agent in new[] { "cool", "fine", "piped" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, agent, "account-1"));
        }

        new HarnessSettings().WithDefault("cool", "account-1").WithDefault("fine", "account-1").WithDefault("piped", "account-1").Save(Settings);
        var present = new HarnessToolchain(Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true);
        _adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["cool"] = new Adapter("cool", present),
            ["fine"] = new Adapter("fine", present),
            ["piped"] = new Adapter("piped", present, SessionWire.Pipe),
        });
        _roster = new HarnessRoster(_adapters, Settings) { Clock = () => Now, Zone = Zone };
    }

    public void Dispose()
    {
        _log?.Dispose();
        GitSeed.Discard(_home);
    }

    private string Settings => DaorisHome.Entry(_home, HomeEntry.Harnesses);

    private string Command => Path.Combine(_home, "agent-here");

    private sealed class Adapter(string name, HarnessToolchain toolchain, SessionWire wire = SessionWire.Acp) : ISessionAdapter
    {
        public string Name => name;

        public SessionWire Wire => wire;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => throw new InvalidOperationException("nothing spawns here");
    }

    /// <summary>A quest's plan over <paramref name="choices"/> as the work's seeded role's order, its passes said under <c>quest:q1</c>.</summary>
    private StartPlan Plan(string work, params ActorChoice[] choices) => Plan(work, null, choices);

    private StartPlan Plan(string work, Func<ActorEffective, ISessionAdapter, (string Code, string Why)?>? gate, params ActorChoice[] choices)
    {
        var config = ActorChoices.With(DriverConfig.Empty with { Adapter = "cool" }, StartPlan.RoleOf(work), choices);
        return new StartPlan(work, config, _roster, _adapters, _memory)
        {
            Workspace = "default", Subject = "quest:q1", Named = ("quest", "q1"), Log = _log, Gate = gate,
        };
    }

    private void Cooling(string agent) =>
        AccountCooling.Cool(_home, new CoolingEntry(agent, "account-1", Now.AddHours(2), true, "weekly", Now, "s0"), Now);

    /// <summary>`cool` given a second account, both in its list, so a look's starts have two accounts to spread across.</summary>
    private void TwoAccounts()
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "cool", "account-2"));
        HarnessSettings.Load(Settings).WithRotation("cool", ["account-1", "account-2"]).Save(Settings);
    }

    /// <summary>The machine log's lines of one event so far, each as the data its line holds.</summary>
    private List<JsonElement> Logged(string @event)
    {
        var day = Now.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var lines = StubFile.Lines(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Logs), $"{day}.driver.jsonl"));
        return [.. lines.Select(line => JsonDocument.Parse(line).RootElement).Where(line => line.GetProperty("event").GetString() == @event)
            .Select(line => line.GetProperty("data"))];
    }

    private static string Said(JsonElement line) => $"{line.GetProperty("choice").GetString()}:{line.GetProperty("agent").GetString()}:{line.GetProperty("code").GetString()}";

    private static IEnumerable<string> Keys(JsonElement line) => line.EnumerateObject().Select(property => property.Name);

    /// <summary>
    /// C5-3.6: the plan's role is the seeded role of the work (the design's §2.2 table), Build's for design work while no role takes it, and its
    /// order the role's <see cref="ActorChoices.Effective"/>, read once for the start; no catalogue is read until row x.
    /// </summary>
    [Fact]
    public void A_work_walks_its_seeded_role_s_effective_order_and_design_work_walks_Build_s()
    {
        Assert.Equal(
            [
                ("build", "build"), ("design", "build"), ("chat", "chat"), ("ask-daoris", "ask-daoris"), ("intake", "intake"), ("help", "helper"),
                ("partner", "partner"), ("review", "opinion"), ("brainstorm", "brainstorm"),
            ],
            ParticipantWork.Codes.Select(work => (work, StartPlan.RoleOf(work))));
        Assert.All(ParticipantWork.Codes, work => Assert.Contains(StartPlan.RoleOf(work), ActorChoices.Ids));
        Assert.Throws<ArgumentOutOfRangeException>(() => StartPlan.RoleOf("garden"));

        var plan = Plan(ParticipantWork.Design, new ActorChoice("cool", "opus", "high"), new ActorChoice("fine"));

        Assert.Equal(("design", "build"), (plan.Work, plan.Role));
        Assert.Equal([new ActorEffective("cool", "opus", "high", "1"), new ActorEffective("fine", null, null, "2")], plan.Order);
    }

    /// <summary>The pass codes are one closed set (survey §1.3 row 3's two sets made one), and an account walk that held is read by its facts.</summary>
    [Fact]
    public void One_vocabulary_names_every_pass_and_reads_a_held_account_walk_by_its_facts()
    {
        Assert.Equal(["unavailable", "no-posture", "no-connector", "not-offered", "cooling", "signed-out", "held"], StartPasses.Codes);
        Assert.Equal(AgentOffers.NotOffered, StartPasses.NotOffered);

        var cooling = new CoolingEntry("cool", "account-1", Now.AddHours(2), true, "weekly", Now, "s0");
        var signedOut = SignedOutAccounts.Of("cool", ["account-1"], null);
        Assert.Equal(StartPasses.NotOffered, StartPasses.Of(new HarnessSelection("held") { NotOffered = ["account-1"], Cooling = cooling }));
        Assert.Equal(StartPasses.Cooling, StartPasses.Of(new HarnessSelection("held") { Cooling = cooling, SignedOut = signedOut }));
        Assert.Equal(StartPasses.SignedOut, StartPasses.Of(new HarnessSelection("held") { SignedOut = signedOut }));
        Assert.Equal(StartPasses.Held, StartPasses.Of(new HarnessSelection("held")));
    }

    /// <summary>
    /// 🔴 Exactly one reservation per start: each choice the walk reaches asks its account walk once, a passed choice's walk counts nothing,
    /// and the one count is the taken choice's, carried on the settled selection the start opens on.
    /// </summary>
    [Fact]
    public async Task A_start_takes_one_reservation_at_its_selection_however_many_choices_it_passes()
    {
        Cooling("cool");
        var plan = Plan(ParticipantWork.Build, new ActorChoice("gone"), new ActorChoice("cool", "opus"), new ActorChoice("fine", "sonnet"));
        var (counted, walks) = (_roster.Mark(), _roster.Selected);

        var settled = await plan.ResolveAsync(CancellationToken.None);

        Assert.True(settled.Selection.Allowed, settled.Selection.Refusal);
        Assert.Equal(("build", new ActorEffective("fine", "sonnet", null, "3")), (settled.Role, settled.Chosen));
        Assert.Equal(["1:gone:unavailable", "2:cool:cooling"], settled.Passes.Select(pass => pass.Word));
        // `gone` asks no account walk; `cool`'s held and `fine`'s took the start: two walks asked, one start counted.
        Assert.Equal((counted + 1, walks + 2), (_roster.Mark(), _roster.Selected));
        Assert.Equal(_roster.Mark(), settled.Reservation);
        Assert.Equal("fine", settled.Adapter!.Name);
    }

    /// <summary>A probe (<c>counts: false</c>) takes no count, says no pass and remembers none, so the start after it is its quest's first look.</summary>
    [Fact]
    public async Task A_probe_takes_no_count_says_nothing_and_leaves_the_last_look_as_it_was()
    {
        _log = new MachineLog(_home, "driver", () => Now);
        Cooling("cool");
        var counted = _roster.Mark();

        var probe = await Plan(ParticipantWork.Build, new ActorChoice("gone"), new ActorChoice("cool"), new ActorChoice("fine"))
            .ResolveAsync(CancellationToken.None, counts: false);

        Assert.Equal(("fine", 0L), (probe.Chosen!.Agent, probe.Reservation));
        Assert.Equal(counted, _roster.Mark());
        Assert.Empty(Logged("actor.chose"));
        Assert.Empty(Logged("actor.passed"));

        var started = await Plan(ParticipantWork.Build, new ActorChoice("gone"), new ActorChoice("cool"), new ActorChoice("fine"))
            .ResolveAsync(CancellationToken.None);

        Assert.Equal(counted + 1, started.Reservation);
        Assert.Equal(["1:gone:unavailable", "2:cool:cooling"], Logged("actor.passed").Select(Said));
        Assert.Equal(["1:gone:unavailable", "2:cool:cooling"], Assert.Single(Logged("actor.chose")).GetProperty("passed").EnumerateArray().Select(word => word.GetString()));
    }

    /// <summary>An order of one entry is today's walk exactly: its one account walk asked with the caller's count, its own refusal, no pass.</summary>
    [Fact]
    public async Task An_order_of_one_asks_its_agent_once_with_the_caller_s_count_and_passes_nothing()
    {
        _log = new MachineLog(_home, "driver", () => Now);
        Cooling("cool");
        var (counted, walks) = (_roster.Mark(), _roster.Selected);

        var held = await Plan(ParticipantWork.Build, new ActorChoice("cool")).ResolveAsync(CancellationToken.None);
        var probe = await Plan(ParticipantWork.Build, new ActorChoice("fine")).ResolveAsync(CancellationToken.None, counts: false);

        Assert.False(held.Selection.Allowed);
        Assert.DoesNotContain("Build waits", held.Selection.Refusal, StringComparison.Ordinal);
        Assert.Equal(("cool", "fine"), (held.Chosen!.Agent, probe.Chosen!.Agent));
        Assert.Empty(held.Passes);
        Assert.Null(held.Adapter);
        Assert.True(probe.Selection.Allowed, probe.Selection.Refusal);
        Assert.Equal((counted, walks + 2, 0L), (_roster.Mark(), _roster.Selected, probe.Reservation));
        Assert.Empty(Logged("actor.passed"));
        Assert.Empty(Logged("actor.chose"));

        var started = await Plan(ParticipantWork.Build, new ActorChoice("fine")).ResolveAsync(CancellationToken.None);
        Assert.Equal((counted + 1, counted + 1), (_roster.Mark(), started.Reservation));
    }

    /// <summary>
    /// 🔴 A look's starts spread as today (TOOL6b, PARALLEL1c): the count is taken at the selection, before the next start's walk in the same
    /// look, so a second start goes to the account the first left; a probe between them counts nothing; and a start whose agent refused it
    /// at the open gives its one count back, so the next start takes that account again.
    /// </summary>
    [Fact]
    public async Task A_look_s_starts_spread_across_accounts_as_each_is_chosen_and_a_refused_start_gives_its_count_back()
    {
        TwoAccounts();
        var plan = Plan(ParticipantWork.Build, new ActorChoice("gone"), new ActorChoice("cool"));

        var first = await plan.ResolveAsync(CancellationToken.None);
        var probe = await plan.ResolveAsync(CancellationToken.None, counts: false);
        var second = await plan.ResolveAsync(CancellationToken.None);

        Assert.Equal(("account-1", "account-2", "account-2"), (first.Selection.Profile, probe.Selection.Profile, second.Selection.Profile));
        Assert.Equal((first.Reservation + 1, 0L), (second.Reservation, probe.Reservation));

        _roster.Release(second.Reservation);
        var third = await plan.ResolveAsync(CancellationToken.None);

        Assert.Equal("account-2", third.Selection.Profile);
    }

    /// <summary>
    /// 🔴 The settled selection opens once: the re-ask at the open walks again as a probe ranking without the start's own count, and its
    /// same-account answer is set on the session already open, so the start keeps the one reservation its settled selection carries.
    /// </summary>
    [Fact]
    public async Task The_settled_selection_opens_once_its_re_ask_moving_on_the_same_account_with_no_second_reservation()
    {
        _log = new MachineLog(_home, "driver", () => Now);
        var plan = Plan(ParticipantWork.Build, new ActorChoice("cool", "opus"), new ActorChoice("cool", "sonnet"));
        var settled = await plan.ResolveAsync(CancellationToken.None);
        var (counted, walks) = (_roster.Mark(), _roster.Selected);

        var moved = await plan.ReaskAsync(settled.Chosen!, settled.Selection, CancellationToken.None);

        Assert.Equal(("2", "sonnet"), (moved!.Value.Choice, moved.Value.Asked.NamedModel));
        Assert.Equal((counted, walks + 2), (_roster.Mark(), _roster.Selected));
        Assert.Equal(counted, settled.Reservation);
        var reasked = Assert.Single(Logged("actor.reasked"));
        Assert.Equal(["actor", "quest", "refused", "answer", "same"], Keys(reasked));
        Assert.Equal(("1", "2", true), (reasked.GetProperty("refused").GetString(), reasked.GetProperty("answer").GetString(), reasked.GetProperty("same").GetBoolean()));
    }

    /// <summary>The caller's gate passes an entry by its code before any of its accounts is asked (Build's <c>no-posture</c> and <c>no-connector</c>).</summary>
    [Fact]
    public async Task An_entry_the_caller_s_gate_refuses_is_passed_by_its_code_before_any_account_is_asked()
    {
        var plan = Plan(
            ParticipantWork.Build,
            (entry, adapter) => adapter.Wire == SessionWire.Pipe ? (StartPasses.NoConnector, $"`{entry.Agent}` cannot take a quest here") : null,
            new ActorChoice("piped"), new ActorChoice("fine"));
        var walks = _roster.Selected;

        var settled = await plan.ResolveAsync(CancellationToken.None);

        Assert.Equal("fine", settled.Chosen!.Agent);
        Assert.Equal(["1:piped:no-connector"], settled.Passes.Select(pass => pass.Word));
        Assert.Equal(walks + 1, _roster.Selected);
    }

    /// <summary>
    /// FOUNDATION1j part ii: a plan whose rule names who may run walks its work's role over that fence, each agent taking the role's entries for
    /// it, else <c>rule-default</c>; a caller holding its entries already (a helper's own order, a recheck's continuation) walks exactly those.
    /// </summary>
    [Fact]
    public void A_plan_walks_its_rule_s_fence_or_the_entries_its_caller_holds()
    {
        var config = ActorChoices.With(
            DriverConfig.Empty with { Adapter = "cool" }, "opinion", [new ActorChoice("fine", "opus"), new ActorChoice("cool", "sonnet")]);

        var fenced = new StartPlan(ParticipantWork.Review, config, _roster, _adapters, _memory, fence: ["cool", "gone"]) { Subject = "opinion" };
        IReadOnlyList<ActorEffective> held = [new ActorEffective("fine", "haiku", null, "rule-default")];
        var handed = new StartPlan(ParticipantWork.Help, config, _roster, _adapters, _memory, order: held) { Subject = "helper" };

        Assert.Equal(("review", "opinion"), (fenced.Work, fenced.Role));
        Assert.Equal([new ActorEffective("cool", "sonnet", null, "2"), new ActorEffective("gone", null, null, "rule-default")], fenced.Order);
        Assert.Equal(("helper", held), (handed.Role, handed.Order));
    }

    /// <summary>
    /// FOUNDATION1j part ii: the plan's one loop walks the order its caller gives, under the caller's own rule of which entries an earlier pass
    /// holds: an entry held is neither asked nor passed, the first that runs is taken, and each pass is said once, in order.
    /// </summary>
    [Fact]
    public async Task The_plan_s_one_loop_walks_its_caller_s_order_under_its_caller_s_rule()
    {
        ActorEffective[] order = [new("cool", "opus", null, "1"), new("cool", "sonnet", null, "2"), new("fine", null, null, "3")];
        var asked = new List<string>();
        var said = new List<string>();

        var walked = await StartPlan.LoopAsync(
            order, entry => entry,
            entry =>
            {
                asked.Add(entry.From);
                return Task.FromResult(entry.Agent == "fine"
                    ? EntryTry.Runs(new HarnessSelection(null, "account-1"))
                    : EntryTry.Passed(StartPasses.Cooling, "its accounts cool", new HarnessSelection("held") { Cooling = new CoolingEntry("cool", "account-1", Now.AddHours(2), true, "weekly", Now, "s0") }));
            },
            holds: (entry, passes) => passes.Any(pass => pass.Entry.Agent == entry.Agent),
            said: pass => said.Add(pass.Word));

        Assert.Equal(["1", "3"], asked);
        Assert.Equal((order[2], "account-1"), (walked.Taken, walked.Selection!.Profile));
        Assert.Equal(["1:cool:cooling"], walked.Passes.Select(pass => pass.Word));
        Assert.Equal(["1:cool:cooling"], said);
        Assert.Equal(Now.AddHours(2), walked.Soonest!.Until);
    }

    /// <summary>
    /// FOUNDATION1j part ii: an agent nothing of which is installed, or none at the version its pin names, cannot run here at all, so every walk
    /// passes it <c>unavailable</c> (D205 point 4), read from the account walk's facts as the reviewer's walk reads them.
    /// </summary>
    [Fact]
    public void An_agent_not_installed_or_not_at_its_pin_is_passed_unavailable()
    {
        Assert.Equal(StartPasses.Unavailable, StartPasses.Of(new HarnessSelection("not installed") { Absent = AgentAbsence.NotInstalled }));
        Assert.Equal(StartPasses.Unavailable, StartPasses.Of(new HarnessSelection("its pin is not installed") { Absent = AgentAbsence.PinNotInstalled }));
    }

    /// <summary>
    /// 🔴 FOUNDATION1j part ii (the foundation design §2.4, §7 row 5; D205 point 1 and its PRESSMODEL1 note): a continuation goes on with the
    /// agent its record's choice was asked of, else the agent it opened on, else its role's first entry; never the machine's <c>adapter</c> key,
    /// which a role read <i>as before</i> answers itself.
    /// </summary>
    [Fact]
    public void A_continuation_goes_on_with_its_record_s_asked_choice_never_the_machine_s_adapter()
    {
        var config = ActorChoices.With(DriverConfig.Empty with { Adapter = "cool" }, "build", [new ActorChoice("cool", "opus"), new ActorChoice("fine", "sonnet")]);
        var second = new PriorSession("s1", null, "failed")
        {
            Actor = "build", Choice = "2", Asked = new ActorEffective("fine", "sonnet", null, "2").Asked, Adapter = "fine",
        };

        Assert.Equal("fine", StartPlan.ContinuedOn(second, config));
        Assert.Equal("fine", StartPlan.ContinuedOn(second with { Adapter = "cool" }, config));
        Assert.Equal("fine", StartPlan.ContinuedOn(second with { Asked = null }, config with { Adapter = "piped" }));
        // A record from before the actors, naming no agent: its role's first entry, Build's own (`cool`), or Chat's for a conversation.
        var before = new PriorSession("s0", null, "failed");
        Assert.Equal("cool", StartPlan.ContinuedOn(before, config with { Adapter = "piped" }));
        Assert.Equal("fine", StartPlan.ContinuedOn(before, ActorChoices.With(config, "chat", [new ActorChoice("fine")]), work: "chat"));
        Assert.Equal("piped", StartPlan.ContinuedOn(before with { Actor = "garden" }, DriverConfig.Empty with { Adapter = "piped" }));
    }

    /// <summary>
    /// 🔴 FOUNDATION1j part iv (the foundation design §2.4's second row): the agent a start is named for before its walk runs, which the look's
    /// report of a wait names, is a hand-over's, else a continuation's record's, else its role's first entry, the machine's <c>adapter</c> key
    /// only where that entry does not read.
    /// </summary>
    [Fact]
    public void A_start_is_named_for_the_hand_over_the_record_or_its_role_s_first_entry_before_its_walk()
    {
        var config = ActorChoices.With(DriverConfig.Empty with { Adapter = "cool" }, "build", [new ActorChoice("fine", "opus"), new ActorChoice("cool")]);
        config = config with { Adapter = "piped" };
        Assert.Equal("fine", StartPlan.AgentFor("build", config));
        Assert.Equal("handed", StartPlan.AgentFor("build", config, handed: "handed"));
        Assert.Equal("fine", StartPlan.AgentFor("design", config));
        var second = new PriorSession("s1", null, "failed") { Actor = "build", Choice = "2", Adapter = "cool" };
        Assert.Equal("cool", StartPlan.AgentFor("build", config, record: second));
        Assert.Equal("piped", StartPlan.AgentFor("build",DriverConfig.Empty with { Adapter = "piped", ActorErrors = new Dictionary<string, string> { ["build"] = "unreadable" } }));
    }

    /// <summary>
    /// An intake's plan waits in Intake's own words (D205 §4.5), each choice named and the soonest cool-off beside it, and its machine log's
    /// lines name the ask, keyed as the intake's walk always wrote them.
    /// </summary>
    [Fact]
    public async Task An_intake_s_plan_waits_in_its_own_words_and_its_lines_name_the_ask()
    {
        _log = new MachineLog(_home, "driver", () => Now);
        Cooling("fine");
        StartPlan Intake(params ActorChoice[] choices) =>
            new(ParticipantWork.Intake, ActorChoices.With(DriverConfig.Empty with { Adapter = "cool" }, "intake", choices), _roster, _adapters, _memory)
            {
                Workspace = "default", Subject = "ask:a1", Named = ("ask", "a1"), Log = _log,
            };

        var waits = await Intake(new ActorChoice("gone"), new ActorChoice("fine")).ResolveAsync(CancellationToken.None);

        Assert.Null(waits.Chosen);
        Assert.StartsWith(
            "Intake waits: none of its choices can run now. choice 1, gone · account default: `gone` is not an agent on this machine; choice 2, fine · account default:",
            waits.Selection.Refusal, StringComparison.Ordinal);
        Assert.Equal(Now.AddHours(2), waits.Selection.Cooling!.Until);
        Assert.All(Logged("actor.passed"), line => Assert.Equal(["actor", "ask", "choice", "agent", "code"], Keys(line)));
        Assert.Equal(["1:gone:unavailable", "2:fine:cooling"], Logged("actor.passed").Select(Said));

        var taken = await Intake(new ActorChoice("gone"), new ActorChoice("cool", "opus")).ResolveAsync(CancellationToken.None);

        Assert.Equal(("intake", "cool"), (taken.Role, taken.Chosen!.Agent));
        var chose = Assert.Single(Logged("actor.chose"));
        Assert.Equal(["actor", "ask", "choice", "agent", "model", "passed"], Keys(chose));
        Assert.Equal(("intake", "a1", "opus"), (chose.GetProperty("actor").GetString(), chose.GetProperty("ask").GetString(), chose.GetProperty("model").GetString()));
    }
}
