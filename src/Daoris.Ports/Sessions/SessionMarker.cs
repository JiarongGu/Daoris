using System.Globalization;

namespace Daoris.Ports.Sessions;

/// <summary>
/// A session's marker, <c>run/sessions/&lt;session&gt;.pid</c>, in its one spelling: the process that works on the session (its id and
/// start, UTC ticks), the application's generation it started in, and its owner, the driver process that started it and sees its turn
/// through (its id and start). An opener's mark names the driver that is opening the record, with the word <c>opening</c>, and no process
/// of the session's. Every reader and writer of a marker parses and spells it here, the driver's registry and the baseline's console alike.
/// </summary>
/// <param name="Pid">The marked process.</param>
/// <param name="Started">Its start, UTC ticks: an id the machine handed to another process since is told apart by it.</param>
/// <param name="Generation">The application's generation it started in; 0 where none is known.</param>
/// <param name="OwnerPid">The driver process that started it; 0 for an opener's mark.</param>
/// <param name="OwnerStarted">That driver's start, UTC ticks.</param>
/// <param name="Opening">An opener's mark: <see cref="Pid"/> is the opening driver, and no process of the session's runs yet.</param>
public sealed record SessionMarker(int Pid, long Started, long Generation, int OwnerPid, long OwnerStarted, bool Opening = false)
{
    /// <summary>The word that closes an opener's mark.</summary>
    public const string OpeningWord = "opening";

    /// <summary>
    /// Whether a process that started at <paramref name="processStarted"/> (UTC ticks) is the one a marker names as started at
    /// <paramref name="marked"/>: within a second, since a start time is read back rounded.
    /// </summary>
    public static bool SameStart(long marked, long processStarted) => Math.Abs(processStarted - marked) < TimeSpan.TicksPerSecond;

    /// <summary>The marker's line: <c>&lt;pid&gt; &lt;start&gt; &lt;generation&gt; &lt;owner pid&gt; &lt;owner start&gt;</c>, or an opener's.</summary>
    public string Line() => Opening
        ? string.Create(CultureInfo.InvariantCulture, $"{Pid} {Started} {OpeningWord}")
        : string.Create(CultureInfo.InvariantCulture, $"{Pid} {Started} {Generation} {OwnerPid} {OwnerStarted}");

    /// <summary>The marker a line spells, or null for one that does not read as either shape.</summary>
    /// <remarks>
    /// A line of two fields, a process and its start, is what a build before the generations wrote for a process it started: it reads
    /// as a process with no generation and no owner, a void lease, so a process that build left running is still known alive and never
    /// adopted. That reader ends with the first start of the application after the update, which no such process outlives as a lease.
    /// </remarks>
    public static SessionMarker? Parse(string text)
    {
        var fields = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 2 || !Int(fields[0], out var pid) || !Long(fields[1], out var started)) return null;
        if (fields.Length == 2) return new(pid, started, 0, 0, 0);
        if (fields is [_, _, OpeningWord]) return new(pid, started, 0, 0, 0, Opening: true);
        return fields.Length == 5 && Long(fields[2], out var generation) && Int(fields[3], out var owner) && Long(fields[4], out var ownerStarted)
            ? new(pid, started, generation, owner, ownerStarted)
            : null;
    }

    /// <summary>
    /// The processes the markers in <paramref name="folder"/> prove: each marker of a session's process that reads, whose process
    /// <paramref name="runs"/> as the one it marked, with whether its owner still runs. An opener's mark proves no process of the
    /// session's, a marker that does not read proves nothing, and a folder that cannot be read proves none.
    /// </summary>
    /// <param name="runs">Whether the process of an id runs as the one started at the ticks given: the reader's own look at the machine.</param>
    public static IReadOnlyList<ProvenLease> Prove(string? folder, Func<int, long, bool> runs)
    {
        List<string> files;
        try
        {
            if (folder is null || !Directory.Exists(folder)) return [];
            files = [.. Directory.EnumerateFiles(folder, "*.pid")];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        var proven = new List<ProvenLease>();
        foreach (var file in files)
        {
            SessionMarker? marker;
            try
            {
                marker = Parse(File.ReadAllText(file));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (marker is not { Opening: false } || !runs(marker.Pid, marker.Started)) continue;
            proven.Add(new ProvenLease(
                Path.GetFileNameWithoutExtension(file), marker.Generation, marker.OwnerPid > 0 && runs(marker.OwnerPid, marker.OwnerStarted)));
        }

        return proven;
    }

    private static bool Int(string text, out int value) => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static bool Long(string text, out long value) => long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
}

/// <summary>A session's process proved alive by its marker: its session, the generation it started in, and whether its owner still runs.</summary>
/// <param name="Session">The session it works on.</param>
/// <param name="Generation">The application's generation its marker names.</param>
/// <param name="OwnerRuns">
/// Whether the driver that started it still runs: a lease is live only while it does, since nothing else sees its turn through, so a
/// process that outlived its driver's restart is void and never adopted, and one a running terminal's driver owns is never voided by
/// another driver's start.
/// </param>
public sealed record ProvenLease(string Session, long Generation, bool OwnerRuns);
