using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The steward's standing over the real pipe (STEWARDROLE1d; D209 point 2, D201's four presses as the steward's acts): a press its
/// proved holder makes within the acts the person granted runs with no card and is logged; one outside them asks the person as its
/// route's own press; an act the person takes back asks again at the next press; and a process that does not prove the holder's key
/// is refused, nothing asked.
/// </summary>
/// <remarks>
/// Each test serves its own pipe in this process, named by its own scratch home's id; where the terminal's process stands and the
/// holder's configuration walk are stand-ins, as <c>PipeStandingTests</c>' are. Windows only.
/// </remarks>
public sealed class StewardStandingTests : Bridge
{
    /// <summary>A driver module whose <c>ROLE_ACT</c> is the door's own handler and whose every other route says it ran, and under which door.</summary>
    private sealed class StewardModule : ModuleBase
    {
        public InstancePipe? Pipe { get; set; }

        public RoleActTests.World World { get; } = new();

        public List<(string Route, RoleJudgement? Role)> Ran { get; } = [];

        /// <summary>That every route but the role's answers as a refusal of its own, as a place's workflow choice it cannot keep.</summary>
        public bool Refuses { get; set; }

        public override string ModuleName => DriverModule.Name;

        protected override async Task<object?> RouteMessageAsync(IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
        {
            if (request.Type == RoleRoutes.Act) return await SupervisorDoor.ActAsync(Pipe, request.Payload, cancellationToken, world: World);
            lock (Ran) Ran.Add((request.Type!, PipeDoor.Role));
            return Refuses ? new { Done = false, Refused = "reach", Message = "Nothing could start there." } : new { Done = true };
        }
    }

    /// <summary>The steward's terminal started over a served pipe with the build's own table, the steward holding <paramref name="acts"/>.</summary>
    private (InstancePipe Pipe, StandingConfirmation Judge, string Key, string Generation) Appointed(
        IMessageDispatcher dispatcher, MachineLog log, params string[] acts)
    {
        var standing = new StandingConfirmation(
            Home, log, new StandingConfirmationTests.Placed(StandingPlace.Outside), walk: new StandingConfirmationTests.Walked()).Staff(RoleKit.Steward(acts));
        var pipe = new InstancePipe(dispatcher, Bus, Home, log, version: "9.9.9-test", standing: standing) { Grounds = new StubGrounds() };
        Assert.NotNull(pipe.Serve());
        var (change, key) = RoleKit.Start(standing, RoleKit.Stewarding(share: null));
        Assert.True(change.Answer.Done, change.Answer.Message);
        return (pipe, standing, key!, RoleKit.GenerationOf(standing, AppointedRoles.Steward));
    }

    private const string Workflow = """{"repository":"engine","workflow":"review"}""";

    [Fact]
    public async Task The_steward_s_workflow_choice_runs_its_route_under_the_role_s_door_with_no_card_and_is_the_steward_s()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new StewardModule();
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, judge, key, generation) = Appointed(dispatcher, log, StewardActNames.Workflow);
        using var served = pipe;
        driver.Pipe = pipe;

        await using var client = await PipeClient.ConnectAsync(pipe.Name!);
        var nonce = NonceOf(await client.HelloAsync(HomeId))!;
        var answer = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, RoleRoutes.Act,
            RoleKit.Act(key, nonce, generation, act: StewardActNames.Workflow, form: Workflow)));

        Assert.True(answer.GetProperty("data").GetProperty("done").GetBoolean(), answer.GetRawText());
        var ran = Assert.Single(driver.Ran);
        Assert.Equal("WORKFLOW_USE", ran.Route);
        Assert.Equal(StewardActNames.Workflow, ran.Role?.Act);
        Assert.False(Confirming(client));
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        var did = Assert.Single(judge.StewardActsNow());
        Assert.Equal((StewardActNames.Workflow, "engine"), (did.Act, did.Repository));
    }

    [Fact]
    public async Task A_workflow_choice_its_route_refuses_is_refused_and_not_the_steward_s()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new StewardModule { Refuses = true };
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, judge, key, generation) = Appointed(dispatcher, log, StewardActNames.Workflow);
        using var served = pipe;
        driver.Pipe = pipe;

        await using var client = await PipeClient.ConnectAsync(pipe.Name!);
        var nonce = NonceOf(await client.HelloAsync(HomeId))!;
        var answer = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, RoleRoutes.Act,
            RoleKit.Act(key, nonce, generation, act: StewardActNames.Workflow, form: Workflow)));

        Assert.False(answer.GetProperty("data").GetProperty("done").GetBoolean(), answer.GetRawText());
        Assert.Equal("reach", answer.GetProperty("data").GetProperty("why").GetString());
        Assert.Empty(judge.StewardActsNow());
    }

    private (IMessageDispatcher Dispatcher, ServiceProvider Services) Compose(MachineLog log, IIpcModule module)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(Bus);
        services.AddSingleton<IIpcRequestTracker>(new IpcRequestTracker(Bus, new IpcRequestTrackerOptions { GracePeriod = TimeSpan.FromMilliseconds(50) }));
        services.AddSingleton(module);
        services.UseMessageDispatcher((_, dispatcher) => dispatcher.Use(RefusalLog.Middleware(log)));
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IMessageDispatcher>(), provider);
    }

    /// <summary>The steward's terminal started over a served pipe, its table the probe, which asks the person as <c>SET_HOLD</c> outside its acts.</summary>
    private (InstancePipe Pipe, StandingConfirmation Judge, string Key, string Generation) Stewarded(
        IMessageDispatcher dispatcher, MachineLog log, RoleKit.Probe probe)
    {
        var standing = new StandingConfirmation(
            Home, log, new StandingConfirmationTests.Placed(StandingPlace.Outside), walk: new StandingConfirmationTests.Walked()).Probing(probe);
        var pipe = new InstancePipe(dispatcher, Bus, Home, log, version: "9.9.9-test", standing: standing) { Grounds = new StubGrounds() };
        Assert.NotNull(pipe.Serve());
        var (change, key) = RoleKit.Start(standing, RoleKit.Stewarding(share: null));
        Assert.True(change.Answer.Done, change.Answer.Message);
        return (pipe, standing, key!, RoleKit.GenerationOf(standing, AppointedRoles.Steward));
    }

    private const string Hold = """{"repository":"engine","held":true}""";

    private static RoleKit.Probe Probe() => new() { PersonRoute = "SET_HOLD", Form = ["repository", "held"] };

    private string HomeId => HomeIdentity.Read(Home)!;

    private static string? NonceOf(JsonElement hello) =>
        hello.GetProperty("data").TryGetProperty("nonce", out var nonce) ? nonce.GetString() : null;

    private static string? CodeOf(JsonElement answer) =>
        answer.TryGetProperty("error", out var error) ? error.GetProperty("code").GetString() : null;

    private static bool Confirming(PipeClient client) =>
        client.Told.SelectMany(line => line.GetProperty("payload").EnumerateArray())
            .Any(each => each.GetProperty("module").GetString() == InstancePipe.Module && each.GetProperty("type").GetString() == InstancePipe.Confirming);

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

    /// <summary>One act sent on a connection of its own, proved by <paramref name="key"/> over that connection's nonce; its id and the connection.</summary>
    private async Task<(PipeClient Client, string Id)> SendAsync(InstancePipe pipe, string key, string generation)
    {
        var client = await PipeClient.ConnectAsync(pipe.Name!);
        var nonce = NonceOf(await client.HelloAsync(HomeId))!;
        return (client, await client.SendAsync(DriverModule.Name, RoleRoutes.Act, RoleKit.Act(key, nonce, generation, form: Hold)));
    }

    [Fact]
    public async Task A_press_within_the_steward_s_acts_runs_with_no_card_and_is_logged()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new StewardModule();
        var probe = Probe();
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, _, key, generation) = Stewarded(dispatcher, log, probe);
        using var served = pipe;
        driver.Pipe = pipe;

        var (client, id) = await SendAsync(pipe, key, generation);
        await using var connected = client;
        var answer = await client.AnswerAsync(id);

        Assert.True(answer.GetProperty("data").GetProperty("done").GetBoolean(), answer.GetRawText());
        Assert.Equal(1, probe.Ran);
        Assert.False(Confirming(client));
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Empty(driver.Ran);
        Assert.Contains(Lines("role.act"), line => line.GetProperty("data").GetProperty("act").GetString() == RoleKit.Probe.Named);
    }

    [Fact]
    public async Task A_press_outside_its_acts_asks_the_person_as_its_route_s_press_and_runs_as_theirs_once_confirmed()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new StewardModule();
        var probe = Probe();
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, judge, key, generation) = Stewarded(dispatcher, log, probe);
        using var served = pipe;
        driver.Pipe = pipe;
        judge.Staff(RoleKit.Steward([]));

        var (client, id) = await SendAsync(pipe, key, generation);
        await using var connected = client;
        await Until(() => pipe.Confirmations.Now().Waiting.Count == 1, () => "the steward's press outside its acts was not asked");

        var shown = Assert.Single(pipe.Confirmations.Now().Waiting);
        Assert.Equal(("DAORIS.DRIVER", "SET_HOLD"), (shown.Module, shown.Route));
        Assert.Equal(
            [new PipeConfirmationField("/repository", "string", "engine"), new PipeConfirmationField("/held", "boolean", "true")], shown.Fields);
        Assert.Equal(0, probe.Ran);
        Assert.Empty(driver.Ran);
        Assert.Contains(Lines("role.asked"), line => line.GetProperty("data").GetProperty("act").GetString() == RoleKit.Probe.Named);

        // Confirmed, it is the person's press: its route runs once, never under the role's door.
        Assert.Equal(PipeAnswered.Answered, (await pipe.Confirmations.AnswerAsync(shown.Id, confirm: true)).Outcome);
        var answer = await client.AnswerAsync(id);
        Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
        Assert.True(Confirming(client));
        var ran = Assert.Single(driver.Ran);
        Assert.Equal(("SET_HOLD", (RoleJudgement?)null), ran);
        Assert.Equal(0, probe.Ran);
    }

    [Fact]
    public async Task An_act_the_person_takes_back_asks_again_at_the_next_press()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new StewardModule();
        var probe = Probe();
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, judge, key, generation) = Stewarded(dispatcher, log, probe);
        using var served = pipe;
        driver.Pipe = pipe;

        var (first, firstId) = await SendAsync(pipe, key, generation);
        await using (first)
        {
            Assert.True((await first.AnswerAsync(firstId)).GetProperty("data").GetProperty("done").GetBoolean());
        }

        // The person takes the act out of the steward's acts in the window: the hold lives on, its standing for that act gone at once.
        judge.Staff(RoleKit.Steward([]));
        Assert.Empty(judge.RolesChanged());

        var (next, nextId) = await SendAsync(pipe, key, generation);
        await using var connected = next;
        await Until(() => pipe.Confirmations.Now().Waiting.Count == 1, () => "the press after the take-back was not asked");
        Assert.Equal(1, probe.Ran);

        // The person declines it: nothing of the steward's runs, and the terminal hears it was asked.
        Assert.Equal(PipeAnswered.Answered, (await pipe.Confirmations.AnswerAsync(pipe.Confirmations.Now().Waiting[0].Id, confirm: false)).Outcome);
        Assert.False((await next.AnswerAsync(nextId)).GetProperty("success").GetBoolean());
        Assert.True(Confirming(next));
        Assert.Equal(1, probe.Ran);
        Assert.Empty(driver.Ran);
    }

    public static TheoryData<string[]> Verbs => [["drive", "engine"], ["undrive", "engine"], ["trees", "engine", "on"], ["hold", "engine"],
        ["resume", "engine"], ["stop", "s1a2b3c4"], ["finish", "s1a2b3c4"], ["retry", "#q-1"], ["workflow", "engine", "review"]];

    /// <summary>Each act the steward's verb makes asks outside its acts as a person's route that takes the verb's form as its own press.</summary>
    [Theory]
    [MemberData(nameof(Verbs))]
    public void Each_act_asks_as_a_person_s_route_in_the_form_its_verb_sends(string[] words)
    {
        var (act, form) = StewardCommand.Read(words)!.Value;
        var element = JsonSerializer.SerializeToElement(form);
        var row = StewardActs.Table[act];

        Assert.True(row.IsForm(element), element.GetRawText());
        Assert.NotNull(row.PersonRoute);
        Assert.Equal(PipeClass.Person, PipeCensus.Daoris.Of(DriverModule.Name, row.PersonRoute!, element));
    }

    public static TheoryData<bool> Granted => [true, false];

    [Theory]
    [MemberData(nameof(Granted))]
    public async Task A_process_that_does_not_prove_the_holder_s_key_is_refused_and_nothing_is_asked(bool granted)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new StewardModule();
        var probe = Probe();
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, judge, _, generation) = Stewarded(dispatcher, log, probe);
        using var served = pipe;
        driver.Pipe = pipe;
        if (!granted) judge.Staff(RoleKit.Steward([]));

        var (client, id) = await SendAsync(pipe, HolderKey.Draw(), generation);
        await using var connected = client;
        var answer = await client.AnswerAsync(id);

        Assert.Equal(SupervisorWhy.NotHolder, CodeOf(answer));
        Assert.False(Confirming(client));
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Equal(0, probe.Ran);
        Assert.Empty(driver.Ran);
    }
}
