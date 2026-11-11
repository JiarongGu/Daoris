using System.Text.Json;

using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Every round of a plugin's command is a <c>plugin.command</c> line whichever door asked, and <i>Done and failed</i> reads from those
/// lines the rounds the person did not watch or that failed, by names alone.
/// </summary>
public sealed class PluginCommandNoticesTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-plugin-notices-" + Guid.NewGuid().ToString("N")[..8]);

    public PluginCommandNoticesTests()
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, "acme-tickets");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), """
            { "id": "acme-tickets", "hooks": { "command": ["node", "plugin.mjs"], "points": ["work/state"] },
              "interfaces": { "commands": [ { "name": "status", "about": "Says what is kept." } ] } }
            """);
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    internal sealed class Channel(string answer) : IHookChannel
    {
        public IReadOnlyList<string> Points => [];

        public bool Alive => true;

        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) => Task.FromResult(HookDecision.Allow);

        public Task EndedAsync(object payload, CancellationToken ct) => Task.CompletedTask;

        public void Listen(Action<JsonElement?>? steps) { }

        public Task WatchAsync(WorkWatchFrame frame, CancellationToken ct) => Task.CompletedTask;

        public Task<JsonElement> CommandAsync(object frame, CancellationToken ct) => Task.FromResult(JsonDocument.Parse(answer).RootElement.Clone());

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private async Task Round(string from, string answer, PluginPlace? place = null)
    {
        var plugin = PluginCatalog.Load(_home).Plugins.Single();
        var call = new CommandCall("status", CommandCall.NewRun(), new Dictionary<string, object?>(), new Dictionary<string, string>(), from, place ?? PluginPlace.Machine, "en");
        await PluginCommandRun.RoundAsync(_home, plugin, call, CancellationToken.None, (_, _) => Task.FromResult<IHookChannel>(new Channel(answer)));
    }

    [Fact]
    public async Task About_lists_the_last_twenty_rounds_newest_first_with_who_asked_where_and_how_it_ended()
    {
        for (var each = 0; each < 22; each++) await Round(CommandWire.Window, """{ "said": ["ok"], "exit": 0 }""");
        await Round(CommandWire.Agent, """{ "said": ["no"], "exit": 2 }""", new PluginPlace("engine"));

        var runs = PluginActivity.Read(_home, "acme-tickets", DateTimeOffset.UtcNow.AddDays(-1)).Runs;

        Assert.Equal(PluginActivitySummary.LastRuns, runs.Count);
        Assert.Equal(("status", "agent", "repository:engine", 2), (runs[0].Command, runs[0].From, runs[0].Place, runs[0].Exit));
        Assert.Equal(("window", "machine", 0), (runs[1].From, runs[1].Place, runs[1].Exit));
    }

    [Fact]
    public async Task A_round_from_any_door_is_a_line_with_its_place_and_the_notices_are_the_unwatched_and_the_failed()
    {
        await Round(CommandWire.Window, """{ "said": ["ok"], "exit": 0 }""", new PluginPlace("engine"));
        await Round(CommandWire.Window, """{ "said": ["no"], "exit": 3 }""");
        await Round(CommandWire.Agent, """{ "said": ["ok"], "exit": 0 }""");
        await Round(CommandWire.Removal, """{ "said": ["bye"], "exit": 0 }""");
        await Round(CommandWire.Terminal, """{ "ask": { "name": "token", "prompt": "Token", "masked": true } }""");

        var lines = MachineLogReader.Read(DaorisHome.Entry(_home, HomeEntry.Logs), new LogFilter(Event: PluginEvents.Command)).Lines;
        Assert.Equal(5, lines.Count);
        Assert.Equal("repository:engine", lines[0].Data.GetProperty("place").GetString());
        Assert.DoesNotContain("ok", lines[0].Raw.Replace("\"plugin\"", ""));

        var told = PluginCommandNotices.Occurrences(_home, DateTimeOffset.UtcNow).Select(each => each.Notice!).ToList();
        Assert.Equal(
            [("failed", "window"), ("reached", "agent"), ("reached", "removal")],
            told.Select(notice => (notice.State!, notice.Route!)).Order());
        Assert.All(told, notice => Assert.Equal(("plugin-command", "acme-tickets", "status"), (notice.Kind, notice.Code, notice.Act)));
    }
}
