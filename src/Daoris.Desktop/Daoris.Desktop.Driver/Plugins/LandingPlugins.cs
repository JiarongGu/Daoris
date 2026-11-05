namespace Daoris.Driver;

/// <summary>
/// The trees' side of the plugin wire: whether a plugin can open pull requests here, and an occasion's state asks at
/// <see cref="HookPoints.State"/>, a process started for one occasion's frames and stopped after them.
/// </summary>
/// <remarks>
/// <b>Daoris pushes through its own push act and opens nothing</b> (D37, D241 point 4). A pull request is opened by the plugin's
/// <c>pull-request</c> step on <see cref="HookPoints.Perform"/>, which the work wire's loop speaks; nothing here speaks it.
/// </remarks>
public sealed class LandingPlugins(
    string home,
    Func<PluginEntry, Action<string>, CancellationToken, Task<IHookChannel>>? start = null,
    // Where a plugin's word goes: the console under `plugin:<id>`, or a terminal's own lines.
    Action<string, string>? say = null,
    TimeSpan? patience = null,
    // The process's machine log (PLUGUI1d). Null writes none.
    MachineLog? log = null,
    // The loop's own record of each plugin's health (D119 §2), in the process that runs the loop. Null keeps none.
    PluginHealth? health = null,
    // The loop's own writer of the plugin lines, where the loop lands work itself (LAND2b): its hook set's, so a landing at
    // done is logged and recorded where its hooks are. It outranks the two above.
    PluginLog? pluginLog = null,
    // What an occasion's asks reckon their bound by (PLUGHOOK1a); the clock by default.
    Func<DateTimeOffset>? clock = null,
    // How long one ask waits, its handshake inside the first (PLUGHOOK1a); StatePatience by default.
    TimeSpan? statePatience = null) : IWorkplacePluginWire
{
    private readonly PluginLog _log = pluginLog ?? new(log, health);
    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly TimeSpan _statePatience = statePatience ?? StatePatience;

    /// <summary>
    /// How long one ask at <see cref="HookPoints.State"/> waits, the handshake inside the first (PLUGHOOK1a, D148 point 2): one
    /// <c>az</c> or <c>gh</c> call takes seconds. A judgement, not a measurement.
    /// </summary>
    public static readonly TimeSpan StatePatience = TimeSpan.FromSeconds(30);

    /// <summary>After this long an occasion starts no more frames; an entry it did not reach is not asked this time (design §2.1).</summary>
    public static readonly TimeSpan StateBound = TimeSpan.FromSeconds(60);

    /// <summary>
    /// How long a plugin has to push and open the pull request. Longer than a decision's ten seconds: a
    /// push and a platform's API are network round trips, and the person pressed and is waiting. The workplace's
    /// <see cref="LandingPatience.Plugin"/> holds it, which the landing's hold is reckoned past.
    /// </summary>
    public static readonly TimeSpan DefaultPatience = LandingPatience.Plugin;

    // A start a test handed in serves the asks too; the driver's own waits StatePatience there rather than a landing's two minutes.
    private readonly bool _injected = start is not null;

    private readonly Func<PluginEntry, Action<string>, CancellationToken, Task<IHookChannel>> _start =
        start ?? (async (plugin, onLine, ct) =>
            await HookChannels.StartAsync(plugin, home, onLine, ct, patience ?? DefaultPatience).ConfigureAwait(false));

    /// <summary>The moment an occasion reckons by: whether an entry was asked in the last minute, and when an answer was asked.</summary>
    public DateTimeOffset Now => _clock();

    /// <summary>The home's plugins, read as the driver reads them — a harness name this build carries refused.</summary>
    public PluginCatalog Catalog() => PluginCatalog.Load(home, AdapterSet.Built().Names);

    /// <summary>Why <paramref name="plugin"/> cannot land work here now, or null (<see cref="ProblemOf"/>).</summary>
    public string? Problem(string plugin) => ProblemOf(plugin, Catalog());

    /// <summary>
    /// Why the plugin a landing names cannot land work on this machine, in a sentence, or null when it can: not installed, switched off,
    /// refused by the catalogue, or speaking on no <see cref="HookPoints.Perform"/> point, where a pull request's step is performed.
    /// </summary>
    public static string? ProblemOf(string plugin, IWorkplacePluginCatalog catalog)
    {
        var entry = catalog.Find(plugin);
        if (entry is null)
        {
            return $"the landing names plugin `{plugin}`, which is not installed on this machine — "
                + "`daoris plugin add <folder>` installs one, and `daoris plugin list` shows what there is.";
        }

        if (!entry.Enabled)
        {
            return $"plugin `{plugin}` is switched off on this machine — `daoris plugin enable {entry.Id}` switches it on.";
        }

        if (entry.Problem is { } problem) return $"plugin `{plugin}` contributes nothing: {problem}";

        return entry.Points.Contains(HookPoints.Perform, StringComparer.Ordinal)
            ? null
            : $"plugin `{plugin}` does not open pull requests — its manifest speaks on no `{HookPoints.Perform}` point.";
    }

    /// <summary>
    /// An occasion's asks at <see cref="HookPoints.State"/> (PLUGHOOK1a, D148 point 2, design §2.1): one process per plugin,
    /// started for its frames and stopped after them; frames one at a time, the entry asked longest ago first, each waiting
    /// <see cref="StatePatience"/> with the handshake inside the first; and no frame started once <see cref="StateBound"/> has
    /// passed. It never throws for a plugin's sake: each failure comes back as its code and Daoris's sentence, and keeps
    /// everything where it is.
    /// </summary>
    /// <returns>One answer per ask, in the order asked.</returns>
    public async Task<IReadOnlyList<StateAnswer>> AskStatesAsync(IReadOnlyList<StateAsk> asks, CancellationToken ct = default)
    {
        var results = new List<StateAnswer>();
        if (asks.Count == 0) return results;
        var catalog = Catalog();
        var began = _clock();
        // Each plugin's process for this occasion, or why it has none: a start that failed is not tried again for its next entry.
        var held = new Dictionary<string, (IHookChannel? Channel, string Id, string? Kind, string? Why)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var ask in asks.OrderBy(each => PullRequestAsking.LastAsked(each.Entry)))
            {
                if (_clock() - began >= StateBound)
                {
                    results.Add(new(ask, null, PullRequestCodes.NotAsked,
                        $"`{ask.Entry.Branch}`'s pull request was not asked this time: the asks before it took {PluginKit.Seconds(StateBound)}."));
                    continue;
                }

                results.Add(await AskOneAsync(ask, catalog, held, ct).ConfigureAwait(false));
            }
        }
        finally
        {
            foreach (var (channel, id, _, _) in held.Values)
            {
                if (channel is null) continue;
                await channel.DisposeAsync().ConfigureAwait(false);
                _log.Stopped(id, PluginEvents.Ended, PluginEvents.ByState);
            }
        }

        return results;
    }

    private async Task<StateAnswer> AskOneAsync(
        StateAsk ask, PluginCatalog catalog, Dictionary<string, (IHookChannel? Channel, string Id, string? Kind, string? Why)> held,
        CancellationToken ct)
    {
        // When this entry was asked (FABRIC-END6c): the attempt's time, then, once its process listens, the time its own request went
        // out, so each answer is stamped with its own and not the occasion's start.
        var asked = _clock();
        StateAnswer Failed(string kind, string why)
        {
            Say(ask.Plugin, why);
            return new(ask, null, kind, why) { AskedAt = asked };
        }

        // The handshake waits inside the first frame's bound (design §2.1).
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(_statePatience);
        var where = PluginEvents.AtStart;
        var took = System.Diagnostics.Stopwatch.StartNew();
        if (!held.TryGetValue(ask.Plugin, out var process))
        {
            if (PullRequestAsking.Problem(ask.Plugin, catalog) is { } problem) return new(ask, null, PullRequestCodes.Unready, problem);
            var entry = catalog.Plugins.First(p => string.Equals(p.Manifest.Id, ask.Plugin, StringComparison.OrdinalIgnoreCase));
            var id = entry.Manifest.Id;
            try
            {
                var channel = await StartForStateAsync(entry, line => Say(id, line), bound.Token).ConfigureAwait(false);
                _log.Started(id, channel.Points, took.ElapsedMilliseconds, PluginEvents.ByState);
                process = channel.Points.Contains(HookPoints.State, StringComparer.Ordinal)
                    ? (channel, id, null, null)
                    : (channel, id, PluginEvents.Unreadable, $"plugin `{id}` declares `{HookPoints.State}` but its process does not listen there.");
                if (process.Kind is not null) _log.Failed(id, HookPoints.State, PluginEvents.Unreadable, code: null, ms: null, PluginEvents.ByState);
            }
            catch (DriverException error)
            {
                var kind = PluginFailures.KindOf(error, PluginEvents.Unstartable);
                _log.Failed(id, PluginEvents.AtStart, kind, code: null, took.ElapsedMilliseconds, PluginEvents.ByState);
                process = (null, id, kind, error.Message);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _log.Failed(id, PluginEvents.AtStart, PluginEvents.Late, code: null, took.ElapsedMilliseconds, PluginEvents.ByState);
                process = (null, id, PluginEvents.Late, $"plugin `{id}` did not answer the handshake within {PluginKit.Seconds(_statePatience)}.");
            }

            held[ask.Plugin] = process;
        }

        if (process.Kind is { } refused) return Failed(refused, process.Why!);
        where = HookPoints.State;
        took.Restart();
        asked = _clock();
        try
        {
            var answer = await process.Channel!.StateAsync(HookFrames.State(ask.Frame), bound.Token).ConfigureAwait(false);
            // The answer's code, never its address or its words (D119 §4.2).
            _log.Called(process.Id, HookPoints.State, answer.State, took.ElapsedMilliseconds);
            Say(process.Id, $"answered for `{ask.Entry.Branch}`: {answer.State}" + (answer.Message is { } words ? $" — {words}" : ""));
            return new(ask, answer with { Plugin = process.Id }, null) { AskedAt = asked };
        }
        catch (DriverException error)
        {
            var kind = PluginFailures.KindOf(error, PluginEvents.Errored);
            _log.Failed(process.Id, where, kind, kind == PluginEvents.Exited ? process.Channel!.ExitCode : null, took.ElapsedMilliseconds, PluginEvents.ByState);
            return Failed(kind, error.Message);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _log.Failed(process.Id, where, PluginEvents.Late, code: null, took.ElapsedMilliseconds, PluginEvents.ByState);
            return Failed(PluginEvents.Late, $"plugin `{process.Id}` did not answer `hook/{HookPoints.State}` within {PluginKit.Seconds(_statePatience)}.");
        }
    }

    /// <summary>The plugin's process for an occasion's asks: the injected start where a test hands one, else the driver's own, waiting <see cref="StatePatience"/>.</summary>
    private Task<IHookChannel> StartForStateAsync(PluginEntry plugin, Action<string> onLine, CancellationToken ct) =>
        _injected ? _start(plugin, onLine, ct) : StartProcessAsync(plugin, onLine, ct);

    private async Task<IHookChannel> StartProcessAsync(PluginEntry plugin, Action<string> onLine, CancellationToken ct) =>
        await HookChannels.StartAsync(plugin, home, onLine, ct, _statePatience).ConfigureAwait(false);

    private void Say(string plugin, string line) => say?.Invoke(plugin, line);
}
