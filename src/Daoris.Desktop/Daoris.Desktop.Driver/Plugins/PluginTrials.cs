using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>The kit's trial (PluginKit's <c>try</c>): a plugin's hooks run against frames in a scratch folder under the home.</summary>
public static class PluginTrials
{
    /// <summary>
    /// Where a trial keeps what it keeps, under the home: never the system's temporary folder, which is
    /// under the user profile, where nothing of Daoris's lives (D63; the owner's call on D101). A dot-folder,
    /// which both catalogue readers skip, so a scratch left by a trial cut short is never read as a plugin.
    /// </summary>
    public const string TrialsFolder = ".trials";

    /// <summary>The home's folder of trials' scratch, <c>&lt;home&gt;/plugins/.trials</c>.</summary>
    public static string TrialsOf(string home) => Path.Combine(home, PluginCatalog.Folder, TrialsFolder);

    /// <summary>
    /// Try a plugin in a folder anywhere — a plugins repository's, before anything is installed. What it
    /// keeps goes to a scratch data folder, and it is told a scratch home: a plugin nobody installed is
    /// part of no machine yet.
    /// </summary>
    /// <param name="home">The Daoris home, where the trial keeps its scratch (<see cref="TrialsOf"/>).</param>
    /// <exception cref="DriverException">There is nothing here try can check, or the options ask for something it cannot do.</exception>
    public static Task<PluginTrial> TryFolderAsync(string home, string folder, TrialOptions? options = null, CancellationToken ct = default) =>
        TryFolderAsync(home, folder, options, ct, scratchParent: null);

    /// <param name="scratchParent">Where the scratch folder goes instead of the home's — a test's, under a repository, to prove the sample root still names none.</param>
    internal static async Task<PluginTrial> TryFolderAsync(string home, string folder, TrialOptions? options, CancellationToken ct, string? scratchParent)
    {
        var full = PluginFolders.WholePath(folder);
        if (!PluginFolders.HasFile(Path.Combine(full, PluginCatalog.ManifestName)))
        {
            throw new DriverException($"no `{PluginCatalog.ManifestName}` in {full} — a plugin is a folder with a manifest at its root.");
        }

        var scratch = PluginKitFiles.ScratchIn(scratchParent ?? TrialsOf(home));
        try
        {
            var data = Path.Combine(scratch, "data");
            var (manifest, problem) = PluginCatalog.ReadFolder(full, data);
            // The plugin is told a scratch home of its own: one nobody installed is part of no machine yet. Its tools are
            // the machine's, so they are found by the home's own tools (PLUGTOOL1a).
            var told = Path.Combine(scratch, "home");
            PluginFolders.Make(told);
            return await TryAsync(new PluginEntry(manifest, full, data, Enabled: true, problem), told, home, scratch, options ?? new(), ct)
                .ConfigureAwait(false);
        }
        finally
        {
            PluginKitFiles.Remove(scratch);
        }
    }

    /// <summary>
    /// Try a plugin installed under the home, by its id, as the driver would start it there: its own
    /// folder, its own data folder, the home. Switched off or not — a person may try one before turning
    /// it on.
    /// </summary>
    public static async Task<PluginTrial> TryInstalledAsync(string home, string id, TrialOptions? options = null, CancellationToken ct = default)
    {
        var entry = PluginCatalog.Load(home, AdapterSet.Built().Names).Plugins
            .FirstOrDefault(plugin => string.Equals(plugin.Manifest.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? throw new DriverException($"no plugin `{id}` on this machine — `daoris plugin list` shows what there is.");

        var scratch = PluginKitFiles.ScratchIn(TrialsOf(home));
        try
        {
            return await TryAsync(entry, home, home, scratch, options ?? new(), ct).ConfigureAwait(false);
        }
        finally
        {
            PluginKitFiles.Remove(scratch);
        }
    }

    /// <remarks>
    /// <para><b>As the driver would.</b> The process is started from <see cref="HookProcess.StartInfo"/>,
    /// spoken to through <see cref="HookPeer"/>, and sent <see cref="HookFrames"/>' frames, so an answer the
    /// trial accepts is one the driver reads. The trial owns the process itself only to see what the driver
    /// does not need to: an exit code, a line on stdout that is not a frame, whether it left when told. The process is the
    /// <see cref="PluginTrialProcess"/>'s (STRUCT3a); what each answer means is said here.</para>
    ///
    /// <para><b>Stricter than the driver in one place</b>: a point the manifest declares and the process
    /// does not listen on fails. The driver tolerates it; a person reading the manifest would not.</para>
    ///
    /// <para>🔴 <b>The sample frames name no repository.</b> `root` is an empty scratch folder and git is
    /// pointed at a repository that is not there, because git walks UP: a folder with no repository of its
    /// own answers for the one above it, and a landing plugin that pushes first would push that. With the
    /// person's own frame, the frame is theirs, and so is what it names.</para>
    ///
    /// <para><b>Its tools first</b> (PLUGTOOL1a): each declared tool, then each of its checks, is a step in its own
    /// sentence, found by <paramref name="toolsHome"/>'s tools and checked in the scratch `root`. A problem there fails
    /// that step and never stops the trial.</para>
    /// </remarks>
    /// <param name="home">The home the plugin is told, and its start reads.</param>
    /// <param name="toolsHome">The machine's home, whose tools the plugin's declared tools are found by.</param>
    private static async Task<PluginTrial> TryAsync(
        PluginEntry entry, string home, string toolsHome, string scratch, TrialOptions options, CancellationToken ct)
    {
        var id = entry.Manifest.Id;
        if (options.Frame is not null && options.Point is null)
        {
            throw new DriverException("a frame needs its point: --point names where it is sent.");
        }

        if (options.Point is { } asked && PluginKit.Find(asked) is null)
        {
            throw new DriverException($"`{asked}` is not a point — one of: {PluginKit.Names}.");
        }

        var steps = new List<TrialStep>();
        var said = new List<string>();
        var command = entry.Manifest.Hooks?.Command ?? [];
        PluginTrial Trial() => new(id, entry.Folder, command, steps, Snapshot(said));

        if (entry.Problem is { } problem)
        {
            steps.Add(new("manifest", false, problem));
            return Trial();
        }

        var hooks = entry.Manifest.Hooks
            ?? throw new DriverException($"`{id}` speaks on no point — try speaks the hook wire, and a plugin that only declares runs nothing to try.");

        if (options.Point is { } point && !hooks.Points.Contains(point, StringComparer.Ordinal))
        {
            throw new DriverException($"`{point}` is not a point `{id}` declares ({string.Join(", ", hooks.Points)}).");
        }

        if (hooks.Points.FirstOrDefault(declared => PluginKit.Find(declared) is null) is { } unknown)
        {
            steps.Add(new("manifest", false, $"declares `{unknown}`, which is not a point this build has ({PluginKit.Names})."));
            return Trial();
        }

        var tried = (options.Point is { } one ? [one] : hooks.Points.Distinct(StringComparer.Ordinal)).Select(name => PluginKit.Find(name)!).ToList();
        // The handshake waits as the driver's does at these points, whatever patience the points' calls are given: given one
        // second, node's start and the handshake outlasted it under load, and a silent point was never asked (PLUGKIT2).
        var handshake = tried.Max(kit => kit.Patience);
        var patience = options.Patience ?? handshake;
        var root = Path.Combine(scratch, "root");
        PluginFolders.Make(root);
        PluginFolders.Make(entry.Data);

        // The tools it declares, each said in its own sentence (PLUGTOOL1a); a problem there never stops the trial.
        steps.AddRange(await ToolStepsAsync(entry.Manifest, toolsHome, root, path: null, PluginToolRuns.RunAsync, ct).ConfigureAwait(false));

        PluginTrialProcess process;
        try
        {
            // The sample frames name no repository, so git is pointed at one that is not there; a person's frame is theirs.
            process = await PluginTrialProcess.StartAsync(entry, home, noRepositoryAt: options.Frame is null ? root : null).ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            // A first word whose tool cannot run as the person chose (TOOLS5): the driver would refuse it the same way.
            steps.Add(new("start", false, $"{error.Message} So the driver would not start it either."));
            return Trial();
        }
        catch (Win32Exception error)
        {
            steps.Add(new("start", false, $"`{command[0]}` could not start: {error.Message}. So the driver would not start it either."));
            return Trial();
        }

        using (process)
        {
            var stderr = RelayAsync(process.Error, line => { lock (said) said.Add(line); });
            var noise = new List<string>();
            var peer = new HookPeer(process.Output, process.Input, id, line => { lock (noise) noise.Add(line); }, patience);

            // Each call, judged: its sentence when it answered, and when it did not, which of the three
            // ways it did not — the process gone, the process silent, or the process wrong.
            async Task<bool> StepAsync(string name, string method, string onFailure, Func<Task<string>> call, TimeSpan waits)
            {
                var clock = Stopwatch.StartNew();
                try
                {
                    steps.Add(new(name, true, await call().ConfigureAwait(false)));
                    return true;
                }
                catch (DriverException error)
                {
                    if (await process.ExitedAsync(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false))
                    {
                        await Task.WhenAny(stderr, Task.Delay(500, CancellationToken.None)).ConfigureAwait(false);
                        var last = Snapshot(said).LastOrDefault();
                        steps.Add(new(name, false,
                            $"its process exited (code {process.ExitCode}) before answering `{method}`"
                            + (last is null ? "" : $" — the last thing it said: `{last}`") + $", so {onFailure}."));
                        return false;
                    }

                    steps.Add(new(name, false, clock.Elapsed >= waits - TimeSpan.FromMilliseconds(100)
                        ? $"it is running and did not answer `{method}` within {waits.TotalSeconds:0.#}s, so {onFailure}."
                        : $"{error.Message} So {onFailure}."));
                    return true;
                }
            }

            var alive = await StepAsync("handshake", "initialize", "the driver would not start it", async () =>
            {
                await peer.InitializeAsync(home, entry.Data, hooks.Points, ct, handshake).ConfigureAwait(false);
                return $"speaks hook wire {HookPeer.ProtocolVersion} and listens on {Listed(peer.Points)}.";
            }, handshake).ConfigureAwait(false);
            var shook = steps[^1].Ok;

            foreach (var kit in shook ? tried : [])
            {
                if (!alive) break;
                if (!peer.Points.Contains(kit.Name, StringComparer.Ordinal))
                {
                    steps.Add(new(kit.Name, false,
                        $"`{kit.Name}` is declared but the process does not listen there — it listens on {Listed(peer.Points)}. "
                        + "A manifest is what a person reads before turning a plugin on, so the process keeps its word."));
                    continue;
                }

                var frame = (options.Frame ?? kit.Frame).DeepClone().AsObject();
                if (options.Frame is null && frame.ContainsKey("root")) frame["root"] = root;
                alive = await StepAsync(kit.Name, $"hook/{kit.Name}", kit.OnFailure, () => AskAsync(peer, kit.Name, frame, ct), patience)
                    .ConfigureAwait(false);
            }

            // Told to go, and given what the driver gives; a process already gone is not told.
            await peer.DisposeAsync().ConfigureAwait(false);
            if (alive && !process.HasExited)
            {
                if (await process.ExitedAsync(HookProcess.Grace).ConfigureAwait(false))
                {
                    steps.Add(new("shutdown", true, $"left when told (exit {process.ExitCode})."));
                }
                else
                {
                    process.Kill();
                    steps.Add(new("shutdown", false,
                        $"did not leave within {HookProcess.Grace.TotalSeconds:0}s of `shutdown`, so it was ended here, as the driver ends a plugin that stays."));
                }
            }
            else if (!process.HasExited)
            {
                process.Kill();
            }

            var heard = Snapshot(noise);
            if (shook || heard.Count > 0)
            {
                steps.Add(heard.Count == 0
                    ? new("stdout", true, "held nothing but frames.")
                    : new("stdout", false,
                        (heard.Count == 1
                            ? $"it wrote 1 line on stdout that is not a frame: `{Clip(heard[0])}`"
                            : $"it wrote {heard.Count} lines on stdout that are not frames, the first `{Clip(heard[0])}`")
                        + " — stdout is the wire, and anything else belongs on stderr."));
            }

            await Task.WhenAny(stderr, Task.Delay(500, CancellationToken.None)).ConfigureAwait(false);
        }

        return Trial();
    }

    /// <summary>A `state` answer in words; the commits are cut with <see cref="TextBounds.ShortCommit"/>, so a short id is shown whole (DEDUP7c).</summary>
    internal static string StateWords(PullRequestState state) =>
        state.State
        + (state.PullRequest is { } address ? $", pull request {address}" : "")
        + (state.State == PullRequestStates.Completed
            ? $", merged {TextBounds.ShortCommit(state.SourceCommit ?? "", 8)} as {TextBounds.ShortCommit(state.MergeCommit ?? "", 8)}"
            : "")
        + (state.Message is { } words ? $" — {words}" : "");

    /// <summary>One frame at one point, and the answer in words — read by the driver's own reader, which throws for a wrong one.</summary>
    private static async Task<string> AskAsync(HookPeer peer, string point, JsonObject frame, CancellationToken ct)
    {
        switch (point)
        {
            case HookPoints.QuestConsider:
            {
                var decision = await peer.ConsiderAsync(frame, ct).ConfigureAwait(false);
                return decision.Allowed ? "allow — the start would go ahead." : $"hold — {decision.Reason}";
            }

            case HookPoints.SessionEnded:
                await peer.EndedAsync(frame, ct).ConfigureAwait(false);
                return "answered; an observation changes nothing.";

            case HookPoints.StateChanged:
                await peer.ChangedAsync(frame, ct).ConfigureAwait(false);
                return "answered; an observation changes nothing.";

            case HookPoints.Land:
            {
                var landing = await peer.LandAsync(frame, ct).ConfigureAwait(false);
                return (landing.Pushed ? "pushed" : "not pushed")
                    + (landing.PullRequest is { } address ? $", pull request {address}" : "")
                    + $" — {landing.Message}";
            }

            case HookPoints.State:
            {
                var state = await peer.StateAsync(frame, ct).ConfigureAwait(false);
                return StateWords(state);
            }

            default:
                throw new DriverException($"`{point}` is not a point this build can try.");
        }
    }

    /// <summary>
    /// A manifest's tools as a trial's steps (PLUGTOOL1a, D150 point 7): `tools` that is not an array is one step; each
    /// declared tool is `tool &lt;id&gt;` (by its place where it names none), saying where it was found and its version
    /// against its range or its problem; then each of its checks, `&lt;id&gt; check &lt;n&gt;`. Each fails alone and none
    /// stops the trial: a problem in `tools` is shown, never a refusal.
    /// </summary>
    internal static async Task<List<TrialStep>> ToolStepsAsync(
        PluginManifest manifest, string home, string root, string? path, CheckRunner run, CancellationToken ct)
    {
        var steps = new List<TrialStep>();
        if (manifest.ToolsProblem is { } problem) steps.Add(new("tools", false, problem));

        var at = 0;
        foreach (var tool in manifest.Tools)
        {
            at++;
            var name = tool.Id ?? at.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var found = await PluginToolChecks.CheckAsync(home, tool, root, path, run, ct).ConfigureAwait(false);
            steps.Add(new($"tool {name}", found.Ok, found.Sentence));
            var check = 0;
            foreach (var answer in found.Ready) steps.Add(new($"{name} check {++check}", answer.Ready, answer.Sentence));
        }

        return steps;
    }

    private static string Listed(IReadOnlyList<string> points) => points.Count > 0 ? string.Join(", ", points) : "nothing";

    private static string Clip(string line) => TextBounds.Clip(line, 80, keep: 77);

    private static List<string> Snapshot(List<string> lines)
    {
        lock (lines) return [.. lines];
    }

    private static async Task RelayAsync(TextReader stderr, Action<string> onLine)
    {
        try
        {
            while (await stderr.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (line.Length > 0) onLine(line);
            }
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException)
        {
        }
    }
}
