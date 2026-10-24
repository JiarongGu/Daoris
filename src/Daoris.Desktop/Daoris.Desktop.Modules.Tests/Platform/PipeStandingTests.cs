using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// TERMTRUST1a part (ii) over the real pipe (D201 points 5 and 7, the terminal-trust design §5 and §8 case 5; <c>PipeConfirmTests</c>'
/// neighbour): a press the person's standing confirmation covers runs with no card, the terminal told <c>STANDING</c> and the log
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

    private (InstancePipe Pipe, StandingConfirmationTests.Placed Caller) Pipe(IMessageDispatcher dispatcher, MachineLog log, bool grant = true)
    {
        var caller = new StandingConfirmationTests.Placed(StandingPlace.Outside);
        var standing = new StandingConfirmation(Home, log, caller, offered: true);
        var pipe = new InstancePipe(dispatcher, Bus, Home, log, version: "9.9.9-test", standing: standing) { Grounds = new StubGrounds() };
        Assert.NotNull(pipe.Serve());
        if (grant) Assert.True(standing.Set(StandingPresses.Built).Done);
        return (pipe, caller);
    }

    private static IEnumerable<JsonElement> Each(IEnumerable<JsonElement> told) =>
        told.SelectMany(line => line.GetProperty("payload").EnumerateArray());

    private static bool Is(JsonElement each, string type) =>
        each.GetProperty("module").GetString() == InstancePipe.Module && each.GetProperty("type").GetString() == type;

    [Fact]
    public async Task A_covered_stop_runs_with_no_card_and_the_terminal_hears_it_ran_under_the_grant()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new HeldModule(DriverModule.Name);
        driver.Let.SetResult();
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, _) = Pipe(dispatcher, log);
        using var served = pipe;
        var used = new List<EventMessage>();
        using var heard = Bus.Subscribe(StatePublisher.Module, StandingConfirmation.UsedEvent, message =>
        {
            lock (used) used.Add(message);
            return Task.CompletedTask;
        });
        await using var client = await PipeClient.ConnectAsync(pipe.Name!);
        await client.HelloAsync(HomeId);

        var id = await client.SendAsync(DriverModule.Name, "STOP_SESSION", new { id = "s1a2b3c4", said = true });
        var answer = await client.AnswerAsync(id);

        Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
        Assert.Equal(1, driver.Ran);
        var told = Each(client.Told).ToList();
        Assert.DoesNotContain(told, each => Is(each, InstancePipe.Confirming));
        var standing = Assert.Single(told, each => Is(each, InstancePipe.Standing));
        Assert.Equal(id, standing.GetProperty("payload").GetProperty("id").GetString());
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Equal("answered", (await client.ResultAsync(id)).GetProperty("state").GetString());
        Assert.Equal(1, pipe.StandingConfirmation.Now().Used);
        await Until(() => { lock (used) return used.Count == 1; }, () => "no window was told the use");

        // The log says it by codes: the state, the route and the door, never the session or the asker.
        var lines = Lines(PipeConfirmations.LogEvent);
        var line = Assert.Single(lines);
        Assert.Equal(
            """{"state":"standing","route":"DAORIS.DRIVER.STOP_SESSION","door":"terminal"}""", line.GetProperty("data").GetRawText());
        Assert.DoesNotContain(lines, each => each.GetRawText().Contains("s1a2b3c4", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_revoke_after_the_admission_lets_the_admitted_press_run_and_the_next_one_asks()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new HeldModule(DriverModule.Name);
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, _) = Pipe(dispatcher, log);
        using var served = pipe;
        await using var client = await PipeClient.ConnectAsync(pipe.Name!);
        await client.HelloAsync(HomeId);

        var admitted = await client.SendAsync(DriverModule.Name, "SET_HOLD", new { repository = "engine", held = true });
        await driver.Started.Task.WaitAsync(Poll.Patience);
        Assert.True(pipe.StandingConfirmation.Set([]).Done);
        driver.Let.SetResult();

        Assert.True((await client.AnswerAsync(admitted)).GetProperty("success").GetBoolean());
        Assert.Equal(1, driver.Ran);

        var next = await client.SendAsync(DriverModule.Name, "SET_HOLD", new { repository = "engine", held = true });
        await Until(() => pipe.Confirmations.Now().Waiting.Count == 1, () => "the next press was not asked");
        Assert.Equal(1, driver.Ran);
        // Off, it says nothing of standing: the plain ask.
        var asked = Lines(PipeConfirmations.LogEvent).Last();
        Assert.Equal("asked", asked.GetProperty("data").GetProperty("state").GetString());
        Assert.False(asked.GetProperty("data").TryGetProperty("standing", out _));
        await pipe.Confirmations.AnswerAsync(pipe.Confirmations.Now().Waiting[0].Id, confirm: false);
        Assert.False((await client.AnswerAsync(next)).GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task A_press_asked_before_the_grant_keeps_its_card()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new HeldModule(DriverModule.Name);
        driver.Let.SetResult();
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, _) = Pipe(dispatcher, log, grant: false);
        using var served = pipe;
        await using var client = await PipeClient.ConnectAsync(pipe.Name!);
        await client.HelloAsync(HomeId);

        var id = await client.SendAsync(DriverModule.Name, "STOP_SESSION", new { id = "s1a2b3c4", said = true });
        await Until(() => pipe.Confirmations.Now().Waiting.Count == 1, () => "the press was not asked");
        Assert.True(pipe.StandingConfirmation.Set(StandingPresses.Built).Done);

        Assert.Single(pipe.Confirmations.Now().Waiting);
        Assert.Equal(0, driver.Ran);
        await pipe.Confirmations.AnswerAsync(pipe.Confirmations.Now().Waiting[0].Id, confirm: true);
        Assert.True((await client.AnswerAsync(id)).GetProperty("success").GetBoolean());
        Assert.Equal(0, pipe.StandingConfirmation.Now().Used);
    }

    [Fact]
    public async Task A_press_the_grant_names_that_asks_anyway_says_why_to_the_terminal_and_in_the_log()
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

        await client.SendAsync(DriverModule.Name, "STOP_SESSION", new { id = "s1a2b3c4", said = true });

        var told = await client.NotificationAsync(Poll.Patience);
        var waits = Assert.Single(told!.Value.GetProperty("payload").EnumerateArray());
        Assert.Equal(InstancePipe.Confirming, waits.GetProperty("type").GetString());
        Assert.Equal(StandingWhy.InAgentJob, waits.GetProperty("payload").GetProperty("standing").GetString());
        // Told before its grounds are read (CONFIRMWAIT1 §15.2 item 9): the card, and its line, come once they are.
        await Until(() => Lines(PipeConfirmations.LogEvent).Any(), () => "the press was never asked");
        var asked = Lines(PipeConfirmations.LogEvent).Single();
        Assert.Equal(
            """{"state":"asked","route":"DAORIS.DRIVER.STOP_SESSION","door":"terminal","standing":"in-agent-job"}""",
            asked.GetProperty("data").GetRawText());
        Assert.Equal(0, driver.Ran);
    }

    /// <summary>§4.1, §8 case 9: a terminal cannot grant it, nor ask for a card that would; refused before it is dispatched.</summary>
    [Fact]
    public async Task A_terminal_is_refused_the_grant_before_it_is_dispatched()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new HeldModule(DriverModule.Name);
        driver.Let.SetResult();
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, _) = Pipe(dispatcher, log, grant: false);
        using var served = pipe;
        await using var client = await PipeClient.ConnectAsync(pipe.Name!);
        await client.HelloAsync(HomeId);

        var answer = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, "SET_STANDING_CONFIRMATION", new { presses = new[] { "stop" } }));

        Assert.Equal(InstancePipe.WindowOnly, answer.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(0, driver.Ran);
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.False(pipe.StandingConfirmation.Now().On);
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

    // SUPERVISOR1a-ii over the real pipe (D208 point 2; the supervisor design §2.2 and §11 row 1a-ii): the hello draws a nonce for its
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
            return Task.FromResult<object?>(new { Kept = true, Standing = PipeDoor.Standing });
        }
    }

    private (InstancePipe Pipe, StandingConfirmation Judge, string Key, string Generation) Appointed(
        IMessageDispatcher dispatcher, MachineLog log, IHolderWalk? walk = null, bool steward = false)
    {
        var standing = new StandingConfirmation(
            Home, log, new StandingConfirmationTests.Placed(StandingPlace.Outside), offered: true, supervisor: true, steward: steward,
            walk: walk ?? new StandingConfirmationTests.Walked())
        {
            Drives = _ => true,
        };
        var pipe = new InstancePipe(dispatcher, Bus, Home, log, version: "9.9.9-test", standing: standing) { Grounds = new StubGrounds() };
        Assert.NotNull(pipe.Serve());
        string? handed = null;
        Assert.True(standing.Appoint(StandingConfirmationTests.Supervising("engine"), open =>
        {
            handed = open.Key;
            return StandingConfirmationTests.Opened(open);
        }).Answer.Done);
        return (pipe, standing, handed!, standing.Admit("engine").Generation!);
    }

    /// <summary>An answer proved by <paramref name="key"/> over <paramref name="nonce"/>, as the verb builds it.</summary>
    private static object Proved(string key, string? nonce, string generation, string choice = "Monthly")
    {
        SupervisorReason[] reasons = [new("The ticket says monthly.", "the quest's line 2")];
        var proof = nonce is null ? new string('0', 64) : SupervisorProof.Prove(key, nonce, SupervisorProof.Canonical("s1", "q-1", generation, choice, reasons))!;
        return new SupervisorForm("s1", "q-1", generation, proof, choice, reasons).Payload();
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

        // No press is granted: the role is judged on its own, never open.
        Assert.False(pipe.StandingConfirmation.Now().On);
        var id = await client.SendAsync(DriverModule.Name, SupervisorRoutes.Answer, Proved(key, nonce, generation));
        var answer = await client.AnswerAsync(id);

        Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
        Assert.False(answer.GetProperty("data").GetProperty("standing").GetBoolean());
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
            Assert.True((await client.AnswerAsync(await client.SendAsync(DriverModule.Name, SupervisorRoutes.Answer, payload))).GetProperty("success").GetBoolean());
        }

        var answer = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, SupervisorRoutes.Answer, payload));
        Assert.False(answer.GetProperty("success").GetBoolean());
        Assert.Equal(SupervisorWhy.NotHolder, CodeOf(answer));
        Assert.Equal(wrong == "replayed" ? 1 : 0, driver.Seen.Count);
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Contains(Lines("supervisor.refused"), line => line.GetProperty("data").GetProperty("why").GetString() == SupervisorWhy.NotHolder);
    }

    /// <summary>A caller in an agent's job is refused before its proof is read, and a role act in a build that does not offer it is refused.</summary>
    [Fact]
    public async Task A_contained_caller_and_a_dark_build_are_refused_before_any_proof()
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
        var refused = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, SupervisorRoutes.Answer, Proved(key, nonce, generation)));
        Assert.Equal(StandingWhy.InAgentJob, CodeOf(refused));
        Assert.Empty(driver.Seen);

        // This build's own judge: appointments are not offered, so nothing is covered whatever is sent.
        using var darkLog = new MachineLog(Home, "dark");
        var dark = new InstancePipe(dispatcher, Bus, Path.Combine(Home, "dark-home"), darkLog, version: "9.9.9-test");
        Directory.CreateDirectory(Path.Combine(Home, "dark-home"));
        if (dark.Serve() is { } name)
        {
            using var darkServed = dark;
            await using var darkClient = await PipeClient.ConnectAsync(name);
            var darkNonce = NonceOf(await darkClient.HelloAsync(HomeIdentity.Read(Path.Combine(Home, "dark-home"))!))!;
            var answer = await darkClient.AnswerAsync(await darkClient.SendAsync(DriverModule.Name, SupervisorRoutes.Answer, Proved(key, darkNonce, generation)));
            Assert.Equal(SupervisorWhy.NotOffered, CodeOf(answer));
            Assert.Empty(driver.Seen);
        }
    }

    // STEWARDROLE1a-i over the real pipe and the judge (D209 §3.3, §3.4, §5.1a, §6.1; the steward design §10 row 1a-i's PipeStandingTests):
    // ROLE_ACT judged as the supervisor's answer is, admitted once, checked again at its gate; the run's memory of pending operations by
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

    /// <summary>The steward appointed over a served pipe, its table holding the probe.</summary>
    private (InstancePipe Pipe, StandingConfirmation Judge, string Key, string Generation) Stewarded(
        IMessageDispatcher dispatcher, MachineLog log, RoleKit.Probe probe, IHolderWalk? walk = null)
    {
        var standing = new StandingConfirmation(
            Home, log, new StandingConfirmationTests.Placed(StandingPlace.Outside), offered: false, supervisor: true, steward: true,
            walk: walk ?? new StandingConfirmationTests.Walked()) { Drives = _ => true }.Probing(probe);
        var pipe = new InstancePipe(dispatcher, Bus, Home, log, version: "9.9.9-test", standing: standing) { Grounds = new StubGrounds() };
        Assert.NotNull(pipe.Serve());
        var (change, key) = RoleKit.Appoint(standing, RoleKit.Stewarding(share: null));
        Assert.True(change.Answer.Done, change.Answer.Message);
        return (pipe, standing, key!, RoleKit.GenerationOf(standing, Roles.Steward));
    }

    public static TheoryData<string> WrongActs => ["none", "key", "other-role", "replayed"];

    /// <summary>§3.4: a ROLE_ACT with no proof, a wrong key, a holder's key for a role it does not hold, or a replay, each refused, nothing run.</summary>
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

        // A holder that holds the supervisor alone: its key proves nothing for the steward's appointment.
        string? supervisorKey = null;
        if (wrong == "other-role")
        {
            Assert.True(judge.Appoint(StandingConfirmationTests.Supervising("engine"), open =>
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
            Assert.Equal([Roles.Steward], first.GetProperty("data").GetProperty("roles").EnumerateArray().Select(each => each.GetString()));
            Assert.Equal(RolesBrief.Revision, first.GetProperty("data").GetProperty("brief").GetString());
        }

        var answer = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, RoleRoutes.Act, payload));
        Assert.False(answer.GetProperty("success").GetBoolean());
        Assert.Equal(SupervisorWhy.NotHolder, CodeOf(answer));
        Assert.Equal(wrong == "replayed" ? 1 : 0, probe.Ran);
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Contains(Lines("role.refused"), line => line.GetProperty("data").GetProperty("why").GetString() == SupervisorWhy.NotHolder);
    }

    /// <summary>§3.4: an act outside the appointment's <c>acts</c> is refused <c>not-granted</c>; the steward's table is empty in this build.</summary>
    [Fact]
    public async Task An_act_outside_the_appointment_s_acts_is_refused_not_granted_over_the_pipe()
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

    /// <summary>§3.4 step 8: a revoke between an act's admission and its gate: refused <c>appointment-changed</c> under the gate, nothing run.</summary>
    [Fact]
    public async Task A_revoke_between_admission_and_the_gate_refuses_the_act_under_the_gate_and_runs_nothing()
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
        driver.Before = () => Assert.True(judge.Appoint(RoleKit.Stewarding(on: false), StandingConfirmationTests.Opened).Answer.Done);
        await using var client = await PipeClient.ConnectAsync(pipe.Name!);
        var nonce = NonceOf(await client.HelloAsync(HomeId))!;

        var answer = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, RoleRoutes.Act, RoleKit.Act(key, nonce, generation)));

        Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
        var data = answer.GetProperty("data");
        Assert.Equal((false, SupervisorWhy.AppointmentChanged), (data.GetProperty("done").GetBoolean(), data.GetProperty("why").GetString()));
        Assert.Equal(0, probe.Ran);
    }

    /// <summary>§3.4: a revoke after the act moved to running does not recall it: it completes, and the pending operation it left is cancelled.</summary>
    [Fact]
    public async Task A_revoke_after_running_lets_the_act_complete_and_cancels_its_pending_operation()
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
            Assert.True(judge.Appoint(RoleKit.Stewarding(on: false), StandingConfirmationTests.Opened).Answer.Done);
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
        var judge = new StandingConfirmation(home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside), steward: true,
            walk: new StandingConfirmationTests.Walked()).Probing(new RoleKit.Probe());
        judge.Load();
        var (_, key) = RoleKit.Appoint(judge, RoleKit.Stewarding(share: null));
        var nonce = SupervisorProof.Nonce();
        var admitted = RoleKit.JudgeAct(judge, RoleKit.Act(key!, nonce, RoleKit.GenerationOf(judge, Roles.Steward)), nonce);
        Assert.True(admitted.Covered, admitted.Why);
        Assert.Null(judge.RunAct(admitted.ActId!));
        // Step 8 is taken once: a second run of the same act is refused.
        Assert.Equal(SupervisorWhy.AppointmentChanged, judge.RunAct(admitted.ActId!));
        var operation = judge.Pend(admitted.ActId!, "parked yes", stages, binds, new { quest = "q1", agent = "codex" })!;
        judge.ActDone(admitted.ActId!);
        return (judge, operation);
    }

    private static void Revoke(StandingConfirmation judge) =>
        Assert.True(judge.Appoint(RoleKit.Stewarding(on: false), StandingConfirmationTests.Opened).Answer.Done);

    /// <summary>§5.1a, finding 4: three stages taken by three stand-in consumers in turn, each once; a second take of a stage refused.</summary>
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

    /// <summary>§5.1a, finding 4: a revoke between each pair of stages ends every later one.</summary>
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

    /// <summary>§5.1a: a hand-over's last stage is a binding, read by later starts this run, and ended by a revoke between them.</summary>
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

    /// <summary>§5.1a, finding 5: a revoke while a consumer's await is suspended between its two steps: the cutoff then refuses, and nothing executes.</summary>
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

    // ——— The configuration walk (§6.1, finding 13), for every route of the Role class, through one guard.

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
        tree.Processes[25] = new ProcessFacts(25, 20, Born.AddSeconds(5), @"C:\Program Files\Git\usr\bin\bash.exe", ["-c", "daoris-driver steward probe"], _ => null);
        tree.Processes[30] = new ProcessFacts(30, 25, Born.AddSeconds(6), @"C:\install\app\daoris-driver.exe", ["steward", "probe"], _ => null);
        return tree;
    }

    public static TheoryData<string, string> Misconfigured => new()
    {
        { RoleRoutes.Act, "path-claude" }, { RoleRoutes.Act, "cmd-shim" }, { RoleRoutes.Act, "node-script" }, { RoleRoutes.Act, "one-argument" },
        { RoleRoutes.Act, "other-config" }, { RoleRoutes.Act, "changed-settings" }, { RoleRoutes.Act, "planted-file" }, { RoleRoutes.Act, "outside" },
        { SupervisorRoutes.Answer, "path-claude" }, { SupervisorRoutes.Answer, "one-argument" }, { SupervisorRoutes.Answer, "other-config" },
        { SupervisorRoutes.Answer, "planted-file" }, { SupervisorRoutes.Answer, "outside" },
        { SupervisorRoutes.Defer, "cmd-shim" }, { SupervisorRoutes.Defer, "node-script" }, { SupervisorRoutes.Defer, "changed-settings" },
        { SupervisorRoutes.Defer, "outside" },
    };

    /// <summary>
    /// §6.1: a process tree whose harness image is not the managed binary (PATH's <c>claude</c>, a <c>.cmd</c> shim, a node running a
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

    /// <summary>§6.1: the walk admits a harness whose configuration directory also holds the harness's own state, as the real harness writes it.</summary>
    [Theory]
    [InlineData(RoleRoutes.Act)]
    [InlineData(SupervisorRoutes.Answer)]
    [InlineData(SupervisorRoutes.Defer)]
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
        var judge = new StandingConfirmation(Home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside), supervisor: true, steward: true,
            walk: new HolderWalk(tree)) { Drives = _ => true }.Probing(new RoleKit.Probe());
        judge.Load();
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
            var request = route == RoleRoutes.Act ? RoleKit.Stewarding(share: null) : StandingConfirmationTests.Supervising("engine", share: null);
            Assert.True(judge.Appoint(request, Open).Answer.Done);
        }

        var role = route == RoleRoutes.Act ? Roles.Steward : Roles.Supervisor;
        var generation = judge.Now().Appointments!.Any(each => each.Role == role && !each.Dormant) ? RoleKit.GenerationOf(judge, role) : "gone";
        SupervisorReason[] reasons = [new("Because.", "the quest's line 2")];
        return (key!, holder => route switch
        {
            RoleRoutes.Act => RoleKit.Act(holder, nonce, generation),
            SupervisorRoutes.Answer => JsonDocument.Parse(JsonSerializer.Serialize(new SupervisorForm("s1", "q-1", generation,
                SupervisorProof.Prove(holder, nonce, SupervisorProof.Canonical("s1", "q-1", generation, "Monthly", reasons))!, "Monthly", reasons).Payload())).RootElement.Clone(),
            _ => JsonDocument.Parse(JsonSerializer.Serialize(new SupervisorDeferForm("s1", "q-1", generation,
                SupervisorProof.Prove(holder, nonce, SupervisorProof.Canonical(SupervisorRoutes.Defer, "s1", "q-1", generation, "sign-in", reasons))!, "sign-in", reasons).Payload())).RootElement.Clone(),
        });
    }
}
