using System.ComponentModel;
using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>What a process Daoris starts is for: its purpose names the job that holds it, or says why none does.</summary>
public enum ProcessPurpose
{
    /// <summary>
    /// An agent, plugin process or piece the launcher took: born in this process's agents' job from its first instruction; joined to nothing here.
    /// </summary>
    Launched,

    /// <summary>An agent's harness the launcher could not take (none laid out, as in a test's home, or a job refused).</summary>
    Agent,

    /// <summary>A plugin's hook program or the kit's trial of it, where the launcher could not take it.</summary>
    Plugin,

    /// <summary>git, and everything git starts: its transports, ssh, credential helpers, hooks, a detached gc.</summary>
    Git,

    /// <summary>A question read to its end (<see cref="ChildProcess"/>): git's, a version probe, a plugin tool's check, an account's usage read.</summary>
    Read,

    /// <summary>A tool's own command: an agent's install or sign-in, a plugin tool's set-up, a key made, the doctrine CLI, a server's package.</summary>
    Tool,

    /// <summary>Daoris's own browser, started through the window kit and not the gate: it ends with this process, and early where its bring-up fails.</summary>
    Browser,

    /// <summary>Held by no job: it outlives this process by design (the update's launcher, the Edge recorded for the next shell).</summary>
    Outlives,

    /// <summary>Held by no job: it ends itself when this process goes, and a job's kill would cut that end short (the HTTP host, on its input's end).</summary>
    EndsItself,
}

/// <summary>What one purpose's job holds now: every process that joined it, and every one those started since, by id.</summary>
public sealed record RunningPurpose(ProcessPurpose Purpose, IReadOnlyList<int> Ids);

/// <summary>
/// The one management of the processes this Daoris process starts. Every start through the gate (<see cref="ProcessStarts"/>) is
/// handed here with its purpose and joined, as it returns, to that purpose's job: one job per purpose, made at its first start, ending what it
/// holds when its handle closes, and held for this process's life, so nothing this process started, nor anything that started, outlives it,
/// however it ends. A process ended early is ended here (<see cref="End"/>), and what is running is read here (<see cref="Census"/>).
/// </summary>
/// <remarks>
/// <para><b>Joined as its start returns.</b> .NET starts no process suspended, so a child joins inside the gate's place, before its caller
/// holds it; what it starts in that instant is outside the job. A start that must not race starts suspended and joins a job of its own (a
/// terminal's shell, a heavy run's gate, a git act's step), and an agent's is the launcher's, born in the agents' job.</para>
/// <para><b>Stops nothing Daoris did not start.</b> A job holds only what joined it and what that started since; the census reads and never
/// ends; and <see cref="End"/> is handed only a process its caller started.</para>
/// <para>Off Windows there is no job: nothing is joined, the census is empty, and a crash leaves what it left before.</para>
/// </remarks>
public sealed class OwnedProcesses
{
    /// <summary>This process's own.</summary>
    public static OwnedProcesses Shared { get; } = new();

    // The kill a test's flow makes instead of the runtime's (EndingWith); null in production.
    private static readonly AsyncLocal<Action<Process>?> s_end = new();

    private readonly object _gate = new();
    private readonly Dictionary<ProcessPurpose, ProcessJob> _jobs = [];

    /// <summary>Whether a purpose's processes are joined to a job of this management's: all but the launcher's and the two held by none.</summary>
    public static bool Joins(ProcessPurpose purpose) =>
        purpose is not (ProcessPurpose.Launched or ProcessPurpose.Outlives or ProcessPurpose.EndsItself);

    /// <summary>
    /// Join <paramref name="process"/> to <paramref name="purpose"/>'s job, the job made at the purpose's first start: true where it joined;
    /// false where the purpose joins none here, there is no job, or the process would not join (it had already exited).
    /// </summary>
    public bool Take(Process? process, ProcessPurpose purpose)
    {
        if (process is null || !Joins(purpose) || JobOf(purpose) is not { } job) return false;
        try
        {
            return job.Join(process.Handle);
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Exited before it could join, or its handle is not this process's to take: nothing to hold.
            return false;
        }
    }

    public static bool Own(Process process, string purpose) => Enum.TryParse<ProcessPurpose>(purpose, out var known) && Shared.Take(process, known);

    /// <summary>The purpose whose job holds <paramref name="process"/>, or null where none of this management's does.</summary>
    public ProcessPurpose? Holding(Process process)
    {
        foreach (var (purpose, job) in Jobs())
        {
            try
            {
                if (job.Contains(process.Handle)) return purpose;
            }
            catch (Exception error) when (error is InvalidOperationException or Win32Exception or NotSupportedException)
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>What is running now, by purpose: each job of this management's that holds a process, with the ids it holds.</summary>
    public IReadOnlyList<RunningPurpose> Census() =>
        [.. Jobs().Select(each => new RunningPurpose(each.Purpose, each.Job.Ids)).Where(each => each.Ids.Count > 0)];

    /// <summary>
    /// End <paramref name="process"/> and everything it started: the kill a runner makes past its bound, on its caller's cancellation, or at
    /// its owner's end. A process that ended when stopped is already what the kill asks for, so the refusal of one that is gone is
    /// answered, never thrown: why the kill is refused, or null where it is made. Whether it ended is the exit wait's to say
    /// (<see cref="ChildProcess.EndAsync"/>).
    /// </summary>
    public static string? End(Process process)
    {
        try
        {
            if (s_end.Value is { } stand) stand(process);
            else process.Kill(entireProcessTree: true);
            return null;
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception or NotSupportedException or AggregateException)
        {
            // It ended when stopped, or the exit wait after it says it did not; a tree some of whose members refuse is an aggregate.
            return error.Message;
        }
    }

    /// <summary>The test seam: until disposed, every <see cref="End"/> this flow makes is <paramref name="end"/>'s instead of the runtime's kill.</summary>
    internal static IDisposable EndingWith(Action<Process> end)
    {
        var before = s_end.Value;
        s_end.Value = end;
        return new Ending(before);
    }

    private sealed class Ending(Action<Process>? before) : IDisposable
    {
        public void Dispose() => s_end.Value = before;
    }

    private (ProcessPurpose Purpose, ProcessJob Job)[] Jobs()
    {
        lock (_gate) return [.. _jobs.Select(each => (each.Key, each.Value))];
    }

    /// <summary>
    /// The purpose's job, made at its first start and never let go: its handle closes with this process, which ends what it holds. A job the
    /// system refused is asked for again at the next start.
    /// </summary>
    private ProcessJob? JobOf(ProcessPurpose purpose)
    {
        lock (_gate)
        {
            if (_jobs.TryGetValue(purpose, out var held)) return held;
            if (ProcessJob.Create() is not { } made) return null;
            _jobs[purpose] = made;
            return made;
        }
    }
}
