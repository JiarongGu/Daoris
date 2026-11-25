using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// One process Daoris started for a session, kept beside the session's marker (<c>&lt;session&gt;.&lt;pid&gt;.agent</c> in the markers'
/// folder): the process by its id and start time, the session it served, the paths in its start (<see cref="Paths"/>) and, once a look
/// has found it left running, when (<see cref="Seen"/>).
/// </summary>
/// <param name="Pid">The process's id.</param>
/// <param name="Started">When it started, in UTC ticks: with its id, the process it names, never another that the machine gave the id.</param>
/// <param name="Session">The session it served.</param>
/// <param name="Paths">The rooted paths its start named (its file and its arguments), which say whether it is a program of Daoris's toolchain.</param>
/// <param name="Seen">When a look first found it running with no session to run it, or null.</param>
internal sealed record StartRecord(int Pid, long Started, string Session, IReadOnlyList<string> Paths, DateTimeOffset? Seen = null)
{
    /// <summary>The agent and the version of the toolchain's folder its start names (<c>&lt;toolchain&gt;/&lt;agent&gt;/&lt;version&gt;</c>), or null where it names none.</summary>
    public (string Agent, string Pin)? Toolchain(string toolchain)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(toolchain));
        foreach (var path in Paths)
        {
            string full;
            try
            {
                full = Path.GetFullPath(path);
            }
            catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
            {
                continue;
            }

            if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
            var parts = full[(root.Length + 1)..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3) return (parts[0], parts[1]);
        }

        return null;
    }
}

/// <summary>
/// The record Daoris keeps of each process it starts for a session (<see cref="StartRecord"/>), so a process left running when its
/// session has ended is known as Daoris's own and nothing else's (<see cref="StrayAgents"/>). Written as the process is tracked
/// (<see cref="SessionProcesses.Track"/>) and removed as its tracking ends with the process gone, or at a look that finds it so.
/// </summary>
internal static class StartRecords
{
    /// <summary>The extension of a record, which no session marker (<c>.pid</c>) shares.</summary>
    internal const string Extension = ".agent";

    /// <summary>The most paths and the longest path a record keeps.</summary>
    private const int MostPaths = 8;
    private const int LongestPath = 400;

    private sealed record Stored(int Pid, long Started, string Session, string[] Paths, long? Seen);

    /// <summary>Keep the record of a process just started for a session; best effort, since the process runs either way.</summary>
    internal static void Write(string markers, string session, Process process)
    {
        if (!SafeNames.IsFolderId(session)) return;
        try
        {
            var paths = PathsOf(process.StartInfo);
            if (paths.Count == 0) return;
            var started = process.StartTime.ToUniversalTime().Ticks;
            Save(PathOf(markers, session, process.Id), new StartRecord(process.Id, started, session, paths));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException
                                          or System.ComponentModel.Win32Exception)
        {
            // Unrecorded, the process is never reaped by a look: it ends with its session as it did before.
        }
    }

    /// <summary>The records in <paramref name="markers"/>, each with its file; one that cannot be read is left out.</summary>
    internal static IReadOnlyList<(string File, StartRecord Start)> Read(string markers)
    {
        var found = new List<(string, StartRecord)>();
        try
        {
            if (!Directory.Exists(markers)) return found;
            foreach (var file in Directory.EnumerateFiles(markers, "*" + Extension))
            {
                if (Load(file) is { } start) found.Add((file, start));
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A folder that cannot be listed has nothing to reap this time.
        }

        return found;
    }

    /// <summary>The record a look rewrites as having found the process left running at <paramref name="seen"/>, or without a time.</summary>
    internal static void Mark(string file, StartRecord start, DateTimeOffset? seen) => Save(file, start with { Seen = seen });

    /// <summary>Remove a record; one held open stays for the next look.</summary>
    internal static void Remove(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left for the next look, which finds the process gone or reaps it.
        }
    }

    /// <summary>Remove the record of <paramref name="process"/> where it has ended; one still running keeps its record, since it may be left.</summary>
    internal static void Forget(string markers, string session, Process process)
    {
        if (!SafeNames.IsFolderId(session)) return;
        try
        {
            if (process.HasExited) Remove(PathOf(markers, session, process.Id));
        }
        catch (Exception error) when (error is InvalidOperationException or ObjectDisposedException or System.ComponentModel.Win32Exception)
        {
            // Its state cannot be read: the record stays, and a look reads the process itself.
        }
    }

    private static string PathOf(string markers, string session, int pid) =>
        Path.Combine(markers, $"{session}.{pid.ToString(CultureInfo.InvariantCulture)}{Extension}");

    private static IReadOnlyList<string> PathsOf(ProcessStartInfo info)
    {
        var paths = new List<string>();
        foreach (var word in new[] { info.FileName }.Concat(info.ArgumentList))
        {
            if (paths.Count >= MostPaths) break;
            if (word is { Length: > 0 and <= LongestPath } && Path.IsPathRooted(word) && !word.Contains('\n')) paths.Add(word);
        }

        return paths;
    }

    private static StartRecord? Load(string file)
    {
        try
        {
            var stored = JsonSerializer.Deserialize<Stored>(AtomicFile.ReadText(file));
            return stored is { Session.Length: > 0 }
                ? new StartRecord(stored.Pid, stored.Started, stored.Session, stored.Paths ?? [],
                    stored.Seen is { } seen ? new DateTimeOffset(seen, TimeSpan.Zero) : null)
                : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static void Save(string file, StartRecord start)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        AtomicFile.WriteText(
            file,
            JsonSerializer.Serialize(new Stored(start.Pid, start.Started, start.Session, [.. start.Paths], start.Seen?.UtcTicks)));
    }
}
