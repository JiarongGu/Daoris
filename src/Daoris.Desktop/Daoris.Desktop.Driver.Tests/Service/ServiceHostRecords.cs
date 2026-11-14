using System.Diagnostics;
using System.Globalization;
using Daoris.Adapters.Machine;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The hosts a test run started, recorded beside the build output they run from, so the next run of the same checkout ends one a dead run
/// left: a host that outlived its run holds its build output open, and the next build of it then fails to copy over it.
/// </summary>
/// <remarks>
/// One file per host, named by the host's process id and holding its start time and its owner's process id and start time (UTC ticks), so
/// a host is stale only where it still runs as the process recorded and its owner no longer runs: a host of a run still going, in
/// this checkout or beside it, is never touched, and an id since handed to another process is told apart by its start time.
/// </remarks>
internal static class ServiceHostRecords
{
    /// <summary>The folder the records of the host built under <paramref name="artifacts"/> are kept in.</summary>
    internal static string FolderOf(string artifacts) => Path.Combine(artifacts, "hosts");

    /// <summary>Record <paramref name="host"/> as started by <paramref name="by"/>, this process where none is named.</summary>
    internal static void Record(string folder, Process host, Process? by = null)
    {
        using var current = by is null ? Process.GetCurrentProcess() : null;
        var owner = by ?? current!;
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, Name(host.Id)),
            string.Join(' ', Ticks(host.StartTime), owner.Id.ToString(CultureInfo.InvariantCulture), Ticks(owner.StartTime)));
    }

    /// <summary>Forget <paramref name="host"/>: it ended with its run.</summary>
    internal static void Forget(string folder, int host)
    {
        try
        {
            File.Delete(Path.Combine(folder, Name(host)));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A record left behind names a process that is gone, which the next reap clears.
        }
    }

    /// <summary>End every recorded host whose owner is gone, with its tree; clear the records of hosts that have ended. How many were ended.</summary>
    internal static int Reap(string folder)
    {
        if (!Directory.Exists(folder)) return 0;
        var reaped = 0;
        foreach (var file in Directory.GetFiles(folder))
        {
            var host = int.TryParse(Path.GetFileName(file), CultureInfo.InvariantCulture, out var id) ? id : 0;
            var words = File.ReadAllText(file).Split(' ');
            if (host == 0 || words.Length != 3 || !long.TryParse(words[0], CultureInfo.InvariantCulture, out var started)
                || !int.TryParse(words[1], CultureInfo.InvariantCulture, out var owner) || !long.TryParse(words[2], CultureInfo.InvariantCulture, out var ownerStarted))
            {
                File.Delete(file);
                continue;
            }

            if (!ProcessLiveness.Alive(host, started))
            {
                File.Delete(file);
                continue;
            }

            if (ProcessLiveness.Alive(owner, ownerStarted)) continue;
            try
            {
                using var stale = Process.GetProcessById(host);
                stale.Kill(entireProcessTree: true);
                stale.WaitForExit(10_000);
                reaped++;
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // Already ending as it is looked for.
            }

            File.Delete(file);
        }

        return reaped;
    }

    private static string Name(int host) => host.ToString(CultureInfo.InvariantCulture);

    private static string Ticks(DateTime started) => started.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture);
}
