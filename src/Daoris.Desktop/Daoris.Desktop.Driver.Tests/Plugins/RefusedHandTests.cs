using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A place whose hand is refused (the identity serving its origin holds no API token) is not a plugin that cannot start: `plugin try`
/// takes no hand and passes, while the loop never reaches the start. The refusal is its own kind, never counted as an exit, so it is
/// never marked unsound, is asked again once a minute and not at every look, and the process starts once the token is there.
/// </summary>
public sealed class RefusedHandTests : IDisposable
{
    private const string Pr = "acme-pull-request";
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-refused-" + Guid.NewGuid().ToString("N")[..8]);
    private DateTimeOffset _now = new(2026, 11, 10, 15, 27, 0, TimeSpan.Zero);
    private readonly List<string?> _started = [];
    private readonly MachineLog _log;
    private string? _token;
    private int _asked;

    public RefusedHandTests()
    {
        Directory.CreateDirectory(_home);
        _log = new MachineLog(_home, "desktop", () => _now);
    }

    public void Dispose()
    {
        _log.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private sealed class Channel : IHookChannel
    {
        public IReadOnlyList<string> Points => [HookPoints.Watch, HookPoints.Step, HookPoints.Perform];

        public IReadOnlyList<string> Capabilities => [WorkWire.Capability];

        public bool Alive => true;

        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) => Task.FromResult(HookDecision.Allow);

        public Task EndedAsync(object payload, CancellationToken ct) => Task.CompletedTask;

        public void Listen(Action<JsonElement?>? steps) { }

        public Task WatchAsync(WorkWatchFrame frame, CancellationToken ct) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private HookSet Set() => new(_home, say: (_, _) => { }, holdsHome: () => true, clock: () => _now, log: _log,
        hostToken: (_, place, _) =>
        {
            _asked++;
            return Task.FromResult(_token is { } token
                ? new HostTokenHand(token, null)
                : new HostTokenHand(null, $"work has no API token set for dev.azure.com, so the plugin was not started"));
        },
        start: (_, _, _) =>
        {
            _started.Add(HookProcess.HostToken.Value);
            return Task.FromResult<IHookChannel>(new Channel());
        });

    private PluginCatalog Catalog()
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, Pr);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), JsonSerializer.Serialize(new
        {
            id = Pr,
            hooks = new { command = new[] { "node", "plugin.mjs" }, points = new[] { HookPoints.Watch, HookPoints.Step, HookPoints.Perform } },
            steps = new[] { new { id = "pull-request", reach = "performed" } },
        }));
        return PluginCatalog.Load(_home);
    }

    private static Dictionary<string, IReadOnlyList<WatchedUnit>> Awaiting() => new()
    {
        [Pr] = [new WatchedUnit("engine", "game", "C:/fam/engine", "quest/a", Commit, "main", null, ["pull-request"]) { Unit = "u1" }],
    };

    private List<(string? Where, string? Kind)> Failed() =>
        [.. MachineLogReader.Read(DaorisHome.Entry(_home, HomeEntry.Logs), new LogFilter()).Lines
            .Where(line => line.Event == PluginEvents.Failed)
            .Select(line => (Text(line, "where"), Text(line, "kind")))];

    private static string? Text(LogLine line, string key) =>
        line.Data.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    [Fact]
    public async Task A_refused_hand_is_said_as_refused_never_unsound_and_the_plugin_starts_once_the_token_is_set()
    {
        await using var set = Set();
        set.Watch(Awaiting());
        var catalog = Catalog();

        // Six looks over twenty minutes, as the installed build looked: one line, of its own kind, and never unsound.
        for (var look = 0; look < 6; look++)
        {
            await set.ReconcileAsync(catalog, CancellationToken.None);
            _now = _now.AddMinutes(4);
        }

        Assert.Empty(_started);
        Assert.Equal([(PluginEvents.AtStart, PluginEvents.Refused)], Failed());
        Assert.Equal(6, _asked);

        // Within the minute, the hand is not asked again at every look.
        await set.ReconcileAsync(catalog, CancellationToken.None);
        _now = _now.AddSeconds(5);
        await set.ReconcileAsync(catalog, CancellationToken.None);
        Assert.Equal(7, _asked);

        // The person sets the token: the next look past the minute starts it, handed that token, with no press.
        _token = "the-api-token";
        _now = _now.AddMinutes(1);
        await set.ReconcileAsync(catalog, CancellationToken.None);

        Assert.Equal(["the-api-token"], _started);
        Assert.Equal([Pr], set.Running);
    }
}
