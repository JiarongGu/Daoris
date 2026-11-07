namespace Daoris.Driver;

/// <summary>
/// What the performer asks of a plugin's work process: <see cref="HookSet"/>'s fenced calls, each returning the generation
/// that answered, and the generation now current. A port, so the durable rules are proved without a process.
/// </summary>
public interface IWorkPerformPort
{
    Task<int> PerformAsync(string plugin, WorkPerformCall call, CancellationToken ct);

    Task<(WorkReconcileAnswer Answer, int Generation)> ReconcileStepAsync(string plugin, WorkReconcileCall call, CancellationToken ct);

    int? GenerationOf(string plugin);

    /// <summary>
    /// The current generation of the process generation <paramref name="held"/> belongs to: the same plugin's other place has its own, so an
    /// attempt or an event is fenced against the process that took it, never another place's.
    /// </summary>
    int? GenerationOf(string plugin, int held) => GenerationOf(plugin);

    /// <summary>
    /// One receipt's removal asked of the plugin's current process, with that process's generation. A port that has
    /// no cleanup adapter refuses, and the receipt is asked again on its back-off.
    /// </summary>
    Task<(WorkCleanupAnswer Answer, int Generation)> CleanupAsync(string plugin, WorkCleanupCall call, CancellationToken ct) =>
        Task.FromException<(WorkCleanupAnswer, int)>(new DriverException($"plugin `{plugin}` is asked to clean only on the work wire, and this port has none."));

    /// <summary>
    /// A frame from <paramref name="plugin"/> not believed: counted as a <c>plugin.step</c> line of its code, with no
    /// words, at most once a minute per plugin and code.
    /// </summary>
    void Refused(string plugin, string code)
    {
    }
}

/// <summary>
/// What the evaluation runtime asks of the plugins' work processes: the performer's fenced calls, and the supervision's two
/// halves, the awaited set told (<see cref="HookSet.Watch"/>) and the completions heard (<see cref="HookSet.TakeSteps"/>). A port, so the
/// runtime is proved without a process.
/// </summary>
public interface IWorkRuntimePort : IWorkPerformPort
{
    void Watch(IReadOnlyDictionary<string, IReadOnlyList<WatchedUnit>> awaited);

    IReadOnlyList<WorkStepHeard> TakeSteps();

    /// <summary>
    /// The last <c>work/watch</c> frame whose send to <paramref name="plugin"/>'s process of <paramref name="generation"/> completed,
    /// or null where none did: what that process is actually told, never the set desired since.
    /// </summary>
    WorkWatchFrame? SentTo(string plugin, int generation) => null;

    /// <summary>
    /// The plugins owed a receipt's cleanup (<see cref="HookSet.Owe"/>), whole: their process is kept up though no unit
    /// awaits their steps, since only it answers <c>work/cleanup</c>.
    /// </summary>
    void Owe(IReadOnlySet<string> plugins)
    {
    }
}
