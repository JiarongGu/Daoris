using System.Diagnostics;
using System.Globalization;

namespace Daoris.Driver;

/// <summary>What a start through the launcher is (TERMTRUST1a; the machine log's <c>agent.contained</c> <c>kind</c>).</summary>
public enum ContainedKind
{
    /// <summary>An agent's harness: a driven session, a carry-on, a conversation, an intake, a helper, a reviewer, a brainstorm pass, a workflow node.</summary>
    Agent,

    /// <summary>A plugin's process, its hook program or the kit's trial of it: a performed step runs a repository's own commands.</summary>
    Plugin,

    /// <summary>A partner's piece, which nests its own strict job beneath the agents'.</summary>
    Piece,
}

/// <summary>Why a start was not contained (TERMTRUST1a; the machine log's <c>agent.contained</c> <c>why</c>).</summary>
public static class ContainedWhy
{
    /// <summary>No launcher is laid out beside the home: a workspace build, a test's home.</summary>
    public const string NoLauncher = "no-launcher";

    /// <summary>The system made no agents' job: refused, or not Windows.</summary>
    public const string JobRefused = "job-refused";

    /// <summary>Another process already holds an object of the agents' job's name.</summary>
    public const string NameHeld = "name-held";

    /// <summary>
    /// The start could not be handed to the launcher whole: its program not found by a full path, which the launcher would refuse, not
    /// there at all, or its words one string. Made as it was, so it starts, or fails, as it did before the launcher.
    /// </summary>
    public const string NotHanded = "not-handed";
}

/// <summary>
/// This Daoris process's agents' job (TERMTRUST1a part (i); D201 point 4; the terminal-trust design §3.3 point 3): one named job,
/// <c>Local\daoris-agents-&lt;process id&gt;</c>, made empty at the first start that can be contained and held for the process's life, which every
/// agent, plugin process and piece it starts is joined to from its first instruction, through the launcher (<see cref="PieceLauncher"/>).
/// It ends what it holds as its last handle closes, this process's, so no agent outlives the Daoris that started it, as each agent's own job
/// already had it (ORPHAN1).
/// </summary>
/// <remarks>
/// <para><b>Best effort for the start, published for every judge</b> (Fable's F2): a start that cannot be contained (no launcher laid out,
/// a job the system refuses, a name already held) is made as it was, and while any such start of this process lives, the process holds a
/// second named object of the prefix, an event <c>Local\daoris-agents-&lt;process id&gt;-open</c>, which part (ii)'s judge reads as
/// <c>untold</c>. A name lives only while a handle to it is open, so a reused process id meets no stale one.</para>
/// <para>A test makes its own (<see cref="AgentJobs(int, Func{string, ValueTuple{ProcessJob, bool}})"/>), under a process id that is not its own,
/// and hands it to a flow through <see cref="Within"/>, so its counts are its starts' alone.</para>
/// </remarks>
public sealed class AgentJobs : IDisposable
{
    /// <summary>This process's own.</summary>
    public static AgentJobs Shared { get; } = new(Environment.ProcessId);

    // The jobs a test's flow starts under (Within); null in production, which uses Shared.
    private static readonly AsyncLocal<AgentJobs?> s_within = new();

    private readonly Func<string, (ProcessJob? Job, bool NameHeld)> _make;
    private readonly object _gate = new();
    private ProcessJob? _job;
    private EventWaitHandle? _open;
    private int _uncontained;

    /// <param name="processId">Whose job this is: this process's in production, a stand-in id in a test.</param>
    /// <param name="make">How the job is made: <see cref="ProcessJob.CreateNamed"/> unless a test stands in for the system.</param>
    internal AgentJobs(int processId, Func<string, (ProcessJob? Job, bool NameHeld)>? make = null)
    {
        Name = NameOf(processId);
        OpenName = OpenNameOf(processId);
        _make = make ?? ProcessJob.CreateNamed;
    }

    /// <summary>The agents' job's name for a Daoris process.</summary>
    public static string NameOf(int processId) => PieceLauncher.AgentsPrefix + processId.ToString(CultureInfo.InvariantCulture);

    /// <summary>The name of the event a Daoris process holds while an agent of its runs uncontained.</summary>
    public static string OpenNameOf(int processId) => NameOf(processId) + "-open";

    /// <summary>The jobs this flow's starts are made under: a test's, where one was handed in, else this process's.</summary>
    public static AgentJobs Current => s_within.Value ?? Shared;

    /// <summary>This one's job's name.</summary>
    public string Name { get; }

    /// <summary>This one's open event's name.</summary>
    public string OpenName { get; }

    /// <summary>How many of this process's starts run uncontained now: part (ii)'s "every agent of this process contained" is none.</summary>
    public int Uncontained
    {
        get
        {
            lock (_gate) return _uncontained;
        }
    }

    /// <summary>
    /// Make <paramref name="info"/>'s start the launcher's in place, joining this process's agents' job first and
    /// <paramref name="piece"/>'s beneath it, where it can be; otherwise leave it as it was and say why. The answer holds this process's
    /// open event for an uncontained start until <see cref="AgentStart.Lives"/> sees its process end, or it is disposed unstarted.
    /// </summary>
    /// <param name="launcher">The launcher laid out beside the home (<see cref="PieceStarts.LauncherPath"/>), or null for none.</param>
    /// <param name="piece">A piece's job, which it must join: a piece is never started outside it.</param>
    /// <exception cref="DriverException">A piece that cannot be started through the launcher at all.</exception>
    public AgentStart Contain(ProcessStartInfo info, ContainedKind kind, string? launcher, string? piece = null)
    {
        // A start already the launcher's (a run tried again on the same start, SESSCTX1c) is left as it is, read for what it joins.
        if (PieceLauncher.Carries(info))
        {
            var (call, _) = PieceLauncher.Read(info.ArgumentList);
            return call is { } read && read.Jobs.Contains(Name, StringComparer.Ordinal)
                ? new AgentStart(this, kind, null)
                : Open(kind, ContainedWhy.JobRefused);
        }

        if (launcher is null)
        {
            if (piece is not null) throw new DriverException($"the partner's launcher ({PieceLauncher.Program}) is not laid out in this install, so the partner was not started.");
            return Open(kind, ContainedWhy.NoLauncher);
        }

        var (job, why) = Job();
        List<string> jobs = [.. job is null ? Array.Empty<string>() : [Name], .. piece is null ? Array.Empty<string>() : [piece]];
        if (jobs.Count == 0) return Open(kind, why!);

        // The launcher starts a harness named by its full path alone (it would search the session's tree first): the driver names it, from
        // the start's own PATH, then this process's, as a direct start would have found it.
        if (!Path.IsPathFullyQualified(info.FileName)
            && (CommandPresence.Resolve(info.FileName, PathOf(info), startable: true) ?? CommandPresence.Resolve(info.FileName, null, startable: true)) is { } found
            && Path.IsPathFullyQualified(found))
        {
            info.FileName = found;
        }

        // A program not there is left to fail as it failed before the launcher, its start's own refusal saying so where the caller reads it.
        if (!Path.IsPathFullyQualified(info.FileName) || !File.Exists(info.FileName) || PieceStarts.Through(info, launcher, jobs) is not null)
        {
            if (piece is not null) throw new DriverException("the partner's start could not be handed to its launcher whole, so the partner was not started.");
            return Open(kind, ContainedWhy.NotHanded);
        }

        return job is null ? Open(kind, why!) : new AgentStart(this, kind, null);
    }

    /// <summary>
    /// The agents' job, made at the first start that needs it and held for this process's life; or why there is none. A failure is asked
    /// again at the next start: a refusal may pass, and a name held may be let go.
    /// </summary>
    private (ProcessJob? Job, string? Why) Job()
    {
        lock (_gate)
        {
            if (_job is not null) return (_job, null);
            var (made, held) = _make(Name);
            _job = made;
            return made is not null ? (made, null) : (null, held ? ContainedWhy.NameHeld : ContainedWhy.JobRefused);
        }
    }

    private AgentStart Open(ContainedKind kind, string why)
    {
        lock (_gate)
        {
            if (_uncontained++ == 0 && OperatingSystem.IsWindows())
            {
                try
                {
                    _open = new EventWaitHandle(false, EventResetMode.ManualReset, OpenName);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or WaitHandleCannotBeOpenedException)
                {
                    // An object of the name another kind holds, or one this account may not open: the name is taken either way, which every
                    // judge reads as untold, as this event would be.
                }
            }
        }

        return new AgentStart(this, kind, why);
    }

    /// <summary>An uncontained start ended: the open event let go with the last of them.</summary>
    internal void Ended()
    {
        lock (_gate)
        {
            if (_uncontained == 0 || --_uncontained > 0) return;
            _open?.Dispose();
            _open = null;
        }
    }

    /// <summary>The start's PATH, where its environment sets one.</summary>
    private static string? PathOf(ProcessStartInfo info) =>
        info.Environment.TryGetValue(Tools.PathVariable, out var path) && !string.IsNullOrEmpty(path) ? path : null;

    /// <summary>
    /// The test seam: until disposed, every start this flow makes through <see cref="AgentStarts"/> is contained under
    /// <paramref name="jobs"/> instead of this process's. Production opens none.
    /// </summary>
    public static IDisposable Within(AgentJobs jobs)
    {
        var before = s_within.Value;
        s_within.Value = jobs;
        return new Ending(before);
    }

    private sealed class Ending(AgentJobs? before) : IDisposable
    {
        public void Dispose() => s_within.Value = before;
    }

    /// <summary>A test's: its job closed, ending what it holds, and its open event let go. This process's is never disposed.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _job?.Dispose();
            _job = null;
            _open?.Dispose();
            _open = null;
        }
    }
}

/// <summary>
/// One start's containment (TERMTRUST1a): whether it was made through the launcher, why not where it was not, and, for an uncontained one,
/// its share of its process's open event, let go as its process ends (<see cref="Lives"/>) or as it is disposed unstarted.
/// </summary>
public sealed class AgentStart : IDisposable
{
    private readonly AgentJobs _jobs;
    private int _released;
    private int _bound;
    private Process? _watch;

    internal AgentStart(AgentJobs jobs, ContainedKind kind, string? why)
    {
        _jobs = jobs;
        Kind = kind;
        Why = why;
        // A contained start holds no share of the open event.
        if (why is null) _released = 1;
    }

    public ContainedKind Kind { get; }

    /// <summary>Whether its harness is born in this process's agents' job.</summary>
    public bool Contained => Why is null;

    /// <summary>Why not (<see cref="ContainedWhy"/>), or null where it was.</summary>
    public string? Why { get; }

    /// <summary>
    /// Bind its share of the open event to <paramref name="process"/>'s life: let go as it exits, watched through a handle of this one's own,
    /// so a caller disposing its <see cref="Process"/> early never keeps the event. Null, or a process already gone, lets go now.
    /// </summary>
    public void Lives(Process? process)
    {
        if (Interlocked.Exchange(ref _bound, 1) == 1 || Volatile.Read(ref _released) == 1) return;
        if (process is null)
        {
            Release();
            return;
        }

        try
        {
            // While the caller's Process holds its handle, the id cannot name another process.
            var watch = Process.GetProcessById(process.Id);
            _watch = watch;
            watch.Exited += (_, _) => Release();
            watch.EnableRaisingEvents = true;
            if (watch.HasExited) Release();
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Gone already, or not one this process may watch: nothing of it runs uncontained now that can be told.
            Release();
        }
    }

    private void Release()
    {
        if (Interlocked.Exchange(ref _released, 1) == 1) return;
        _jobs.Ended();
        Interlocked.Exchange(ref _watch, null)?.Dispose();
    }

    /// <summary>Unstarted, or its start failed: its share let go. Once bound to a process, its end lets go instead.</summary>
    public void Dispose()
    {
        if (Volatile.Read(ref _bound) == 0) Release();
    }
}
