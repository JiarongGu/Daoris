using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// What the loop's run (<see cref="LoopRun"/>) brings up and the loop's doors (<see cref="DriverLoop"/>) read: one for the loop's life.
/// </summary>
/// <remarks>
/// <para><b>Who writes what.</b> <see cref="Stopping"/> is cancelled by the stop alone, under the address's lock
/// (<see cref="LoopAddress"/>). The run completes <see cref="HostReady"/> once, whatever the host did, since the splash awaits it. The
/// run sets <see cref="Sync"/>, <see cref="Hooks"/> and <see cref="Watch"/> once each, in that order, as it comes up; the stop takes the
/// hooks back once (<see cref="TakeHooks"/>), after the run ended or its wait ran out. Each look replaces <see cref="Placed"/> whole as
/// it reads the registry, and the journal's reader sets <see cref="HearsJournal"/> at each answer. <see cref="Focus"/> keeps a window's
/// focus until the watch exists, and the run then tells it.</para>
/// <para><b>The order.</b> No lock: each member is one reference or flag written whole and read as it stands. The placed map and the
/// journal's flag are read through a fence, since the standing judge reads the map under its own lock, which never awaits.</para>
/// </remarks>
internal sealed class LoopState
{
    private HookSet? _hooks;
    private volatile IReadOnlyDictionary<string, string> _placed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private bool _hearsJournal;

    /// <summary>The loop's stop, which ends the run, the slices' workers and every task the run started.</summary>
    public CancellationTokenSource Stopping { get; } = new();

    /// <summary>Whether the HTTP host answers, or provably will not: what navigation waits on.</summary>
    public TaskCompletionSource<bool> HostReady { get; } = new();

    /// <summary>The run's syncs, the ones its looks run and the ones <i>Sync now</i> runs; null before the service answers.</summary>
    public RemoteSyncSet? Sync { get; set; }

    /// <summary>The plugins that speak, whose processes live with the loop and stop with it.</summary>
    public HookSet? Hooks
    {
        get => _hooks;
        set => _hooks = value;
    }

    /// <summary>The watch every look runs in, once the run holds the home's lease.</summary>
    public DriverWatch? Watch { get; set; }

    /// <summary>
    /// The workspace the registry placed each repository in, as the last look read it: what a workspace's supervisor covers, read by
    /// the standing judge. Empty until the first look reads the registry.
    /// </summary>
    public IReadOnlyDictionary<string, string> Placed
    {
        get => _placed;
        set => _placed = value;
    }

    /// <summary>Whether the last answer to this process's journal reader came from a host that keeps the journal.</summary>
    public bool HearsJournal
    {
        get => Volatile.Read(ref _hearsJournal);
        set => Volatile.Write(ref _hearsJournal, value);
    }

    /// <summary>The window's focus, kept until the watch exists: a start's window takes the focus before the run makes its watch.</summary>
    public PendingFocus Focus { get; } = new();

    /// <summary>The hooks, taken back for the stop to let go, once.</summary>
    public HookSet? TakeHooks() => Interlocked.Exchange(ref _hooks, null);
}
