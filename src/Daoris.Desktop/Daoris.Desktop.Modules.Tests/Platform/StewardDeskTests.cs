using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The steward's desk (STEWARDROLE1c, D209 §6.2; the design §10 row 1c's <c>StewardDeskTests</c>): any caller files a request; nothing is
/// granted; a request never admits an act nor reaches a handler; the per-caller bound, filed from many verb processes of one agent job and
/// from many children of one long-lived ancestor, binds at ten, and the whole bound at a hundred; and a request is closed by the act that
/// names it, or by the holder's decline with its reason.
/// </summary>
/// <remarks>
/// Each test has its own scratch home under the checkout's <c>_fixtures</c>, handed by path: no test sets <c>DAORIS_HOME</c>, starts a
/// window, a console or an agent. The verb processes are a stand-in process table (<see cref="Tree"/>): the bound is proved over the walk
/// the desk's caller look makes, not over processes started here. The pipe is served in this process, as <c>RoleActTests</c>' is.
/// </remarks>
public sealed class StewardDeskTests : IDisposable
{
    private readonly string _home;
    private readonly EventBus _bus = new(NullLogger<EventBus>.Instance);

    public StewardDeskTests()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "daoris.json"))) root = root.Parent;
        _home = Path.Combine(root?.FullName ?? throw new InvalidOperationException("the tests run in a checkout."), "_fixtures", "steward-desk-tests",
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

    private const string Drive = """{"act":"drive","target":"engine","why":"the backlog's next row is engine's"}""";

    // ——— Any caller files; nothing is granted.

    /// <summary>A caller in an agent job, one the look cannot place, and one outside both file alike: the desk is an open route.</summary>
    [Theory]
    [InlineData(StandingPlace.InAgentJob)]
    [InlineData(StandingPlace.Untold)]
    [InlineData(StandingPlace.Outside)]
    public void Any_caller_files(StandingPlace place)
    {
        var judge = Judge(new Keyed("caller"), place);

        var filed = judge.FileRequest(Payload(Drive), client: null);

        Assert.True(filed.Filed, filed.Why);
        var held = Assert.Single(judge.Now().StewardDesk);
        Assert.Equal((filed.Id, "drive", "engine", "the backlog's next row is engine's", DeskStates.Open), (held.Id, held.Act, held.Target, held.Why, held.State));
    }

    /// <summary>
    /// Nothing is granted: a request for an act changes no appointment's acts, records no act of the steward's, admits nothing, and the
    /// act it asks for is still judged by <c>ROLE_ACT</c> alone, refused there as it would be with no request.
    /// </summary>
    [Fact]
    public void Nothing_is_granted()
    {
        var judge = Judge(new Keyed("caller"));
        judge.Load();
        var (change, key) = RoleKit.Appoint(judge, RoleKit.Stewarding(share: null));
        Assert.True(change.Answer.Done, change.Answer.Message);
        var before = judge.Now().Appointments.Single().Acts;

        Assert.True(judge.FileRequest(Payload("""{"act":"stop","target":"s1","why":"it loops"}"""), null).Filed);

        Assert.Equal(before, judge.Now().Appointments.Single().Acts);
        Assert.Empty(judge.StewardActsNow());
        // The act it asks for, sent in another form, is refused `form` by the role's judge, as it is with no request on the desk.
        var nonce = SupervisorProof.Nonce();
        var judged = RoleKit.JudgeAct(judge, RoleKit.Act(key!, nonce, RoleKit.GenerationOf(judge, Roles.Steward), act: "stop", form: """{"id":"s1"}"""), nonce);
        Assert.Equal((false, SupervisorWhy.Form), (judged.Covered, judged.Why));
    }

    /// <summary>The build offers no steward: the desk keeps nothing and shows nothing.</summary>
    [Fact]
    public void A_build_that_does_not_offer_the_steward_keeps_nothing()
    {
        var judge = new StandingConfirmation(_home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside), steward: false, desk: new Keyed("caller"));

        Assert.Equal(new DeskFiled(false, null, DeskWhy.NotOffered), judge.FileRequest(Payload(Drive), null));
        Assert.Empty(judge.Desk.Now());
        Assert.Empty(judge.Now().StewardDesk);
    }

    /// <summary>The exact form, every name once; an act the steward's table does not make is refused <c>not-role</c>, nothing kept.</summary>
    [Theory]
    [InlineData("""{"act":"drive","target":"engine"}""", DeskWhy.Form)]
    [InlineData("""{"act":"drive","target":"engine","why":"x","generation":"g"}""", DeskWhy.Form)]
    [InlineData("""{"act":"drive","target":"engine","why":"x","ACT":"drive"}""", DeskWhy.Form)]
    [InlineData("""{"act":"drive","target":" engine","why":"x"}""", DeskWhy.Form)]
    [InlineData("""{"act":"drive","target":"engine","why":""}""", DeskWhy.Form)]
    [InlineData("""{"act":"drive","target":"engine","why":7}""", DeskWhy.Form)]
    [InlineData("""{"act":"setup","target":"engine","why":"x"}""", DeskWhy.NotRole)]
    [InlineData("""{"act":"decline","target":"q1","why":"x"}""", DeskWhy.NotRole)]
    [InlineData("""{"act":"desk-decline","target":"r1","why":"x"}""", DeskWhy.NotRole)]
    [InlineData("""{"Act":"drive","TARGET":"engine","why":"x"}""", null)]
    public void Anything_but_the_exact_form_of_one_of_the_steward_s_acts_is_refused(string payload, string? why)
    {
        var judge = Judge(new Keyed("caller"));

        var filed = judge.FileRequest(Payload(payload), null);

        Assert.Equal((why is null, why), (filed.Filed, filed.Why));
        Assert.Equal(why is null ? 1 : 0, judge.Desk.Now().Count);
    }

    // ——— A request never admits an act nor reaches a handler, over the pipe.

    /// <summary>
    /// <c>STEWARD_REQUEST</c> is <c>Open</c>: no role's judgement is made, no act admitted and no act's handler run; a request naming the
    /// probe's act is kept as a claim and the probe never runs; a <c>ROLE_ACT</c>'s whole proved payload sent to the desk is refused
    /// <c>form</c>. The caller look is handed the connection's own client.
    /// </summary>
    [Fact]
    public async Task A_request_never_admits_an_act_nor_reaches_a_handler()
    {
        var probe = new RoleKit.Probe();
        var keyed = new Keyed("caller");
        await using var served = await ServedAsync(keyed, probe);

        var filed = await served.RequestAsync("""{"act":"drive","target":"engine","why":"run it"}""");
        var proved = RoleKit.Act(served.Key, SupervisorProof.Nonce(), RoleKit.GenerationOf(served.Judge, Roles.Steward), act: "drive",
            form: """{"repository":"engine","drivable":true}""");
        var smuggled = await served.RequestAsync(proved.GetRawText());

        Assert.True(filed.GetProperty("filed").GetBoolean(), filed.GetRawText());
        Assert.Equal((false, DeskWhy.Form), (smuggled.GetProperty("filed").GetBoolean(), smuggled.GetProperty("why").GetString()));
        Assert.Equal(0, probe.Ran);
        Assert.Empty(served.Judge.StewardActsNow());
        Assert.Equal(0, served.Judge.Now().Appointments.Single().Answers);
        Assert.Equal(DeskStates.Open, Assert.Single(served.Judge.Desk.Now()).State);
        if (OperatingSystem.IsWindows()) Assert.True(keyed.SawClient, "the desk's caller look is handed the pipe's client");
    }

    // ——— The bounds (§6.2, finding 11): per caller, keyed by its agent job or its long-lived ancestor, and in all.

    /// <summary>
    /// Many verb processes of one agent job, each a new process with its own parents, bind at ten: the job is the caller. A caller
    /// outside the job still files.
    /// </summary>
    [Fact]
    public void The_per_caller_bound_from_many_verb_processes_of_one_agent_job_binds_at_ten()
    {
        var tree = new Tree();
        var jobbed = new HashSet<int>();
        for (var at = 0; at < 11; at++)
        {
            // Each verb under a harness of its own, so only the job is common to them.
            tree.Add(1000 + at, 1, Born.AddMinutes(-30));
            tree.Add(2000 + at, 1000 + at, Born.AddSeconds(-1));
            jobbed.Add(2000 + at);
        }

        tree.Add(500, 1, Born.AddHours(-2));
        tree.Add(3000, 500, Born.AddSeconds(-1));
        var judge = Judge(new DeskCaller(_ => jobbed.Contains(tree.Client!.Value) ? "daoris-agents-77" : null, tree, new Fixed(Born), own: 1));

        var answers = Enumerable.Range(0, 11).Select(at => tree.Filing(2000 + at, () => judge.FileRequest(Payload(Drive), null))).ToList();
        var outside = tree.Filing(3000, () => judge.FileRequest(Payload(Drive), null));

        Assert.All(answers.Take(10), each => Assert.True(each.Filed, each.Why));
        Assert.Equal(new DeskFiled(false, null, DeskWhy.Full), answers[10]);
        Assert.True(outside.Filed, outside.Why);
    }

    /// <summary>
    /// Many children of one long-lived ancestor (the verb, under the short-lived shell a harness's tool starts for each command, under the
    /// working session's harness) bind at ten: the harness is the caller, never the verb or its shell. A terminal's shell beside it files.
    /// </summary>
    [Fact]
    public void The_per_caller_bound_from_many_children_of_one_long_lived_ancestor_binds_at_ten()
    {
        var tree = new Tree();
        tree.Add(100, 1, Born.AddHours(-1));
        for (var at = 0; at < 11; at++)
        {
            tree.Add(200 + at, 100, Born.AddSeconds(-2));
            tree.Add(300 + at, 200 + at, Born.AddSeconds(-1));
        }

        tree.Add(700, 1, Born.AddMinutes(-5));
        tree.Add(701, 700, Born.AddSeconds(-1));
        var judge = Judge(new DeskCaller(_ => null, tree, new Fixed(Born), own: 1));

        var answers = Enumerable.Range(0, 11).Select(at => tree.Filing(300 + at, () => judge.FileRequest(Payload(Drive), null))).ToList();
        var beside = tree.Filing(701, () => judge.FileRequest(Payload(Drive), null));

        Assert.All(answers.Take(10), each => Assert.True(each.Filed, each.Why));
        Assert.Equal(new DeskFiled(false, null, DeskWhy.Full), answers[10]);
        Assert.True(beside.Filed, beside.Why);
    }

    /// <summary>
    /// The walk never passes this Daoris's own process, and a parent created after its child is another process: neither is the caller.
    /// With no long-lived ancestor the oldest one read is; with none read, every such caller shares the untold ten.
    /// </summary>
    [Fact]
    public void The_caller_look_stops_at_this_Daoris_and_at_a_reused_id()
    {
        var tree = new Tree();
        tree.Add(10, 1, Born.AddHours(-3));
        tree.Add(11, 10, Born.AddSeconds(-5));
        tree.Add(12, 11, Born.AddSeconds(-1));
        tree.Add(20, 99, Born.AddSeconds(-1));
        tree.Add(99, 1, Born.AddSeconds(1));
        var look = new DeskCaller(_ => null, tree, new Fixed(Born), own: 10);

        var underDaoris = tree.Filing(12, () => look.KeyOf(null));
        var reused = tree.Filing(20, () => look.KeyOf(null));

        Assert.Equal($"process:11:{Born.AddSeconds(-5).Ticks}", underDaoris);
        Assert.Equal(DeskCaller.Untold, reused);
        Assert.Equal(DeskCaller.Untold, tree.Filing(404, () => look.KeyOf(null)));
    }

    /// <summary>The whole bound: a hundred open from ten callers, and the next caller's first refused <c>desk-full</c>; a closed request frees its place.</summary>
    [Fact]
    public void The_whole_bound_binds_at_a_hundred()
    {
        var keyed = new Keyed("c0");
        var judge = Judge(keyed);
        for (var caller = 0; caller < 10; caller++)
        {
            keyed.Key = $"c{caller}";
            for (var at = 0; at < 10; at++) Assert.True(judge.FileRequest(Payload($$"""{"act":"hold","target":"r{{caller}}-{{at}}","why":"x"}"""), null).Filed);
        }

        keyed.Key = "c10";
        Assert.Equal(new DeskFiled(false, null, DeskWhy.Full), judge.FileRequest(Payload(Drive), null));
        Assert.Equal(StewardDesk.InAll, judge.Desk.Now().Count(each => each.State == DeskStates.Open));

        judge.StewardDid("a1", "hold", repository: "r3-3");
        Assert.True(judge.FileRequest(Payload(Drive), null).Filed);
    }

    // ——— Closing: by the act that names it, or the holder's decline with its reason.

    /// <summary>
    /// The steward's act that makes what a request asked, on what it named, closes it as answered by that act; a request for another
    /// act or another target stays open. A session's act names its session, and a Try again its quest without its <c>#</c>.
    /// </summary>
    [Fact]
    public void A_request_is_closed_by_the_act_that_names_it()
    {
        var judge = Judge(new Keyed("caller"));
        var trees = judge.FileRequest(Payload("""{"act":"trees","target":"Engine","why":"x"}"""), null).Id;
        var drive = judge.FileRequest(Payload("""{"act":"drive","target":"engine","why":"x"}"""), null).Id;
        var other = judge.FileRequest(Payload("""{"act":"trees","target":"atlas","why":"x"}"""), null).Id;
        var stop = judge.FileRequest(Payload("""{"act":"stop","target":"s1","why":"x"}"""), null).Id;
        var retry = judge.FileRequest(Payload("""{"act":"retry","target":"#q1","why":"x"}"""), null).Id;

        judge.StewardDid("a-trees", "trees", repository: "engine");
        judge.StewardDid("a-stop", "stop", "engine", "q1", "s1");
        judge.StewardDid("a-retry", "retry", quest: "q1", session: "s1");

        var desk = judge.Desk.Now().ToDictionary(each => each.Id);
        Assert.Equal((DeskStates.Answered, "a-trees"), (desk[trees!].State, desk[trees!].AnsweredBy));
        Assert.Equal((DeskStates.Answered, "a-stop"), (desk[stop!].State, desk[stop!].AnsweredBy));
        Assert.Equal((DeskStates.Answered, "a-retry"), (desk[retry!].State, desk[retry!].AnsweredBy));
        Assert.Equal(DeskStates.Open, desk[drive!].State);
        Assert.Equal(DeskStates.Open, desk[other!].State);
    }

    /// <summary>
    /// Over the pipe: the holder's act closes the request that named it; the holder's <c>desk-decline</c>, which no appointment's
    /// <c>acts</c> names, closes another with its reason and records no act of the steward's; a decline of a closed request is refused
    /// <c>not-open</c>, and one in another form <c>form</c>.
    /// </summary>
    [Fact]
    public async Task The_holder_answers_a_request_by_an_act_and_declines_another_with_its_reason()
    {
        var probe = new RoleKit.Probe();
        await using var served = await ServedAsync(new Keyed("caller"), probe);
        var asked = (await served.RequestAsync("""{"act":"drive","target":"engine","why":"run it"}""")).GetProperty("request").GetString()!;
        var refused = (await served.RequestAsync("""{"act":"hold","target":"atlas","why":"hold it"}""")).GetProperty("request").GetString()!;

        Assert.DoesNotContain(DeskDecline.Named, served.Judge.Now().Appointments.Single().Acts);
        var declined = await served.ActAsync(DeskDecline.Named, $$"""{"request":"{{refused}}","reason":"atlas is the owner's to hold"}""");
        var again = await served.ActAsync(DeskDecline.Named, $$"""{"request":"{{refused}}","reason":"again"}""");
        var form = await served.ActAsync(DeskDecline.Named, $$"""{"request":"{{asked}}"}""", success: false);
        served.Judge.StewardDid("a-drive", "drive", repository: "engine");

        Assert.True(declined.GetProperty("done").GetBoolean(), declined.GetRawText());
        Assert.Equal((false, DeskWhy.NotOpen), (again.GetProperty("done").GetBoolean(), again.GetProperty("why").GetString()));
        Assert.Equal(SupervisorWhy.Form, form.GetProperty("error").GetProperty("code").GetString());
        var desk = served.Judge.Desk.Now().ToDictionary(each => each.Id);
        Assert.Equal((DeskStates.Declined, "atlas is the owner's to hold"), (desk[refused].State, desk[refused].Reason));
        Assert.Equal((DeskStates.Answered, "a-drive"), (desk[asked].State, desk[asked].AnsweredBy));
        Assert.Equal(["drive"], served.Judge.StewardActsNow().Select(each => each.Act));
        Assert.Equal(0, probe.Ran);
    }

    // ——— The kit.

    private static JsonElement Payload(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static readonly DateTime Born = DateTime.Parse("2026-10-25T09:00:00Z").ToUniversalTime();

    private StandingConfirmation Judge(IDeskCaller desk, StandingPlace place = StandingPlace.Outside) =>
        new(_home, null, new StandingConfirmationTests.Placed(place), steward: true, walk: new StandingConfirmationTests.Walked(), desk: desk)
        {
            Drives = _ => true,
        };

    /// <summary>A stand-in caller look: every request the caller <see cref="Key"/> names; and whether it was handed a client.</summary>
    private sealed class Keyed(string key) : IDeskCaller
    {
        public string Key { get; set; } = key;

        public bool SawClient { get; private set; }

        public string KeyOf(PipeClientProcess? client)
        {
            SawClient |= client?.Handle is not null;
            return Key;
        }
    }

    private sealed class Fixed(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
    }

    /// <summary>A stand-in process table: each process its parent and its creation; the client the verb filing now.</summary>
    private sealed class Tree : IProcessTable
    {
        private readonly Dictionary<int, ProcessFacts> _processes = [];

        public int? Client { get; private set; }

        public void Add(int id, int parent, DateTime created) =>
            _processes[id] = new ProcessFacts(id, parent, created, null, null, _ => null);

        public T Filing<T>(int verb, Func<T> file)
        {
            Client = verb;
            return file();
        }

        public int? IdOf(PipeClientProcess? client) => Client;

        public ProcessFacts? Read(int id) => _processes.GetValueOrDefault(id);
    }

    /// <summary>A driver module whose <c>STEWARD_REQUEST</c> is the judge's filing and <c>ROLE_ACT</c> the door's own handler.</summary>
    private sealed class DeskModule : ModuleBase
    {
        public InstancePipe? Pipe { get; set; }

        public override string ModuleName => DriverModule.Name;

        protected override async Task<object?> RouteMessageAsync(IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
        {
            if (request.Type == RoleRoutes.Act) return await SupervisorDoor.ActAsync(Pipe, request.Payload, cancellationToken, new OwnerSetTests.Scopes());
            var filed = Pipe!.StandingConfirmation.FileRequest(request.Payload, PipeDoor.Client);
            return new { filed.Filed, Request = filed.Id, filed.Why };
        }
    }

    private sealed class Served(InstancePipe pipe, ServiceProvider services, StandingConfirmation judge, string key, string home) : IAsyncDisposable
    {
        public StandingConfirmation Judge => judge;

        public string Key => key;

        public async Task<JsonElement> RequestAsync(string payload)
        {
            await using var client = await PipeClient.ConnectAsync(pipe.Name!);
            await client.HelloAsync(HomeIdentity.Read(home)!);
            var answer = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, "STEWARD_REQUEST", Payload(payload)));
            Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
            return answer.GetProperty("data");
        }

        public async Task<JsonElement> ActAsync(string act, string form, bool success = true)
        {
            await using var client = await PipeClient.ConnectAsync(pipe.Name!);
            var hello = await client.HelloAsync(HomeIdentity.Read(home)!);
            var nonce = hello.GetProperty("data").GetProperty("nonce").GetString();
            var payload = RoleKit.Act(key, nonce, RoleKit.GenerationOf(judge, Roles.Steward), act: act, form: form);
            var answer = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, RoleRoutes.Act, payload));
            Assert.Equal(success, answer.GetProperty("success").GetBoolean());
            return success ? answer.GetProperty("data") : answer;
        }

        public async ValueTask DisposeAsync()
        {
            pipe.Dispose();
            await services.DisposeAsync();
        }
    }

    private async Task<Served> ServedAsync(IDeskCaller desk, RoleKit.Probe probe)
    {
        var module = new DeskModule();
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(_bus);
        services.AddSingleton<IIpcRequestTracker>(new IpcRequestTracker(_bus, new IpcRequestTrackerOptions { GracePeriod = TimeSpan.FromMilliseconds(50) }));
        services.AddSingleton<IIpcModule>(module);
        services.UseMessageDispatcher((_, _) => { });
        var provider = services.BuildServiceProvider();
        var judge = Judge(desk);
        // The probe joins the steward's table beside the desk's decline, so a request naming no act of the table is still refused.
        judge.TableOf = role => role == Roles.Steward
            ? new Dictionary<string, IRoleAct>(StewardActs.TableOf(role), StringComparer.Ordinal) { [RoleKit.Probe.Named] = probe }
            : new Dictionary<string, IRoleAct>(StringComparer.Ordinal);
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
        return new Served(pipe, provider, judge, key!, _home);
    }
}
