using System.Diagnostics;
using System.Globalization;
using Daoris.Knowledge;
using Daoris.Ports.Sessions;

namespace Daoris.Knowledge.Http;

/// <summary>
/// `session-life baseline [--store &lt;file&gt;] [--markers &lt;folder&gt;] [--count N]` — the baseline a store is measured by before the
/// fold's trial (<see cref="SessionBaseline"/>). A console verb on the serving binary, as `keys` is, that opens the store read only and
/// exits without binding: it writes nothing, so it reads a copy of an installed store. Its leases are the session markers' processes
/// alive as the ones they marked, read by the one marker reader (<see cref="SessionMarker.Prove"/>); none named reads none.
/// Exit codes: 0 read, 2 bad usage or no store.
/// </summary>
public static class SessionLifeConsole
{
    private const string Usage = "usage: session-life baseline [--store <file>] [--markers <folder>] [--count N]";

    public static async Task<int> RunAsync(string[] args, string database)
    {
        if (Options(args, database) is not { } options)
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        var (store, markers, count) = options;
        if (!File.Exists(store))
        {
            Console.Error.WriteLine($"session-life: no store at {store}.");
            return 2;
        }

        await using var connection = await SessionBaseline.OpenReadOnlyAsync(store);
        var reading = await SessionBaseline.ReadAsync(connection, Leases(markers), DateTimeOffset.UtcNow, count);
        foreach (var line in reading.Lines()) Console.WriteLine(line);
        return 0;
    }

    /// <summary>The verb's options, each flag with its value; null for a flag with none, one it does not know, or a count that is no positive number.</summary>
    public static (string Store, string? Markers, int Count)? Options(string[] args, string database)
    {
        if (args is not ["baseline", .. var rest]) return null;
        var store = database;
        string? markers = null;
        var count = 50;
        for (var at = 0; at < rest.Length; at += 2)
        {
            if (at + 1 >= rest.Length) return null;
            var value = rest[at + 1];
            switch (rest[at])
            {
                case "--store":
                    store = value;
                    break;
                case "--markers":
                    markers = value;
                    break;
                case "--count" when int.TryParse(value, CultureInfo.InvariantCulture, out count) && count > 0:
                    break;
                default:
                    return null;
            }
        }

        return (store, markers, count);
    }

    /// <summary>The leases the markers in <paramref name="markers"/> prove, as the driver's own reader proves them.</summary>
    public static IReadOnlyList<SessionLease> Leases(string? markers) =>
        [.. SessionMarker.Prove(markers, Runs).Select(lease => new SessionLease(lease.Session, lease.Generation, lease.OwnerRuns))];

    // This machine's look at a process, against the marker's own rule for a start (SessionMarker.SameStart).
    private static bool Runs(int pid, long started)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return SessionMarker.SameStart(started, process.StartTime.ToUniversalTime().Ticks);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
