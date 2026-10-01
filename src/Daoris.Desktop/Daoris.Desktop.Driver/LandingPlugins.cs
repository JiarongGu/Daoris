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
    PluginHealth? health = null)
{
    private readonly PluginLog _log = new(log, health);

    /// <summary>
    /// How long a plugin has to push and open the pull request. Longer than a decision's ten seconds: a
    /// push and a platform's API are network round trips, and the person pressed and is waiting.
    /// </summary>
    public static readonly TimeSpan DefaultPatience = TimeSpan.FromMinutes(2);

    private readonly Func<PluginEntry, Action<string>, CancellationToken, Task<IHookChannel>> _start =
        start ?? (async (plugin, onLine, ct) =>
            await HookProcess.StartAsync(plugin, home, onLine, ct, patience ?? DefaultPatience).ConfigureAwait(false));

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

    private PluginLanding Failed(string plugin, string why)
    {
        Say(plugin, why);
        return new PluginLanding(plugin, Pushed: false, PullRequest: null, why, Failed: true);
    }

    private void Say(string plugin, string line) => say?.Invoke(plugin, line);
}
