using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// HOMELAYOUT3's move against the machine (the install layout design §7.1, §8.5): what holds a path the move would rename is a
/// real process or a real open file, so these start one or hold one, and end it. A process running from under the install holds
/// the whole move off; a file another holds open keeps its entry where it is while the rest moves; a lease a live process holds
/// is never moved and moves nothing under it.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class HomePlacingProcessTests : IDisposable
{
    private readonly string _install = Path.Combine(Path.GetTempPath(), "daoris-home-placing-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly List<Process> _spawned = [];

    public HomePlacingProcessTests()
    {
        Directory.CreateDirectory(Path.Combine(_install, "app"));
        File.WriteAllText(Path.Combine(_install, InstallHome.Marker), "# Daoris — installed desktop\n");
        foreach (var file in (string[])["data/driver.json", "data/keys.json", "data/logs/2026-10-19.desktop.jsonl", "data/locks/records/r.lock"])
        {
            var path = Path.Combine([_install, .. file.Split('/')]);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, file);
        }
    }

    public void Dispose()
    {
        foreach (var process in _spawned)
        {
            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(10_000);
            }
            catch (InvalidOperationException)
            {
            }

            process.Dispose();
        }

        try { Directory.Delete(_install, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Home => Path.Combine(_install, InstallHome.Folder);

    private HomeEstablished? Start(Func<string, IReadOnlyList<int>>? others = null)
    {
        var process = new Dictionary<string, string>(StringComparer.Ordinal) { [DaorisHome.Variable] = Home };
        return InstallHome.Establish(
            _install,
            name => process.TryGetValue(name, out var value) ? value : null,
            (name, value) => process[name] = value,
            () => Home,
            _ => { },
            Path.Combine(_install, "no-legacy"),
            othersRunning: others ?? HomePlacing.OthersRunningFrom);
    }

    /// <summary>
    /// 🔴 A program running from under the install (here a copy of the system's command shell in its <c>app/</c>, waiting) is a
    /// process of the folder: the move is held off whole, nothing is renamed, and once it ends the next start moves.
    /// </summary>
    [Fact]
    public void A_process_running_from_the_install_holds_the_whole_move_off()
    {
        if (!OperatingSystem.IsWindows()) return;
        var shell = Path.Combine(_install, "app", "stand-in.exe");
        File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), shell);
        var child = Process.Start(new ProcessStartInfo(shell, ["/c", "ping", "-n", "60", "127.0.0.1"])
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        })!;
        _spawned.Add(child);
        Assert.True(SpinWait.SpinUntil(() => HomePlacing.OthersRunningFrom(_install).Contains(child.Id), TimeSpan.FromSeconds(10)));

        var held = Start();

        Assert.Empty(held!.Placed);
        Assert.Contains($"{child.Id}", held.PlaceHeld);
        Assert.True(File.Exists(Path.Combine(Home, "keys.json")));

        child.Kill(entireProcessTree: true);
        child.WaitForExit(10_000);
        var moved = Start();
        Assert.Contains("keys", moved!.Placed.Select(move => move.Entry));
        Assert.True(File.Exists(Path.Combine(_install, "profiles", "keys.json")));
    }

    /// <summary>
    /// 🔴 The design §8.5: a log file another holds open keeps the logs where they are (Windows refuses the folder's rename), said
    /// with why; the rest moves; the next start, the file let go, moves the logs.
    /// </summary>
    [Fact]
    public void A_log_held_open_keeps_the_logs_where_they_are_while_the_rest_moves()
    {
        if (!OperatingSystem.IsWindows()) return;
        var log = Path.Combine(Home, "logs", "2026-10-19.desktop.jsonl");
        Dictionary<string, HomeMove> placed;
        HomeMove notPlaced;
        using (new FileStream(log, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
        {
            var first = Start(_ => []);
            placed = first!.Placed.ToDictionary(move => move.Entry);
            notPlaced = Assert.Single(first.NotPlaced);
        }

        Assert.Equal("logs", notPlaced.Entry);
        Assert.False(string.IsNullOrWhiteSpace(notPlaced.Reason));
        Assert.Contains("keys", placed.Keys);
        Assert.Contains("locks", placed.Keys);
        Assert.True(File.Exists(log));

        var second = Start(_ => []);
        Assert.Equal(["logs"], second!.Placed.Select(move => move.Entry));
        Assert.True(File.Exists(Path.Combine(_install, "logs", "2026-10-19.desktop.jsonl")));
    }

    /// <summary>
    /// 🔴 A held lease is never renamed under itself (D172's HOMELAYOUT3 note): a lease at the old place naming a live process (a
    /// build from before the switch, here a waiting node) holds the move off and stays where it is; its holder gone, the rest
    /// moves and the lease, now stale, is still left where it was.
    /// </summary>
    [Fact]
    public void A_lease_a_live_process_holds_is_never_moved_and_moves_nothing_under_it()
    {
        var child = Process.Start(new ProcessStartInfo("node", ["-e", "setTimeout(() => {}, 60000)"])
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        _spawned.Add(child);
        var lease = Path.Combine(Home, HomeLease.FileName);
        File.WriteAllText(lease, new JsonObject
        {
            ["kind"] = "headless", ["pid"] = child.Id, ["started"] = child.StartTime.ToUniversalTime().Ticks,
            ["since"] = "2026-10-20T08:00:00+00:00",
        }.ToJsonString());

        var held = Start(_ => []);

        Assert.Empty(held!.Placed);
        Assert.Contains("headless", held.PlaceHeld);
        Assert.True(File.Exists(lease));
        Assert.True(File.Exists(Path.Combine(Home, "keys.json")));

        child.Kill(entireProcessTree: true);
        child.WaitForExit(10_000);
        var moved = Start(_ => []);
        Assert.Contains("keys", moved!.Placed.Select(move => move.Entry));
        Assert.True(File.Exists(lease), "a stale lease at the old place is left where it is");
    }
}
