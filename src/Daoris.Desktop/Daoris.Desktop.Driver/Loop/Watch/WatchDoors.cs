namespace Daoris.Driver;

/// <summary>
/// The parts of a running loop that its watch's own surface reaches (<see cref="DriverWatch"/>): the evaluation runtime a door reads, link
/// 8's wake and its doors' whole-pass asks, the home's and the refs' watchers a resume reopens, and the window's focus kept for the pull
/// request's moments. A run publishes each as it starts it; the window's threads read them at any moment.
/// </summary>
/// <remarks>
/// <para><b>How each is published.</b> One reference each, written whole, so no lock: the runtime through a volatile field, the wake and the
/// asks with <see cref="Volatile"/>, and the asks taken back with one exchange. The watchers are plain references, read only to reopen or
/// unblock a watcher that each call tolerates finding stopped.</para>
///
/// <para><b>What a race would break.</b> The run withdraws the wake and the asks as it closes, before its executor and transport are
/// disposed: a wake read after that fires nothing, and the asks taken back by the exchange are answered not passed by the close alone, so
/// no press waits on a loop that closed and none is answered twice.</para>
/// </remarks>
internal sealed class WatchDoors
{
    private volatile NodeRuntime? _nodes;

    // Fires link 8 alone on the loop's chain: set while the loop runs with a hook set, null before and after.
    private Action<ChainEvent>? _nodesWake;

    // The doors' asks for a whole pass of link 8: set while the runtime runs, null otherwise.
    private PassAsks? _passAsks;

    /// <summary>
    /// The window's focus told to the due moments of a pull request's ask; one that comes before the loop starts them is kept
    /// until they exist, since a start's window takes the focus first.
    /// </summary>
    internal PendingFocus Focus { get; } = new();

    /// <summary>The home's record files' watch the run started, or null before it.</summary>
    internal HomeWatch? Files { get; set; }

    /// <summary>The ref watch the run started, or null before it.</summary>
    internal RefWatch? Refs { get; set; }

    /// <summary>The run's evaluation runtime, or null before a run starts one.</summary>
    internal NodeRuntime? Nodes
    {
        get => _nodes;
        set => _nodes = value;
    }

    /// <summary>Fires link 8 alone, where a run has bound it.</summary>
    internal void Wake(ChainEvent wake) => Volatile.Read(ref _nodesWake)?.Invoke(wake);

    /// <summary>The run's wake of link 8, or null as it closes.</summary>
    internal void Waking(Action<ChainEvent>? wake) => Volatile.Write(ref _nodesWake, wake);

    /// <summary>The doors' whole-pass asks while the runtime runs; null otherwise.</summary>
    internal PassAsks? Asks => Volatile.Read(ref _passAsks);

    /// <summary>The runtime's asks published, as link 8 is bound.</summary>
    internal void Asking(PassAsks asks) => Volatile.Write(ref _passAsks, asks);

    /// <summary>The asks taken back as the run closes, once: null where none were published or another close took them.</summary>
    internal PassAsks? Unasked() => Interlocked.Exchange(ref _passAsks, null);
}
