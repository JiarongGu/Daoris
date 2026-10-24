using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The owner's mark and the gates (STEWARDROLE1a-ii, D209 §5.4, §3.4 step 8; the steward design §10 row 1a-ii's <c>OwnerSetTests</c>): an
/// owner's door marks what it names <c>owner-set</c> for the run, by repository and by quest, a session resolved to its quest and a
/// workspace to its repositories; an Ask Daoris Apply marks by its plan; a person's write through the service's door marks by the record,
/// a publication and the ask's own words never; and a steward's act takes the gates of its target and its dependencies alone, in a fixed
/// order, its dependent conditions judged again under them, so it is never run after the owner's act on the same scope.
/// </summary>
/// <remarks>
/// Each test has its own scratch home under the checkout's <c>_fixtures</c>, handed to every part by its path: no test sets
/// <c>DAORIS_HOME</c>, starts a window or a console, or runs an agent. The steward's acts are stand-ins in its table, which this build
/// leaves empty; the pipe is served in this process, as <c>PipeStandingTests</c>' is. The judge's build offers the steward, which only a
/// test's judge does in this build: an instance's offers neither role, so its owner's doors mark nothing and take no gate.
/// </remarks>
public sealed class OwnerSetTests : IDisposable
{
    private readonly string _home;
    private readonly EventBus _bus = new(NullLogger<EventBus>.Instance);

    public OwnerSetTests()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "daoris.json"))) root = root.Parent;
        _home = Path.Combine(root?.FullName ?? throw new InvalidOperationException("the tests run in a checkout."), "_fixtures", "owner-set-tests",
            Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_home, recursive: true);
        }
        catch (IOException)
        {
            // A log still held by a pipe that closes after us: the scratch is the checkout's, and its parent is ignored.
        }
    }

    // ——— What the owner's doors name and mark.

    [Fact]
    public async Task A_mark_by_repository_and_by_quest_holds_the_steward_back_from_each()
    {
        var judge = Judge();
        var (_, key) = Appointed(judge);

        await MarkAsync(judge, "SET_HOLD", new { repository = "engine", held = true });
        await MarkAsync(judge, "RETRY_QUEST", new { quest = "#q1" });

        Assert.True(judge.IsOwnerSet(new RoleScope(OwnerKinds.Repository, "ENGINE")));
        Assert.True(judge.IsOwnerSet(new RoleScope(OwnerKinds.Quest, "q1")));
        Assert.False(judge.IsOwnerSet(new RoleScope(OwnerKinds.Repository, "atlas")));
        Assert.Equal(RoleWhy.OwnerSet, Act(judge, key, RoleKit.Probe.Named, """{"repository":"engine"}""").Why);
        Assert.Equal(RoleWhy.OwnerSet, Act(judge, key, QuestProbe.Named, """{"quest":"q1"}""").Why);
        Assert.True(Act(judge, key, RoleKit.Probe.Named, """{"repository":"atlas"}""").Covered);
    }

    /// <summary>An Ask Daoris Apply of <i>Stop driving</i> marks by its plan, so the steward's drive after it is refused <c>owner-set</c>.</summary>
    [Fact]
    public async Task An_applied_stop_driving_then_the_steward_s_drive_is_refused_owner_set()
    {
        var judge = Judge();
        var (_, key) = Appointed(judge);
        var plan = HelpProposals.Owned(new HelpProposal("p1", "setting", "undrive", "engine", null, null, null, "", "h1", "proposed"));

        await using (await OwnerMarks.EnterAsync(judge, new Scopes(), HelpRoutes.Owned(plan), standing: false, CancellationToken.None))
        {
        }

        Assert.Equal(RoleWhy.OwnerSet, Act(judge, key, RoleKit.Probe.Named, """{"repository":"engine"}""").Why);
    }

    /// <summary>
    /// D201's <c>Forget</c> moved into the mark: a hold the standing confirmation covered is forgotten by the owner's own press on the
    /// repository, through any door, so its covered resume asks (<c>not-held-here</c>, the role's <c>not-own</c> in 1b); the covered
    /// press's own mark keeps it.
    /// </summary>
    [Fact]
    public async Task The_owner_s_resume_and_hold_after_a_covered_hold_leave_the_covered_resume_asking()
    {
        var held = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var judge = Judge(offered: true);
        judge.HeldNow = held.Contains;
        Assert.True(judge.Set(StandingPresses.Built).Done);
        Assert.True(judge.Judge(DriverModule.Name, "SET_HOLD", Payload(new { repository = "engine", held = true }), null).Covered);
        held.Add("engine");

        // The covered press's own mark (under the standing door) keeps the hold it remembered.
        await MarkAsync(judge, "SET_HOLD", new { repository = "engine", held = true }, standing: true);
        Assert.True(judge.Judge(DriverModule.Name, "SET_HOLD", Payload(new { repository = "engine", held = false }), null).Covered);
        held.Remove("engine");

        // Held again by the covered press; then the owner's own Resume and Hold, from the window or Ask Daoris.
        Assert.True(judge.Judge(DriverModule.Name, "SET_HOLD", Payload(new { repository = "engine", held = true }), null).Covered);
        held.Add("engine");
        await MarkAsync(judge, "SET_HOLD", new { repository = "engine", held = false });
        held.Remove("engine");
        await MarkAsync(judge, "SET_HOLD", new { repository = "engine", held = true });
        held.Add("engine");

        var resume = judge.Judge(DriverModule.Name, "SET_HOLD", Payload(new { repository = "engine", held = false }), null);
        Assert.Equal((false, StandingWhy.NotHeldHere), (resume.Covered, resume.Why));
    }

    [Theory]
    [InlineData("SESSION_QUEUE_EDIT", """{"session":"s1","id":"w1","text":"later"}""")]
    [InlineData("SESSION_GO_ON_NEW", """{"id":"s1"}""")]
    [InlineData("DISCARD_SESSION_TREE", """{"id":"s1","force":true}""")]
    [InlineData("STOP_SESSION", """{"id":"s2"}""")]
    public async Task The_owner_s_press_on_a_session_marks_its_quest_and_every_successor_shares_it(string route, string payload)
    {
        var judge = Judge();
        var (_, key) = Appointed(judge);

        // s2 is s1's successor (a retry, a hand-over, a carry-on, a new part): the same quest.
        await MarkAsync(judge, route, JsonDocument.Parse(payload).RootElement);

        Assert.True(judge.IsOwnerSet(new RoleScope(OwnerKinds.Quest, "q1")));
        Assert.Equal(RoleWhy.OwnerSet, Act(judge, key, QuestProbe.Named, """{"quest":"q1"}""").Why);
    }

    [Theory]
    [InlineData("SESSION_OPEN_FOLDER", """{"id":"s1"}""")]
    [InlineData("NOTICES_READ", """{"workspace":"games"}""")]
    [InlineData("SET_APPOINTMENT", """{"role":"supervisor","repository":"engine","holder":"terminal","on":true}""")]
    [InlineData("START_CHAT", """{"repository":"engine"}""")]
    [InlineData("END_CHAT", """{"id":"c1"}""")]
    public async Task What_acts_on_nothing_a_role_acts_on_marks_nothing(string route, string payload)
    {
        var judge = Judge();
        var (_, key) = Appointed(judge);

        await MarkAsync(judge, route, JsonDocument.Parse(payload).RootElement);

        Assert.False(judge.IsOwnerSet(new RoleScope(OwnerKinds.Repository, "engine")));
        Assert.False(judge.IsOwnerSet(new RoleScope(OwnerKinds.Quest, "q1")));
        Assert.True(Act(judge, key, RoleKit.Probe.Named, """{"repository":"engine"}""").Covered);
    }

    /// <summary>A workspace-scoped <c>SET_LANDING</c> and a workspace's Ask Daoris Apply each mark every repository of the workspace.</summary>
    [Fact]
    public async Task A_workspace_s_landing_and_its_apply_each_mark_every_repository_of_it()
    {
        var judge = Judge();
        await MarkAsync(judge, "SET_LANDING", new { workspace = "games", form = "merge" });
        Assert.True(judge.IsOwnerSet(new RoleScope(OwnerKinds.Repository, "engine")));
        Assert.True(judge.IsOwnerSet(new RoleScope(OwnerKinds.Repository, "atlas")));
        Assert.False(judge.IsOwnerSet(new RoleScope(OwnerKinds.Repository, "ledger")));

        var applied = Judge();
        var plan = HelpProposals.Owned(new HelpProposal("p2", "setting", "landing", null, "games", "merge", null, "", "h1", "proposed"));
        await using (await OwnerMarks.EnterAsync(applied, new Scopes(), HelpRoutes.Owned(plan), standing: false, CancellationToken.None))
        {
        }

        Assert.True(applied.IsOwnerSet(new RoleScope(OwnerKinds.Repository, "engine")));
        Assert.True(applied.IsOwnerSet(new RoleScope(OwnerKinds.Repository, "atlas")));
    }

    /// <summary>A target the owner's press names that cannot be resolved holds every quest for the run: the owner is never held up, the steward is.</summary>
    [Fact]
    public async Task A_session_that_cannot_be_resolved_holds_every_quest()
    {
        var judge = Judge();
        var (_, key) = Appointed(judge);

        await MarkAsync(judge, "STOP_SESSION", new { id = "s9" }, scopes: new Scopes { Fails = true });

        Assert.Equal(RoleWhy.OwnerSet, Act(judge, key, QuestProbe.Named, """{"quest":"q7"}""").Why);
        Assert.True(Act(judge, key, RoleKit.Probe.Named, """{"repository":"engine"}""").Covered);
    }

    /// <summary>A build that offers no steward marks nothing and takes no gate: the owner's doors are as before.</summary>
    [Fact]
    public async Task A_build_offering_no_steward_marks_nothing()
    {
        var judge = Judge(steward: false);

        await MarkAsync(judge, "SET_HOLD", new { repository = "engine", held = true });

        Assert.False(judge.IsOwnerSet(new RoleScope(OwnerKinds.Repository, "engine")));
    }

    // ——— By the record: the service's person doors.

    /// <summary>A quest published this run, and its ask's own words, mark nothing; a person's word or move after its publication marks it.</summary>
    [Theory]
    [InlineData("published", null)]
    [InlineData("added", null)]
    [InlineData("before", null)]
    [InlineData("done", "note:quest.person-done")]
    [InlineData("accepted", "move:accepted")]
    [InlineData("answered", "session-note:ledger.answered")]
    [InlineData("said", "said:person")]
    [InlineData("reopened", "word:reopened")]
    public void The_record_marks_a_person_s_write_after_the_run_s_start_and_never_a_publication(string write, string? evidence)
    {
        var start = DateTimeOffset.Parse("2026-10-24T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        const string after = "2026-10-24T10:00:00Z";
        const string earlier = "2026-10-24T08:00:00Z";
        var quest = $$"""[{"id":"q1","to":"engine","status":"{{(write == "done" ? "Done" : "Taken")}}","filed":"{{after}}","updated":"{{after}}","noteParts":[{{(write == "done" ? """{"code":"quest.person-done","by":"person"}""" : "")}}]}]""";
        var moves = $$"""{"moves":[{"kind":"published","at":"{{after}}","machine":"m","sequence":1},{"kind":"taken","at":"{{after}}","machine":"m","sequence":2}{{(write == "accepted" ? $$""",{"kind":"accepted","at":"{{after}}","machine":"m","sequence":3}""" : write == "before" ? $$""",{"kind":"accepted","at":"{{earlier}}","machine":"m","sequence":3}""" : "")}}]}""";
        var words = write switch
        {
            "added" => $$"""{"kind":"added","text":"also this","at":"{{after}}","session":"s1","quest":"q1"}""",
            "reopened" => $$"""{"kind":"reopened","text":"go on","at":"{{after}}","session":"s1","quest":"q1"}""",
            _ => "",
        };
        var asks = $$"""[{"id":"a1","quests":["q1"],"words":[{{words}}]}]""";
        var note = write == "answered" ? """{"code":"ledger.answered","by":"person"}""" : "";
        var said = write == "said" ? """{"text":"stop there","at":"2026-10-24T10:00:00Z"}""" : """{"text":"helper's","helper":"h1"}""";
        var sessions = $$"""[{"id":"s1","quest":"q1","updated":"{{after}}","noteParts":[{{note}}],"said":[{{said}}]},{"id":"c1","updated":"{{after}}","noteParts":[{"code":"ledger.answered"}]}]""";

        var facts = OwnerRecords.Read("q1", quest, moves, asks, sessions);

        Assert.Equal(evidence, OwnerRecords.Marked(facts, start));
    }

    // ——— The gates.

    /// <summary>
    /// The landing race (§5.4, finding 6): an owner's pushing landing rule, set by repository or by workspace alone, lands between the
    /// steward's admission of a stop of a parked <c>Done</c> session and its gates; the act is refused <c>landing-pushes</c> under the
    /// gates of its quest, its repository and the workspace, which it judges again there, and nothing runs.
    /// </summary>
    [Theory]
    [InlineData("repository")]
    [InlineData("workspace")]
    public async Task The_owner_s_pushing_landing_between_admission_and_the_gates_refuses_the_act_landing_pushes(string scope)
    {
        if (!OperatingSystem.IsWindows()) return;
        var pushes = false;
        var probe = new QuestProbe { Under = () => pushes ? "landing-pushes" : null };
        await using var served = await ServedAsync(probe);
        served.Module.Before = async () =>
        {
            object landing = scope == "repository" ? new { repository = "engine", form = "push" } : new { workspace = "games", form = "push" };
            await using (await OwnerMarks.EnterAsync(served.Judge, served.Scopes, DriverModule.OwnerDoor("SET_LANDING", Payload(landing))!, false, CancellationToken.None))
            {
                pushes = true;
            }
        };

        var answer = await served.ActAsync(QuestProbe.Named, """{"quest":"q1"}""");

        Assert.Equal((false, "landing-pushes"), (answer.GetProperty("done").GetBoolean(), answer.GetProperty("why").GetString()));
        Assert.Equal(0, probe.Ran);
        Assert.False(served.Judge.IsOwnerSet(new RoleScope(OwnerKinds.Quest, "q1")));
        Assert.True(served.Judge.IsOwnerSet(new RoleScope(OwnerKinds.Repository, "engine")));
    }

    /// <summary>
    /// The gate's two orders: a steward act admitted then an owner's press on its scope waits for the act to end, then marks it; an
    /// owner's press holding the gate makes the act wait, and once the owner's mark is set the act is refused <c>owner-set</c>. The judge's
    /// lock is never held across either await: the judge answers while both wait.
    /// </summary>
    [Fact]
    public async Task The_owner_s_act_and_the_steward_s_never_interleave_either_way_round()
    {
        if (!OperatingSystem.IsWindows()) return;
        var inside = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var let = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var probe = new QuestProbe
        {
            Inside = async () =>
            {
                inside.SetResult();
                await let.Task;
            },
        };
        await using var served = await ServedAsync(probe);

        // A steward act first: it holds the quest's gate across its handler; the owner's press on the quest waits on it.
        var acting = served.ActAsync(QuestProbe.Named, """{"quest":"q1"}""");
        await inside.Task.WaitAsync(Poll.Patience);
        var owner = Task.Run(async () =>
        {
            await using (await OwnerMarks.EnterAsync(served.Judge, served.Scopes, DriverModule.OwnerDoor("RETRY_QUEST", Payload(new { quest = "q1" }))!, false, CancellationToken.None))
            {
            }
        });
        await Task.Delay(100);
        Assert.False(owner.IsCompleted);
        Assert.False(served.Judge.IsOwnerSet(new RoleScope(OwnerKinds.Quest, "q1")));
        Assert.True(served.Judge.Gates.IsHeld(new RoleScope(OwnerKinds.Workspace, "games")));
        let.SetResult();
        Assert.True((await acting).GetProperty("done").GetBoolean());
        await owner.WaitAsync(Poll.Patience);
        Assert.True(served.Judge.IsOwnerSet(new RoleScope(OwnerKinds.Quest, "q1")));
        Assert.Equal(1, probe.Ran);

        // The owner's press first, on another quest: it holds the gate; the act waits, and finds the mark set.
        var held = await served.Judge.Gates.EnterAsync([new RoleScope(OwnerKinds.Quest, "q2")], alone: false, CancellationToken.None);
        var waiting = served.ActAsync(QuestProbe.Named, """{"quest":"q2"}""");
        await Task.Delay(100);
        Assert.False(waiting.IsCompleted);
        served.Judge.MarkOwner(new OwnerResolved([new RoleScope(OwnerKinds.Quest, "q2")], [], false), standing: false);
        await held.DisposeAsync();
        var refused = await waiting;
        Assert.Equal((false, RoleWhy.OwnerSet), (refused.GetProperty("done").GetBoolean(), refused.GetProperty("why").GetString()));
        Assert.Equal(1, probe.Ran);
    }

    /// <summary>The gates a quest's act takes are its quest's, its repository's and that repository's workspace's, in the fixed order.</summary>
    [Fact]
    public async Task A_quest_s_act_depends_on_its_repository_and_workspace_in_the_fixed_order()
    {
        var gates = await SupervisorDoor.DependenciesAsync(new RoleScope(OwnerKinds.Quest, "q1"), new Scopes(), CancellationToken.None);

        Assert.Equal(["workspace:GAMES", "repository:ENGINE", "quest:Q1"], gates!.Select(each => each.Key));
        Assert.Null(await SupervisorDoor.DependenciesAsync(new RoleScope(OwnerKinds.Quest, "q1"), new Scopes { Fails = true }, CancellationToken.None));
        Assert.Equal(
            ["workspace:A", "workspace:B", "repository:A", "quest:A"],
            RoleScope.Order([new(OwnerKinds.Quest, "a"), new(OwnerKinds.Repository, "a"), new(OwnerKinds.Workspace, "b"), new(OwnerKinds.Workspace, "a"), new(OwnerKinds.Quest, "A")])
                .Select(each => each.Key));
    }

    // ——— The kit.

    /// <summary>A stand-in quest's act for the steward's table: <c>{ quest }</c> exactly, its dependent condition <see cref="Under"/>.</summary>
    internal sealed class QuestProbe : IRoleAct
    {
        public const string Named = "quest-probe";

        public string Name => Named;

        public int Ran { get; private set; }

        public Func<string?>? Under { get; init; }

        public Func<Task>? Inside { get; init; }

        public bool IsForm(JsonElement form) =>
            form.EnumerateObject().Select(each => each.Name).SequenceEqual(["quest"]) && form.GetProperty("quest").ValueKind == JsonValueKind.String;

        public string Target(JsonElement form) => form.GetProperty("quest").GetString()!;

        public string TargetKind => OwnerKinds.Quest;

        public Task<string?> UnderGatesAsync(RoleActRun run, CancellationToken ct) => Task.FromResult(Under?.Invoke());

        public async Task<object?> RunAsync(RoleActRun run, CancellationToken ct)
        {
            Ran++;
            if (Inside is { } inside) await inside();
            return new { Ran = true };
        }
    }

    /// <summary>The reads a mark makes, stood in: sessions s1 and s2 serve q1, c1 is a conversation; engine and atlas are in games.</summary>
    internal sealed class Scopes : IOwnerScopes
    {
        public bool Fails { get; init; }

        public bool Wrote { get; init; }

        private void Read()
        {
            if (Fails) throw new InvalidOperationException("the service did not answer.");
        }

        public Task<string?> QuestOfSessionAsync(string session, CancellationToken ct)
        {
            Read();
            return Task.FromResult(session is "s1" or "s2" ? "q1" : session == "c1" ? null : throw new InvalidOperationException("no such record"));
        }

        public Task<IReadOnlyList<string>> QuestsOfAskAsync(string ask, string? repository, CancellationToken ct)
        {
            Read();
            return Task.FromResult<IReadOnlyList<string>>(["q1", "q2"]);
        }

        public Task<string?> RepositoryOfQuestAsync(string quest, CancellationToken ct)
        {
            Read();
            return Task.FromResult<string?>("engine");
        }

        public Task<string?> WorkspaceOfAsync(string repository, CancellationToken ct)
        {
            Read();
            return Task.FromResult<string?>(repository is "engine" or "atlas" ? "games" : "other");
        }

        public Task<IReadOnlyList<string>> RepositoriesOfAsync(string workspace, CancellationToken ct)
        {
            Read();
            return Task.FromResult<IReadOnlyList<string>>(workspace == "games" ? ["engine", "atlas"] : ["ledger"]);
        }

        public Task<bool> PersonWroteAsync(string quest, DateTimeOffset since, CancellationToken ct)
        {
            Read();
            return Task.FromResult(Wrote);
        }
    }

    private StandingConfirmation Judge(bool steward = true, bool offered = false) =>
        new StandingConfirmation(_home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside), offered: offered, steward: steward,
            walk: new StandingConfirmationTests.Walked())
        {
            Drives = _ => true,
        }.Acting(new RoleKit.Probe(), new QuestProbe());

    /// <summary>The steward appointed on <paramref name="judge"/>, the key its stand-in tab was handed.</summary>
    private static (AppointmentChange Change, string Key) Appointed(StandingConfirmation judge)
    {
        judge.Load();
        var (change, key) = RoleKit.Appoint(judge, RoleKit.Stewarding(share: null));
        Assert.True(change.Answer.Done, change.Answer.Message);
        return (change, key!);
    }

    /// <summary>A steward's act judged on the judge alone, over a nonce drawn for it.</summary>
    private static RoleJudgement Act(StandingConfirmation judge, string key, string act, string form)
    {
        var nonce = SupervisorProof.Nonce();
        return RoleKit.JudgeAct(judge, RoleKit.Act(key, nonce, RoleKit.GenerationOf(judge, Roles.Steward), act: act, form: form), nonce);
    }

    /// <summary>An owner's press of <paramref name="route"/> marked as the router marks it.</summary>
    private static async Task MarkAsync(StandingConfirmation judge, string route, object payload, bool standing = false, IOwnerScopes? scopes = null)
    {
        var named = DriverModule.OwnerDoor(route, payload is JsonElement element ? element : Payload(payload)) ?? [];
        await using (await OwnerMarks.EnterAsync(judge, scopes ?? new Scopes(), named, standing, CancellationToken.None))
        {
        }
    }

    private static JsonElement Payload(object payload) => JsonSerializer.SerializeToElement(payload);

    /// <summary>A driver module whose <c>ROLE_ACT</c> is the door's own handler with the stand-in reads, <see cref="Before"/> awaited first.</summary>
    private sealed class ActModule(Scopes scopes) : ModuleBase
    {
        public InstancePipe? Pipe { get; set; }

        public Func<Task>? Before { get; set; }

        public override string ModuleName => DriverModule.Name;

        protected override async Task<object?> RouteMessageAsync(IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
        {
            if (Before is { } before) await before();
            return await SupervisorDoor.ActAsync(Pipe, request.Payload, cancellationToken, scopes);
        }
    }

    /// <summary>A pipe served in this process over the scratch home, the steward appointed, its acts sent as the holder's verb sends them.</summary>
    private sealed class Served(InstancePipe pipe, ServiceProvider services, StandingConfirmation judge, ActModule module, Scopes scopes, string key, string home)
        : IAsyncDisposable
    {
        public StandingConfirmation Judge => judge;

        public ActModule Module => module;

        public Scopes Scopes => scopes;

        public async Task<JsonElement> ActAsync(string act, string form)
        {
            await using var client = await PipeClient.ConnectAsync(pipe.Name!);
            var hello = await client.HelloAsync(HomeIdentity.Read(home)!);
            var nonce = hello.GetProperty("data").GetProperty("nonce").GetString();
            var payload = RoleKit.Act(key, nonce, RoleKit.GenerationOf(judge, Roles.Steward), act: act, form: form);
            var answer = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, RoleRoutes.Act, payload));
            Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
            return answer.GetProperty("data");
        }

        public async ValueTask DisposeAsync()
        {
            pipe.Dispose();
            await services.DisposeAsync();
        }
    }

    private async Task<Served> ServedAsync(QuestProbe probe)
    {
        var scopes = new Scopes();
        var module = new ActModule(scopes);
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(_bus);
        services.AddSingleton<IIpcRequestTracker>(new IpcRequestTracker(_bus, new IpcRequestTrackerOptions { GracePeriod = TimeSpan.FromMilliseconds(50) }));
        services.AddSingleton<IIpcModule>(module);
        services.UseMessageDispatcher((_, _) => { });
        var provider = services.BuildServiceProvider();
        var judge = new StandingConfirmation(_home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside), steward: true,
            walk: new StandingConfirmationTests.Walked()) { Drives = _ => true }.Acting(new RoleKit.Probe(), probe);
        var pipe = new InstancePipe(provider.GetRequiredService<IMessageDispatcher>(), _bus, _home, version: "9.9.9-test", standing: judge)
        {
            Grounds = new StubGrounds(),
        };
        Assert.NotNull(pipe.Serve());
        module.Pipe = pipe;
        var (_, key) = Appointed(judge);
        await Task.CompletedTask;
        return new Served(pipe, provider, judge, module, scopes, key, _home);
    }
}

/// <summary>The judge's steward table and offered acts holding the stand-ins (STEWARDROLE1a-ii's tests).</summary>
internal static class OwnerSetKit
{
    public static StandingConfirmation Acting(this StandingConfirmation standing, params IRoleAct[] acts)
    {
        standing.TableOf = role => role == Roles.Steward
            ? acts.ToDictionary(each => each.Name, StringComparer.Ordinal)
            : new Dictionary<string, IRoleAct>(StringComparer.Ordinal);
        standing.OffersActs = role => role == Roles.Steward ? [.. acts.Select(each => each.Name)] : [];
        return standing;
    }
}
