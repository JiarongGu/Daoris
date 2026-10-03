namespace Daoris.Driver;

/// <summary>
/// The landing's side of the plugin wire (WSR4, D100): the plugin a branch rule names, started for one
/// landing, told the branch Daoris made on <see cref="HookPoints.Land"/>, and stopped again.
/// </summary>
/// <remarks>
/// <para><b>Daoris pushes nothing and opens nothing</b> (D37, D87). The plugin's process does both, for
/// its own platform, with whatever that platform's tools are signed in as — the push is the plugin's act,
/// installed and named by the person, never Daoris's.</para>
///
/// <para><b>Started per landing, not kept with the loop.</b> A landing is a press, and it happens where
/// the press is: in the shell, or in a terminal's <c>daoris-driver trees land</c>, which has no loop. A
/// process that exists exactly as long as the one frame it answers is the registration-as-effect rule
/// (D64 §4) at its smallest.</para>
///
/// <para><b>It never throws for the plugin's sake.</b> Whatever the plugin does — not start, answer late,
/// answer wrongly, refuse — comes back as a <see cref="PluginLanding"/> marked failed, with the sentence,
/// because the branch is already made and the press is owed an answer about both.</para>
///
/// <para><b>What it did is kept without the plugin's words</b> (PLUGUI1d, D119 §4.2): the frame's start, its
/// answer or failure, and its stop are <c>plugin.*</c> lines, by <c>landing</c> or <c>hand</c>, and words in the
/// loop's record of the plugin's health where the process runs the loop. Never the pull request's address, the
/// plugin's sentence or what it wrote to stderr. A plugin the press refuses before it is started was never
/// spoken to, and writes nothing.</para>
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
    TimeSpan? statePatience = null)
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
    /// push and a platform's API are network round trips, and the person pressed and is waiting.
    /// </summary>
    public static readonly TimeSpan DefaultPatience = TimeSpan.FromMinutes(2);

    // A start a test handed in serves the asks too; the driver's own waits StatePatience there rather than a landing's two minutes.
    private readonly bool _injected = start is not null;

    private readonly Func<PluginEntry, Action<string>, CancellationToken, Task<IHookChannel>> _start =
        start ?? (async (plugin, onLine, ct) =>
            await HookProcess.StartAsync(plugin, home, onLine, ct, patience ?? DefaultPatience).ConfigureAwait(false));

    /// <summary>The moment an occasion reckons by: whether an entry was asked in the last minute, and when an answer was asked.</summary>
    public DateTimeOffset Now => _clock();

    /// <summary>The home's plugins, read as the driver reads them — a harness name this build carries refused.</summary>
    public PluginCatalog Catalog() => PluginCatalog.Load(home, AdapterSet.Built().Names);

    /// <summary>Why <paramref name="plugin"/> cannot land work here now, or null (<see cref="LandingRules.PluginProblem"/>).</summary>
    public string? Problem(string plugin) => LandingRules.PluginProblem(plugin, Catalog());

    /// <summary>Speak the one frame and hear the answer — or the sentence saying why there is none.</summary>
    public Task<PluginLanding> LandAsync(string plugin, LandingFrame frame, CancellationToken ct = default) =>
        SpeakAsync(plugin, frame, PluginEvents.ByLanding, ct);

    /// <summary>The same frame for a branch a landing made earlier, handed on after it (WSR5b): the machine log says it was a hand-off.</summary>
    public Task<PluginLanding> HandAsync(string plugin, LandingFrame frame, CancellationToken ct = default) =>
        SpeakAsync(plugin, frame, PluginEvents.ByHand, ct);

    private async Task<PluginLanding> SpeakAsync(string plugin, LandingFrame frame, string by, CancellationToken ct)
    {
        var catalog = Catalog();
        if (LandingRules.PluginProblem(plugin, catalog) is { } problem) return Failed(plugin, problem);
        var entry = catalog.Plugins.First(p => string.Equals(p.Manifest.Id, plugin, StringComparison.OrdinalIgnoreCase));
        var id = entry.Manifest.Id;

        IHookChannel? channel = null;
        var where = PluginEvents.AtStart;
        var took = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            channel = await _start(entry, line => Say(id, line), ct).ConfigureAwait(false);
            _log.Started(id, channel.Points, took.ElapsedMilliseconds, by);
            if (!channel.Points.Contains(HookPoints.Land, StringComparer.Ordinal))
            {
                // It answered the handshake, and not with the point it declares: it cannot be asked.
                _log.Failed(id, HookPoints.Land, PluginEvents.Unreadable, code: null, ms: null, by);
                return Failed(id, $"plugin `{id}` declares `{HookPoints.Land}` but its process does not listen there.");
            }

            where = HookPoints.Land;
            took.Restart();
            var answer = await channel.LandAsync(HookFrames.Land(frame), ct).ConfigureAwait(false);
            _log.Called(id, HookPoints.Land, answer.Pushed ? PluginEvents.Pushed : PluginEvents.NotPushed, took.ElapsedMilliseconds);
            Say(id, $"landed `{frame.Branch}`: {(answer.Pushed ? "pushed" : "not pushed")}"
                + (answer.PullRequest is { } pr ? $", {pr}" : "") + $" — {answer.Message}");
            return answer with { Plugin = id };
        }
        catch (DriverException error)
        {
            var kind = PluginFailures.KindOf(error, where == PluginEvents.AtStart ? PluginEvents.Unstartable : PluginEvents.Errored);
            _log.Failed(id, where, kind, kind == PluginEvents.Exited ? channel?.ExitCode : null, took.ElapsedMilliseconds, by);
            return Failed(id, error.Message);
        }
        finally
        {
            if (channel is not null)
            {
                await channel.DisposeAsync().ConfigureAwait(false);
                _log.Stopped(id, PluginEvents.Ended, by);
            }
        }
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
        StateAnswer Failed(string kind, string why)
        {
            Say(ask.Plugin, why);
            return new(ask, null, kind, why);
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
        try
        {
            var answer = await process.Channel!.StateAsync(HookFrames.State(ask.Frame), bound.Token).ConfigureAwait(false);
            // The answer's code, never its address or its words (D119 §4.2).
            _log.Called(process.Id, HookPoints.State, answer.State, took.ElapsedMilliseconds);
            Say(process.Id, $"answered for `{ask.Entry.Branch}`: {answer.State}" + (answer.Message is { } words ? $" — {words}" : ""));
            return new(ask, answer with { Plugin = process.Id }, null);
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
        await HookProcess.StartAsync(plugin, home, onLine, ct, _statePatience).ConfigureAwait(false);

    private PluginLanding Failed(string plugin, string why)
    {
        Say(plugin, why);
        return new PluginLanding(plugin, Pushed: false, PullRequest: null, why, Failed: true);
    }

    private void Say(string plugin, string line) => say?.Invoke(plugin, line);
}
