using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>A driven or conversation process's lineage brackets: its token issued before the spawn, and its end written once its exit is read and its capture drained.</summary>
internal sealed class LineageBrackets(string home, SessionProcesses processes, SessionWatching watching)
{
    /// <summary>A process's lineage token: issued, its <c>start</c> written to the session's lineage, and set anew on the spawn's environment, all before the spawn.</summary>
    /// <param name="moved">The token its run's move issued (a resume, a chat's go-on), whose <c>start</c> is written already.</param>
    public static string Bracketed(string home, string sessionId, ProcessStartInfo info, string? moved = null)
    {
        var proc = moved ?? ChildLineage.Opened(home, sessionId);
        info.Environment[ChildMarks.LineageProcVariable] = proc;
        return proc;
    }

    /// <summary>The spawn under <paramref name="proc"/>: its process handed the token, or the token let go where it made none.</summary>
    public static async Task<Process?> Spawned(string proc, Func<Task<Process?>> spawn)
    {
        var tokens = LineageTokens.Current;
        Process? process;
        try
        {
            process = await spawn().ConfigureAwait(false);
        }
        catch
        {
            tokens.Unspawned(proc);
            throw;
        }

        if (process is null) tokens.Unspawned(proc);
        else tokens.Hand(proc, process);
        return process;
    }

    /// <summary>A process's end once its exit is read and its capture drained: clean, or killed, stopped or failed.</summary>
    public void Ended(string sessionId, string proc, int? exitCode) =>
        ChildLineage.End(home, sessionId, clean: exitCode == 0 && processes.StopMade(sessionId) is null, DateTimeOffset.UtcNow, proc);

    /// <summary>
    /// Loop's wait on a bracketed process: the exit it read told to the token set, which may not watch that process; where the wait
    /// throws as the loop closes, its kill having read the exit, the process's end written unclean before the throw leaves the run.
    /// </summary>
    public async Task<int?> WaitedAsync(string sessionId, string proc, Task<int?> wait, CancellationToken ct)
    {
        try
        {
            var exitCode = await wait.ConfigureAwait(false);
            LineageTokens.Current.Exited(proc);
            return exitCode;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            ChildLineage.Exited(home, sessionId, proc, clean: false);
            throw;
        }
    }

    /// <summary>A bracketed process's drain: a capture that faults past the exit writes its end, unclean, before the fault leaves.</summary>
    public async Task<bool> DrainedAsync(string sessionId, string proc, Task capture)
    {
        try
        {
            return await watching.CaptureWithinAsync(capture, sessionId).ConfigureAwait(false);
        }
        catch
        {
            ChildLineage.Exited(home, sessionId, proc, clean: false);
            throw;
        }
    }
}
