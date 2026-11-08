namespace Daoris.Driver;

/// <summary>Where a placed plugin's process serves: a repository and its workspace, with the checkout its origin is read from.</summary>
public sealed record WorkPlace(string Repository, string Workspace, string Root);

/// <summary>
/// The work processes of the plugins that take the host's API token, one per place: a plugin with a pull-request step is
/// started for each repository and workspace that awaits it, with the token of the identity serving that place's origin, and a place
/// never shares a process with another, so a token reaches no process that serves another identity's origin. Every other plugin keeps its
/// one process under its own id.
/// </summary>
/// <remarks>
/// <para>A place's process is kept by a key beneath the plugin's id, so <see cref="WorkPlugins"/> supervises it as it does any other: its
/// waits, its unsound mark, its generation and its idle stop are the place's own. What the evaluation runtime sees stays the plugin's id: its
/// generations are <see cref="Outer"/> numbers, unique across the plugin's places, and an attempt or an event is routed to the place its unit
/// is told for.</para>
/// </remarks>
internal sealed class PlacedWork
{
    /// <summary>The room one place's own generations take in an <see cref="Outer"/> number.</summary>
    public const int Span = 1_000_000;

    private const char Joint = '\u001f';

    private sealed record Cell(string Plugin, int Index, WorkPlace Place);

    private readonly Lock _gate = new();
    private readonly Lock _applying = new();
    private readonly Dictionary<string, Cell> _cells = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Plugin, string Unit), string> _unitKeys = [];
    private HashSet<string> _placed = new(StringComparer.Ordinal);
    private IReadOnlyDictionary<string, IReadOnlyList<WatchedUnit>> _awaited = new Dictionary<string, IReadOnlyList<WatchedUnit>>();

    /// <summary>Whether a plugin takes the host's token: it declares the pull-request step.</summary>
    public static bool Takes(PluginEntry plugin) =>
        plugin.Manifest.Steps.Any(step => PullRequestKind.PluginOf(step.Kind) == plugin.Manifest.Id);

    /// <summary>The plugin a process key belongs to: the key itself where it is a plugin's own.</summary>
    public static string PluginOf(string key)
    {
        var joint = key.IndexOf(Joint);
        return joint < 0 ? key : key[..joint];
    }

    /// <summary>Whether <paramref name="key"/> is a place's process rather than a plugin's own.</summary>
    public static bool IsPlace(string key) => key.Contains(Joint);

    /// <summary>The plugins that take the token, whole as the catalogue says; each place already known is kept.</summary>
    public void Place(IEnumerable<string> plugins)
    {
        lock (_gate) _placed = new HashSet<string>(plugins, StringComparer.Ordinal);
    }

    public bool IsPlaced(string plugin)
    {
        lock (_gate) return _placed.Contains(plugin);
    }

    /// <summary>The awaited set the runtime told, whole, held for <see cref="Divide"/>.</summary>
    public void Await(IReadOnlyDictionary<string, IReadOnlyList<WatchedUnit>> awaited)
    {
        lock (_gate) _awaited = awaited;
    }

    /// <summary>
    /// The held awaited set as the processes are told it: a placed plugin's units split by place, each place under its own key, and the
    /// rest as they were. A place no unit awaits any longer is named by no key, so its process is idle.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<WatchedUnit>> Divide()
    {
        lock (_gate)
        {
            var told = new Dictionary<string, IReadOnlyList<WatchedUnit>>(StringComparer.Ordinal);
            _unitKeys.Clear();
            foreach (var (plugin, units) in _awaited)
            {
                if (!_placed.Contains(plugin))
                {
                    told[plugin] = units;
                    continue;
                }

                foreach (var place in units.GroupBy(unit => (unit.Repository, unit.Workspace)))
                {
                    var key = $"{plugin}{Joint}{place.Key.Repository}{Joint}{place.Key.Workspace}";
                    if (!_cells.ContainsKey(key))
                    {
                        var index = _cells.Values.Count(cell => cell.Plugin == plugin) + 1;
                        _cells[key] = new Cell(plugin, index, new WorkPlace(place.Key.Repository, place.Key.Workspace, place.First().Root));
                    }

                    told[key] = [.. place];
                    foreach (var unit in place)
                    {
                        if (unit.Unit is not null) _unitKeys[(plugin, unit.Unit)] = key;
                    }
                }
            }

            return told;
        }
    }

    /// <summary>
    /// <see cref="Divide"/> and <paramref name="apply"/> as one step: a look and a <c>Watch</c> each divide the set held and apply it, and
    /// the one that divided first applies first, so the process is never left told a set older than the one held.
    /// </summary>
    public void Apply(Action<IReadOnlyDictionary<string, IReadOnlyList<WatchedUnit>>> apply)
    {
        lock (_applying) apply(Divide());
    }

    /// <summary>The keys of every place a placed plugin has been told for, or the plugin's own key where it is not placed.</summary>
    public IReadOnlyList<string> KeysOf(string plugin)
    {
        lock (_gate)
        {
            return _placed.Contains(plugin)
                ? [.. _cells.Where(pair => pair.Value.Plugin == plugin).OrderBy(pair => pair.Value.Index).Select(pair => pair.Key)]
                : [plugin];
        }
    }

    /// <summary>Every place's key, for the plugins the catalogue still places.</summary>
    public IReadOnlyList<string> Places()
    {
        lock (_gate) return [.. _cells.Where(pair => _placed.Contains(pair.Value.Plugin)).Select(pair => pair.Key)];
    }

    public WorkPlace? PlaceOf(string key)
    {
        lock (_gate) return _cells.TryGetValue(key, out var cell) ? cell.Place : null;
    }

    /// <summary>
    /// The key of the process an attempt on <paramref name="unit"/> is sent to: the plugin's own where it is not placed, the place the unit
    /// is told for where it is, and null where it is told for none.
    /// </summary>
    public string? KeyFor(string plugin, string? unit)
    {
        lock (_gate)
        {
            if (!_placed.Contains(plugin)) return plugin;
            return unit is not null && _unitKeys.TryGetValue((plugin, unit), out var key) ? key : null;
        }
    }

    /// <summary>The number the runtime sees of a process's generation: unique across the plugin's places, and the generation itself for a plugin's own.</summary>
    public int Outer(string key, int generation)
    {
        lock (_gate) return (_cells.TryGetValue(key, out var cell) ? cell.Index * Span : 0) + generation;
    }

    /// <summary>The process an <see cref="Outer"/> number of <paramref name="plugin"/> names, with its own generation; null where no place has that index.</summary>
    public (string Key, int Generation)? Inner(string plugin, int outer)
    {
        var index = outer / Span;
        if (index == 0) return (plugin, outer);
        lock (_gate)
        {
            var found = _cells.FirstOrDefault(pair => pair.Value.Plugin == plugin && pair.Value.Index == index);
            return found.Key is null ? null : (found.Key, outer % Span);
        }
    }
}
