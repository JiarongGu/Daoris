using System.Collections.Concurrent;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The home's record files on the change watch (FABRIC-EVENT4, the design §A4.1 and link 7): <c>driver.json</c>, the plugins
/// folder, <c>lines-moved.json</c> and the held words each wake their topic within the debounce, are found by the topic's
/// deadline when their source is silent, and the loop's own writes wake nothing. On the fake source.
/// </summary>
public sealed class HomeWatchTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-home-watch-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly FakeChangeSource _source = new();

    private readonly ConcurrentQueue<(string Topic, string Subject)> _woken = new();

    public HomeWatchTests()
    {
        Directory.CreateDirectory(_home);
        Directory.CreateDirectory(Plugins);
        Directory.CreateDirectory(Path.GetDirectoryName(HeldWordsFile.PathOf(_home))!);
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Config => Path.Combine(_home, "driver.json");

    private string Plugins => DaorisHome.Entry(_home, HomeEntry.Plugins);

    private string Lines => DaorisHome.Entry(_home, HomeEntry.LinesMoved);

    private string Words => HeldWordsFile.PathOf(_home);

    private HomeWatch Watching(TimeSpan every)
    {
        var watch = HomeWatch.ForDriver(_home, Config, (topic, subject) => _woken.Enqueue((topic, subject)), every, _source);
        watch.Start();
        return watch;
    }

    /// <summary>What a source says of a name, said on every handle open on its folder, as the system tells each watcher of it.</summary>
    private void Say(string folder, string name, ChangeKind kind = ChangeKind.Changed)
    {
        foreach (var handle in _source.Opened.Where(handle => handle.Folder == folder && !handle.Disposed)) handle.Raise(name, kind);
    }

    private static async Task Settle(HomeWatch watch, int judgements)
    {
        await Poll.Until(() => watch.Changes.Judgements >= judgements, () => $"{watch.Changes.Judgements} judgements");
        await watch.Changes.Settled();
    }

    /// <summary>Each file's move wakes its own topic within the debounce, with the deadline an hour away: only the hint can have done it.</summary>
    [Fact]
    public async Task Each_file_is_heard_within_the_debounce()
    {
        await using var watch = Watching(TimeSpan.FromHours(1));

        File.WriteAllText(Config, "{}");
        Say(_home, "driver.json", ChangeKind.Renamed);
        await Poll.Until(() => _woken.Contains((ChangeTopics.Config, "driver.json")), () => "the configuration was never heard");

        File.WriteAllText(Lines, "{}");
        Say(_home, "lines-moved.json", ChangeKind.Renamed);
        await Poll.Until(() => _woken.Contains((ChangeTopics.Lines, "lines-moved.json")), () => "the moved lines were never heard");

        File.WriteAllText(Words, "{}");
        Say(Path.GetDirectoryName(Words)!, "held-words.json", ChangeKind.Renamed);
        await Poll.Until(() => _woken.Contains((ChangeTopics.Words, "held-words.json")), () => "the held words were never heard");

        Directory.CreateDirectory(Path.Combine(Plugins, "one"));
        File.WriteAllText(Path.Combine(Plugins, "one", "plugin.json"), "{}");
        Say(Plugins, "one/plugin.json", ChangeKind.Created);
        await Poll.Until(() => _woken.Contains((ChangeTopics.Plugins, "plugins")), () => "the plugins were never heard");
    }

    /// <summary>With every source silent, each topic's deadline finds what moved, by reading it afresh.</summary>
    [Fact]
    public async Task Each_file_is_found_by_its_deadline_when_its_source_is_silent()
    {
        await using var watch = Watching(TimeSpan.FromMilliseconds(300));

        File.WriteAllText(Config, "{}");
        File.WriteAllText(Lines, "{}");
        File.WriteAllText(Words, "{}");
        Directory.CreateDirectory(Path.Combine(Plugins, "one"));
        File.WriteAllText(Path.Combine(Plugins, "one", "plugin.json"), "{}");

        foreach (var heard in new[] { (ChangeTopics.Config, "driver.json"), (ChangeTopics.Lines, "lines-moved.json"), (ChangeTopics.Words, "held-words.json"), (ChangeTopics.Plugins, "plugins") })
        {
            await Poll.Until(() => _woken.Contains(heard), () => $"{heard} was never found at its deadline");
        }
    }

    /// <summary>A deadline that finds nothing moved wakes nothing: the reconciliation is no look.</summary>
    [Fact]
    public async Task A_deadline_that_finds_nothing_wakes_nothing()
    {
        File.WriteAllText(Config, "{}");
        await using var watch = Watching(TimeSpan.FromMilliseconds(200));

        await Task.Delay(1200);

        Assert.Empty(_woken);
    }

    /// <summary>What the loop writes itself (a line it moved, the words it held) is marked, and its hint, though heard, wakes no look.</summary>
    [Fact]
    public async Task The_loops_own_write_wakes_nothing()
    {
        await using var watch = Watching(TimeSpan.FromHours(1));

        RegistryFollowing.Moved(_home, "repo", DateTimeOffset.UtcNow);
        Say(_home, "lines-moved.json", ChangeKind.Renamed);
        await Settle(watch, 1);

        File.WriteAllText(Words, "{\"held\":[]}");
        HomeWrites.Mine(Words);
        Say(Path.GetDirectoryName(Words)!, "held-words.json", ChangeKind.Renamed);
        await Settle(watch, 2);

        Assert.Empty(_woken);

        // A terminal's move to the same file afterwards is no mark of ours, and is heard.
        File.WriteAllText(Lines, "{\"moved\":{\"other\":\"2026-01-01T00:00:00Z\"}}");
        Say(_home, "lines-moved.json", ChangeKind.Renamed);
        await Poll.Until(() => _woken.Contains((ChangeTopics.Lines, "lines-moved.json")), () => "a move that was not ours was not heard");
    }

    /// <summary>A plugin's own data, and a temporary a writer renames away, are not the catalogue moving.</summary>
    [Fact]
    public async Task A_plugins_data_and_a_temporary_wake_nothing()
    {
        await using var watch = Watching(TimeSpan.FromHours(1));

        Directory.CreateDirectory(Path.Combine(Plugins, PluginCatalog.DataFolder, "one"));
        File.WriteAllText(Path.Combine(Plugins, PluginCatalog.DataFolder, "one", "state.json"), "{}");
        Say(Plugins, PluginCatalog.DataFolder + "/one/state.json");
        Say(Plugins, "one/plugin.json.tmp");
        Say(_home, "driver.json.tmp", ChangeKind.Created);
        Say(_home, "unrelated.json");
        await Settle(watch, 0);
        await Task.Delay(700);

        Assert.Empty(_woken);
        Assert.Equal(0, watch.Changes.Judgements);
    }

    /// <summary>A watcher lost its handle: what it missed is unknown, so the topic is judged at once and finds what moved.</summary>
    [Fact]
    public async Task A_lost_watcher_finds_what_it_missed()
    {
        await using var watch = Watching(TimeSpan.FromHours(1));
        File.WriteAllText(Config, "{}");

        foreach (var handle in _source.Opened.Where(handle => handle.Folder == _home)) handle.Lose();

        await Poll.Until(() => _woken.Contains((ChangeTopics.Config, "driver.json")), () => "the loss was never reconciled");
    }
}
