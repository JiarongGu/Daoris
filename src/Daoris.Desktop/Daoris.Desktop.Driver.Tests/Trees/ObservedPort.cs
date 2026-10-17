using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The plugins' work processes as the runtime sees them (FABRIC-PERFORM1i): each call counted, each set told kept as the frame actually sent
/// to the current generation unless its send fails, the events each plugin sends queued, and every refusal counted.
/// </summary>
internal sealed class ObservedPort : IWorkRuntimePort
{
    public int? Generation { get; set; } = 1;

    public List<WorkPerformCall> Sent { get; } = [];

    public List<IReadOnlyDictionary<string, IReadOnlyList<WatchedUnit>>> Told { get; } = [];

    public Queue<WorkStepHeard> Heard { get; } = new();

    public List<(string Plugin, string Code)> Refusals { get; } = [];

    /// <summary>Where set and true for a plugin's units, that send fails: the process keeps the frame it was last sent.</summary>
    public Func<IReadOnlyList<WatchedUnit>, bool>? Fails { get; set; }

    private readonly Dictionary<string, WorkWatchFrame> _sent = new(StringComparer.Ordinal);

    public Task<int> PerformAsync(string plugin, WorkPerformCall call, CancellationToken ct)
    {
        var generation = Generation ?? throw new DriverException("not running");
        lock (Sent) Sent.Add(call);
        return Task.FromResult(generation);
    }

    public Task<(WorkReconcileAnswer Answer, int Generation)> ReconcileStepAsync(string plugin, WorkReconcileCall call, CancellationToken ct) =>
        Task.FromResult((new WorkReconcileAnswer(ReconcileStates.Running), Generation ?? throw new DriverException("not running")));

    public int? GenerationOf(string plugin) => Generation;

    public void Watch(IReadOnlyDictionary<string, IReadOnlyList<WatchedUnit>> awaited)
    {
        lock (Told) Told.Add(awaited);
        if (Generation is not { } generation) return;
        foreach (var plugin in awaited.Keys.Concat(_sent.Keys).Distinct(StringComparer.Ordinal).ToList())
        {
            var units = awaited.GetValueOrDefault(plugin) ?? [];
            if (Fails?.Invoke(units) == true) continue;
            _sent[plugin] = new WorkWatchFrame(generation, false, units);
        }
    }

    public WorkWatchFrame? SentTo(string plugin, int generation) =>
        _sent.TryGetValue(plugin, out var frame) && frame.Generation == generation ? frame : null;

    /// <summary>The plugin's process restarted: a new generation, told nothing yet.</summary>
    public void Restart()
    {
        Generation = (Generation ?? 0) + 1;
        _sent.Clear();
    }

    public IReadOnlyList<WorkStepHeard> TakeSteps()
    {
        lock (Heard)
        {
            var taken = Heard.ToList();
            Heard.Clear();
            return taken;
        }
    }

    public void Refused(string plugin, string code) => Refusals.Add((plugin, code));
}
