using System.Collections.Concurrent;
using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;
using CatalogueScope = Daoris.Driver.RoleScope;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A working session's asked press as an <c>allow</c> subject <c>{ press }</c> over the real pipe: raised at its session's place for the
/// allow supervisor with no card, allowed once as the session's own press, left or unanswered for ten minutes to the person's card once,
/// and asked of the person at once where no supervisor answers there or the press is Ask Daoris's.
/// </summary>
/// <remarks>Each test serves its own pipe in this process. Windows only.</remarks>
public sealed class SessionPressAllowTests : Bridge
{
    private const string Archived = "s1a2b3c4";

    private const string Session = "s-quest";

    private static readonly JsonSerializerOptions Wire = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly SessionDelegates _delegates = new();
    private readonly ConcurrentDictionary<string, string> _modes = new(StringComparer.Ordinal);

    private sealed class StandIn : ModuleBase
    {
        private int _ran;

        public override string ModuleName => DriverModule.Name;

        public int Ran => Volatile.Read(ref _ran);

        public ConcurrentQueue<string?> Workers { get; } = new();

        protected override Task<object?> RouteMessageAsync(IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _ran);
            Workers.Enqueue(PipeDoor.Worker);
            return Task.FromResult<object?>(new { Done = true });
        }
    }

    private sealed class Stand(InstancePipe pipe, PipeClient client, string nonce, StandIn driver, ServiceProvider services, MachineLog log) : IDisposable
    {
        public InstancePipe Pipe { get; } = pipe;

        public PipeClient Client { get; } = client;

        public string Nonce { get; } = nonce;

        public StandIn Driver { get; } = driver;

        public void Dispose()
        {
            Client.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Pipe.Dispose();
            services.Dispose();
            log.Dispose();
        }
    }

    private async Task<Stand> OpenAsync(bool supervised = true)
    {
        var log = new MachineLog(Home, "desktop");
        var driver = new StandIn();
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(Bus);
        services.AddSingleton<IIpcRequestTracker>(new IpcRequestTracker(Bus, new IpcRequestTrackerOptions { GracePeriod = TimeSpan.FromMilliseconds(50) }));
        services.AddSingleton<IIpcModule>(driver);
        services.UseMessageDispatcher((_, dispatcher) => dispatcher.Use(RefusalLog.Middleware(log)));
        var provider = services.BuildServiceProvider();
        var judge = new StandingConfirmation(Home, log, new StandingConfirmationTests.Placed(StandingPlace.Outside), walk: new StandingConfirmationTests.Walked())
        {
            Wider = () => RuleWiderPlaces.None,
        };
        if (supervised)
        {
            judge.Staff(RoleKit.Steward([]), RoleKit.Supervisor("supervisor-allows", CatalogueScope.Everywhere, decides: [SupervisorKinds.Allow],
                acts: [SupervisorKinds.Qualified(SupervisorKinds.Allow, SessionPressAllows.Act)]));
            var (supervising, _) = RoleKit.Start(judge, new HoldRequest("supervisor-allows", "own"));
            Assert.True(supervising.Answer.Done, supervising.Answer.Message);
        }

        var pipe = new InstancePipe(provider.GetRequiredService<IMessageDispatcher>(), Bus, Home, log, standing: judge)
        {
            Grounds = new StubGrounds().Session(Archived, "completed"),
        };
        pipe.Delegates = session => new PipeDelegateSession(_delegates.Of(session), _modes.GetValueOrDefault(session)) { Repository = "engine" };
        var client = await PipeClient.ConnectAsync(pipe.Serve()!);
        var hello = await client.HelloAsync(HomeIdentity.Read(Home)!);
        return new Stand(pipe, client, hello.GetProperty("data").GetProperty("nonce").GetString()!, driver, provider, log);
    }

    private async Task<string> PressAsync(Stand stand, string key)
    {
        var id = Guid.NewGuid().ToString();
        var requestKey = Guid.NewGuid().ToString("N");
        var text = JsonSerializer.Serialize(new { ids = new[] { Archived }, archived = true }, Wire);
        var proof = DelegateProof.Prove(key, stand.Nonce, DriverModule.Name, "SESSION_ARCHIVE", text, requestKey);
        var said = JsonSerializer.Serialize(new { session = Session, proof }, Wire);
        await stand.Client.SendLineAsync(
            $$"""{"id":"{{id}}","module":"{{DriverModule.Name}}","type":"SESSION_ARCHIVE","payload":{{text}},"key":"{{requestKey}}","{{DelegateProof.Field}}":{{said}}}""");
        return id;
    }

    private static async Task UntilAsync(Func<bool> condition, string because)
    {
        var until = DateTime.UtcNow + Poll.Patience;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < until, because);
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task An_asked_press_is_the_supervisors_subject_with_no_card_and_allowed_once_runs_as_the_session_s_own()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var stand = await OpenAsync();
        var id = await PressAsync(stand, _delegates.Draw(Session));

        await UntilAsync(() => stand.Pipe.SessionPresses.Waiting().Count == 1, "the press was not offered to the supervisor");
        var press = Assert.Single(stand.Pipe.SessionPresses.Waiting());
        Assert.Equal((Session, "engine", $"{DriverModule.Name}.SESSION_ARCHIVE"), (press.Session, press.Place.Repository, press.Route));
        Assert.Empty(stand.Pipe.Confirmations.Now().Waiting);
        Assert.Equal(0, stand.Driver.Ran);

        PendingPress? kept = null;
        Assert.Null(stand.Pipe.SessionPresses.Allow(press.Id, each => kept = each));
        Assert.Equal(press.Id, kept?.Id);
        Assert.True((await stand.Client.AnswerAsync(id)).GetProperty("success").GetBoolean());
        Assert.Equal(1, stand.Driver.Ran);
        Assert.Equal(Session, Assert.Single(stand.Driver.Workers));
        Assert.Empty(stand.Pipe.Confirmations.Now().Waiting);
        Assert.Empty(stand.Pipe.SessionPresses.Waiting());
        Assert.NotNull(stand.Pipe.SessionPresses.Allow(press.Id));
    }

    [Fact]
    public async Task A_press_the_supervisor_leaves_falls_to_the_persons_card_and_is_not_run()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var stand = await OpenAsync();
        await PressAsync(stand, _delegates.Draw(Session));
        await UntilAsync(() => stand.Pipe.SessionPresses.Waiting().Count == 1, "the press was not offered to the supervisor");
        var press = Assert.Single(stand.Pipe.SessionPresses.Waiting());

        Assert.True(stand.Pipe.SessionPresses.Leave(press.Id));

        await UntilAsync(() => stand.Pipe.Confirmations.Now().Waiting.Count == 1, "the card did not follow the leave");
        Assert.Equal(Session, Assert.Single(stand.Pipe.Confirmations.Now().Waiting).Asker.Session);
        Assert.Equal(0, stand.Driver.Ran);
        Assert.False(stand.Pipe.SessionPresses.Leave(press.Id));
        Assert.NotNull(stand.Pipe.SessionPresses.Allow(press.Id));
    }

    [Fact]
    public async Task A_press_unanswered_for_the_limit_falls_to_the_persons_card_once()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var stand = await OpenAsync();
        stand.Pipe.SessionPresses.Limit = TimeSpan.FromMilliseconds(300);
        await PressAsync(stand, _delegates.Draw(Session));

        await UntilAsync(() => stand.Pipe.Confirmations.Now().Waiting.Count == 1, "the card did not follow the wait");
        await Task.Delay(500);
        Assert.Single(stand.Pipe.Confirmations.Now().Waiting);
        Assert.Empty(stand.Pipe.SessionPresses.Waiting());
        Assert.Equal(0, stand.Driver.Ran);
    }

    [Fact]
    public async Task A_press_where_no_supervisor_answers_is_asked_of_the_person_at_once()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var stand = await OpenAsync(supervised: false);
        await PressAsync(stand, _delegates.Draw(Session));

        await UntilAsync(() => stand.Pipe.Confirmations.Now().Waiting.Count == 1, "the card was not asked");
        Assert.Empty(stand.Pipe.SessionPresses.Waiting());
    }

    [Fact]
    public async Task An_Ask_Daoris_press_is_never_the_supervisors_to_allow()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var stand = await OpenAsync();
        _modes[Session] = HelpModes.Restricted;
        await PressAsync(stand, _delegates.Draw(Session));

        await UntilAsync(() => stand.Pipe.Confirmations.Now().Waiting.Count == 1, "the card was not asked");
        Assert.Empty(stand.Pipe.SessionPresses.Waiting());
    }
}
