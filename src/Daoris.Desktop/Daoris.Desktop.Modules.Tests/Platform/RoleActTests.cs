using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The steward's immediate acts (STEWARDROLE1b, D209 §3.1 rows 1 to 8; the design §10 row 1b's <c>RoleActTests</c>): each admitted in its
/// exact form and nothing else, every act of the owner's refused <c>not-role</c>, each condition refused by its code with nothing written,
/// each effect recorded as the steward's, and the steward's Try again a release only this run's memory honours, consumed once by its start.
/// </summary>
/// <remarks>
/// Each test has its own scratch home under the checkout's <c>_fixtures</c>, handed by path: no test sets <c>DAORIS_HOME</c>, starts a
/// window, a console or an agent. The acts run over a stand-in world (<see cref="World"/>), so no process is stopped and no service is
/// asked; the pipe is served in this process, as <c>OwnerSetTests</c>' is, and the judge's build offers the steward, which only a test's
/// judge does in this build.
/// </remarks>
public sealed class RoleActTests : IDisposable
{
    private readonly string _home;
    private readonly EventBus _bus = new(NullLogger<EventBus>.Instance);

    public RoleActTests()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "daoris.json"))) root = root.Parent;
        _home = Path.Combine(root?.FullName ?? throw new InvalidOperationException("the tests run in a checkout."), "_fixtures", "role-act-tests",
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

    // ——— The forms (§3.1, §3.3).

    public static TheoryData<string, string> Forms() => new()
    {
        { "drive", """{"repository":"engine","drivable":true}""" },
        { "undrive", """{"repository":"engine","drivable":false}""" },
        { "trees", """{"repository":"engine","ownTree":true}""" },
        { "hold", """{"repository":"engine","held":true}""" },
        { "resume", """{"repository":"engine","held":false}""" },
        { "stop", """{"id":"s1","said":true}""" },
        { "finish", """{"id":"s1","state":"completed","said":true}""" },
        { "retry", """{"quest":"q1"}""" },
    };

    [Theory]
    [MemberData(nameof(Forms))]
    public void Each_act_is_admitted_in_its_exact_form(string act, string form)
    {
        var (judge, key) = Appointed();

        Assert.Equal(StewardActs.Offered, ["drive", "undrive", "trees", "hold", "resume", "stop", "finish", "retry"]);
        var judged = Act(judge, key, act, form);
        Assert.True(judged.Covered, judged.Why);
    }

    /// <summary>Another field or value is refused <c>form</c>; a field name's casing is read as D201's; a name given twice is refused.</summary>
    [Theory]
    [InlineData("drive", """{"repository":"engine","drivable":true,"why":"x"}""", "form")]
    [InlineData("drive", """{"repository":"engine","drivable":"true"}""", "form")]
    [InlineData("drive", """{"Repository":"engine","DRIVABLE":true}""", null)]
    [InlineData("drive", """{"repository":"engine","REPOSITORY":"engine","drivable":true}""", "form")]
    [InlineData("drive", """{"repository":" engine","drivable":true}""", "form")]
    [InlineData("trees", """{"repository":"engine","ownTree":false}""", "form")]
    [InlineData("finish", """{"id":"s1","state":"declined","said":true}""", "form")]
    [InlineData("stop", """{"id":"s1"}""", "form")]
    [InlineData("retry", """{"quest":"q1","session":"s1"}""", "form")]
    public void Anything_but_the_exact_form_is_refused(string act, string form, string? why)
    {
        var (judge, key) = Appointed();

        var judged = Act(judge, key, act, form);

        Assert.Equal((why is null, why), (judged.Covered, judged.Why));
    }

    /// <summary>§3.2: what is the owner's is never the steward's, whatever its form: closing a quest as done and a decline among them.</summary>
    [Theory]
    [InlineData("QUEST_DONE")]
    [InlineData("done")]
    [InlineData("decline")]
    [InlineData("SET_TREES")]
    [InlineData("trees-off")]
    [InlineData("hand-over")]
    [InlineData("go-on")]
    [InlineData("setup")]
    [InlineData("SET_CAP")]
    [InlineData("SESSION_GO_AHEAD")]
    [InlineData("LAND_SESSION_TREE")]
    [InlineData("DISCARD_SESSION_TREE")]
    [InlineData("SESSION_INPUT")]
    public void Every_act_of_the_owner_s_is_refused_not_role(string act)
    {
        var (judge, key) = Appointed();

        Assert.Equal(RoleWhy.NotRole, Act(judge, key, act, """{"repository":"engine"}""").Why);
    }

    // ——— The conditions, under the gates (§3.4 step 6), each refusing with nothing written.

    [Fact]
    public async Task An_unregistered_repository_is_refused_and_nothing_is_written()
    {
        await using var served = await ServedAsync(new World { Registered = [] });

        var answer = await served.ActAsync("hold", """{"repository":"engine","held":true}""");

        Assert.Equal((false, StewardWhy.Unregistered), Done(answer));
        Assert.Empty(served.World.Config().Holds);
        Assert.Empty(served.Judge.StewardActsNow());
    }

    /// <summary>
    /// Row 1, finding 2: a repository whose sessions open no trees of their own is the owner's to drive, refused <c>no-tree</c> with nothing
    /// written; the steward gives it trees first, and then its drive is admitted, each recorded as the steward's.
    /// </summary>
    [Fact]
    public async Task A_drive_of_a_repository_with_no_trees_is_refused_until_the_steward_gives_it_trees()
    {
        await using var served = await ServedAsync(new World());

        Assert.Equal((false, StewardWhy.NoTree), Done(await served.ActAsync("drive", """{"repository":"engine","drivable":true}""")));
        Assert.DoesNotContain("engine", served.World.Config().Drivable);

        Assert.Equal((true, null), Done(await served.ActAsync("trees", """{"repository":"engine","ownTree":true}""")));
        Assert.Equal((true, null), Done(await served.ActAsync("drive", """{"repository":"engine","drivable":true}""")));

        Assert.True(served.World.Config().OpensOwnTree("engine"));
        Assert.Contains("engine", served.World.Config().Drivable);
        Assert.Equal(["drive", "trees"], served.Judge.StewardActsNow().Select(each => each.Act));
        Assert.True(served.Judge.StewardDrove("engine"));
    }

    /// <summary>Rows 1 and 3: drive and trees on are refused <c>landing-pushes</c> where the effective landing is a plugin that pushes.</summary>
    [Theory]
    [InlineData("drive", """{"repository":"engine","drivable":true}""")]
    [InlineData("trees", """{"repository":"engine","ownTree":true}""")]
    public async Task Drive_and_trees_on_are_refused_where_the_landing_pushes(string act, string form)
    {
        var world = new World();
        world.Change(config => config.WithTrees("engine", true).WithWorkspaceLanding("games", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}") { Plugin = "github" }));
        await using var served = await ServedAsync(world);

        Assert.Equal((false, StewardWhy.LandingPushes), Done(await served.ActAsync(act, form)));
        Assert.Empty(served.Judge.StewardActsNow());
    }

    /// <summary>
    /// Rows 6 and 7: a stop of a session whose quest is <c>Done</c>, and a finish of such a park, would make a pushed landing due under an
    /// auto-accepting branch rule with a pushing plugin: refused <c>landing-pushes</c>; under a rule that does not push, admitted.
    /// </summary>
    [Theory]
    [InlineData("stop", """{"id":"s1","said":true}""", "github", false)]
    [InlineData("finish", """{"id":"s1","state":"completed","said":true}""", "github", false)]
    [InlineData("stop", """{"id":"s1","said":true}""", null, true)]
    [InlineData("finish", """{"id":"s1","state":"completed","said":true}""", null, true)]
    public async Task A_stop_or_finish_that_would_release_a_push_is_refused(string act, string form, string? plugin, bool admitted)
    {
        var world = new World { QuestStatus = "Done", Root = _home };
        world.Records["s1"] = Parked("s1") with { Tree = world.TreeOf("s1") };
        world.Change(config => config.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}") { AutoAccept = true, Plugin = plugin }));
        await using var served = await ServedAsync(world);

        var answer = Done(await served.ActAsync(act, form));

        Assert.Equal(admitted ? (true, null) : (false, StewardWhy.LandingPushes), answer);
        Assert.Equal(admitted ? 1 : 0, world.Moved.Count);
    }

    /// <summary>Row 7: a helper's session, a partner's, a conversation and a park with a word waiting are never the steward's to finish.</summary>
    [Theory]
    [InlineData("helper", StewardWhy.NotQuestSession)]
    [InlineData("conversation", StewardWhy.NotQuestSession)]
    [InlineData("words", StewardWhy.WordsWaiting)]
    [InlineData("working", StewardWhy.NotParked)]
    [InlineData("running-here", StewardWhy.RunningHere)]
    public async Task A_finish_of_what_is_not_a_quiet_quest_park_is_refused(string kind, string why)
    {
        var world = new World();
        world.Records["s1"] = kind switch
        {
            "helper" => Parked("s1") with { Serves = "s0" },
            "conversation" => Parked("s1") with { Kind = "chat", Quest = null },
            "words" => Parked("s1") with { Said = [new SaidWordView("w1", "go on", DateTimeOffset.UtcNow, [], false)] },
            "working" => Parked("s1") with { State = "working" },
            _ => Parked("s1"),
        };
        if (kind == "running-here") world.Here.Add("s1");
        await using var served = await ServedAsync(world);

        Assert.Equal((false, why), Done(await served.ActAsync("finish", """{"id":"s1","state":"completed","said":true}""")));
        Assert.Empty(world.Moved);
    }

    /// <summary>
    /// Row 7, finding 14: the finish moves the parked record alone, as the steward's, and neither accepts nor closes its quest.
    /// </summary>
    [Fact]
    public async Task A_finish_moves_the_park_alone_as_the_steward_s()
    {
        var world = new World { QuestStatus = "Taken" };
        world.Records["s1"] = Parked("s1");
        await using var served = await ServedAsync(world);

        Assert.Equal((true, null), Done(await served.ActAsync("finish", """{"id":"s1","state":"completed","said":true}""")));

        Assert.Equal(["finish s1"], world.Moved);
        Assert.Equal("Taken", world.QuestStatus);
        var done = Assert.Single(served.Judge.StewardActsNow());
        Assert.Equal(("finish", "q1", "s1"), (done.Act, done.Quest, done.Session));
    }

    /// <summary>
    /// Row 6, §5.1a: a live stop is the instance's own stop with the steward's note handed in at the call, never a request file; a stop of
    /// a session another Daoris process of this machine runs is refused <c>running-elsewhere</c>, nothing written.
    /// </summary>
    [Fact]
    public async Task A_live_stop_is_the_instance_s_own_with_the_steward_s_note_and_one_elsewhere_is_refused()
    {
        var world = new World();
        world.Records["s1"] = Parked("s1") with { State = "working" };
        world.Records["s2"] = Parked("s2") with { State = "working" };
        world.Here.Add("s1");
        await using var served = await ServedAsync(world);

        Assert.Equal((true, null), Done(await served.ActAsync("stop", """{"id":"s1","said":true}""")));
        Assert.Equal((false, StewardWhy.RunningElsewhere), Done(await served.ActAsync("stop", """{"id":"s2","said":true}""")));

        var (session, note) = Assert.Single(world.Stopped);
        Assert.Equal(("s1", NoteCodes.EndedStoppedSteward.Code), (session, Assert.Single(note.Parts).Code));
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.Sessions)) && Directory.EnumerateFileSystemEntries(
            DaorisHome.Entry(_home, HomeEntry.Sessions), "*request*", SearchOption.AllDirectories).Any());
        var stop = Assert.Single(served.Judge.StewardActsNow());
        Assert.Equal((true, true), (stop.Standing, stop.Waiting));
    }

    /// <summary>Row 5: a resume releases only a hold the steward made this run that nothing has touched since (<c>not-own</c>).</summary>
    [Fact]
    public async Task A_resume_releases_only_the_steward_s_own_hold()
    {
        var world = new World();
        world.Change(config => config.WithHold("atlas", true));
        await using var served = await ServedAsync(world);

        Assert.Equal((false, StewardWhy.NotOwn), Done(await served.ActAsync("resume", """{"repository":"atlas","held":false}""")));
        Assert.Equal((true, null), Done(await served.ActAsync("hold", """{"repository":"engine","held":true}""")));
        Assert.Equal((true, null), Done(await served.ActAsync("resume", """{"repository":"engine","held":false}""")));
        Assert.Equal(["atlas"], world.Config().Holds);
        Assert.Equal((false, StewardWhy.NotOwn), Done(await served.ActAsync("resume", """{"repository":"engine","held":false}""")));
    }

    // ——— Try again and its deferred authority (row 8, §5.1a, findings 7 and 8).

    /// <summary>A retry of the owner's stop, of a quest no stop holds, and of a quest the strikes park (<c>Marked</c>) are refused <c>not-own</c>.</summary>
    [Theory]
    [InlineData("owner")]
    [InlineData("exhausted")]
    [InlineData("none")]
    public async Task A_retry_of_anything_but_the_steward_s_own_stop_is_refused(string held)
    {
        var world = new World();
        world.Verdicts = held switch
        {
            "owner" => [new Consideration(Quest(), StartVerdict.Stopped, "you stopped it") { HeldBy = new PriorSession("s1", null, "stopped") }],
            "exhausted" => [new Consideration(Quest(), StartVerdict.Exhausted, "parked") { Failures = 3 }],
            _ => [],
        };
        await using var served = await ServedAsync(world);

        Assert.Equal((false, StewardWhy.NotOwn), Done(await served.ActAsync("retry", """{"quest":"q1"}""")));
        Assert.Empty(world.Config().RoleReleases);
        Assert.Empty(world.Config().Released);
    }

    /// <summary>
    /// The steward's Try again of its own stop is kept in <c>roleReleases</c> with <c>by: steward</c> and its act's id, never the owner's
    /// map; the run's memory holds it at <c>released</c>, and the start's take, its final consumer, moves it past once.
    /// </summary>
    [Fact]
    public async Task The_steward_s_retry_is_a_role_release_its_start_takes_once()
    {
        var (served, world) = await StoppedAsync();
        await using var _ = served;

        Assert.Equal((true, null), Done(await served.ActAsync("retry", """{"quest":"q1"}""")));

        var release = world.Config().RoleReleaseFor("q1")!;
        Assert.Equal((RoleReleaseKinds.Stop, "s1", "steward"), (release.Kind, release.Session, release.By));
        Assert.Null(world.Config().ReleasedFor("q1"));
        var starts = new RoleStarts(served.Judge, world.Path);
        world.Config().Save(world.Path);
        Assert.True(starts.Released("q1", release));
        Assert.True(starts.Take("q1", release));
        Assert.False(starts.Take("q1", release));
        Assert.Equal(RoleOperationStates.Executed, served.Judge.Operation(release.Act)!.State);
        Assert.Null(DriverConfig.Load(world.Path).RoleReleaseFor("q1"));
        var acts = served.Judge.StewardActsNow();
        Assert.Equal(["retry", "stop"], acts.Select(each => each.Act));
        Assert.False(acts.Single(each => each.Act == "stop").Waiting);
    }

    /// <summary>
    /// Finding 8: an owner's stop or Try again, and a revoke, between the steward's release and the planner's start each end it: the run's
    /// memory no longer holds it, the start's take refuses, and nothing of the steward's starts.
    /// </summary>
    [Theory]
    [InlineData("STOP_SESSION")]
    [InlineData("RETRY_QUEST")]
    [InlineData("revoke")]
    public async Task What_lands_between_the_steward_s_release_and_the_start_ends_it(string between)
    {
        var (served, world) = await StoppedAsync();
        await using var _ = served;
        Assert.Equal((true, null), Done(await served.ActAsync("retry", """{"quest":"q1"}""")));
        var release = world.Config().RoleReleaseFor("q1")!;
        world.Config().Save(world.Path);

        if (between == "revoke")
        {
            Assert.True(served.Judge.Appoint(RoleKit.Stewarding(on: false, share: null), _ => throw new InvalidOperationException()).Answer.Done);
        }
        else
        {
            object payload = between == "STOP_SESSION" ? new { id = "s1" } : new { quest = "q1" };
            await using (await OwnerMarks.EnterAsync(served.Judge, served.Scopes, DriverModule.OwnerDoor(between, JsonSerializer.SerializeToElement(payload))!, false, CancellationToken.None))
            {
            }
        }

        var starts = new RoleStarts(served.Judge, world.Path);
        Assert.False(starts.Released("q1", release));
        Assert.False(starts.Take("q1", release));
        Assert.Null(DriverConfig.Load(world.Path).RoleReleaseFor("q1"));
    }

    /// <summary>
    /// After a restart the memory is gone: the steward's release is no release, and the start's sweep withdraws it from the file while the
    /// owner's own release on another quest is kept whole.
    /// </summary>
    [Fact]
    public async Task A_restart_withdraws_the_steward_s_release_and_keeps_the_owner_s()
    {
        var (served, world) = await StoppedAsync();
        await using var _ = served;
        Assert.Equal((true, null), Done(await served.ActAsync("retry", """{"quest":"q1"}""")));
        world.Change(config => config.WithReleased("q2", "s9"));
        world.Config().Save(world.Path);
        var release = world.Config().RoleReleaseFor("q1")!;

        var restarted = Judge();
        restarted.Load();

        Assert.False(new RoleStarts(restarted, world.Path).Released("q1", release));
        Assert.Equal(1, RoleStarts.Sweep(restarted, world.Path));
        var after = DriverConfig.Load(world.Path);
        Assert.Null(after.RoleReleaseFor("q1"));
        Assert.True(after.Releases("q2", "s9"));
    }

    /// <summary>
    /// §3.1a: a start of a repository the steward drove this run is a start a steward's act caused, until the owner acts on the repository;
    /// one the checkout would take is held for the owner and ends the steward's operations on its quest.
    /// </summary>
    [Fact]
    public async Task A_steward_s_drive_causes_its_repository_s_starts_until_the_owner_acts_on_it()
    {
        var world = new World();
        world.Change(config => config.WithTrees("engine", true));
        await using var served = await ServedAsync(world);
        var starts = new RoleStarts(served.Judge, world.Path);

        Assert.False(starts.Caused("engine", "q1"));
        Assert.Equal((true, null), Done(await served.ActAsync("drive", """{"repository":"engine","drivable":true}""")));
        Assert.True(starts.Caused("engine", "q1"));
        Assert.False(starts.Caused("atlas", "q2"));

        await using (await OwnerMarks.EnterAsync(
                         served.Judge, served.Scopes, DriverModule.OwnerDoor("SET_DRIVABLE", JsonSerializer.SerializeToElement(new { repository = "engine", drivable = true }))!,
                         false, CancellationToken.None))
        {
        }

        Assert.False(starts.Caused("engine", "q1"));
        Assert.False(served.Judge.StewardActsNow().Single().Standing);
    }

    /// <summary><c>STATE</c>'s answer names the offered acts and the run's acts, newest first, each with its target and moment, never a key.</summary>
    [Fact]
    public async Task The_window_reads_the_offered_acts_and_the_run_s_acts()
    {
        var world = new World();
        await using var served = await ServedAsync(world);
        Assert.Equal((true, null), Done(await served.ActAsync("hold", """{"repository":"engine","held":true}""")));

        var now = served.Judge.Now();

        Assert.Equal(StewardActs.Offered, now.StewardActsOffered);
        var hold = Assert.Single(now.StewardActs);
        Assert.Equal(("hold", "engine", true, false), (hold.Act, hold.Repository, hold.Standing, hold.Waiting));
        Assert.Equal(StewardActs.Offered, now.Appointments!.Single().Acts);
    }

    // ——— The kit.

    private static QuestView Quest() => new("q1", "Asker", "engine", "Ask q1", "Why.", "Taken");

    private static PriorSession Parked(string id) =>
        new(id, null, "awaiting-person", Repository: "engine") { Quest = "q1" };

    private static (bool Done, string? Why) Done(JsonElement answer) =>
        (answer.GetProperty("done").GetBoolean(), answer.TryGetProperty("why", out var why) && why.ValueKind == JsonValueKind.String ? why.GetString() : null);

    /// <summary>A served pipe whose steward has stopped s1 on q1, and the verdict holding q1 by that stop.</summary>
    private async Task<(Served Served, World World)> StoppedAsync()
    {
        var world = new World();
        world.Records["s1"] = Parked("s1");
        var served = await ServedAsync(world);
        Assert.Equal((true, null), Done(await served.ActAsync("stop", """{"id":"s1","said":true}""")));
        var stopped = new PriorSession("s1", null, "stopped") { NoteParts = SessionMoves.ByTheStewardNoted("stopped").Parts };
        world.Verdicts = [new Consideration(Quest(), StartVerdict.Stopped, "the steward stopped it") { HeldBy = stopped }];
        return (served, world);
    }

    /// <summary>A stand-in world: the driver's file in memory (saved to its path on request), the registry, the records and the moves made.</summary>
    internal sealed class World : IStewardWorld
    {
        private DriverConfig _config = DriverConfig.Empty;

        public string Path { get; set; } = "";

        public IReadOnlyList<string> Registered { get; init; } = ["engine", "atlas"];

        public Dictionary<string, PriorSession> Records { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Here { get; } = new(StringComparer.Ordinal);

        public string? QuestStatus { get; set; } = "Taken";

        public IReadOnlyList<Consideration> Verdicts { get; set; } = [];

        public List<string> Moved { get; } = [];

        public List<(string Session, Noted Note)> Stopped { get; } = [];

        /// <summary>The scratch home, under whose <c>trees</c> a tree is this home's own.</summary>
        public string Root { get; set; } = "";

        public string TreeOf(string id) => System.IO.Path.Combine(Root, "trees", id);

        public DriverConfig Config() => _config;

        public void Change(Func<DriverConfig, DriverConfig> change) => _config = change(_config);

        public Task<bool> RegisteredAsync(string repository, CancellationToken ct) =>
            Task.FromResult(Registered.Contains(repository, StringComparer.OrdinalIgnoreCase));

        public Task<string?> WorkspaceOfAsync(string repository, CancellationToken ct) => Task.FromResult<string?>("games");

        public Task<PriorSession?> SessionAsync(string id, CancellationToken ct) => Task.FromResult(Records.GetValueOrDefault(id));

        public Task<string?> QuestStatusAsync(string quest, CancellationToken ct) => Task.FromResult(QuestStatus);

        public bool RunsHere(string id) => Here.Contains(id);

        public bool AliveOnThisMachine(string id) => Here.Contains(id);

        public bool OwnTree(string? tree) => tree is not null && Root.Length > 0 && tree.StartsWith(System.IO.Path.Combine(Root, "trees"), StringComparison.OrdinalIgnoreCase);

        public bool StopHere(string id, Noted note)
        {
            Stopped.Add((id, note));
            return true;
        }

        public Task<string> StopParkedAsync(string id, CancellationToken ct)
        {
            Moved.Add("stop " + id);
            return Task.FromResult("stopped");
        }

        public Task<string> FinishAsync(string id, CancellationToken ct)
        {
            Moved.Add("finish " + id);
            return Task.FromResult("finished");
        }

        public Task<IReadOnlyList<Consideration>> VerdictsAsync(CancellationToken ct) => Task.FromResult(Verdicts);
    }

    private StandingConfirmation Judge() =>
        new(_home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside), steward: true, walk: new StandingConfirmationTests.Walked())
        {
            Drives = _ => true,
        };

    private (StandingConfirmation Judge, string Key) Appointed()
    {
        var judge = Judge();
        judge.Load();
        var (change, key) = RoleKit.Appoint(judge, RoleKit.Stewarding(share: null));
        Assert.True(change.Answer.Done, change.Answer.Message);
        return (judge, key!);
    }

    private static RoleJudgement Act(StandingConfirmation judge, string key, string act, string form)
    {
        var nonce = SupervisorProof.Nonce();
        return RoleKit.JudgeAct(judge, RoleKit.Act(key, nonce, RoleKit.GenerationOf(judge, Roles.Steward), act: act, form: form), nonce);
    }

    /// <summary>A driver module whose <c>ROLE_ACT</c> is the door's own handler over the stand-in world and reads.</summary>
    private sealed class ActModule(OwnerSetTests.Scopes scopes, World world) : ModuleBase
    {
        public InstancePipe? Pipe { get; set; }

        public override string ModuleName => DriverModule.Name;

        protected override async Task<object?> RouteMessageAsync(IpcRequest request, IModuleContext context, CancellationToken cancellationToken) =>
            await SupervisorDoor.ActAsync(Pipe, request.Payload, cancellationToken, scopes, world);
    }

    private sealed class Served(InstancePipe pipe, ServiceProvider services, StandingConfirmation judge, OwnerSetTests.Scopes scopes, World world, string key, string home)
        : IAsyncDisposable
    {
        public StandingConfirmation Judge => judge;

        public OwnerSetTests.Scopes Scopes => scopes;

        public World World => world;

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

    private async Task<Served> ServedAsync(World world)
    {
        world.Path = System.IO.Path.Combine(_home, "driver.json");
        var scopes = new OwnerSetTests.Scopes();
        var module = new ActModule(scopes, world);
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(_bus);
        services.AddSingleton<IIpcRequestTracker>(new IpcRequestTracker(_bus, new IpcRequestTrackerOptions { GracePeriod = TimeSpan.FromMilliseconds(50) }));
        services.AddSingleton<IIpcModule>(module);
        services.UseMessageDispatcher((_, _) => { });
        var provider = services.BuildServiceProvider();
        var judge = Judge();
        var pipe = new InstancePipe(provider.GetRequiredService<IMessageDispatcher>(), _bus, _home, version: "9.9.9-test", standing: judge)
        {
            Grounds = new StubGrounds(),
        };
        Assert.NotNull(pipe.Serve());
        module.Pipe = pipe;
        judge.Load();
        var (change, key) = RoleKit.Appoint(judge, RoleKit.Stewarding(share: null));
        Assert.True(change.Answer.Done, change.Answer.Message);
        await Task.CompletedTask;
        return new Served(pipe, provider, judge, scopes, world, key!, _home);
    }
}
