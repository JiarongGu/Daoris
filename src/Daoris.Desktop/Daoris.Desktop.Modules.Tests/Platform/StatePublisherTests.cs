using System.Text.Json;
using System.Text.RegularExpressions;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// What a route changed reaches every window as <c>STATE_CHANGED</c> (STATEHUB1b, D169): published by the dispatcher's
/// middleware after the route answered, from what the route declares beside its mark, never by the handler.
/// </summary>
/// <remarks>
/// Composed as the shell composes its dispatcher (the kit's <c>UseMessageDispatcher</c>, the middleware in its application
/// slot), over the kit's real bus, which the bridge's base subscribes to: so what is asserted is the event a window receives.
/// </remarks>
public sealed partial class StatePublisherTests : DriverModuleBridge
{
    private static readonly JsonSerializerOptions Wire = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>A route that notes, as it answers, whether the event was raised before it: it must not have been.</summary>
    private sealed class Moving(Func<bool> raisedYet) : ModuleBase
    {
        public override string ModuleName => "TEST.STATE";

        public bool? RaisedBeforeItAnswered { get; private set; }

        protected override Task<object?> RouteMessageAsync(IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
        {
            RaisedBeforeItAnswered = raisedYet();
            // A refusal answered as data is an answer; one thrown is an error, which the driver refused before acting.
            return request.Type == "REFUSE"
                ? throw new DriverException("refused before anything moved.")
                : Task.FromResult<object?>(new { Done = request.Type != "MOVE_REFUSED_AS_DATA" });
        }
    }

    /// <summary>A route that answers only once the test lets it, so what is under way can be read while it is.</summary>
    private sealed class Held : ModuleBase
    {
        private readonly TaskCompletionSource _let = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override string ModuleName => "TEST.HELD";

        public void Let() => _let.TrySetResult();

        protected override async Task<object?> RouteMessageAsync(IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
        {
            await _let.Task;
            return request.Type == "REFUSE" ? throw new DriverException("refused before anything moved.") : new { Done = true };
        }
    }

    /// <summary>A route that says which step it has reached (PRESSSTEP1), and waits for the test between them.</summary>
    private sealed class Stepping : ModuleBase
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _landing = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override string ModuleName => "TEST.STEPS";

        public void LetGate() => _gate.TrySetResult();

        public void LetLanding() => _landing.TrySetResult();

        protected override async Task<object?> RouteMessageAsync(IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
        {
            PressSteps.Reach(PressSteps.Gate);
            await _gate.Task;
            PressSteps.Reach(PressSteps.Landing);
            // Reached again: the same step tells nothing new.
            PressSteps.Reach(PressSteps.Landing);
            await _landing.Task;
            return new { Done = true };
        }
    }

    /// <summary>
    /// A route whose landing waits behind another's hold (LANDHOLD2), said as the hold says it, and ends when the test lets it; where
    /// <paramref name="named"/> is given, the hold comes to name its holder once the wait has begun (LANDHOLD3).
    /// </summary>
    private sealed class Queued(LandingHolder? named = null) : ModuleBase
    {
        private readonly TaskCompletionSource _wait = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _end = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override string ModuleName => "TEST.QUEUED";

        public void LetWait() => _wait.TrySetResult();

        public void LetEnd() => _end.TrySetResult();

        protected override async Task<object?> RouteMessageAsync(IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
        {
            PressSteps.Reach(PressSteps.Landing);
            using var waiting = PressSteps.WhileWaiting(PressSteps.Landing);
            LandingHoldNotice.Waiting(true);
            if (named is not null) LandingHoldNotice.Waiting(true, named);
            await _wait.Task;
            LandingHoldNotice.Waiting(false, named);
            await _end.Task;
            return new { Done = true };
        }
    }

    private IMessageDispatcher Compose(IIpcModule module, Func<IpcRequest, StateChange?>? declared = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(Bus);
        services.AddSingleton(module);
        services.UseMessageDispatcher((_, dispatcher) => dispatcher.Use(
            declared is null ? StatePublisher.Middleware(Bus) : StatePublisher.Middleware(Bus, declared)));
        return services.BuildServiceProvider().GetRequiredService<IMessageDispatcher>();
    }

    /// <summary>The modules, over the publisher's list <paramref name="underWay"/>, as the shell composes it over its loop's.</summary>
    private IMessageDispatcher Compose(IEnumerable<IIpcModule> modules, Func<IpcRequest, StateChange?> declared, PressesUnderWay underWay)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(Bus);
        foreach (var module in modules) services.AddSingleton(module);
        services.UseMessageDispatcher((_, dispatcher) => dispatcher.Use(StatePublisher.Middleware(Bus, declared, underWay)));
        return services.BuildServiceProvider().GetRequiredService<IMessageDispatcher>();
    }

    private static Task<IpcResponse> Dispatch(IMessageDispatcher dispatcher, string module, string type, object? payload = null) =>
        dispatcher.DispatchAsync(
            new IpcRequest
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Module = module,
                Type = type,
                Payload = payload is null ? null : JsonSerializer.SerializeToElement(payload),
            },
            CancellationToken.None);

    private IReadOnlyList<EventMessage> Changed() => [.. Raised.Where(message => message.Type == StatePublisher.Type)];

    /// <summary>What was told under way, in the order the publisher numbered it (the bus does not wait, so not always raised so).</summary>
    private IReadOnlyList<string> Told() =>
    [
        .. Raised.Where(message => message.Type == StatePublisher.UnderWayType)
            .Select(message => (Seq: ((UnderWay)message.Payload!).Seq, Shape: Shape(message)))
            .OrderBy(told => told.Seq)
            .Select(told => told.Shape),
    ];

    private static string Shape(EventMessage message) => JsonSerializer.Serialize(message.Payload, Wire);

    private static StateChange? Declared(IpcRequest request) => request.Type switch
    {
        "MOVE" or "MOVE_REFUSED_AS_DATA" or "REFUSE" => new StateChange(StateScopes.Landing | StateScopes.Sessions, "id"),
        "SWEEP_ALL" => new StateChange(StateScopes.Sweep),
        _ => null,
    };

    [Fact]
    public async Task A_route_s_declared_change_is_published_once_it_has_answered()
    {
        var route = new Moving(() => Changed().Count > 0);
        var dispatcher = Compose(route, Declared);

        var answer = await Dispatch(dispatcher, route.ModuleName, "MOVE", new { id = "s1a2b3c4" });

        Assert.True(answer.Success);
        await UntilAsync(() => Changed().Count == 1);
        Assert.False(route.RaisedBeforeItAnswered);
        var changed = Assert.Single(Changed());
        Assert.Equal("DAORIS", changed.Module);
        // The scopes in the page's words, in the order they are declared, and the session the payload named: nothing else.
        Assert.Equal("""{"scopes":["sessions","landing"],"sessions":["s1a2b3c4"]}""", Shape(changed));
    }

    [Fact]
    public async Task A_change_about_no_session_names_none()
    {
        var route = new Moving(() => false);
        var dispatcher = Compose(route, Declared);

        await Dispatch(dispatcher, route.ModuleName, "SWEEP_ALL", new { only = new[] { "daoris/s-1" } });

        await UntilAsync(() => Changed().Count == 1);
        Assert.Equal("""{"scopes":["sweep"]}""", Shape(Assert.Single(Changed())));
    }

    /// <summary>
    /// D169 point 5: a refusal answered as data publishes as an answer does (a landing refused for a conflict fetched the line
    /// its plan reads against); one thrown publishes nothing; and a route that declares nothing, a read, publishes nothing.
    /// </summary>
    [Fact]
    public async Task A_refusal_answered_as_data_publishes_and_an_error_or_a_read_does_not()
    {
        var route = new Moving(() => false);
        var dispatcher = Compose(route, Declared);

        var thrown = await Dispatch(dispatcher, route.ModuleName, "REFUSE", new { id = "s1" });
        var read = await Dispatch(dispatcher, route.ModuleName, "PLAN", new { id = "s1" });
        var refused = await Dispatch(dispatcher, route.ModuleName, "MOVE_REFUSED_AS_DATA", new { id = "s2" });

        Assert.False(thrown.Success);
        Assert.True(read.Success);
        Assert.True(refused.Success);
        await UntilAsync(() => Changed().Count == 1);
        await Task.Delay(50);
        Assert.Equal("""{"scopes":["sessions","landing"],"sessions":["s2"]}""", Shape(Assert.Single(Changed())));
    }

    /// <summary>
    /// STATEHUB1c (D169 point 7): a declared press is under way from its dispatch until it answers, and every window is told what
    /// is, whole and numbered at each change, with the session its payload named; so *落地中…* is shown by every view of that
    /// session, whichever window pressed. One refused as an error ends as one that answered does; a read is never under way.
    /// </summary>
    [Fact]
    public async Task A_declared_press_is_told_under_way_whole_until_it_answers()
    {
        var held = new Held();
        var dispatcher = Compose(held, Declared);

        var moving = Dispatch(dispatcher, held.ModuleName, "MOVE", new { id = "s1" });
        await UntilAsync(() => Told().Count == 1);
        var sweeping = Dispatch(dispatcher, held.ModuleName, "SWEEP_ALL", new { only = new[] { "daoris/s-1" } });
        await UntilAsync(() => Told().Count == 2);
        var refusing = Dispatch(dispatcher, held.ModuleName, "REFUSE", new { id = "s2" });
        var reading = Dispatch(dispatcher, held.ModuleName, "PLAN", new { id = "s1" });
        await UntilAsync(() => Told().Count == 3);

        Assert.Equal(
            [
                """{"seq":1,"presses":[{"route":"MOVE","session":"s1"}]}""",
                """{"seq":2,"presses":[{"route":"MOVE","session":"s1"},{"route":"SWEEP_ALL"}]}""",
                """{"seq":3,"presses":[{"route":"MOVE","session":"s1"},{"route":"SWEEP_ALL"},{"route":"REFUSE","session":"s2"}]}""",
            ],
            Told());

        held.Let();
        var answers = await Task.WhenAll(moving, sweeping, refusing, reading);

        Assert.Equal([true, true, false, true], answers.Select(answer => answer.Success));
        await UntilAsync(() => Told().Count == 6);
        await Task.Delay(50);
        // Each end told whole, the thrown refusal's too, and the read never: the last word is that nothing is under way.
        Assert.Equal(6, Told().Count);
        Assert.Equal("""{"seq":6,"presses":[]}""", Told()[^1]);
        Assert.Equal(2, Changed().Count);
    }

    /// <summary>
    /// PRESSSTEP1 (D169 point 3): the handler of a declared press says the step it has reached, and every window is told the press
    /// whole with it, beside its route and session; the same step again tells nothing, and a step said outside a press (a read, or
    /// no press at all) is said to nobody. The step ends with the press.
    /// </summary>
    [Fact]
    public async Task A_press_says_the_step_it_has_reached()
    {
        var stepping = new Stepping();
        var loop = Loop();
        var dispatcher = Compose([stepping], request => new StateChange(StateScopes.Landing, "id"), loop.UnderWay);

        PressSteps.Reach(PressSteps.Gate);
        var pressing = Dispatch(dispatcher, stepping.ModuleName, "MOVE", new { id = "s1" });
        await UntilAsync(() => Told().Count == 2);
        Assert.Equal("""{"seq":2,"presses":[{"route":"MOVE","session":"s1","step":"gate"}]}""", Told()[^1]);
        // A window opened now reads the step too.
        Assert.Equal(2, loop.UnderWay.Now().Seq);

        stepping.LetGate();
        await UntilAsync(() => Told().Count == 3);
        await Task.Delay(50);
        Assert.Equal(3, Told().Count);
        Assert.Equal("""{"seq":3,"presses":[{"route":"MOVE","session":"s1","step":"landing"}]}""", Told()[^1]);

        stepping.LetLanding();
        Assert.True((await pressing).Success);
        await UntilAsync(() => Told().Count == 4);
        Assert.Equal("""{"seq":4,"presses":[]}""", Told()[^1]);
    }

    /// <summary>
    /// LANDHOLD2 (D145 point 2, D173's LANDRACE1 note): a press whose landing waits behind another's hold says the waiting step before it
    /// waits, and the step it was at when it has the hold; the waiting reaches the page's telling like any step, and ends with the press.
    /// </summary>
    [Fact]
    public async Task A_press_waiting_behind_a_landing_says_so_and_goes_back_to_its_step()
    {
        var queued = new Queued();
        var dispatcher = Compose([queued], request => new StateChange(StateScopes.Landing, "id"), Loop().UnderWay);

        var pressing = Dispatch(dispatcher, queued.ModuleName, "MOVE", new { id = "s1" });
        await UntilAsync(() => Told().Count == 3);
        Assert.Equal("""{"seq":3,"presses":[{"route":"MOVE","session":"s1","step":"waiting"}]}""", Told()[^1]);

        queued.LetWait();
        await UntilAsync(() => Told().Count == 4);
        Assert.Equal("""{"seq":4,"presses":[{"route":"MOVE","session":"s1","step":"landing"}]}""", Told()[^1]);

        queued.LetEnd();
        Assert.True((await pressing).Success);
    }

    /// <summary>
    /// LANDHOLD3: once the hold names who holds it, the waiting step says it again with the holder's session, and only its session, no
    /// branch; the step it goes back to names nobody.
    /// </summary>
    [Fact]
    public async Task A_waiting_press_names_the_session_it_waits_behind()
    {
        var queued = new Queued(new LandingHolder("s-other", "feature/other"));
        var dispatcher = Compose([queued], request => new StateChange(StateScopes.Landing, "id"), Loop().UnderWay);

        var pressing = Dispatch(dispatcher, queued.ModuleName, "MOVE", new { id = "s1" });
        await UntilAsync(() => Told().Count == 4);
        Assert.Equal("""{"seq":3,"presses":[{"route":"MOVE","session":"s1","step":"waiting"}]}""", Told()[^2]);
        Assert.Equal("""{"seq":4,"presses":[{"route":"MOVE","session":"s1","step":"waiting","behind":"s-other"}]}""", Told()[^1]);

        queued.LetWait();
        await UntilAsync(() => Told().Count == 5);
        Assert.Equal("""{"seq":5,"presses":[{"route":"MOVE","session":"s1","step":"landing"}]}""", Told()[^1]);

        queued.LetEnd();
        Assert.True((await pressing).Success);
    }

    /// <summary>
    /// ACCEPTALL1e (D169 point 3): a press whose declaration names a payload field as its subject carries it, so an entry's or an
    /// ask's Accept under way is shown by every window that shows that entry or ask; it names no session of its own.
    /// </summary>
    [Fact]
    public async Task A_press_carries_the_entry_or_ask_its_declaration_names()
    {
        var held = new Held();
        var dispatcher = Compose(held, request => request.Type == "LAND_ENTRY" ? new StateChange(StateScopes.Landing, null, "entry") : null);

        var landing = Dispatch(dispatcher, held.ModuleName, "LAND_ENTRY", new { entry = "e-1", leaveOut = new[] { "s2" } });
        await UntilAsync(() => Told().Count == 1);
        Assert.Equal("""{"seq":1,"presses":[{"route":"LAND_ENTRY","subject":"e-1"}]}""", Told()[0]);

        held.Let();
        await landing;
        await UntilAsync(() => Changed().Count == 1);
        // The change is told whole, as ever: no word of the subject on the wire.
        Assert.Equal("""{"scopes":["landing"]}""", Shape(Assert.Single(Changed())));
    }

    /// <summary>
    /// STATEHUB1c2 (D169's STATEHUB1c2 note): a window opened, or a page reloaded, while a press runs has heard none of its
    /// tellings, so it reads the list the publisher holds, the loop's, through the driver's route: whole, numbered as the last
    /// telling, so the window keeps whichever is newer. The read is no press: never under way, and it tells nothing.
    /// </summary>
    [Fact]
    public async Task A_window_opened_mid_press_reads_what_is_under_way_through_the_driver_s_route()
    {
        var held = new Held();
        var loop = Loop();
        var dispatcher = Compose(
            [held, new DriverModule(Bus, loop)],
            request => request.Module == held.ModuleName ? Declared(request) : DriverModule.Declared(request),
            loop.UnderWay);
        async Task<string> Read()
        {
            var read = await Dispatch(dispatcher, DriverModule.Name, "UNDER_WAY");
            Assert.True(read.Success);
            return JsonSerializer.Serialize(read.Data, Wire);
        }

        Assert.Equal("""{"seq":0,"presses":[]}""", await Read());

        var moving = Dispatch(dispatcher, held.ModuleName, "MOVE", new { id = "s1" });
        await UntilAsync(() => Told().Count == 1);
        Assert.Equal("""{"seq":1,"presses":[{"route":"MOVE","session":"s1"}]}""", await Read());

        held.Let();
        await moving;
        await UntilAsync(() => Told().Count == 2);
        Assert.Equal("""{"seq":2,"presses":[]}""", await Read());

        await Task.Delay(50);
        // Three reads told nothing and changed nothing: only the press did.
        Assert.Equal(2, Told().Count);
        Assert.Single(Changed());
    }

    /// <summary>
    /// The driver's own routes, through the real module: setting a landing rule changes every plan and the rules, and
    /// publishes both; a rule refused in the driver's words, and a read (the accounts, a file read), publish nothing.
    /// </summary>
    [Fact]
    public async Task The_driver_s_route_publishes_what_its_mark_declares()
    {
        var dispatcher = Compose(Module());

        var set = await Dispatch(dispatcher, DriverModule.Name, "SET_LANDING", new { repository = "engine", form = "merge" });
        var refused = await Dispatch(dispatcher, DriverModule.Name, "SET_LANDING", new { repository = "engine", form = "push" });
        var read = await Dispatch(dispatcher, DriverModule.Name, "ACCOUNTS");

        Assert.True(set.Success);
        Assert.False(refused.Success);
        Assert.True(read.Success);
        await UntilAsync(() => Changed().Count == 1);
        await Task.Delay(50);
        Assert.Equal("""{"scopes":["landing","lines"]}""", Shape(Assert.Single(Changed())));
    }

    /// <summary>
    /// D169's first slice, the landing family: what each press declares it changes, and which payload field names the session
    /// where the change is one session's; the plans, gates and lists beside them declare nothing.
    /// </summary>
    [Theory]
    [InlineData("LAND_SESSION_TREE", "sessions,landing,diff,handOff,sweep,treesSync", "id")]
    [InlineData("LAND_ENTRY", "sessions,landing,diff,handOff,sweep,treesSync", null)]
    [InlineData("LAND_ASK", "sessions,landing,diff,handOff,sweep,treesSync", null)]
    [InlineData("ENTRY_LANDING", "", null)]
    [InlineData("ASK_LANDING", "", null)]
    [InlineData("DISCARD_SESSION_TREE", "sessions,landing,diff,sweep,treesSync", "id")]
    [InlineData("HANDOFF", "handOff,sweep", "id")]
    [InlineData("ASK_OPINION", "sessions,landing", "id")]
    [InlineData("OPINION_ANYWAY", "sessions,landing", "id")]
    [InlineData("OPINION_MYSELF", "sessions,landing", "id")]
    [InlineData("STOP_OPINION", "sessions,landing", null)]
    [InlineData("SWEEP", "sessions,landing,diff,handOff,sweep,treesSync", null)]
    [InlineData("DISCARD_SESSION_BRANCH", "sessions,landing,diff,handOff,sweep,treesSync", null)]
    [InlineData("RECOVERY_DROP", "sessions,landing,diff,handOff,sweep,treesSync", null)]
    [InlineData("TREES_SYNC", "sessions,landing,diff,handOff,sweep,treesSync", null)]
    [InlineData("SET_LANDING", "landing,lines", null)]
    [InlineData("LANDING", "", null)]
    [InlineData("SESSION_PROCESS", "", null)]
    [InlineData("OPINION_GATE", "", null)]
    [InlineData("OPINION_WAITS", "", null)]
    [InlineData("HANDOFF_PLAN", "", null)]
    [InlineData("SWEEP_PLAN", "", null)]
    [InlineData("TREES_SYNC_PLAN", "", null)]
    [InlineData("TREES_SYNC_SCOPE", "", null)]
    [InlineData("WORK_STATE", "", null)]
    [InlineData("WORK_HELD", "sessions", null)]
    public void The_landing_family_declares_what_it_changes(string route, string scopes, string? session)
    {
        var declared = DriverModule.Declared(new IpcRequest { Id = "x", Module = DriverModule.Name, Type = route });

        Assert.Equal(scopes, string.Join(",", StatePublisher.Names(declared?.Scopes ?? StateScopes.None)));
        Assert.Equal(session, declared?.Session);
    }

    /// <summary>
    /// D169's rest of the routes, domain by domain (STATEHUB1d): sessions, conversations, work, rules, history, sync and plugins
    /// each declare what their presses change, the scopes their hooks used to ask again by hand; the looks beside them, the git
    /// domain's every route and a look that fetches (<c>TREES_SYNC_PLAN</c>, above) declare nothing.
    /// </summary>
    [Theory]
    [InlineData("SESSION_ARCHIVE", "sessions", null)]
    [InlineData("SESSION_DELETE", "sessions,help", "id")]
    [InlineData("STOP_SESSION", "sessions,driver", "id")]
    [InlineData("RESOLVE_SESSION", "sessions,quests,driver", "id")]
    [InlineData("SESSION_GO_ON_NEW", "sessions,driver", "id")]
    // MIXAGENT2b: the person's ask of a helper and their yes to an agent's, each a file the loop's next look takes; the asks' read.
    [InlineData("SESSION_HELPER_ASK", "sessions,driver", "id")]
    [InlineData("SESSION_HELPER_APPROVE", "sessions,driver", "session")]
    // HELPERNO1: the person's no, the ask refused and the asker's note written at once.
    [InlineData("SESSION_HELPER_DECLINE", "sessions,driver", "session")]
    [InlineData("SESSION_HELPERS", "", null)]
    // MIXAGENT1c: the person's yes and no to a session's agent's own ask to hand it over, and the ask's read.
    [InlineData("SESSION_HAND_OVER_APPROVE", "sessions,driver", "session")]
    [InlineData("SESSION_HAND_OVER_DECLINE", "sessions,driver", "session")]
    [InlineData("SESSION_HAND_OVER_ASK", "", null)]
    // MIXAGENT2c: what of the agents' asks waits on the person, *What needs you*'s read.
    [InlineData("HELPER_WAITS", "", null)]
    // COWORK1e: the person's answer to a third answer alone held for them, the mark taken away or kept; and the waits' read.
    [InlineData("SESSION_ANSWER_ALONE", "sessions,driver", "session")]
    [InlineData("ANSWER_WAITS", "", null)]
    [InlineData("SESSION_OPENINGS", "", null)]
    // SESSCTX1d: what each part spent, the *Parts* strip's read.
    [InlineData("SESSION_SPENT", "", null)]
    [InlineData("SESSION_WHERE", "", null)]
    [InlineData("SESSION_GROUPS", "", null)]
    [InlineData("SESSION_SEARCH", "", null)]
    [InlineData("SESSION_OPEN_FOLDER", "", null)]
    [InlineData("WORK_RECORDS_FOLDER", "", null)]
    [InlineData("START_CHAT", "sessions,driver", null)]
    [InlineData("SESSION_START_FROM", "sessions,driver", "id")]
    // SESSCTX1c part B: *Go on with Daoris's brief*, a new part of the session opened and the words taken off the one it continues.
    [InlineData("SESSION_GO_ON_BRIEF", "sessions,driver", "id")]
    [InlineData("SESSION_HAND_OVER", "sessions,driver", "id")]
    [InlineData("END_CHAT", "sessions", "id")]
    [InlineData("SESSION_INPUT", "sessions,help", "id")]
    [InlineData("SESSION_HISTORY", "", null)]
    [InlineData("SESSION_QUEUE", "", null)]
    [InlineData("WORK_PAUSE", "sessions,quests,asks,driver", null)]
    [InlineData("WORK_RESUME", "sessions,quests,asks,driver", null)]
    [InlineData("WORK_ABANDON", "sessions,quests,asks,driver", null)]
    [InlineData("WORK_PLAN", "", null)]
    [InlineData("RULE_ACTION", "rules", null)]
    [InlineData("RULE_PROPOSAL", "rules", null)]
    [InlineData("RULES", "", null)]
    [InlineData("HISTORY_CLEAR", "sessions,quests,asks,driver,history", null)]
    [InlineData("HISTORY_PLAN", "", null)]
    [InlineData("SYNC_NOW", "sessions,quests,sync,registry", null)]
    [InlineData("GIT_BRANCHES", "", null)]
    [InlineData("CHECKOUT_BRANCH", "", null)]
    [InlineData("GIT_GRAPH", "", null)]
    [InlineData("GIT_COMMIT", "", null)]
    [InlineData("GIT_HISTORY", "", null)]
    [InlineData("GIT_BLAME", "", null)]
    [InlineData("GIT_COMPARE", "", null)]
    // HELPMODE1c3: and the driver's, whose helpGrants draws the browser's grant from the plugin's approval.
    [InlineData("PLUGIN_ACTION", "driver,plugins,harnesses", null)]
    [InlineData("PLUGIN_UPDATE", "driver,plugins,harnesses", null)]
    [InlineData("PLUGIN_INSTALL", "driver,plugins,harnesses", null)]
    [InlineData("PLUGIN_ADD", "driver,plugins,harnesses", null)]
    [InlineData("PLUGINS", "", null)]
    [InlineData("PLUGIN", "", null)]
    [InlineData("PLUGIN_READ", "", null)]
    [InlineData("PLUGIN_ACTIVITY", "", null)]
    [InlineData("PLUGIN_NEW", "", null)]
    [InlineData("PLUGIN_TRY", "", null)]
    [InlineData("PLUGIN_OPEN_FOLDER", "", null)]
    public void The_rest_of_the_routes_declare_what_they_change(string route, string scopes, string? session)
    {
        Assert.Contains(route, DriverModule.Routes);
        var declared = DriverModule.Declared(new IpcRequest { Id = "x", Module = DriverModule.Name, Type = route });

        Assert.Equal(scopes, string.Join(",", StatePublisher.Names(declared?.Scopes ?? StateScopes.None)));
        Assert.Equal(session, declared?.Session);
    }

    /// <summary>
    /// The presses still told only to the window that pressed (STATEHUB1h, D169's STATEHUB1h note): the driver's standing
    /// choices, a go-ahead, a turn stopped and a word taken back, and what the accounts, agents, asks, across, registry, tools
    /// and help domains press, each the families its hook asked again by hand, or the window wrote its own answer into. The
    /// looks beside them, the workflow domain's every route, and the presses whose change an event already tells whole or no
    /// window reads declare nothing; the roster's read declares nothing, and *Read again* is its own route, which does (HARNESSREFRESH1).
    /// </summary>
    [Theory]
    [InlineData("SET_DRIVABLE", "driver", null)]
    [InlineData("SET_HOLD", "driver", null)]
    [InlineData("SET_TREES", "driver", null)]
    [InlineData("SET_STANDING", "driver", null)]
    [InlineData("SET_HEAVY_GATES", "driver", null)]
    [InlineData("SET_KEEP", "driver,wiring", null)]
    [InlineData("SET_SESSION_CHECKS", "driver", null)]
    // HEAVYSLOT1: who holds a repository's heavy-run slot and who waits, a read.
    [InlineData("HEAVY_SLOT", "", null)]
    [InlineData("SET_NOTIFY", "driver", null)]
    [InlineData("SET_STRIKES", "driver", null)]
    [InlineData("SET_COOLOFF", "driver", null)]
    [InlineData("SET_CAP", "driver", null)]
    [InlineData("SET_INTAKE", "driver,harnesses", null)]
    [InlineData("SET_HELPER", "driver", null)]
    [InlineData("SET_HELP_MODE", "driver", null)]
    [InlineData("SET_HELP_GRANT", "driver", null)]
    [InlineData("TRUST_FOLDER", "driver", null)]
    [InlineData("RETRY_QUEST", "sessions,driver", null)]
    [InlineData("SHOW_REVIEW_AGAIN", "driver", null)]
    [InlineData("STATE", "", null)]
    [InlineData("STATE_SLICE", "", null)]
    [InlineData("UNDER_WAY", "", null)]
    [InlineData("NUDGE", "", null)]
    [InlineData("SET_LINE", "driver", null)]
    [InlineData("SET_LANGUAGE", "driver", null)]
    [InlineData("SET_REVIEW", "driver", null)]
    [InlineData("SET_OPINION", "driver", null)]
    [InlineData("SET_COMMITS", "driver", null)]
    [InlineData("SET_HELPERS", "driver", null)]
    [InlineData("SET_HAND_OVER", "driver", null)]
    [InlineData("SET_ACCESS", "driver", null)]
    [InlineData("LINES", "", null)]
    [InlineData("SET_READ_ACROSS", "driver", null)]
    [InlineData("SET_WRITE_ACROSS", "driver", null)]
    [InlineData("ACROSS", "", null)]
    [InlineData("SESSION_GO_AHEAD", "sessions,asks", "id")]
    [InlineData("CANCEL_TURN", "sessions", "id")]
    [InlineData("SESSION_QUEUE_EDIT", "sessions", "session")]
    [InlineData("SET_SESSION_OPTION", "", null)]
    [InlineData("SESSION_OPTIONS", "", null)]
    [InlineData("ACCOUNT_USE", "harnesses", null)]
    [InlineData("ACCOUNTS", "", null)]
    [InlineData("ACCOUNTS_READ", "harnesses", null)]
    [InlineData("HARNESS_ACTION", "harnesses", null)]
    [InlineData("SET_AGENT_SETTINGS", "harnesses", null)]
    [InlineData("HARNESS_INPUT", "", null)]
    [InlineData("HARNESS_CANCEL", "", null)]
    [InlineData("HARNESSES", "", null)]
    [InlineData("HARNESSES_REFRESH", "harnesses", null)]
    [InlineData("STARTS", "", null)]
    [InlineData("USAGE", "", null)]
    [InlineData("CHANNEL_POST", "asks", null)]
    [InlineData("CHANNEL_MARK", "asks", null)]
    [InlineData("NOTICES_READ", "notices", null)]
    [InlineData("NOTICE_DISMISS", "notices", null)]
    // NOTIFY2i: the terminal's read of what came to the person, which changes nothing.
    [InlineData("NOTIFICATIONS", "", null)]
    [InlineData("CHANNEL_READ", "", null)]
    [InlineData("ASKS_ELSEWHERE", "", null)]
    [InlineData("REGISTRY_REFRESH", "registry", null)]
    // LAYOUT8: a set-up's press publishes one ask, whose quest it becomes, and adds the repository's rule; its plan reads.
    [InlineData("SETUP_PRESS", "quests,asks,rules", null)]
    [InlineData("SETUP_PLAN", "", null)]
    [InlineData("TOOLS_USE", "tools", null)]
    [InlineData("TOOLS_DOWNLOAD", "tools", null)]
    [InlineData("TOOLS_DELETE", "tools", null)]
    [InlineData("TOOLS_LOOK", "tools", null)]
    [InlineData("TOOLS_LOCATION", "tools", null)]
    [InlineData("TOOLS_STOP", "", null)]
    [InlineData("TOOLS_LIST", "", null)]
    [InlineData("TOOLS_GIT", "", null)]
    [InlineData("TOOLS_PICK", "", null)]
    [InlineData("WORKFLOW_CURRENT", "", null)]
    [InlineData("WORKFLOW_RUN", "", null)]
    // FABRIC-PIPELINE1a: the landed route set, which the page reads again by its own query, as every workflow route is read.
    [InlineData("WORKFLOW_LANDED", "", null)]
    [InlineData("START_HELP", "sessions", null)]
    [InlineData("HELP_RENAME", "sessions,help", "id")]
    [InlineData("HELP_PIN", "sessions,help", "id")]
    [InlineData("HELP_START_FROM", "sessions,help", "id")]
    [InlineData("HELP_DISMISS", "help", null)]
    // LAYOUT8: a set-up's Apply adds the repository's rule, so an Apply may move the rules too.
    [InlineData("HELP_APPLY", "sessions,quests,asks,driver,rules,history,registry,plugins,harnesses,help,browser", null)]
    [InlineData("HELP_PROPOSALS", "", null)]
    [InlineData("HELP_CONVERSATIONS", "", null)]
    // HELPWIRED1: what each start handed, read from the home; it changes nothing.
    [InlineData("HELP_WIRED", "", null)]
    [InlineData("STOP_TASK", "", null)]
    [InlineData("TAIL_SESSION", "", null)]
    [InlineData("TRACE", "", null)]
    // WIRING1b: This Daoris's read; asking it to measure starts a walk and moves no state a window shows.
    [InlineData("SYSTEM_WIRING", "", null)]
    // CACHECLEAR1: the Downloads row's list reads; its press removes versions the agents' and tools' pages and the sizes say.
    [InlineData("SYSTEM_DOWNLOADS", "", null)]
    [InlineData("SYSTEM_DOWNLOADS_CLEAR", "harnesses,tools,wiring", null)]
    // KEEP1c: the Disk space card's read; asking it to count starts walks, each kind's end telling `wiring` by itself.
    [InlineData("SYSTEM_SPACE", "", null)]
    // KEEP1e: Free up space's press removes what the sizes, the agents' and the tools' pages say; each kind's end tells `wiring` by itself.
    [InlineData("SYSTEM_SPACE_FREE", "harnesses,tools,wiring", null)]
    // RPC1c: what waits for the window is told by its own event (`CONFIRMATIONS`), and a confirmed press publishes what it changes
    // as it runs; the answer itself moves no scope. PERSONDOOR1f: the key is a read.
    [InlineData("PERSON_CONFIRMATIONS", "", null)]
    [InlineData("PERSON_CONFIRM", "", null)]
    [InlineData("PERSON_CONFIRM_SHOWN", "", null)]
    [InlineData("PERSON_KEY", "", null)]
    // NOTIFY2j: what waits at the service's door is told by its own event (`SERVICE_CONFIRMATIONS`); the read moves nothing.
    [InlineData("SERVICE_CONFIRMATIONS", "", null)]
    public void The_remaining_presses_declare_what_they_change(string route, string scopes, string? session)
    {
        Assert.Contains(route, DriverModule.Routes);
        var declared = DriverModule.Declared(new IpcRequest { Id = "x", Module = DriverModule.Name, Type = route });

        Assert.Equal(scopes, string.Join(",", StatePublisher.Names(declared?.Scopes ?? StateScopes.None)));
        Assert.Equal(session, declared?.Session);
    }

    /// <summary>
    /// A press named in the wire's words a page reads: the scopes STATEHUB1d added sit after the first slice's, and STATEHUB1h's
    /// after those, in their declared order, so a scope is added and none is renumbered (D169 point 3).
    /// </summary>
    [Fact]
    public void Every_scope_has_its_word_on_the_wire_in_its_declared_order() =>
        Assert.Equal(
            "sessions,landing,diff,handOff,sweep,treesSync,lines,quests,asks,driver,rules,history,sync,registry,plugins,harnesses,"
            + "tools,help,browser,wiring,notices",
            string.Join(",", StatePublisher.Names((StateScopes)~0)));

    /// <summary>
    /// FABRIC-EVENT5: a change the driver itself makes, with no press behind it (words taken up, a proposal written, a size landed), is
    /// told to every window as a press's is: the scopes in the page's words, and the one session it was about where there is one.
    /// </summary>
    [Fact]
    public async Task A_change_the_driver_makes_is_told_in_the_same_words_a_press_is()
    {
        StatePublisher.Publish(Bus, StateScopes.Help);
        StatePublisher.Publish(Bus, StateScopes.Wiring);
        StatePublisher.Publish(Bus, StateScopes.Sessions, "s1a2b3c4");

        await UntilAsync(() => Changed().Count == 3);
        Assert.Equal(
            new[] { """{"scopes":["help"]}""", """{"scopes":["wiring"]}""", """{"scopes":["sessions"],"sessions":["s1a2b3c4"]}""" }
                .Order(StringComparer.Ordinal),
            Changed().Select(message => Shape(message)).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// HELPWIREDLIVE1b (D183 point 4): the loop tells the help scope, with the session, when a conversation's start receipt is written,
    /// so an open What it has reads a resume's new receipt rather than the old one.
    /// </summary>
    [Fact]
    public async Task A_receipt_written_is_told_as_the_help_scope_with_its_session()
    {
        Daoris.Desktop.DriverLoop.WiredKeptTold(Bus)("s1a2b3c4");

        await UntilAsync(() => Changed().Count == 1);
        Assert.Equal("""{"scopes":["help"],"sessions":["s1a2b3c4"]}""", Shape(Changed().Single()));
    }

    /// <summary>
    /// BROWSERPUBLISH1: the browser module's edits declare the browser, so every window's Settings → Browser and link opener ask it
    /// again; its read declares nothing. Published on the wire as the driver's marks are, through the shell's own middleware.
    /// </summary>
    [Theory]
    [InlineData("ADD_FAVORITE", "browser")]
    [InlineData("REMOVE_FAVORITE", "browser")]
    [InlineData("SET_EXTENSIONS", "browser")]
    [InlineData("SET_BROWSER", "browser")]
    [InlineData("SET_LINKS", "browser")]
    [InlineData("STATE", "")]
    public void The_browser_module_s_presses_declare_the_browser(string route, string scopes)
    {
        var declared = StatePublisher.Declared(new IpcRequest { Id = "x", Module = BrowserModule.Name, Type = route });

        Assert.Equal(scopes, string.Join(",", StatePublisher.Names(declared?.Scopes ?? StateScopes.None)));
        Assert.Null(declared?.Session);
    }

    /// <summary>A browser press is published on the bus once it answers, over the real pipeline.</summary>
    [Fact]
    public async Task A_browser_press_is_published_once_it_has_answered()
    {
        var route = new Moving(() => Changed().Count > 0);
        var dispatcher = Compose(route, request => StatePublisher.Declared(
            new IpcRequest { Id = request.Id, Module = BrowserModule.Name, Type = request.Type }));

        var answer = await Dispatch(dispatcher, route.ModuleName, "SET_LINKS", new { links = "daoris" });

        Assert.True(answer.Success);
        await UntilAsync(() => Changed().Count == 1);
        Assert.Equal("""{"scopes":["browser"]}""", Shape(Assert.Single(Changed())));
    }

    /// <summary>A browser route's name on another module's request declares nothing.</summary>
    [Fact]
    public void A_browser_route_name_on_another_module_declares_nothing() =>
        Assert.Null(StatePublisher.Declared(new IpcRequest { Id = "x", Module = "DAORIS.WINDOWS", Type = "SET_LINKS" }));

    /// <summary>A request to another module is never the driver's, whatever its type is called.</summary>
    [Fact]
    public void Only_the_driver_s_requests_are_read_against_its_marks() =>
        Assert.Null(DriverModule.Declared(new IpcRequest { Id = "x", Module = "DAORIS.WINDOWS", Type = "LAND_SESSION_TREE" }));

    /// <summary>
    /// PUBLISHTWIN1: the page's tests press the driver through a seam that publishes what each press declares, as this publisher
    /// does (<c>src/Daoris.Web/src/test/publishing.ts</c>'s <c>DECLARED</c>, STATEHUB1f), a copy of the marks in another language
    /// with no compiler between them. Each press it names declares here what it says there, scope for scope in the wire's order,
    /// with the same session field, so a mark changed on one side alone fails.
    /// </summary>
    [Fact]
    public void The_page_s_publishing_seam_copies_each_press_s_mark()
    {
        var copied = PageDeclared();

        Assert.NotEmpty(copied);
        foreach (var (route, scopes, session, subject) in copied)
        {
            // The seam answers for the driver's routes and the browser module's, as the publisher reads both.
            var declared = StatePublisher.Declared(new IpcRequest { Id = "x", Module = DriverModule.Name, Type = route })
                ?? StatePublisher.Declared(new IpcRequest { Id = "x", Module = BrowserModule.Name, Type = route });
            // The route with each side's words, so a failure names the press and both its declarations.
            Assert.Equal(
                (route, scopes, session, subject),
                (route, string.Join(",", StatePublisher.Names(declared?.Scopes ?? StateScopes.None)), declared?.Session, declared?.Subject));
        }
    }

    /// <summary>
    /// PUBLISHTWIN2: the twin is two-way. A route that declares a change with no entry in the page's table is a press a test of the
    /// page could make and never hear the change of, so every mark of the driver's and the browser module's is in it.
    /// </summary>
    [Fact]
    public void Every_mark_has_an_entry_in_the_page_s_publishing_seam()
    {
        var copied = PageDeclared().Select(entry => entry.Route).ToHashSet(StringComparer.Ordinal);
        string[] browser = ["ADD_FAVORITE", "REMOVE_FAVORITE", "SET_EXTENSIONS", "SET_BROWSER", "SET_LINKS"];

        var marked = DriverModule.Routes
            .Where(route => DriverModule.Declared(new IpcRequest { Id = "x", Module = DriverModule.Name, Type = route }) is { Scopes: not StateScopes.None })
            .Concat(browser)
            .Where(route => !copied.Contains(route))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(marked.Count == 0, "marks with no entry in test/publishing.ts's DECLARED: " + string.Join(", ", marked));
    }

    /// <summary>
    /// The page's publishing seam's table, each press as it names it: its route, its scopes joined in their order, and the field
    /// naming its session. Every route the table holds is read, or the test fails: one the pattern missed would go unchecked.
    /// </summary>
    private static List<(string Route, string Scopes, string? Session, string? Subject)> PageDeclared()
    {
        var text = File.ReadAllText(Path.Combine(SourceFiles.WorkspaceFolder(), "src", "Daoris.Web", "src", "test", "publishing.ts"));
        var start = text.IndexOf("export const DECLARED", StringComparison.Ordinal);
        Assert.True(start >= 0, "the page's publishing seam names no DECLARED table");
        var table = text[start..text.IndexOf("\n};", start, StringComparison.Ordinal)];

        var copied = PageEntry().Matches(table)
            .Select(entry => (
                entry.Groups["route"].Value,
                string.Join(",", PageScope().Matches(entry.Groups["scopes"].Value).Select(scope => scope.Groups["scope"].Value)),
                entry.Groups["session"].Success ? entry.Groups["session"].Value : null,
                entry.Groups["subject"].Success ? entry.Groups["subject"].Value : null))
            .ToList();
        Assert.Equal(PageRoute().Matches(table).Count, copied.Count);
        return copied;
    }

    /// <summary>One press in the seam's table: <c>ROUTE: { scopes: ['a', 'b'], session: 'id', subject: 'entry' }</c>, over one line or several.</summary>
    [GeneratedRegex(@"\b(?<route>[A-Z][A-Z_]+):\s*\{\s*scopes:\s*\[(?<scopes>[^\]]*)\]\s*(?:,\s*session:\s*'(?<session>\w+)'\s*)?(?:,\s*subject:\s*'(?<subject>\w+)'\s*)?,?\s*\}")]
    private static partial Regex PageEntry();

    /// <summary>A scope's word in a press's list.</summary>
    [GeneratedRegex(@"'(?<scope>\w+)'")]
    private static partial Regex PageScope();

    /// <summary>A route's key at the start of a line of the table, however its value is written.</summary>
    [GeneratedRegex(@"^\s*[A-Z][A-Z_]+:", RegexOptions.Multiline)]
    private static partial Regex PageRoute();
}
