using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The staged update's folder and the request on the change watch (FABRIC-EVENT4, row 12): a build staged or a request said is
/// looked at when its file moves, within the debounce, instead of at a two-second pace; the topic's deadline finds it when the
/// source is silent; and the updater's own request write wakes no second look.
/// </summary>
public sealed class UpdateWatchTests : IDisposable
{
    private readonly string _install = Path.Combine(Path.GetTempPath(), "daoris-update-watch-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly Source _source = new();

    public UpdateWatchTests()
    {
        Directory.CreateDirectory(Path.Combine(_install, "app"));
        // An install's mark, which makes its data/ the install's home (D172 point 2, HOMEFIX1): unmarked, data/ is a root of its
        // own, the request is placed in its data/data/, and the watch never opens the folder this class says a change in.
        File.WriteAllText(Path.Combine(_install, InstallHome.Marker), "# Daoris — installed desktop\n");
        Directory.CreateDirectory(Path.Combine(_install, "update"));
        Directory.CreateDirectory(Data);
    }

    public void Dispose()
    {
        try { Directory.Delete(_install, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Data => Path.Combine(_install, "data");

    private InstallUpdater Updater(TimeSpan reconcile) =>
        new(_install, Data, MachineLog.None, () =>
            {
                Interlocked.Increment(ref _looks);
                return new UpdateWork(1, 0);
            },
            relaunch: _ => Task.FromResult(true), close: () => { }, pid: 1)
        {
            // An hour between looks of its own, so only the watch can have looked.
            Pace = TimeSpan.FromHours(1),
            Idle = TimeSpan.FromHours(1),
            Reconcile = reconcile,
            Source = _source,
        };

    private void Stage(string id)
    {
        var staged = Path.Combine(_install, "update", "staged");
        Directory.CreateDirectory(staged);
        File.WriteAllText(Path.Combine(staged, "build.json"), new JsonObject
        {
            ["schema"] = 1, ["id"] = id, ["version"] = "0.0.1", ["commit"] = "abc1234", ["at"] = "2026-10-03T12:00:00Z",
            ["files"] = new JsonArray(),
        }.ToJsonString());
    }

    private static async Task Until(Func<bool> condition, string seen)
    {
        for (var waited = 0; !condition(); waited += 50)
        {
            if (waited > 30_000) throw new TimeoutException(seen);
            await Task.Delay(50);
        }
    }

    /// <summary>With the pace and the deadline an hour away, the staged manifest's move is what the updater heard.</summary>
    [Fact]
    public async Task A_staged_build_is_heard_within_the_debounce()
    {
        using var updater = Updater(TimeSpan.FromHours(1));
        updater.Start();
        await Until(() => _source.Opened.Count > 0, "the watch never opened");

        Stage("b1");
        _source.Say(Path.Combine(_install, "update"), "staged/build.json", ChangeKind.Renamed);

        await Until(() => updater.State.Staged?.Id == "b1", "the staged build was never looked at");
    }

    /// <summary>The terminal's request, written to the home with no word from the source, is found at the topic's deadline.</summary>
    [Fact]
    public async Task A_staged_build_is_found_by_its_deadline_when_the_source_is_silent()
    {
        using var updater = Updater(TimeSpan.FromMilliseconds(300));
        updater.Start();
        await Until(() => _source.Opened.Count > 0, "the watch never opened");

        Stage("b2");

        await Until(() => updater.State.Staged?.Id == "b2", "the staged build was never found by its deadline");
    }

    /// <summary>The updater's own request write is marked: its hint wakes no look, while a terminal's would.</summary>
    [Fact]
    public async Task The_updaters_own_request_wakes_nothing()
    {
        Stage("b3");
        using var updater = Updater(TimeSpan.FromHours(1));
        updater.Start();
        await Until(() => _source.Opened.Count > 0, "the watch never opened");
        await Until(() => Volatile.Read(ref _looks) >= 1, "the first look never ran");

        await updater.SayAsync(UpdateMode.WhenIdle, "test");
        var after = Volatile.Read(ref _looks);
        _source.Say(Data, "update.json", ChangeKind.Renamed);
        await Task.Delay(900);

        Assert.Equal(after, Volatile.Read(ref _looks));

        // A terminal's word, written by another process, is no mark of ours: the same hint is heard.
        File.WriteAllText(InstallUpdate.PathOf(Data), "{\"mode\":\"not-now\",\"build\":\"b3\",\"at\":\"2026-10-03T12:00:00Z\"}\n");
        _source.Say(Data, "update.json", ChangeKind.Renamed);
        await Until(() => updater.State.State == UpdateStates.Waiting, "a terminal's request was not heard");
    }

    private int _looks;

    private sealed class Source : IChangeSource
    {
        private readonly object _gate = new();
        private readonly List<(string Folder, IChangeSink Sink)> _opened = [];

        public IReadOnlyList<(string Folder, IChangeSink Sink)> Opened
        {
            get
            {
                lock (_gate) return [.. _opened];
            }
        }

        public void Say(string folder, string name, ChangeKind kind)
        {
            foreach (var (watched, sink) in Opened.Where(open => open.Folder == folder)) sink.Moved(name, kind);
        }

        public IChangeHandle? Open(string folder, bool subfolders, IChangeSink sink)
        {
            lock (_gate) _opened.Add((folder, sink));
            return new Handle();
        }

        private sealed class Handle : IChangeHandle
        {
            public bool Same => true;

            public void Dispose()
            {
            }
        }
    }
}
