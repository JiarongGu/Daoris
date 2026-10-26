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

    private string Config => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    private string Plugins => DaorisHome.Entry(_home, HomeEntry.Plugins);

    private string Lines => DaorisHome.Entry(_home, HomeEntry.LinesMoved);

    private string Words => HeldWordsFile.PathOf(_home);

    private HomeWatch Watching(TimeSpan every, bool proposals = false)
    {
        void Wake(string topic, string subject) => _woken.Enqueue((topic, subject));
        var watch = proposals
            ? HomeWatch.ForProposals(_home, Wake, every, _source)
            : HomeWatch.ForDriver(_home, Config, Wake, every, _source);
        watch.Start();
        return watch;
    }

    private string Proposals => HelpProposals.FolderOf(_home);

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
        Say(Path.GetDirectoryName(Config)!, "driver.json", ChangeKind.Renamed);
        await Poll.Until(() => _woken.Contains((ChangeTopics.Config, "driver.json")), () => "the configuration was never heard");

        File.WriteAllText(Lines, "{}");
        Say(Path.GetDirectoryName(Lines)!, "lines-moved.json", ChangeKind.Renamed);
        await Poll.Until(() => _woken.Contains((ChangeTopics.Lines, "lines-moved.json")), () => "the moved lines were never heard");

        File.WriteAllText(Words, "{}");
        Say(Path.GetDirectoryName(Words)!, "held-words.json", ChangeKind.Renamed);
        await Poll.Until(() => _woken.Contains((ChangeTopics.Words, "held-words.json")), () => "the held words were never heard");

        Directory.CreateDirectory(Path.Combine(Plugins, "one"));
        File.WriteAllText(Path.Combine(Plugins, "one", "plugin.json"), "{}");
        Say(Plugins, "one/plugin.json", ChangeKind.Created);
        await Poll.Until(() => _woken.Contains((ChangeTopics.Plugins, "plugins")), () => "the plugins were never heard");
    }

    /// <summary>
    /// LIMITRESUME1c (D206 §3.4): the cooling store and the harness settings wake the <c>accounts</c> topic within the debounce, so an
    /// account freed early (<i>Try now</i>, a sign-in, an account added) wakes the look whose limits stage reads its stamp.
    /// </summary>
    [Fact]
    public async Task The_cooling_store_and_the_harness_settings_wake_the_accounts_topic()
    {
        await using var watch = Watching(TimeSpan.FromHours(1));

        var cooling = AccountCooling.PathOf(_home);
        Directory.CreateDirectory(Path.GetDirectoryName(cooling)!);
        File.WriteAllText(cooling, "{}");
        Say(Path.GetDirectoryName(cooling)!, Path.GetFileName(cooling), ChangeKind.Renamed);
        await Poll.Until(() => _woken.Contains((ChangeTopics.Accounts, ChangeTopics.Accounts)), () => "the cooling store was never heard");

        while (_woken.TryDequeue(out _))
        {
        }

        var settings = DaorisHome.Entry(_home, HomeEntry.Harnesses);
        Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
        File.WriteAllText(settings, "{}");
        Say(Path.GetDirectoryName(settings)!, Path.GetFileName(settings), ChangeKind.Renamed);
        await Poll.Until(() => _woken.Contains((ChangeTopics.Accounts, ChangeTopics.Accounts)), () => "the harness settings were never heard");
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
        Say(Path.GetDirectoryName(Lines)!, "lines-moved.json", ChangeKind.Renamed);
        await Settle(watch, 1);

        File.WriteAllText(Words, "{\"held\":[]}");
        HomeWrites.Mine(Words);
        Say(Path.GetDirectoryName(Words)!, "held-words.json", ChangeKind.Renamed);
        await Settle(watch, 2);

        Assert.Empty(_woken);

        // A terminal's move to the same file afterwards is no mark of ours, and is heard.
        File.WriteAllText(Lines, "{\"moved\":{\"other\":\"2026-01-01T00:00:00Z\"}}");
        Say(Path.GetDirectoryName(Lines)!, "lines-moved.json", ChangeKind.Renamed);
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
        Say(Path.GetDirectoryName(Config)!, "driver.json.tmp", ChangeKind.Created);
        Say(Path.GetDirectoryName(Config)!, "unrelated.json");
        await Settle(watch, 0);
        await Task.Delay(700);

        Assert.Empty(_woken);
        Assert.Equal(0, watch.Changes.Judgements);
    }

    /// <summary>
    /// FABRIC-EVENT5a: a proposal written to Ask Daoris's folder by another process is heard as the <c>help</c> topic, found by the
    /// deadline where the source is silent, and a proposal rewritten with its state kept (a card's units kept) or a claim is no new proposal.
    /// </summary>
    [Fact]
    public async Task A_proposal_written_is_heard_and_found_by_its_deadline_and_a_rewrite_wakes_nothing()
    {
        Directory.CreateDirectory(Proposals);
        await using (var heard = Watching(TimeSpan.FromHours(1), proposals: true))
        {
            File.WriteAllText(Path.Combine(Proposals, "p1.json"), "{\"state\":\"proposed\"}");
            Say(Proposals, "p1.json", ChangeKind.Created);
            await Poll.Until(() => _woken.Contains((ChangeTopics.Help, "proposals")), () => "the proposal was never heard");

            // A card's units kept: the same file again, its state kept, which is no proposal arriving. A claim is no proposal either.
            _woken.Clear();
            File.WriteAllText(Path.Combine(Proposals, "p1.json"), "{\"state\":\"proposed\",\"units\":[\"a\"]}");
            File.WriteAllText(Path.Combine(Proposals, "p1.claim"), "");
            Say(Proposals, "p1.json");
            Say(Proposals, "p1.claim", ChangeKind.Created);
            await Settle(heard, 2);
            await Task.Delay(500);
            Assert.Empty(_woken);
        }

        _woken.Clear();
        await using var silent = Watching(TimeSpan.FromMilliseconds(300), proposals: true);
        File.WriteAllText(Path.Combine(Proposals, "p2.json"), "{}");
        await Poll.Until(() => _woken.Contains((ChangeTopics.Help, "proposals")), () => "the proposal was never found at its deadline");
    }

    /// <summary>
    /// HELP-PROPOSAL-SETTLED1: a proposal settled by another process (a terminal's Apply, through <see cref="HelpProposals.Settle"/>)
    /// rewrites the file under its name, and is heard within the debounce, with the deadline an hour away, so an open window's card
    /// goes; and found by the deadline where the source is silent.
    /// </summary>
    [Fact]
    public async Task A_proposal_settled_from_another_process_is_heard()
    {
        Directory.CreateDirectory(Proposals);
        File.WriteAllText(Path.Combine(Proposals, "p1.json"), "{\"id\":\"p1\",\"state\":\"proposed\"}");
        File.WriteAllText(Path.Combine(Proposals, "p2.json"), "{\"id\":\"p2\",\"state\":\"proposed\"}");
        await using (var heard = Watching(TimeSpan.FromHours(1), proposals: true))
        {
            HelpProposals.Settle(_home, "p1", "applied", "done from a terminal");
            Say(Proposals, "p1.json", ChangeKind.Renamed);
            await Poll.Until(() => _woken.Contains((ChangeTopics.Help, "proposals")), () => "the settle was never heard");
        }

        _woken.Clear();
        await using var silent = Watching(TimeSpan.FromMilliseconds(300), proposals: true);
        HelpProposals.Settle(_home, "p2", "dismissed", null);
        await Poll.Until(() => _woken.Contains((ChangeTopics.Help, "proposals")), () => "the settle was never found at its deadline");
    }

    /// <summary>
    /// NOTIFY2f (the notification design §2.5): <c>notices.json</c> written by another process is heard as the <c>notices</c> topic, and
    /// found by the deadline where the source is silent; this process's own write through <see cref="Notices"/> wakes nothing, its writer
    /// pushing it.
    /// </summary>
    [Fact]
    public async Task The_notices_record_written_elsewhere_is_heard_and_found_by_its_deadline_and_its_own_write_wakes_nothing()
    {
        var path = Notices.PathOf(_home);
        var now = DateTimeOffset.UtcNow;
        Assert.Null(Notices.Add(_home, new Notice("ended-s0", Notices.Ended, now), now));
        void Wake(string topic, string subject) => _woken.Enqueue((topic, subject));
        await using (var heard = HomeWatch.ForNotices(_home, Wake, TimeSpan.FromHours(1), _source))
        {
            heard.Start();
            Assert.Null(Notices.Add(_home, new Notice("ended-s1", Notices.Ended, now), now));
            Say(Path.GetDirectoryName(path)!, "notices.json", ChangeKind.Renamed);
            await Settle(heard, 1);
            await Task.Delay(500);
            Assert.Empty(_woken);

            File.WriteAllText(path, File.ReadAllText(path).Replace("\"read\": false", "\"read\": true", StringComparison.Ordinal));
            Say(Path.GetDirectoryName(path)!, "notices.json");
            await Poll.Until(() => _woken.Contains((ChangeTopics.Notices, "notices.json")), () => "another process's write was never heard");
        }

        _woken.Clear();
        await using var silent = HomeWatch.ForNotices(_home, Wake, TimeSpan.FromMilliseconds(300), _source);
        silent.Start();
        File.WriteAllText(path, File.ReadAllText(path) + "\n");
        await Poll.Until(() => _woken.Contains((ChangeTopics.Notices, "notices.json")), () => "the write was never found at its deadline");
    }

    /// <summary>The driving loop's watch hears no proposals folder: the window's own process watches it, whoever holds the lease.</summary>
    [Fact]
    public async Task The_proposals_folder_is_watched_only_where_asked()
    {
        Directory.CreateDirectory(Proposals);
        await using var watch = Watching(TimeSpan.FromHours(1));

        Assert.DoesNotContain(_source.Opened, handle => handle.Folder == Proposals);
    }

    /// <summary>A watcher lost its handle: what it missed is unknown, so the topic is judged at once and finds what moved.</summary>
    [Fact]
    public async Task A_lost_watcher_finds_what_it_missed()
    {
        await using var watch = Watching(TimeSpan.FromHours(1));
        File.WriteAllText(Config, "{}");

        foreach (var handle in _source.Opened.Where(handle => handle.Folder == Path.GetDirectoryName(Config))) handle.Lose();

        await Poll.Until(() => _woken.Contains((ChangeTopics.Config, "driver.json")), () => "the loss was never reconciled");
    }
}
