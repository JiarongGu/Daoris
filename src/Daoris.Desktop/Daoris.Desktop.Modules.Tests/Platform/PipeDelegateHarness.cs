using System.Collections.Concurrent;
using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// What a test of a session's proved press needs: a pipe served in this process over a driver stand-in, the keys, modes, access and
/// families the pipe reads for a session, and a press sent with its proof. Each test serves its own pipe, named by its own scratch home's id.
/// </summary>
public abstract class PipeDelegateHarness : Bridge
{
    internal const string Archived = "s1a2b3c4";

    internal static readonly JsonSerializerOptions Wire = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    internal readonly SessionDelegates _delegates = new();
    internal readonly ConcurrentDictionary<string, string> _modes = new(StringComparer.Ordinal);
    internal readonly ConcurrentQueue<(string Session, string Route)> _said = new();
    internal readonly ConcurrentDictionary<string, string> _access = new(StringComparer.Ordinal);
    internal readonly ConcurrentDictionary<string, IReadOnlyList<string>> _families = new(StringComparer.Ordinal);
    internal readonly ConcurrentDictionary<string, HelpPressPlace> _places = new(StringComparer.Ordinal);

    /// <summary>The driver's stand-in: each press counted by route and answered done, with the session it ran as and whether it named the owner's.</summary>
    internal sealed class StandIn : ModuleBase
    {
        private readonly ConcurrentDictionary<string, int> _ran = new(StringComparer.Ordinal);

        public override string ModuleName => DriverModule.Name;

        public ConcurrentQueue<(string? Worker, bool Owned)> Doors { get; } = new();

        /// <summary>What a test makes the route do in place of the canned answer: the route's own code over a world of its own.</summary>
        public Func<IpcRequest, Task<object?>>? Press { get; init; }

        public int Ran(string route) => _ran.GetValueOrDefault(route);

        protected override Task<object?> RouteMessageAsync(IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
        {
            _ran.AddOrUpdate(request.Type, 1, (_, count) => count + 1);
            Doors.Enqueue((PipeDoor.Worker, DriverModule.OwnerDoor(request.Type, request.Payload) is { Count: > 0 }));
            if (Press is { } press) return press(request);
            return Task.FromResult<object?>(request.Type == "REGISTRY_CREATE"
                ? new { Done = true, Receipt = new { Kind = "opened", Repository = "ledger", Workspace = "aurora", Folder = "/work/aurora/ledger", Line = "main", Quests = new[] { "q7" }, Done = true } }
                : new { Done = true });
        }
    }

    internal async Task<(InstancePipe Pipe, PipeClient Client, string Nonce, StandIn Driver, ServiceProvider Services, MachineLog Log)> ServeAsync(
        bool delegates = true, Func<IpcRequest, Task<object?>>? press = null)
    {
        var log = new MachineLog(Home, "desktop");
        var driver = new StandIn { Press = press };
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(Bus);
        services.AddSingleton<IIpcRequestTracker>(new IpcRequestTracker(Bus, new IpcRequestTrackerOptions { GracePeriod = TimeSpan.FromMilliseconds(50) }));
        services.AddSingleton<IIpcModule>(driver);
        services.UseMessageDispatcher((_, dispatcher) => dispatcher.Use(RefusalLog.Middleware(log)));
        var provider = services.BuildServiceProvider();
        var pipe = new InstancePipe(provider.GetRequiredService<IMessageDispatcher>(), Bus, Home, log, version: "9.9.9-test", clock: new HeldClock())
        {
            Grounds = new StubGrounds().Session(Archived, "completed"),
        };
        if (delegates)
        {
            pipe.Delegates = session => new PipeDelegateSession(
                _delegates.Of(session), _modes.GetValueOrDefault(session), route => _said.Enqueue((session, route)))
            {
                Permissions = _access.GetValueOrDefault(session),
                Families = _families.GetValueOrDefault(session) ?? [],
                PlaceOf = _places.TryGetValue(session, out var place) ? (_, _) => Task.FromResult<HelpPressPlace?>(place) : null,
            };
        }

        var client = await PipeClient.ConnectAsync(pipe.Serve()!);
        var hello = await client.HelloAsync(HomeIdentity.Read(Home)!);
        return (pipe, client, hello.GetProperty("data").GetProperty("nonce").GetString()!, driver, provider, log);
    }

    /// <summary>A driver press as the verb sends a session's: its text proved (or <paramref name="proofOver"/>), beside <c>delegate: { session, proof }</c>.</summary>
    internal static async Task<string> SendProvedAsync(
        PipeClient client, string route, object payload, string session, string key, string nonce, string? proofOver = null)
    {
        var id = Guid.NewGuid().ToString();
        var requestKey = Guid.NewGuid().ToString("N");
        var text = JsonSerializer.Serialize(payload, Wire);
        var proof = DelegateProof.Prove(key, nonce, DriverModule.Name, route, proofOver ?? text, requestKey);
        var said = JsonSerializer.Serialize(new { session, proof }, Wire);
        await client.SendLineAsync(
            $$"""{"id":"{{id}}","module":"{{DriverModule.Name}}","type":"{{route}}","payload":{{text}},"key":"{{requestKey}}","{{DelegateProof.Field}}":{{said}}}""");
        return id;
    }

    internal static string? Code(JsonElement answer) =>
        answer.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object ? error.GetProperty("code").GetString() : null;

    /// <summary>The machine log's <paramref name="event"/> lines' data, in order.</summary>
    internal List<string> Logged(string @event)
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
            .Select(line => JsonDocument.Parse(line).RootElement)
            .Where(line => line.GetProperty("event").GetString() == @event)
            .Select(line => line.GetProperty("data").GetRawText()),
        ];
    }

    internal static async Task UntilAsync(Func<bool> condition)
    {
        var patience = DateTime.UtcNow + Poll.Patience;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < patience, "the condition never held");
            await Task.Delay(25);
        }
    }
}
