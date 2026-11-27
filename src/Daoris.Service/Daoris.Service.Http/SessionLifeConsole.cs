using System.Diagnostics;
using System.Globalization;
using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge.Http;

/// <summary>
/// `session-life baseline [--store &lt;file&gt;] [--markers &lt;folder&gt;] [--count N]` — the baseline a store is measured by before the
/// fold's trial (<see cref="SessionBaseline"/>). A console verb on the serving binary, as `keys` is, that opens the store read only and
/// exits without binding: it writes nothing, so it reads a copy of an installed store. Its leases are the session markers' processes
/// alive as the ones they marked (the driver's <c>run/sessions/*.pid</c>, <c>&lt;pid&gt; &lt;start ticks&gt;</c>); none named reads none.
/// Exit codes: 0 read, 2 bad usage or no store.
/// </summary>
internal static class SessionLifeConsole
{
    private const string Usage = "usage: session-life baseline [--store <file>] [--markers <folder>] [--count N]";

    public static async Task<int> RunAsync(string[] args, string database)
    {
        if (args is not ["baseline", ..])
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        var store = database;
        string? markers = null;
        var count = 50;
        for (var at = 1; at < args.Length - 1; at++)
        {
            if (args[at] == "--store") store = args[at + 1];
            if (args[at] == "--markers") markers = args[at + 1];
            if (args[at] == "--count" && !int.TryParse(args[at + 1], CultureInfo.InvariantCulture, out count)) count = -1;
        }

        if (count <= 0 || !File.Exists(store))
        {
            Console.Error.WriteLine(count <= 0 ? Usage : $"session-life: no store at {store}.");
            return 2;
        }

        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = store, Mode = SqliteOpenMode.ReadOnly }.ToString());
        await connection.OpenAsync();
        var reading = await SessionBaseline.ReadAsync(connection, Leases(markers), DateTimeOffset.UtcNow, count);
        foreach (var line in reading.Lines()) Console.WriteLine(line);
        return 0;
    }

    /// <summary>The processes the markers in <paramref name="markers"/> prove alive, each as the one its marker names; an opener's mark is none.</summary>
    internal static IReadOnlyList<ShadowLease> Leases(string? markers)
    {
        if (markers is null || !Directory.Exists(markers)) return [];
        var leases = new List<ShadowLease>();
        foreach (var marker in Directory.EnumerateFiles(markers, "*.pid"))
        {
            string[] fields;
            try
            {
                fields = File.ReadAllText(marker).Trim().Split(' ');
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (fields.Length != 2 || !int.TryParse(fields[0], CultureInfo.InvariantCulture, out var pid)
                || !long.TryParse(fields[1], CultureInfo.InvariantCulture, out var started) || !Runs(pid, started))
            {
                continue;
            }

            leases.Add(new ShadowLease(Path.GetFileNameWithoutExtension(marker), new DateTimeOffset(started, TimeSpan.Zero)));
        }

        return leases;
    }

    private static bool Runs(int pid, long started)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return Math.Abs(process.StartTime.ToUniversalTime().Ticks - started) < TimeSpan.TicksPerSecond;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
