using System.Diagnostics;
using Daoris.Ports.Processes;

namespace Daoris.Driver;

/// <summary>
/// Whose a process start is: a person's never waits behind the background's.
/// </summary>
public enum StartClass
{
    /// <summary>
    /// A start a person is waiting on: an agent for a conversation, a session going on with the person's words, a terminal's
    /// shell, a press (an update, a folder, an agent's install or sign-in, a plugin's trial), and the host the window waits on.
    /// </summary>
    Person,

    /// <summary>The machine's own: git, a plugin's background work, a probe, an agent the loop starts.</summary>
    Background,
}

/// <summary>
/// Process starts, a few at a time and never on the caller's thread, a person's first.
/// </summary>
/// <remarks>
/// <para><b>The gate.</b> A start is first admitted by the application's policy the root handed (git's places), then takes a
/// place of the machine's admission for its class (<see cref="StartClass"/>), waiting asynchronously, so the rest of a fan-out
/// queues as tasks rather than as threads; then it runs on the thread pool, so the caller's thread is free from the first wait.
/// How many places there are, and a person's reserved one, are the machine adapter's (<c>StartPlaces</c>). Only the start
/// is placed, never the run: a child that runs for minutes holds no place, and nothing waits on it but its own caller.</para>
///
/// <para><b>Every start of the desktop's</b> passes it: git's, a session's, a harness action's, a plugin's, the
/// host's, Edge's, and the file manager the window kit's launcher opens (<see cref="RunAsync(Action, CancellationToken)"/>).
/// <c>ProcessStartsCensusTests</c> names the few that stay outside, each with why, and whose each start inside is.</para>
/// </remarks>
public sealed class ProcessStarts
{
    /// <summary>The one gate every start of the desktop's passes, placed by the machine's admission the root chose.</summary>
    public static ProcessStarts Shared { get; } = new(() => MachinePorts.Admission, Process.Start);

    // The stand-in a test's flow makes its starts with (Within); null in production.
    private static readonly AsyncLocal<Func<ProcessStartInfo, Process?>?> s_within = new();

    // Whom a wait for a place in this flow is said to: the stage whose work it is, set for that work alone by
    // QueuedToAsync; null in a flow with none, and in work begun beside a stage.
    private static readonly AsyncLocal<Func<IDisposable>?> s_queue = new();

    private readonly Func<IMachineAdmission> _admission;
    private readonly Func<ProcessStartInfo, Process?> _start;

    /// <summary>A gate whose places are <paramref name="admission"/>'s, over <paramref name="start"/>.</summary>
    internal ProcessStarts(IMachineAdmission admission, Func<ProcessStartInfo, Process?> start)
        : this(() => admission, start)
    {
    }

    // The admission is read at each start, so the shared gate takes the one its root chose whenever the root chose it.
    private ProcessStarts(Func<IMachineAdmission> admission, Func<ProcessStartInfo, Process?> start)
    {
        _admission = admission;
        _start = start;
    }

    /// <summary>The machine's class of a start that is <paramref name="whose"/>.</summary>
    internal static string ClassOf(StartClass whose) => whose == StartClass.Person ? AdmissionClasses.Person : AdmissionClasses.Background;

    /// <summary>
    /// A person's start of <paramref name="info"/> (<see cref="StartAsync(ProcessStartInfo, ProcessPurpose, StartClass, CancellationToken)"/>):
    /// the doors a person waits on start through this.
    /// </summary>
    public Task<Process?> StartAsync(ProcessStartInfo info, ProcessPurpose purpose, CancellationToken ct) =>
        StartAsync(info, purpose, StartClass.Person, ct);

    /// <summary>
    /// Start <paramref name="info"/> once a place is free for <paramref name="whose"/>, on the thread pool, and hand the process to the one
    /// management as <paramref name="purpose"/>'s (<see cref="OwnedProcesses"/>) before its caller holds it. Cancelling
    /// <paramref name="ct"/> while waiting for a place throws <see cref="OperationCanceledException"/>; once the start is under
    /// way it is seen through, since a process abandoned as it started would run on unowned. The runtime's refusal to start is
    /// thrown as it threw it.
    /// </summary>
    /// <exception cref="GitStartRefusedException">A git's image started in no place of the git scheduler's.</exception>
    public Task<Process?> StartAsync(ProcessStartInfo info, ProcessPurpose purpose, StartClass whose, CancellationToken ct)
    {
        var place = MachinePorts.Admit(info.FileName, purpose);
        var start = s_within.Value ?? _start;
        return RunAsync(() =>
        {
            var process = start(info);
            OwnedProcesses.Shared.Take(process, purpose);
            if (place is not null && IdOf(process) is { } id) place.Bind(id);
            return process;
        }, place?.AdmissionClass ?? ClassOf(whose), ct);
    }

    private static int? IdOf(Process? process)
    {
        try
        {
            return process?.Id;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Run <paramref name="start"/>, a person's start the runtime's <see cref="Process"/> is not handed directly (the window
    /// kit's launcher opening the file manager, a terminal's shell), as <see cref="StartAsync(ProcessStartInfo, CancellationToken)"/>
    /// runs one: once a place is free, on the thread pool.
    /// </summary>
    public Task RunAsync(Action start, CancellationToken ct) => RunAsync<object?>(() =>
    {
        start();
        return null;
    }, AdmissionClasses.Person, ct);

    private async Task<T> RunAsync<T>(Func<T> start, string admissionClass, CancellationToken ct)
    {
        var timed = s_timed.Value;
        var asked = timed is null ? 0 : Stopwatch.GetTimestamp();
        var place = await PlaceAsync(admissionClass, ct).ConfigureAwait(false);
        var entered = timed is null ? 0 : Stopwatch.GetTimestamp();
        try
        {
            return await Task.Run(start, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            await place.DisposeAsync().ConfigureAwait(false);
            timed?.Invoke(entered - asked, Stopwatch.GetTimestamp() - entered);
        }
    }

    /// <summary>A place of the machine's for <paramref name="admissionClass"/>; a wait for one is said to this flow's stage.</summary>
    private async Task<IAdmittedPlace> PlaceAsync(string admissionClass, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        LockWaits.Waits(LockWaits.Starts);
        var placing = _admission().PlaceAsync(admissionClass, ct);
        if (placing.IsCompleted) return await placing.ConfigureAwait(false);

        // Said to the stage whose work made this start, from here until it is handed its place or gives up its wait.
        using (Queued()) return await placing.ConfigureAwait(false);
    }

    // Who is told a start's wait and creation in this flow (Timing); null in a flow nobody times.
    private static readonly AsyncLocal<Action<long, long>?> s_timed = new();

    /// <summary>
    /// Tell <paramref name="timed"/> the wait for a place and the creation inside it, in <see cref="Stopwatch"/> ticks, of each start this
    /// flow makes through a gate until disposed (the gate's wait, the creation and the run apart).
    /// It changes nothing of the start: one the runtime refused or that threw is timed still, and one cancelled while it waited for its
    /// place, which never started, is not.
    /// </summary>
    internal static IDisposable Timing(Action<long, long> timed)
    {
        var before = s_timed.Value;
        s_timed.Value = timed;
        return new Untimed(before);
    }

    private sealed class Untimed(Action<long, long>? before) : IDisposable
    {
        public void Dispose() => s_timed.Value = before;
    }

    /// <summary>
    /// The test seam: until disposed, every start this flow makes through a gate is made by
    /// <paramref name="start"/> instead of the runtime, still on the thread pool and inside its place, so a test sees where a
    /// site's start is made and answers it without a process. Production opens none.
    /// </summary>
    public static IDisposable Within(Func<ProcessStartInfo, Process?> start)
    {
        var before = s_within.Value;
        s_within.Value = start;
        return new Ending(before);
    }

    private sealed class Ending(Func<ProcessStartInfo, Process?>? before) : IDisposable
    {
        public void Dispose() => s_within.Value = before;
    }

    internal static IDisposable? Queued() => s_queue.Value?.Invoke();

    /// <summary>
    /// Run <paramref name="work"/> with each wait for a place that a start of its flow makes said to <paramref name="queue"/>:
    /// called as a wait begins (<see cref="Queued"/>, the git scheduler's too), and what it returns disposed as the wait ends, handed its place or given
    /// up. A look's stage hands its span, so its line says the wait as the stage's queue, apart from its hold. Null says them to
    /// nobody: work begun beside a stage, which the stage does not wait on, is never the stage's, until a stage of its own takes it.
    /// </summary>
    /// <remarks>
    /// Set inside this method's own flow, so it reaches the work and what the work starts, and never the caller: an async method's
    /// changes to its flow do not return to its caller. Nothing is restored, and no span ended in another flow can leave it set.
    /// </remarks>
    public static async Task<T> QueuedToAsync<T>(Func<IDisposable>? queue, Func<Task<T>> work)
    {
        s_queue.Value = queue;
        return await work().ConfigureAwait(false);
    }

    /// <inheritdoc cref="QueuedToAsync{T}(Func{IDisposable}?, Func{Task{T}})"/>
    public static async Task QueuedToAsync(Func<IDisposable>? queue, Func<Task> work)
    {
        s_queue.Value = queue;
        await work().ConfigureAwait(false);
    }
}
