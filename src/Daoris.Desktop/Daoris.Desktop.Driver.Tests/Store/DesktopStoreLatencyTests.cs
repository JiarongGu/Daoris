using System.Diagnostics;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 z-i's measurement of the write latency <c>synchronous = FULL</c> costs (D212, the design §13.4): a claim or a mark lost to a
/// power cut could repeat an effect, so every commit is synced to the disk, at a write rate low enough for it. Each write here is what a
/// family's write is, one transaction under the write lock that moves its revision and touches the wake file, on this machine's disk in
/// the scratch folder, alone and then with a second process-shaped writer taking turns on the same file.
/// </summary>
/// <remarks>
/// A measurement: it asserts that every write landed and that none was pathological (a bound of seconds, which a busy wait that took the
/// writes would break), and says the percentiles in the test's output for D212's note. The install's own figure is the install's, read on
/// the next real run; a scratch folder on a developer's disk is no install. No variable of the process's environment is set.
/// </remarks>
public sealed class DesktopStoreLatencyTests : IDisposable
{
    private const int Writes = 400;

    private readonly ITestOutputHelper _said;

    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "desktop-store-latency", Guid.NewGuid().ToString("N")[..8]);

    public DesktopStoreLatencyTests(ITestOutputHelper said)
    {
        _said = said;
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static double Percentile(IReadOnlyList<double> sorted, double p) => sorted[Math.Clamp((int)Math.Ceiling(p * sorted.Count) - 1, 0, sorted.Count - 1)];

    private static double[] Time(DesktopStore store, string family, int count)
    {
        var times = new double[count];
        for (var each = 0; each < count; each++)
        {
            var clock = Stopwatch.StartNew();
            store.Write(family, write => write.Execute(
                "INSERT INTO imports (family, path, size, sha256, at, build, rows, class) VALUES ($family, $path, 1, 'x', 't', 'b', 0, 'left')",
                ("$family", family), ("$path", each.ToString(CultureInfo.InvariantCulture))));
            times[each] = clock.Elapsed.TotalMilliseconds;
        }

        return times;
    }

    private string Say(string label, double[] times)
    {
        var sorted = times.Order().ToArray();
        var line = string.Create(CultureInfo.InvariantCulture, $"{label}: {times.Length} writes, p50 {Percentile(sorted, 0.5):F1} ms, p95 {Percentile(sorted, 0.95):F1} ms, max {sorted[^1]:F1} ms");
        _said.WriteLine(line);
        return line;
    }

    [Fact]
    public void A_write_synchronous_full_costs_this_much_alone_and_beside_a_second_writer_and_none_is_pathological()
    {
        using var store = DesktopStore.Open(Path.Combine(_root, "desktop.db"), new DesktopStoreOptions { Kind = "shell", Settled = () => StoreSettled.Yes });
        Assert.Equal(2L, store.Query("PRAGMA synchronous", reader => reader.GetInt64(0))[0]);   // FULL

        var alone = Time(store, "alone", Writes);
        Say("alone", alone);

        using var other = DesktopStore.Open(Path.Combine(_root, "desktop.db"), new DesktopStoreOptions { Kind = "host", Settled = () => StoreSettled.Yes });
        using var stop = new CancellationTokenSource();
        var beside = Task.Run(() =>
        {
            var count = 0;
            while (!stop.IsCancellationRequested)
            {
                other.Write("other", write => write.Execute(
                    "INSERT INTO imports (family, path, size, sha256, at, build, rows, class) VALUES ('other', $path, 1, 'x', 't', 'b', 0, 'left')",
                    ("$path", (count++).ToString(CultureInfo.InvariantCulture))));
            }

            return count;
        });
        var shared = Time(store, "shared", Writes);
        stop.Cancel();
        var theirs = beside.GetAwaiter().GetResult();
        Say($"beside a second writer committing as fast as it can ({theirs} of its own)", shared);

        Assert.Equal(Writes, store.Query("SELECT COUNT(*) FROM imports WHERE family = 'alone'", reader => reader.GetInt64(0))[0]);
        Assert.Equal(Writes, store.Query("SELECT COUNT(*) FROM imports WHERE family = 'shared'", reader => reader.GetInt64(0))[0]);
        Assert.True(alone.Max() < 5000 && shared.Max() < 5000, "a write took seconds: a busy wait is holding the writes");
    }
}
