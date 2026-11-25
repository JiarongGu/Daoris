namespace Daoris.Driver;

/// <summary>
/// A check whose verification's process is gone when Daoris asks after it: no result and no job of it left (a restart, an update, a crash
/// cut it off; an update or a stop of driving that closed its executor says so in its folder and is no loss to count). It is run once more under its key, as the same attempt; lost again with nothing to explain it, it settles with the tail of its newest
/// log and is asked again as a new attempt (<see cref="UnrunChecks"/>, held only once its asks are spent), never sent a third time under its
/// key or taken as a red of the work. It holds no slot: the machine's verification and heavy-run slots are its process's, gone with it.
/// </summary>
public static class LostChecks
{
    /// <summary>Why a check is asked again as a new attempt: its process lost on its rerun too.</summary>
    public const string Code = "lost";

    public const string Lost = "its verification's process was lost on its rerun too, with nothing to say why";

    /// <summary>The sends an attempt takes under its key before a lost one is asked again as a new attempt: its first, and the one rerun.</summary>
    public const int Sends = 2;

    /// <summary>The lines of its newest log kept with it, the last ones.</summary>
    public const int TailLines = 20;

    /// <summary>The most of those lines kept, in characters, the last ones.</summary>
    public const int TailChars = 4000;

    /// <summary>
    /// <paramref name="attempt"/>, the current attempt of <paramref name="node"/> whose executor found no result and no job of it: sent again
    /// under its key while it has sent fewer than <see cref="Sends"/>, else settled with its log's tail and asked again as a new attempt.
    /// </summary>
    internal static (NodeRecord? Next, WorkPerformed Result) Settle(
        NodeRecord record, NodeAttempt attempt, NodeDeclaration node, NodeStatus status, int generation, DateTimeOffset now, string home)
    {
        // Cut off while still queued, or by the executor's own close, or on a first loss nothing explains: sent again. Only losses nothing
        // explains count, so an update or a stop of driving during a long check never holds it.
        if (Unexplained(home, record.Unit, attempt) < Sends)
        {
            return (WorkPerformer.Kept(record, NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Active, NodeCause.Daoris)), "redeliver", now),
                new WorkPerformed(record.Unit, node.Id, PerformedWhat.Redelivered));
        }

        return UnrunChecks.Again(record, attempt with { Tail = Tail(home, record.Unit, attempt.Call.Invocation) }, node, generation, now, Code, Lost);
    }

    /// <summary>
    /// The sends of the attempt that began and were lost with nothing to say why, this one counted: 0 where the current send never began (its
    /// own start mark is absent, though the folder stands from an earlier send) or where the executor's close ended it. A send's marks are
    /// numbered by the delivery the attempt counts for it (<see cref="NodeAttempt.Deliveries"/> once its receipt is kept).
    /// </summary>
    private static int Unexplained(string home, string unit, NodeAttempt attempt)
    {
        try
        {
            var run = CheckExecution.RunOf(unit);
            var invocation = attempt.Call.Invocation;
            bool Began(int send) => File.Exists(AttemptMarks.Began(home, run, invocation, send));
            bool Closed(int send) => File.Exists(AttemptMarks.Closed(home, run, invocation, send));
            if (!Began(attempt.Deliveries) || Closed(attempt.Deliveries)) return 0;
            return Enumerable.Range(1, attempt.Deliveries).Count(send => Began(send) && !Closed(send));
        }
        catch (DriverException)
        {
            return 0;
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
            var cut = stream.Length > TailChars * 4L;
            if (cut) stream.Seek(-TailChars * 4L, SeekOrigin.End);
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd().ReplaceLineEndings("\n");
            if (cut)
            {
                // The read started anywhere: inside a line, inside a character. The broken character goes, and the first line it broke.
                text = text.TrimStart('�');
                if (text.IndexOf('\n') is var first and >= 0) text = text[(first + 1)..];
            }

            var tail = string.Join('\n', text.TrimEnd('\n').Split('\n').TakeLast(TailLines));
            if (tail.Length > TailChars)
            {
                tail = tail[^TailChars..];
                if (char.IsLowSurrogate(tail[0])) tail = tail[1..];
            }

            return tail.Length == 0 ? null : tail;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return null;
        }
    }
}
