using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The steward's standing over the real pipe: a press its
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

        /// <summary>What the owner's marks read the quest and session scopes by; none reads nothing, as the acts of a repository need.</summary>
        public IOwnerScopes? Scopes { get; set; }

        /// <summary>The plugin the landing's plan names as the one that pushes the branch, or none.</summary>
        public string? Pushing { get; set; }

        /// <summary>What the hand-over's judge answers: kept, or refused with its code and its words.</summary>
        public (string Why, string Message)? HandOverRefused { get; set; }

        public override string ModuleName => DriverModule.Name;

        protected override async Task<object?> RouteMessageAsync(IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
        {
            if (request.Type == RoleRoutes.Act) return await SupervisorDoor.ActAsync(Pipe, request.Payload, cancellationToken, Scopes, World);
            lock (Ran) Ran.Add((request.Type!, PipeDoor.Role));
            if (request.Type == "LANDING") return new { Session = "s1", Form = "branch", Plugin = Pushing };
            if (request.Type == "SESSION_HAND_OVER")
            {
                return HandOverRefused is { } refused
                    ? new { SessionId = (string?)null, Kept = false, refused.Why, refused.Message }
                    : new { SessionId = (string?)null, Kept = true, Why = (string?)null, Message = "it is stopped at the driver's next look, then goes on in the same tree with codex." };
            }

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

    private const string Quest = """{"quest":"q1"}""";
    private const string Tree = """{"id":"s1"}""";

    /// <summary>Each route act: its name, its form, the person's route it asks as and runs, and the world's move where it is no route of the pipe's.</summary>
    public static TheoryData<string, string, string, string?> QuestAndTreeActs => new()
    {
        { StewardActNames.Pause, Quest, "WORK_PAUSE", "pause q1" },
        { StewardActNames.Unpause, Quest, "WORK_RESUME", "unpause q1" },
        { StewardActNames.Done, Quest, "QUEST_DONE", null },
        { StewardActNames.Delete, Quest, "QUEST_DELETE", null },
        { StewardActNames.Land, Tree, "LAND_SESSION_TREE", null },
        { StewardActNames.Clean, Tree, "DISCARD_SESSION_TREE", null },
        { StewardActNames.HandOver, HandOver, "SESSION_HAND_OVER", null },
    };

    private const string HandOver = """{"id":"s1","agent":"codex"}""";

    [Fact]
    public async Task A_hand_over_its_route_refuses_is_refused_with_the_route_s_words_and_is_not_the_steward_s()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new StewardModule { HandOverRefused = ("unknown-agent", "no agent here is called codex.") };
        Seed(driver);
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, judge, key, generation) = Appointed(dispatcher, log, StewardActNames.HandOver);
        using var served = pipe;
        driver.Pipe = pipe;

        var (_, client, id) = await ActAsync(pipe, key, generation, StewardActNames.HandOver, HandOver);
        await using var connected = client;
        var data = (await client.AnswerAsync(id)).GetProperty("data");

        Assert.False(data.GetProperty("done").GetBoolean(), data.GetRawText());
        Assert.Equal(("unknown-agent", "no agent here is called codex."), (data.GetProperty("why").GetString(), data.GetProperty("message").GetString()));
        Assert.Empty(judge.StewardActsNow());
        Assert.Empty(pipe.Confirmations.Now().Waiting);
    }

    /// <summary>Outside the acts the person granted, a hand-over its route's judge refuses is refused with that judge's words, and no card is raised.</summary>
    [Fact]
    public async Task A_hand_over_not_granted_that_its_route_refuses_is_refused_before_any_card()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new StewardModule();
        Seed(driver);
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, judge, key, generation) = Appointed(dispatcher, log, StewardActNames.Hold);
        using var served = pipe;
        driver.Pipe = pipe;
        ((StubGrounds)pipe.Grounds!).Held["hand-over"] = "no agent here is called codex.";

        var (_, client, id) = await ActAsync(pipe, key, generation, StewardActNames.HandOver, HandOver);
        await using var connected = client;
        var answer = await client.AnswerAsync(id);

        Assert.Equal(InstancePipe.NotAllowed, CodeOf(answer));
        Assert.Contains("no agent here is called codex.", answer.GetProperty("error").GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Empty(driver.Ran);
        Assert.Empty(judge.StewardActsNow());
    }

    /// <summary>
    /// A hand-over is itself a release of its quest's hold, so one of a quest held by what only the owner releases is refused
    /// <c>owner-held</c> with nothing sent: the owner's stop, an outage past its tries, the strikes, a pause.
    /// </summary>
    [Theory]
    [InlineData("stopped")]
    [InlineData("transient")]
    [InlineData("held-transient")]
    [InlineData("exhausted")]
    [InlineData("paused")]
    public async Task A_hand_over_of_a_quest_held_for_the_owner_is_refused_owner_held(string held)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new StewardModule();
        Seed(driver);
        var record = held switch
        {
            "stopped" => new PriorSession("s1", driver.World.TreeOf("s1"), "stopped") { Quest = "q1", Repository = "engine" },
            "transient" => new PriorSession("s1", driver.World.TreeOf("s1"), "failed")
            {
                Quest = "q1", Repository = "engine", NoteParts = Noted.Of(EndNoteCodes.EndedTransient, "It failed.", ("count", 5), ("minutes", 31)).Parts,
            },
            _ => new PriorSession("s1", driver.World.TreeOf("s1"), "failed") { Quest = "q1", Repository = "engine" },
        };
        driver.World.Records["s1"] = record;
        var quest = new QuestView("q1", "Asker", "engine", "Ask q1", "Why.", "Taken");
        driver.World.Verdicts = held switch
        {
            "held-transient" => [new Consideration(quest, StartVerdict.TransientHeld, "held") { HeldBy = record }],
            "exhausted" => [new Consideration(quest, StartVerdict.Exhausted, "parked") { Failures = 3 }],
            "paused" => [new Consideration(quest, StartVerdict.Paused, "paused") { PausedBy = new PausedBy(WorkScope.Quest, "q1") }],
            _ => [],
        };
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, judge, key, generation) = Appointed(dispatcher, log, StewardActNames.HandOver);
        using var served = pipe;
        driver.Pipe = pipe;

        var (_, client, id) = await ActAsync(pipe, key, generation, StewardActNames.HandOver, HandOver);
        await using var connected = client;
        var data = (await client.AnswerAsync(id)).GetProperty("data");

        Assert.Equal((false, StewardWhy.OwnerHeld), (data.GetProperty("done").GetBoolean(), data.GetProperty("why").GetString()));
        Assert.Empty(driver.Ran);
        Assert.Empty(judge.StewardActsNow());
    }

    /// <summary>The steward's own stop of this run is its own to carry on, so a hand-over of it runs.</summary>
    [Fact]
    public async Task A_hand_over_of_the_steward_s_own_stop_runs()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new StewardModule();
        Seed(driver);
        driver.World.Records["s1"] = new PriorSession("s1", driver.World.TreeOf("s1"), "awaiting-person") { Quest = "q1", Repository = "engine" };
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, judge, key, generation) = Appointed(dispatcher, log, StewardActNames.Stop, StewardActNames.HandOver);
        using var served = pipe;
        driver.Pipe = pipe;
        var (_, stop, stopId) = await ActAsync(pipe, key, generation, StewardActNames.Stop, """{"id":"s1","said":true}""");
        await using (stop) Assert.True((await stop.AnswerAsync(stopId)).GetProperty("data").GetProperty("done").GetBoolean());
        var stopped = new PriorSession("s1", driver.World.TreeOf("s1"), "stopped")
        {
            Quest = "q1", Repository = "engine", NoteParts = SessionMoves.ByTheStewardNoted("stopped").Parts,
        };
        driver.World.Records["s1"] = stopped;
        driver.World.Verdicts = [new Consideration(new QuestView("q1", "Asker", "engine", "Ask q1", "Why.", "Taken"), StartVerdict.Stopped, "stopped") { HeldBy = stopped }];

        var (_, client, id) = await ActAsync(pipe, key, generation, StewardActNames.HandOver, HandOver);
        await using var connected = client;
        var data = (await client.AnswerAsync(id)).GetProperty("data");

        Assert.True(data.GetProperty("done").GetBoolean(), data.GetRawText());
        Assert.Contains(("SESSION_HAND_OVER", StewardActNames.HandOver), driver.Ran.Select(each => (each.Route, each.Role?.Act)));
    }

    /// <summary>Quest <c>q1</c> and its session <c>s1</c> in a tree of the home's own, as the scopes stand-in names them.</summary>
    private void Seed(StewardModule driver)
    {
        driver.World.Root = Home;
        driver.Scopes = new OwnerSetTests.Scopes();
        driver.World.Records["s1"] = new PriorSession("s1", driver.World.TreeOf("s1"), "completed") { Quest = "q1", Repository = "engine" };
    }

    private async Task<(JsonElement Answer, PipeClient Client, string Id)> ActAsync(
        InstancePipe pipe, string key, string generation, string act, string form)
    {
        var client = await PipeClient.ConnectAsync(pipe.Name!);
        var nonce = NonceOf(await client.HelloAsync(HomeId))!;
        var id = await client.SendAsync(DriverModule.Name, RoleRoutes.Act, RoleKit.Act(key, nonce, generation, act: act, form: form));
        return (default, client, id);
    }

    [Theory]
    [MemberData(nameof(QuestAndTreeActs))]
    public async Task A_quest_or_tree_act_the_person_granted_runs_with_no_card_and_is_the_steward_s(string act, string form, string route, string? moved)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new StewardModule();
        Seed(driver);
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, judge, key, generation) = Appointed(dispatcher, log, StewardActNames.Pause, act);
        using var served = pipe;
        driver.Pipe = pipe;

        // Resuming is the steward's own pause's alone, so that one is made first.
        if (act == StewardActNames.Unpause)
        {
            var (_, before, beforeId) = await ActAsync(pipe, key, generation, StewardActNames.Pause, form);
            await using (before) Assert.True((await before.AnswerAsync(beforeId)).GetProperty("data").GetProperty("done").GetBoolean());
        }

        var (_, client, id) = await ActAsync(pipe, key, generation, act, form);
        await using var connected = client;
        var answer = await client.AnswerAsync(id);

        Assert.True(answer.GetProperty("data").GetProperty("done").GetBoolean(), answer.GetRawText());
        Assert.False(Confirming(client));
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        if (moved is null) Assert.Contains(route, driver.Ran.Select(each => each.Route));
        else Assert.Equal(moved, driver.World.Moved[^1]);
        Assert.Equal(act, judge.StewardActsNow()[0].Act);
        Assert.Contains(Lines("role.act"), line => line.GetProperty("data").GetProperty("act").GetString() == act);
    }

    [Theory]
    [MemberData(nameof(QuestAndTreeActs))]
    public async Task A_quest_or_tree_act_not_granted_asks_the_person_as_its_route_s_press(string act, string form, string route, string? moved)
    {
        _ = moved;
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new StewardModule();
        Seed(driver);
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, judge, key, generation) = Appointed(dispatcher, log, StewardActNames.Hold);
        using var served = pipe;
        driver.Pipe = pipe;

        var (_, client, _) = await ActAsync(pipe, key, generation, act, form);
        await using var connected = client;
        await Until(() => pipe.Confirmations.Now().Waiting.Count == 1, () => $"{act} was not asked");

        Assert.Equal(("DAORIS.DRIVER", route), (pipe.Confirmations.Now().Waiting[0].Module, pipe.Confirmations.Now().Waiting[0].Route));
        Assert.Empty(driver.Ran);
        Assert.Empty(driver.World.Moved);
        Assert.Empty(judge.StewardActsNow());
        Assert.Contains(Lines("role.asked"), line => line.GetProperty("data").GetProperty("act").GetString() == act);
    }

    [Theory]
    [MemberData(nameof(QuestAndTreeActs))]
    public async Task A_quest_or_tree_act_the_person_takes_back_asks_again(string act, string form, string route, string? moved)
    {
        _ = moved;
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new StewardModule();
        Seed(driver);
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, judge, key, generation) = Appointed(dispatcher, log, act);
        using var served = pipe;
        driver.Pipe = pipe;

        // Resuming is the steward's own pause's alone: the act that was granted is another's, so take a pause as made, then none granted.
        var earlier = act == StewardActNames.Unpause ? StewardActNames.Pause : act;
        judge.Staff(RoleKit.Steward([earlier]));
        var (_, first, firstId) = await ActAsync(pipe, key, generation, earlier, form);
        await using (first) Assert.True((await first.AnswerAsync(firstId)).GetProperty("data").GetProperty("done").GetBoolean());
        judge.Staff(RoleKit.Steward([]));

        var (_, next, _) = await ActAsync(pipe, key, generation, act, form);
        await using var connected = next;
        await Until(() => pipe.Confirmations.Now().Waiting.Count == 1, () => $"{act} after the take-back was not asked");
        Assert.Equal(route, pipe.Confirmations.Now().Waiting[0].Route);
    }

    [Fact]
    public async Task A_landing_whose_plan_names_a_pushing_plugin_is_refused_and_the_press_is_never_sent()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new StewardModule { Pushing = "push-to-origin" };
        Seed(driver);
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, judge, key, generation) = Appointed(dispatcher, log, StewardActNames.Land);
        using var served = pipe;
        driver.Pipe = pipe;

        var (_, client, id) = await ActAsync(pipe, key, generation, StewardActNames.Land, Tree);
        await using var connected = client;
        var answer = await client.AnswerAsync(id);

        Assert.False(answer.GetProperty("data").GetProperty("done").GetBoolean(), answer.GetRawText());
        Assert.Equal("landing-pushes", answer.GetProperty("data").GetProperty("why").GetString());
        Assert.DoesNotContain("LAND_SESSION_TREE", driver.Ran.Select(each => each.Route));
        Assert.Empty(judge.StewardActsNow());
    }

    [Theory]
    [InlineData("""{"id":"s1","force":true}""")]
    [InlineData("""{"id":"s1","follow":true}""")]
    [InlineData("""{"id":"s1","branch":"main"}""")]
    [InlineData("""{"id":"s1","path":"x"}""")]
    public void A_tree_act_takes_the_session_alone_so_no_option_of_the_press_is_the_steward_s(string form)
    {
        var element = JsonDocument.Parse(form).RootElement;
        Assert.False(StewardActs.Table[StewardActNames.Land].IsForm(element));
        Assert.False(StewardActs.Table[StewardActNames.Clean].IsForm(element));
    }

    public static TheoryData<string> Updates => [StewardActNames.UpdateNow, StewardActNames.UpdateCancel];

    [Theory]
    [MemberData(nameof(Updates))]
    public async Task An_update_act_granted_runs_with_no_card_and_not_granted_is_refused_with_nothing_asked(string act)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new StewardModule();
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, judge, key, generation) = Appointed(dispatcher, log, act);
        using var served = pipe;
        driver.Pipe = pipe;

        var (_, granted, grantedId) = await ActAsync(pipe, key, generation, act, "{}");
        await using (granted)
        {
            Assert.True((await granted.AnswerAsync(grantedId)).GetProperty("data").GetProperty("done").GetBoolean());
            Assert.False(Confirming(granted));
        }

        Assert.Equal([act == StewardActNames.UpdateNow ? "update now" : "update cancel"], driver.World.Moved);
        Assert.Equal(act, Assert.Single(judge.StewardActsNow()).Act);

        judge.Staff(RoleKit.Steward([]));
        var (_, revoked, revokedId) = await ActAsync(pipe, key, generation, act, "{}");
        await using var connected = revoked;
        var refused = await revoked.AnswerAsync(revokedId);
        Assert.Equal(RoleWhy.NotGranted, CodeOf(refused));
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Single(driver.World.Moved);
    }

    public static TheoryData<string[]> Verbs => [["drive", "engine"], ["undrive", "engine"], ["trees", "engine", "on"], ["hold", "engine"],
        ["resume", "engine"], ["stop", "s1a2b3c4"], ["finish", "s1a2b3c4"], ["retry", "#q-1"], ["workflow", "engine", "review"],
        ["pause", "q1"], ["unpause", "q1"], ["done", "q1"], ["delete", "q1"], ["land", "s1"], ["clean", "s1"],
        ["hand-over", "s1", "--agent", "codex"], ["hand-over", "s1", "--agent", "codex", "--account", "work"]];

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
