using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// What a route changed reaches every window as <c>STATE_CHANGED</c> (STATEHUB1b, D169): published by the dispatcher's
/// middleware after the route answered, from what the route declares on its mark, never by the handler.
/// </summary>
/// <remarks>
/// Composed as the shell composes its dispatcher (the kit's <c>UseMessageDispatcher</c>, the middleware in its application
/// slot), over the kit's real bus, which the bridge's base subscribes to: so what is asserted is the event a window receives.
/// </remarks>
public sealed class StatePublisherTests : DriverModuleBridge
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

    private IMessageDispatcher Compose(IIpcModule module, Func<IpcRequest, StateChange?>? declared = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(Bus);
        services.AddSingleton(module);
        services.UseMessageDispatcher((_, dispatcher) => dispatcher.Use(
            declared is null ? StatePublisher.Middleware(Bus) : StatePublisher.Middleware(Bus, declared)));
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
    [InlineData("DISCARD_SESSION_TREE", "sessions,landing,diff,sweep,treesSync", "id")]
    [InlineData("HANDOFF", "handOff,sweep", "id")]
    [InlineData("ASK_OPINION", "sessions,landing", "id")]
    [InlineData("OPINION_ANYWAY", "sessions,landing", "id")]
    [InlineData("OPINION_MYSELF", "sessions,landing", "id")]
    [InlineData("STOP_OPINION", "sessions,landing", null)]
    [InlineData("SWEEP", "sessions,landing,diff,handOff,sweep,treesSync", null)]
    [InlineData("DISCARD_SESSION_BRANCH", "sessions,landing,diff,handOff,sweep,treesSync", null)]
    [InlineData("TREES_SYNC", "sessions,landing,diff,handOff,sweep,treesSync", null)]
    [InlineData("SET_LANDING", "landing,lines", null)]
    [InlineData("LANDING", "", null)]
    [InlineData("OPINION_GATE", "", null)]
    [InlineData("OPINION_WAITS", "", null)]
    [InlineData("HANDOFF_PLAN", "", null)]
    [InlineData("SWEEP_PLAN", "", null)]
    [InlineData("TREES_SYNC_PLAN", "", null)]
    [InlineData("TREES_SYNC_SCOPE", "", null)]
    public void The_landing_family_declares_what_it_changes(string route, string scopes, string? session)
    {
        var declared = DriverModule.Declared(new IpcRequest { Id = "x", Module = DriverModule.Name, Type = route });

        Assert.Equal(scopes, string.Join(",", StatePublisher.Names(declared?.Scopes ?? StateScopes.None)));
        Assert.Equal(session, declared?.Session);
    }

    /// <summary>A request to another module is never the driver's, whatever its type is called.</summary>
    [Fact]
    public void Only_the_driver_s_requests_are_read_against_its_marks() =>
        Assert.Null(DriverModule.Declared(new IpcRequest { Id = "x", Module = "DAORIS.WINDOWS", Type = "LAND_SESSION_TREE" }));
}
