namespace Daoris.Driver;

/// <summary>
/// A check whose verification's process is gone when Daoris asks after it: no result and no job of it left (a restart, an update, a crash
/// cut it off). It is run once more under its key, as the same attempt; lost again, it is held for the person with the tail of its newest
/// log, never sent a third time by itself and never taken as a red of the work. Held, it holds no slot: the machine's verification and
/// heavy-run slots are its process's, gone with it, and the run's <c>again</c> asks it anew (<see cref="UnknownChecks"/>).
/// </summary>
public static class LostChecks
{
    /// <summary>Why a check is held: its process lost on its rerun too.</summary>
    public const string Code = "lost";

    /// <summary>The sends an attempt takes before a lost one is held: its first, and the one rerun.</summary>
    public const int Sends = 2;

    /// <summary>The lines of its newest log kept with it, the last ones.</summary>
    public const int TailLines = 20;

    /// <summary>The most of those lines kept, in characters, the last ones.</summary>
    public const int TailChars = 4000;

    /// <summary>
    /// <paramref name="attempt"/>, the current attempt of <paramref name="node"/> whose executor found no result and no job of it: sent again
    /// under its key while it has sent fewer than <see cref="Sends"/>, else held for the person with its log's tail.
    /// </summary>
    internal static (NodeRecord? Next, WorkPerformed Result) Settle(
        NodeRecord record, NodeAttempt attempt, NodeDeclaration node, NodeStatus status, int generation, DateTimeOffset now, string home)
    {
        // Cut off while still queued for its slots, it left no folder: sent again, as often as that happens.
        if (attempt.Deliveries < Sends || !Began(home, record.Unit, attempt.Call.Invocation))
        {
            return (WorkPerformer.Kept(record, NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Active, NodeCause.Daoris)), "redeliver", now),
                new WorkPerformed(record.Unit, node.Id, PerformedWhat.Redelivered));
        }

        return WorkPerformer.Hold(record, attempt with { Generation = generation, Note = Tail(home, record.Unit, attempt.Call.Invocation) }, Code);
    }

    /// <summary>Whether the attempt began to run: its executor makes its folder first, once both slots are held.</summary>
    private static bool Began(string home, string unit, string invocation)
    {
        try
        {
            return Directory.Exists(VerifyFiles.AttemptFolder(home, CheckExecution.RunOf(unit), invocation));
        }
        catch (DriverException)
        {
            return false;
        }
    }

    /// <summary>The last lines of the attempt's newest log in its folder; null where it left none or none reads.</summary>
    public static string? Tail(string home, string unit, string invocation)
    {
        try
        {
            var folder = VerifyFiles.AttemptFolder(home, CheckExecution.RunOf(unit), invocation);
            if (!Directory.Exists(folder)) return null;
            var newest = new DirectoryInfo(folder).EnumerateFiles("*.log").OrderByDescending(file => file.LastWriteTimeUtc).ThenByDescending(file => file.Name, StringComparer.Ordinal).FirstOrDefault();
            if (newest is null) return null;
            using var stream = new FileStream(newest.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > TailChars * 4L) stream.Seek(-TailChars * 4L, SeekOrigin.End);
            using var reader = new StreamReader(stream);
            var lines = reader.ReadToEnd().ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');
            var tail = string.Join('\n', lines.TakeLast(TailLines));
            return tail.Length == 0 ? null : tail.Length > TailChars ? tail[^TailChars..] : tail;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return null;
        }
    }
}
