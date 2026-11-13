using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Over the real pipe: a press the person's standing confirmation covers runs with no card, the terminal told <c>STANDING</c> and the log
/// <c>person.confirmation</c> <c>state: standing</c>; it is admitted once, so a revoke after its admission lets it run and makes the next
/// ask; a press asked before a grant keeps its card; and one the grant names that asks anyway says why, to the terminal and in the log.
/// </summary>
/// <remarks>
/// Each test serves its own pipe in this process, named by its own scratch home's id; where the terminal's process stands is a stand-in
/// (<see cref="StandingConfirmationTests.Placed"/>), the system's look being <c>StandingCallerTests</c>'. Windows only.
/// </remarks>
public sealed class PipeStandingTests : Bridge
{
    private (IMessageDispatcher Dispatcher, ServiceProvider Services) Compose(MachineLog log, params IIpcModule[] modules)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(Bus);
        var options = new IpcRequestTrackerOptions { GracePeriod = TimeSpan.FromMilliseconds(50) };
        var tracker = new IpcRequestTracker(Bus, options);
        services.AddSingleton<IIpcRequestTracker>(tracker);
        foreach (var module in modules) services.AddSingleton(module);
        services.UseMessageDispatcher((_, dispatcher) => dispatcher.Use(RefusalLog.Middleware(log)));
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IMessageDispatcher>(), provider);
    }

    private string HomeId => HomeIdentity.Read(Home)!;

    private (InstancePipe Pipe, StandingConfirmationTests.Placed Caller) Pipe(IMessageDispatcher dispatcher, MachineLog log)
    {
        var caller = new StandingConfirmationTests.Placed(StandingPlace.Outside);
        var standing = new StandingConfirmation(Home, log, caller);
        var pipe = new InstancePipe(dispatcher, Bus, Home, log, version: "9.9.9-test", standing: standing) { Grounds = new StubGrounds() };
        Assert.NotNull(pipe.Serve());
        return (pipe, caller);
    }

    private static IEnumerable<JsonElement> Each(IEnumerable<JsonElement> told) =>
        told.SelectMany(line => line.GetProperty("payload").EnumerateArray());

    private static bool Is(JsonElement each, string type) =>
        each.GetProperty("module").GetString() == InstancePipe.Module && each.GetProperty("type").GetString() == type;

    /// <summary>
    /// An agent's press accepting a repository's integration
    /// declaration, from its own job and with the standing confirmation granted, is never covered. It waits for the person's Confirm,
    /// runs nothing meanwhile, and the person's no refuses it, so an agent never accepts the commands and the scratch it would run under.
    /// </summary>
    [Fact]
    public async Task An_agents_press_accepting_an_integration_declaration_runs_nothing_without_the_persons_Confirm()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new HeldModule(DriverModule.Name);
        driver.Let.SetResult();
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, caller) = Pipe(dispatcher, log);
        using var served = pipe;
        caller.Answer = StandingPlace.InAgentJob;
        await using var client = await PipeClient.ConnectAsync(pipe.Name!);
        await client.HelloAsync(HomeId);

        var id = await client.SendAsync(DriverModule.Name, "INTEGRATION_GATES", new { repository = "engine", gates = "3b18e512dba79e4c8300dd08aeb37f8e728b8dad" });
        await Until(() => pipe.Confirmations.Now().Waiting.Count == 1, () => "the press was not asked");

        Assert.Equal("INTEGRATION_GATES", Assert.Single(pipe.Confirmations.Now().Waiting).Route);
        Assert.Equal(PipeClass.Person, PipeCensus.Daoris.Of(DriverModule.Name, "INTEGRATION_GATES"));
        Assert.Equal(0, driver.Ran);
        await pipe.Confirmations.AnswerAsync(pipe.Confirmations.Now().Waiting[0].Id, confirm: false);
        Assert.False((await client.AnswerAsync(id)).GetProperty("success").GetBoolean());
        Assert.Equal(0, driver.Ran);
    }

    /// <summary>
    /// Applying a place's plan is the person's alone. The census marks it Person; from an
    /// agent's job with the standing confirmation granted it is never covered, so it waits for the person's Confirm, runs nothing meanwhile and
    /// is refused by their no; and no role's act reaches it, the steward's table naming none and a <c>ROLE_ACT</c> for it refused as no act.
    /// </summary>
    [Fact]
    public async Task A_place_apply_is_the_persons_alone_no_standing_cover_and_no_role_act()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.Equal(PipeClass.Person, PipeCensus.Daoris.Of(DriverModule.Name, "PLACE_APPLY"));
        Assert.DoesNotContain("PLACE_APPLY", AppointedRoles.StewardActs);
        Assert.DoesNotContain("PLACE_APPLY", StewardActs.TableOf(AppointedRoles.Steward).Keys);

        using var log = new MachineLog(Home, "desktop");
        var driver = new HeldModule(DriverModule.Name);
        driver.Let.SetResult();
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, caller) = Pipe(dispatcher, log);
        using var served = pipe;
        caller.Answer = StandingPlace.InAgentJob;
        await using var client = await PipeClient.ConnectAsync(pipe.Name!);
        await client.HelloAsync(HomeId);

        var id = await client.SendAsync(DriverModule.Name, "PLACE_APPLY", new { repositories = new[] { "engine" }, preset = SetupPresets.Merge, digest = new string('a', 64) });
        await Until(() => pipe.Confirmations.Now().Waiting.Count == 1, () => "the press was not asked");

        Assert.Equal("PLACE_APPLY", Assert.Single(pipe.Confirmations.Now().Waiting).Route);
        Assert.Equal(0, driver.Ran);
        await pipe.Confirmations.AnswerAsync(pipe.Confirmations.Now().Waiting[0].Id, confirm: false);
        Assert.False((await client.AnswerAsync(id)).GetProperty("success").GetBoolean());
        Assert.Equal(0, driver.Ran);
    }

    /// <summary>A steward's <c>ROLE_ACT</c> naming <c>PLACE_APPLY</c> is no act of its table, so it is refused and runs nothing.</summary>
    [Fact]
    public async Task A_role_act_naming_a_place_apply_is_refused_as_no_act_and_runs_nothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new RoleActModule();
        var probe = new RoleKit.Probe();
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, _, key, generation) = Stewarded(dispatcher, log, probe);
        using var served = pipe;
        driver.Pipe = pipe;
        await using var client = await PipeClient.ConnectAsync(pipe.Name!);
        var nonce = NonceOf(await client.HelloAsync(HomeId))!;

        var answer = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, RoleRoutes.Act, RoleKit.Act(key, nonce, generation, act: "PLACE_APPLY")));

        Assert.Equal(RoleWhy.NotRole, CodeOf(answer));
        Assert.Equal(0, probe.Ran);
        Assert.Empty(pipe.Confirmations.Now().Waiting);
    }

    /// <summary>Waits for <paramref name="condition"/>, bounded by the patience a signal that only fails to come when the code is wrong gets.</summary>
    private static async Task Until(Func<bool> condition, Func<string> because)
    {
        var until = DateTime.UtcNow + Poll.Patience;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < until, because());
            await Task.Delay(10);
        }
    }

    private List<JsonElement> Lines(string @event)
    {
        var folder = DaorisHome.Entry(Home, HomeEntry.Logs);
        if (!Directory.Exists(folder)) return [];
        return
        [
            .. Directory.GetFiles(folder).SelectMany(path =>
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            })
            .Select(line => JsonDocument.Parse(line).RootElement.Clone())
            .Where(line => line.GetProperty("event").GetString() == @event),
        ];
    }

    // Over the real pipe: the hello draws a nonce for its
    // connection; a `Role` route the judge's role method covers runs with no card, its handler reading the role's door; one it does
    // not cover is refused, never asked, with no press granted as with one; and a proof missing, wrong, over another connection's nonce,
    // replayed or over an altered form is refused `not-holder`. The key never crosses the pipe: the client sends a proof alone.
    /// <summary>A driver module that says what door and role each request ran under.</summary>
    private sealed class RoleModule : ModuleBase
    {
        public List<RoleJudgement?> Seen { get; } = [];

        public override string ModuleName => DriverModule.Name;

        protected override Task<object?> RouteMessageAsync(IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
        {
            lock (Seen) Seen.Add(PipeDoor.Role);
            return Task.FromResult<object?>(new { Kept = true });
        }
    }

    /// <summary>The repository's own supervisor, which the catalogue staffs with the terminal.</summary>
    private const string Engine = "supervisor-engine";

    private (InstancePipe Pipe, StandingConfirmation Judge, string Key, string Generation) Appointed(
        IMessageDispatcher dispatcher, MachineLog log, IHolderWalk? walk = null)
    {
        var standing = new StandingConfirmation(
            Home, log, new StandingConfirmationTests.Placed(StandingPlace.Outside), walk: walk ??new StandingConfirmationTests.Walked())
            .Staff(RoleKit.Supervisor(Engine, RoleKit.Repository("engine")));
        var pipe = new InstancePipe(dispatcher, Bus, Home, log, version: "9.9.9-test", standing: standing) { Grounds = new StubGrounds() };
        Assert.NotNull(pipe.Serve());
        var (change, handed) = RoleKit.Start(standing, new HoldRequest(Engine, "own"));
        Assert.True(change.Answer.Done, change.Answer.Message);
        return (pipe, standing, handed!, standing.Admit("engine").Generation!);
    }

    /// <summary>An answer proved by <paramref name="key"/> over <paramref name="nonce"/>, as the verb builds it.</summary>
    private static object Proved(string key, string? nonce, string generation, string choice = "Monthly")
    {
        SupervisorReason[] reasons = [new("The ticket says monthly.", "the quest's line 2")];
        var form = new SupervisorDecideForm(SupervisorSubject.Question("s1", "q-1"), generation, new string('0', 64), SupervisorKinds.Answer, choice, reasons);
        return (nonce is null ? form : form.Proved(key, nonce)).Payload();
    }

    private static string? NonceOf(JsonElement hello) =>
        hello.GetProperty("data").TryGetProperty("nonce", out var nonce) ? nonce.GetString() : null;

    private static string? CodeOf(JsonElement answer) =>
        answer.TryGetProperty("error", out var error) ? error.GetProperty("code").GetString() : null;

    [Fact]
    public async Task A_covered_role_act_runs_with_no_card_under_the_role_door_and_never_the_presses_standing()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new RoleModule();
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, _, key, generation) = Appointed(dispatcher, log);
        using var served = pipe;
        await using var client = await PipeClient.ConnectAsync(pipe.Name!);
        var nonce = NonceOf(await client.HelloAsync(HomeId));
        Assert.Equal(64, nonce!.Length);

        var id = await client.SendAsync(DriverModule.Name, SupervisorRoutes.Decide, Proved(key, nonce, generation));
        var answer = await client.AnswerAsync(id);

        Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
        var role = Assert.Single(driver.Seen);
        Assert.Equal(("engine", generation), (role!.Repository, role.Generation));
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.DoesNotContain(Each(client.Told), each => Is(each, InstancePipe.Confirming));
    }

    public static TheoryData<string> Wrong => ["none", "key", "other-nonce", "replayed", "altered"];

    [Theory]
    [MemberData(nameof(Wrong))]
    public async Task A_proof_that_is_not_the_holders_for_this_connection_and_form_is_refused_never_asked(string wrong)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new RoleModule();
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, _, key, generation) = Appointed(dispatcher, log);
        using var served = pipe;
        await using var client = await PipeClient.ConnectAsync(pipe.Name!);
        var nonce = NonceOf(await client.HelloAsync(HomeId))!;
        await using var other = await PipeClient.ConnectAsync(pipe.Name!);
        var elsewhere = NonceOf(await other.HelloAsync(HomeId))!;

        object payload = wrong switch
        {
            "none" => Proved(key, null, generation),
            "key" => Proved(HolderKey.Draw(), nonce, generation),
            "other-nonce" => Proved(key, elsewhere, generation),
            _ => Proved(key, nonce, generation),
        };
        if (wrong == "altered")
        {
            var text = JsonSerializer.Serialize(payload).Replace("\"Monthly\"", "\"Weekly\"", StringComparison.Ordinal);
            payload = JsonDocument.Parse(text).RootElement.Clone();
        }

        if (wrong == "replayed")
        {
            // The first use of the nonce is the holder's own; the same proof sent again finds it used.
            Assert.True((await client.AnswerAsync(await client.SendAsync(DriverModule.Name, SupervisorRoutes.Decide, payload))).GetProperty("success").GetBoolean());
        }

        var answer = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, SupervisorRoutes.Decide, payload));
        Assert.False(answer.GetProperty("success").GetBoolean());
        Assert.Equal(SupervisorWhy.NotHolder, CodeOf(answer));
        Assert.Equal(wrong == "replayed" ? 1 : 0, driver.Seen.Count);
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Contains(Lines("supervisor.refused"), line => line.GetProperty("data").GetProperty("why").GetString() == SupervisorWhy.NotHolder);
    }

    /// <summary>A caller in an agent's job is refused before its proof is read, and a role act no terminal holds is refused.</summary>
    /// <summary>
    /// D284 point 2: a proved holder in an agents' job acts as any caller, with no card; a dark build's judge, whose catalogue staffs no
    /// one, still covers nothing.
    /// </summary>
    [Fact]
    public async Task A_contained_holder_acts_by_its_proof_and_a_dark_build_covers_nothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new RoleModule();
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, judge, key, generation) = Appointed(dispatcher, log);
        using var served = pipe;
        ((StandingConfirmationTests.Placed)judge.Caller).Answer = StandingPlace.InAgentJob;
        await using var client = await PipeClient.ConnectAsync(pipe.Name!);
        var nonce = NonceOf(await client.HelloAsync(HomeId))!;
        var ran = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, SupervisorRoutes.Decide, Proved(key, nonce, generation)));
        Assert.True(ran.GetProperty("success").GetBoolean(), ran.GetRawText());
        Assert.Single(driver.Seen);
        driver.Seen.Clear();

        // A home's own judge over the seed's catalogue: your rules alone hold the supervisor, so nothing is covered whatever is sent.
        using var darkLog = new MachineLog(Home, "dark");
        // The judge's caller is placed outside, so what refuses is that no role is staffed, not the test host's own look at the system.
        var darkStanding = new StandingConfirmation(
            Path.Combine(Home, "dark-home"), darkLog, new StandingConfirmationTests.Placed(StandingPlace.Outside), walk: new StandingConfirmationTests.Walked());
        var dark = new InstancePipe(dispatcher, Bus, Path.Combine(Home, "dark-home"), darkLog, version: "9.9.9-test", standing: darkStanding);
        Directory.CreateDirectory(Path.Combine(Home, "dark-home"));
        if (dark.Serve() is { } name)
        {
            using var darkServed = dark;
            await using var darkClient = await PipeClient.ConnectAsync(name);
            var darkNonce = NonceOf(await darkClient.HelloAsync(HomeIdentity.Read(Path.Combine(Home, "dark-home"))!))!;
            var answer = await darkClient.AnswerAsync(await darkClient.SendAsync(DriverModule.Name, SupervisorRoutes.Decide, Proved(key, darkNonce, generation)));
            Assert.Equal(SupervisorWhy.NotStaffed, CodeOf(answer));
            Assert.Empty(driver.Seen);
        }
    }

    // Over the real pipe and the judge: ROLE_ACT judged as the supervisor's answer is, admitted once, checked again at its gate; the run's memory of pending operations by
    // their stages, each consumer taking its own once, bindings read by later starts, the execution cutoff after a consumer's awaits, all
    // of which a revoke cancels; and the configuration walk for every route of the Role class.

    /// <summary>A driver module whose <c>ROLE_ACT</c> is the door's own handler, <see cref="Before"/> run first, as a revoke landing between admission and the gate.</summary>
    private sealed class RoleActModule : ModuleBase
    {
        public InstancePipe? Pipe { get; set; }

        public Action? Before { get; set; }

        public override string ModuleName => DriverModule.Name;

        protected override async Task<object?> RouteMessageAsync(IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
        {
            Before?.Invoke();
            return await SupervisorDoor.ActAsync(Pipe, request.Payload, cancellationToken);
        }
    }

    /// <summary>The steward's terminal started over a served pipe, its table holding the probe; the repository's supervisor staffed beside it.</summary>
    private (InstancePipe Pipe, StandingConfirmation Judge, string Key, string Generation) Stewarded(
        IMessageDispatcher dispatcher, MachineLog log, RoleKit.Probe probe, IHolderWalk? walk = null)
    {
        var standing = new StandingConfirmation(
            Home, log, new StandingConfirmationTests.Placed(StandingPlace.Outside), walk: walk ??new StandingConfirmationTests.Walked())
            .Probing(probe).Staff(RoleKit.Supervisor(Engine, RoleKit.Repository("engine")));
        var pipe = new InstancePipe(dispatcher, Bus, Home, log, version: "9.9.9-test", standing: standing) { Grounds = new StubGrounds() };
        Assert.NotNull(pipe.Serve());
        var (change, key) = RoleKit.Start(standing, RoleKit.Stewarding(share: null));
        Assert.True(change.Answer.Done, change.Answer.Message);
        return (pipe, standing, key!, RoleKit.GenerationOf(standing, AppointedRoles.Steward));
    }

    public static TheoryData<string> WrongActs => ["none", "key", "other-role", "replayed"];

    /// <summary>A ROLE_ACT with no proof, a wrong key, a holder's key for a role it does not hold, or a replay, each refused, nothing run.</summary>
    [Theory]
    [MemberData(nameof(WrongActs))]
    public async Task A_role_act_that_is_not_its_holder_s_for_this_connection_is_refused_and_runs_nothing(string wrong)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new RoleActModule();
        var probe = new RoleKit.Probe();
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, judge, key, generation) = Stewarded(dispatcher, log, probe);
        using var served = pipe;
        driver.Pipe = pipe;
        await using var client = await PipeClient.ConnectAsync(pipe.Name!);
        var nonce = NonceOf(await client.HelloAsync(HomeId))!;

        // A holder that holds the supervisor alone: its key proves nothing for the steward's hold.
        string? supervisorKey = null;
        if (wrong == "other-role")
        {
            Assert.True(judge.Start(new HoldRequest(Engine, "own"), open =>
            {
                supervisorKey = open.Key;
                return StandingConfirmationTests.Opened(open);
            }).Answer.Done);
        }

        var payload = wrong switch
        {
            "none" => RoleKit.Act(key, null, generation),
            "key" => RoleKit.Act(HolderKey.Draw(), nonce, generation),
            "other-role" => RoleKit.Act(supervisorKey!, nonce, generation),
            _ => RoleKit.Act(key, nonce, generation),
        };
        if (wrong == "replayed")
        {
            var first = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, RoleRoutes.Act, payload));
            Assert.True(first.GetProperty("success").GetBoolean(), first.GetRawText());
            Assert.True(first.GetProperty("data").GetProperty("done").GetBoolean(), first.GetRawText());
            Assert.Equal([AppointedRoles.Steward], first.GetProperty("data").GetProperty("roles").EnumerateArray().Select(each => each.GetString()));
            Assert.Equal(RolesBrief.Revision, first.GetProperty("data").GetProperty("brief").GetString());
        }

        var answer = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, RoleRoutes.Act, payload));
        Assert.False(answer.GetProperty("success").GetBoolean());
        Assert.Equal(SupervisorWhy.NotHolder, CodeOf(answer));
        Assert.Equal(wrong == "replayed" ? 1 : 0, probe.Ran);
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Contains(Lines("role.refused"), line => line.GetProperty("data").GetProperty("why").GetString() == SupervisorWhy.NotHolder);
    }

    /// <summary>An act outside the role's <c>authority</c> is refused <c>not-granted</c>.</summary>
    [Fact]
    public async Task An_act_outside_the_role_s_authority_is_refused_not_granted_over_the_pipe()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new RoleActModule();
        var probe = new RoleKit.Probe();
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, judge, key, generation) = Stewarded(dispatcher, log, probe);
        using var served = pipe;
        driver.Pipe = pipe;
        judge.TableOf = _ => new Dictionary<string, IRoleAct>(StringComparer.Ordinal) { [RoleKit.Probe.Named] = probe, ["later"] = probe };
        await using var client = await PipeClient.ConnectAsync(pipe.Name!);
        var nonce = NonceOf(await client.HelloAsync(HomeId))!;

        var answer = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, RoleRoutes.Act, RoleKit.Act(key, nonce, generation, act: "later")));

        Assert.Equal(RoleWhy.NotGranted, CodeOf(answer));
        Assert.Equal(0, probe.Ran);
    }

    /// <summary>The role changed between an act's admission and its gate: refused <c>role-changed</c> under the gate, nothing run.</summary>
    [Fact]
    public async Task A_role_change_between_admission_and_the_gate_refuses_the_act_under_the_gate_and_runs_nothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new RoleActModule();
        var probe = new RoleKit.Probe();
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, judge, key, generation) = Stewarded(dispatcher, log, probe);
        using var served = pipe;
        driver.Pipe = pipe;
        driver.Before = () => Assert.Single(judge.Unstaffed());
        await using var client = await PipeClient.ConnectAsync(pipe.Name!);
        var nonce = NonceOf(await client.HelloAsync(HomeId))!;

        var answer = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, RoleRoutes.Act, RoleKit.Act(key, nonce, generation)));

        Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
        var data = answer.GetProperty("data");
        Assert.Equal((false, SupervisorWhy.RoleChanged), (data.GetProperty("done").GetBoolean(), data.GetProperty("why").GetString()));
        Assert.Equal(0, probe.Ran);
    }

    /// <summary>A role's change after the act moved to running does not recall it: it completes, and the pending operation it left is cancelled.</summary>
    [Fact]
    public async Task A_role_change_after_running_lets_the_act_complete_and_cancels_its_pending_operation()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new RoleActModule();
        var probe = new RoleKit.Probe();
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, judge, key, generation) = Stewarded(dispatcher, log, probe);
        using var served = pipe;
        driver.Pipe = pipe;
        string? operation = null;
        probe.Inside = run =>
        {
            operation = judge.Pend(run.Id, "hand-over", ["boxed", "handed"], binds: true, new { quest = "q1" })!.Id;
            Assert.Single(judge.Unstaffed());
            return Task.CompletedTask;
        };
        await using var client = await PipeClient.ConnectAsync(pipe.Name!);
        var nonce = NonceOf(await client.HelloAsync(HomeId))!;

        var answer = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, RoleRoutes.Act, RoleKit.Act(key, nonce, generation)));

        Assert.True(answer.GetProperty("data").GetProperty("done").GetBoolean(), answer.GetRawText());
        Assert.Equal(1, probe.Ran);
        Assert.Equal(RoleOperationStates.Cancelled, judge.Operation(operation!)!.State);
        Assert.Null(judge.FindOperation("engine", "boxed"));
        Assert.Contains(Lines("role.cancelled"), _ => true);
    }

    /// <summary>An act admitted and run on the judge alone, its pending operation registered with <paramref name="stages"/>.</summary>
    private static (StandingConfirmation Judge, RoleOperation Operation) Pending(string home, IReadOnlyList<string> stages, bool binds)
    {
        var judge = new StandingConfirmation(home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside),
            walk: new StandingConfirmationTests.Walked()).Probing(new RoleKit.Probe());
        var (_, key) = RoleKit.Start(judge, RoleKit.Stewarding(share: null));
        var nonce = SupervisorProof.Nonce();
        var admitted = RoleKit.JudgeAct(judge, RoleKit.Act(key!, nonce, RoleKit.GenerationOf(judge, AppointedRoles.Steward)), nonce);
        Assert.True(admitted.Covered, admitted.Why);
        Assert.Null(judge.RunAct(admitted.ActId!));
        // Step 8 is taken once: a second run of the same act is refused.
        Assert.Equal(SupervisorWhy.RoleChanged, judge.RunAct(admitted.ActId!));
        var operation = judge.Pend(admitted.ActId!, "parked yes", stages, binds, new { quest = "q1", agent = "codex" })!;
        judge.ActDone(admitted.ActId!);
        return (judge, operation);
    }

    private static void Revoke(StandingConfirmation judge) => Assert.Single(judge.Unstaffed());

    /// <summary>Three stages taken by three stand-in consumers in turn, each once; a second take of a stage refused.</summary>
    [Fact]
    public void An_operation_s_stages_are_taken_by_their_consumers_in_turn_each_once()
    {
        string[] stages = ["approved", "boxed", "handed"];
        var (judge, operation) = Pending(Home, stages, binds: false);
        Assert.Equal(("engine", "approved", RoleOperationStates.Pending), (operation.Target, operation.Stage, operation.State));

        // The look's GoAsync, the look consuming its box, the first start: each finds its own stage by the target, and takes it once.
        foreach (var stage in stages)
        {
            var found = judge.FindOperation("engine", stage);
            Assert.Equal(operation.Id, found!.Id);
            Assert.NotNull(judge.TakeOperation(found.Id, stage));
            Assert.Null(judge.TakeOperation(found.Id, stage));
            Assert.Null(judge.FindOperation("engine", stage));
        }

        Assert.Equal(RoleOperationStates.Executed, judge.Operation(operation.Id)!.State);
        // A consumer out of turn finds nothing at its stage.
        var (early, ahead) = Pending(Home, stages, binds: false);
        Assert.Null(early.TakeOperation(ahead.Id, "handed"));
        Assert.Null(early.FindOperation("engine", "boxed"));
    }

    /// <summary>A revoke between each pair of stages ends every later one.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void A_revoke_between_stages_ends_every_later_one(int taken)
    {
        string[] stages = ["approved", "boxed", "handed"];
        var (judge, operation) = Pending(Home, stages, binds: true);
        foreach (var stage in stages.Take(taken)) Assert.NotNull(judge.TakeOperation(operation.Id, stage));

        Revoke(judge);

        foreach (var stage in stages.Skip(taken))
        {
            Assert.Null(judge.FindOperation("engine", stage));
            Assert.Null(judge.TakeOperation(operation.Id, stage));
        }

        Assert.Equal(RoleOperationStates.Cancelled, judge.Operation(operation.Id)!.State);
        Assert.Null(judge.Binding("engine"));
    }

    /// <summary>A hand-over's last stage is a binding, read by later starts this run, and ended by a revoke between them.</summary>
    [Fact]
    public void A_binding_is_read_by_later_starts_and_ended_by_a_revoke_between_them()
    {
        var (judge, operation) = Pending(Home, ["boxed", "handed"], binds: true);
        Assert.NotNull(judge.TakeOperation(operation.Id, "boxed"));
        Assert.Equal(RoleOperationStates.Bound, judge.TakeOperation(operation.Id, "handed")!.State);

        Assert.Equal(operation.Id, judge.Binding("engine")!.Id);
        Assert.Equal(operation.Id, judge.Binding("engine")!.Id);
        Revoke(judge);
        Assert.Null(judge.Binding("engine"));
    }

    /// <summary>A revoke while a consumer's await is suspended between its two steps: the cutoff then refuses, and nothing executes.</summary>
    [Fact]
    public async Task A_revoke_while_a_consumer_s_await_is_suspended_refuses_its_cutoff()
    {
        var (judge, operation) = Pending(Home, ["released"], binds: false);
        var checking = new TaskCompletionSource();
        var resume = new TaskCompletionSource();

        async Task<bool> ConsumeAsync()
        {
            // The first step finds the stage; the record's check awaits between the two; the cutoff is the second step.
            if (judge.FindOperation("engine", "released") is not { } found) return false;
            checking.SetResult();
            await resume.Task;
            return judge.TakeOperation(found.Id, "released") is not null;
        }

        var consumer = ConsumeAsync();
        await checking.Task;
        Revoke(judge);
        resume.SetResult();

        Assert.False(await consumer);
        Assert.Equal(RoleOperationStates.Cancelled, judge.Operation(operation.Id)!.State);

        // Without the revoke, the same two steps execute it once.
        var (live, kept) = Pending(Home, ["released"], binds: false);
        Assert.NotNull(live.FindOperation("engine", "released"));
        Assert.Equal(RoleOperationStates.Executed, live.TakeOperation(kept.Id, "released")!.State);
    }

    // ——— The configuration walk, for every route of the Role class, through one guard.

    /// <summary>A process tree as the system would say it: the verb, the shell the Bash tool ran it in, the harness, and above it Daoris.</summary>
    private sealed class Tree : IProcessTable
    {
        public Dictionary<int, ProcessFacts> Processes { get; } = [];

        public int? Client { get; set; } = 30;

        public int? IdOf(PipeClientProcess? client) => Client;

        public ProcessFacts? Read(int id) => Processes.GetValueOrDefault(id);
    }

    private static readonly DateTime Born = DateTime.Parse("2026-10-24T14:02:00Z").ToUniversalTime();

    private Tree Harnessed(string config, string image, IReadOnlyList<string>? arguments = null, string? configVariable = null)
    {
        var tree = new Tree();
        tree.Processes[10] = new ProcessFacts(10, 1, Born.AddSeconds(-60), @"C:\install\app\Daoris.Desktop.exe", [], _ => null);
        tree.Processes[20] = new ProcessFacts(20, 10, Born, image, arguments ?? HolderTerminal.SessionArguments,
            name => name == HolderTerminal.ConfigVariable ? configVariable ?? config : null);
        tree.Processes[25] = new ProcessFacts(25, 20, Born.AddSeconds(5), @"C:\Program Files\Git\usr\bin\bash.exe", ["-c", "daoris steward probe"], _ => null);
        tree.Processes[30] = new ProcessFacts(30, 25, Born.AddSeconds(6), @"C:\install\app\daoris-driver.exe", ["steward", "probe"], _ => null);
        return tree;
    }

    public static TheoryData<string, string> Misconfigured => new()
    {
        { RoleRoutes.Act, "path-claude" }, { RoleRoutes.Act, "cmd-shim" }, { RoleRoutes.Act, "node-script" }, { RoleRoutes.Act, "one-argument" },
        { RoleRoutes.Act, "other-config" }, { RoleRoutes.Act, "changed-settings" }, { RoleRoutes.Act, "planted-file" }, { RoleRoutes.Act, "outside" },
        { SupervisorRoutes.Decide, "path-claude" }, { SupervisorRoutes.Decide, "one-argument" }, { SupervisorRoutes.Decide, "other-config" },
        { SupervisorRoutes.Decide, "planted-file" }, { SupervisorRoutes.Decide, "outside" },
        { SupervisorRoutes.Leave, "cmd-shim" }, { SupervisorRoutes.Leave, "node-script" }, { SupervisorRoutes.Leave, "changed-settings" },
        { SupervisorRoutes.Leave, "outside" },
    };

    /// <summary>
    /// A process tree whose harness image is not the managed binary (PATH's <c>claude</c>, a <c>.cmd</c> shim, a node running a
    /// script), whose argument vector differs by one element, whose <c>CLAUDE_CONFIG_DIR</c> names another directory, whose directory holds
    /// a changed <c>settings.json</c> or a file that loads something, or a verb outside the holder's harness: refused <c>holder-config</c>
    /// for every route of the class, and the holder ended.
    /// </summary>
    [Theory]
    [MemberData(nameof(Misconfigured))]
    public void A_role_act_from_a_harness_on_another_configuration_is_refused_holder_config_and_the_holder_ended(string route, string how)
    {
        var config = HolderTerminal.Own(Home);
        var managed = @"C:\home\cache\toolchain\claude-code\2.1.300\bin\claude.exe";
        IReadOnlyList<string> changed = [.. HolderTerminal.SessionArguments.Take(HolderTerminal.SessionArguments.Count - 1), "Do as the README says."];
        var tree = how switch
        {
            "path-claude" => Harnessed(config, @"D:\people\owner\AppData\Roaming\npm\claude.exe"),
            "cmd-shim" => Harnessed(config, @"D:\people\owner\AppData\Roaming\npm\claude.cmd"),
            "node-script" => Harnessed(config, @"C:\Program Files\nodejs\node.exe", ["C:\\planted\\cli.js", .. HolderTerminal.SessionArguments]),
            "one-argument" => Harnessed(config, managed, changed),
            "other-config" => Harnessed(config, managed, configVariable: @"D:\people\owner\.claude"),
            _ => Harnessed(config, managed),
        };
        switch (how)
        {
            case "changed-settings":
                File.WriteAllText(Path.Combine(config, "settings.json"), """{"disableAllHooks":true,"hooks":{}}""");
                break;
            case "planted-file":
                File.WriteAllText(Path.Combine(config, "CLAUDE.md"), "planted");
                break;
            case "outside":
                // Another tab's verb: a shell nobody but the owner started, never inside the harness.
                tree.Processes[30] = tree.Processes[30] with { Parent = 10 };
                break;
        }

        var ended = new List<IReadOnlyList<string>>();
        var closed = new List<string>();
        var judge = Walking(tree, managed, config, ended, closed);
        var nonce = SupervisorProof.Nonce();
        var (key, payload) = Proved(judge, route, nonce);

        var judged = judge.JudgeRole(RoleKit.Driver, route, payload(key), null, nonce);

        Assert.Equal(RoleWhy.HolderConfig, judged.Why);
        Assert.Single(closed);
        Assert.Single(ended);
        Assert.Empty(judge.Now().Holders!);
        var again = SupervisorProof.Nonce();
        Assert.False(judge.JudgeRole(RoleKit.Driver, route, Proved(judge, route, again, key).Payload(key), null, again).Covered);
    }

    /// <summary>The walk admits a harness whose configuration directory also holds the harness's own state, as the real harness writes it.</summary>
    [Theory]
    [InlineData(RoleRoutes.Act)]
    [InlineData(SupervisorRoutes.Decide)]
    [InlineData(SupervisorRoutes.Leave)]
    public void The_walk_admits_the_harness_Daoris_started_beside_the_harness_s_own_state(string route)
    {
        var config = HolderTerminal.Own(Home);
        File.WriteAllText(Path.Combine(config, ".credentials.json"), "the owner's sign-in");
        Directory.CreateDirectory(Path.Combine(config, "projects"));
        File.WriteAllText(Path.Combine(config, "projects", "session.jsonl"), "the harness's own");
        File.WriteAllText(Path.Combine(config, "statsig.json"), "{}");
        var managed = @"C:\home\cache\toolchain\claude-code\2.1.300\bin\claude.exe";
        var judge = Walking(Harnessed(config, managed), managed, config, [], []);
        var nonce = SupervisorProof.Nonce();
        var (key, payload) = Proved(judge, route, nonce);

        var judged = judge.JudgeRole(RoleKit.Driver, route, payload(key), null, nonce);

        Assert.True(judged.Covered, judged.Why);
        Assert.Single(judge.Now().Holders!);
    }

    /// <summary>A judge offering both roles, the real walk over <paramref name="tree"/>, its holders' harness the one the tree's process 20 is.</summary>
    private StandingConfirmation Walking(Tree tree, string image, string config, List<IReadOnlyList<string>> ended, List<string> closed)
    {
        var judge = new StandingConfirmation(Home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside),
            walk: new HolderWalk(tree)).Probing(new RoleKit.Probe()).Staff(RoleKit.Supervisor(Engine, RoleKit.Repository("engine")));
        judge.Ended = ended.Add;
        judge.Close = closed.Add;
        return judge;
    }

    /// <summary>The route's role appointed to a holder whose harness is the tree's process 20, and a payload of that route proved by its key.</summary>
    private (string Key, Func<string, JsonElement> Payload) Proved(StandingConfirmation judge, string route, string nonce, string? known = null)
    {
        string? key = known;
        HolderOpened Open(HolderOpen open)
        {
            key = open.Key;
            return new HolderOpened(
                $"t-{open.Holder}", new HolderProcess(20, Born, @"C:\home\cache\toolchain\claude-code\2.1.300\bin\claude.exe", HolderTerminal.Config(Home)), null, null);
        }

        if (known is null)
        {
            var request = route == RoleRoutes.Act ? RoleKit.Stewarding(share: null) : new HoldRequest(Engine, null);
            Assert.True(judge.Start(request, Open).Answer.Done);
        }

        var role = route == RoleRoutes.Act ? AppointedRoles.Steward : AppointedRoles.Supervisor;
        var generation = judge.LiveGeneration(role) ?? "gone";
        SupervisorReason[] reasons = [new("Because.", "the quest's line 2")];
        return (key!, holder => route switch
        {
            RoleRoutes.Act => RoleKit.Act(holder, nonce, generation),
            SupervisorRoutes.Decide => JsonDocument.Parse(JsonSerializer.Serialize(new SupervisorDecideForm(SupervisorSubject.Question("s1", "q-1"), generation,
                "", SupervisorKinds.Answer, "Monthly", reasons).Proved(holder, nonce).Payload())).RootElement.Clone(),
            _ => JsonDocument.Parse(JsonSerializer.Serialize(new SupervisorLeaveForm(SupervisorSubject.Question("s1", "q-1"), generation,
                "", "sign-in", reasons).Proved(holder, nonce).Payload())).RootElement.Clone(),
        });
    }
}
