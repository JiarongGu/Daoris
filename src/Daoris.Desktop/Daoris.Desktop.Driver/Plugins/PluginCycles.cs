using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// What a cycle is given (FABRIC-PERFORM1e, the fabric events design §D9): the performed step it drives, the unit it is told, the two
/// revisions it is about, the inputs each attempt names and the deadline a timed-out attempt is given.
/// </summary>
/// <param name="Step">The performed step's id as its manifest writes it, or null for the plugin's first.</param>
public sealed record CycleFixture(string? Step, string Unit, IReadOnlyList<string> Revisions, IReadOnlyList<string> Inputs, TimeSpan Deadline)
{
    public static readonly CycleFixture Default = new(null, "u-cycle", ["r1", "r2"], [], TimeSpan.FromSeconds(3));
}

/// <summary>
/// The kit's cycle, `daoris-driver plugins try &lt;folder&gt; --cycle &lt;fixture&gt;` (FABRIC-PERFORM1e, the fabric events design §D9): a
/// performer driven through synthetic eligibility, duplicate delivery, failure, timeout, restart and a stale attempt's successor, then
/// its resource's cleanup asked twice, without a real deployment.
/// </summary>
/// <remarks>
/// <para><b>As the driver would.</b> The plugin is started by <see cref="HookProcess.StartInfo"/>, spoken to through <see cref="HookPeer"/>,
/// sent <see cref="WorkWire"/>'s messages, and each event it sends is read by <see cref="WorkWire.ReadStep"/>, the reader the hook set
/// hears with. So an outcome the cycle accepts is one the driver reads; what it is believed for is the runtime's, not this.</para>
///
/// <para>🔴 <b>Nothing reaches a platform.</b> The plugin is told the kit's stand-in world (<c>DAORIS_PLUGIN_WORLD</c>), which answers in
/// its platform's place and counts each external effect. A plugin that does not take it, which says so by writing
/// <c>&lt;world&gt;.taken</c> before its handshake is answered, is sent no attempt: the cycle stops there and says why.</para>
///
/// <para><b>A folder's plugin only</b>, told a scratch home and a scratch data folder under the home's trials (D63), both removed after: a
/// cycle writes a journal of attempts that never happened, which an installed plugin's own data folder must never hold.</para>
/// </remarks>
public static class PluginCycles
{
    /// <summary>The kit's stand-in world, beside the scratch: what a plugin that works answers from in its platform's place.</summary>
    public const string World = "DAORIS_PLUGIN_WORLD";

    /// <summary>How long an outcome, or an event sent again, is waited for.</summary>
    private static readonly TimeSpan Hearing = TimeSpan.FromSeconds(10);

    /// <summary>The fixture a new performer gets (<see cref="PluginKit.CycleFixture"/>): the defaults written out, its step named.</summary>
    public static string Fixture(string step) =>
        $$"""
        {
          "step": "{{step}}",
          "unit": "{{CycleFixture.Default.Unit}}",
          "revisions": ["{{CycleFixture.Default.Revisions[0]}}", "{{CycleFixture.Default.Revisions[1]}}"],
          "inputs": [],
          "deadlineSeconds": {{(int)CycleFixture.Default.Deadline.TotalSeconds}}
        }

        """.Replace("\r\n", "\n");

    /// <summary>A fixture file read: each field optional, its default where absent; a field of the wrong shape refused, naming it.</summary>
    /// <exception cref="DriverException">The file is not there, not a JSON object, or a field is not its shape.</exception>
    public static CycleFixture Read(string file)
    {
        var text = PluginFolders.TextIfThere(file)
            ?? throw new DriverException($"no file `{file}` — --cycle names a JSON file: the step, the unit, two revisions, the inputs and the deadline.");
        JsonElement root;
        try
        {
            root = JsonDocument.Parse(text).RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new DriverException($"`{file}` is not a JSON object — a cycle's fixture is one.");
        }

        if (root.ValueKind != JsonValueKind.Object) throw new DriverException($"`{file}` is not a JSON object — a cycle's fixture is one.");
        DriverException Wrong(string field, string shape) => new($"`{file}`'s `{field}` must be {shape}.");

        var fixture = CycleFixture.Default;
        if (root.TryGetProperty("step", out var step))
        {
            if (step.ValueKind != JsonValueKind.String || !PluginSteps.NameShape.IsMatch(step.GetString()!)) throw Wrong("step", "a step's id, as its manifest writes it");
            fixture = fixture with { Step = step.GetString() };
        }

        if (root.TryGetProperty("unit", out var unit))
        {
            if (unit.ValueKind != JsonValueKind.String || unit.GetString() is not { Length: > 0 and <= WorkWire.NameChars }) throw Wrong("unit", "a unit's id");
            fixture = fixture with { Unit = unit.GetString()! };
        }

        if (root.TryGetProperty("revisions", out var revisions))
        {
            if (Strings(revisions) is not [{ Length: > 0 } first, { Length: > 0 } second] || first == second) throw Wrong("revisions", "two revisions, the second another");
            fixture = fixture with { Revisions = [first, second] };
        }

        if (root.TryGetProperty("inputs", out var inputs))
        {
            fixture = fixture with { Inputs = Strings(inputs) ?? throw Wrong("inputs", "a list of references, `<schema>:<id>`") };
        }

        if (root.TryGetProperty("deadlineSeconds", out var deadline))
        {
            if (deadline.ValueKind != JsonValueKind.Number || !deadline.TryGetInt32(out var seconds) || seconds is < 1 or > 60) throw Wrong("deadlineSeconds", "a whole number of seconds, 1 to 60");
            fixture = fixture with { Deadline = TimeSpan.FromSeconds(seconds) };
        }

        return fixture;
    }

    /// <summary>Drive a folder's performer through the cycle.</summary>
    /// <param name="home">The Daoris home, where the cycle keeps its scratch (<see cref="PluginTrials.TrialsOf"/>).</param>
    /// <exception cref="DriverException">There is no manifest in the folder.</exception>
    public static Task<PluginTrial> TryAsync(string home, string folder, CycleFixture fixture, CancellationToken ct = default) =>
        TryAsync(home, folder, fixture, ct, scratchParent: null);

    internal static async Task<PluginTrial> TryAsync(string home, string folder, CycleFixture fixture, CancellationToken ct, string? scratchParent)
    {
        var full = PluginFolders.WholePath(folder);
        if (!PluginFolders.HasFile(Path.Combine(full, PluginCatalog.ManifestName)))
        {
            throw new DriverException($"no `{PluginCatalog.ManifestName}` in {full} — a plugin is a folder with a manifest at its root.");
        }

        var scratch = PluginKitFiles.ScratchIn(scratchParent ?? PluginTrials.TrialsOf(home));
        try
        {
            var data = Path.Combine(scratch, "data");
            var (manifest, problem) = PluginCatalog.ReadFolder(full, data);
            var told = Path.Combine(scratch, "home");
            PluginFolders.Make(told);
            PluginFolders.Make(data);
            var cycle = new Cycle(new PluginEntry(manifest, full, data, Enabled: true, problem), told, scratch, fixture);
            return await cycle.RunAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            PluginKitFiles.Remove(scratch);
        }
    }

    private static IReadOnlyList<string>? Strings(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array) return null;
        var read = new List<string>();
        foreach (var each in value.EnumerateArray())
        {
            if (each.ValueKind != JsonValueKind.String) return null;
            read.Add(each.GetString()!);
        }

        return read;
    }

    /// <summary>One cycle's run: its steps, the plugin's words, and each life of its process.</summary>
    private sealed class Cycle(PluginEntry entry, string told, string scratch, CycleFixture fixture)
    {
        private readonly List<TrialStep> _steps = [];
        private readonly List<string> _said = [];
        private readonly List<string> _noise = [];
        private readonly string _world = Path.Combine(scratch, "world.json");
        private string Id => entry.Manifest.Id;

        private PluginTrial Trial() => new(Id, entry.Folder, entry.Manifest.Hooks?.Command ?? [], _steps, Snapshot(_said));

        private bool Step(string name, bool ok, string sentence)
        {
            _steps.Add(new(name, ok, sentence));
            return ok;
        }

        public async Task<PluginTrial> RunAsync(CancellationToken ct)
        {
            if (entry.Problem is { } problem)
            {
                Step("manifest", false, problem);
                return Trial();
            }

            var performed = entry.Manifest.Steps.Where(step => step.Reach == NodeReach.Performed).ToList();
            var step = fixture.Step is { } named ? performed.FirstOrDefault(each => PluginSteps.NameOf(each) == named) : performed.FirstOrDefault();
            var points = entry.Manifest.Hooks?.Points ?? [];
            if (step is null)
            {
                Step("manifest", false, fixture.Step is { } wanted
                    ? $"declares no performed step `{wanted}` ({(performed.Count == 0 ? "none at all" : string.Join(", ", performed.Select(PluginSteps.NameOf)))}): a cycle drives one."
                    : "declares no performed step: a cycle drives one, so there is nothing to drive.");
                return Trial();
            }

            if (!points.Contains(HookPoints.Step, StringComparer.Ordinal))
            {
                Step("manifest", false, $"does not name `{HookPoints.Step}` in `hooks.points`, so no outcome it sends is ever heard.");
                return Trial();
            }

            if (!points.Contains(HookPoints.Watch, StringComparer.Ordinal))
            {
                Step("manifest", false, $"does not name `{HookPoints.Watch}` in `hooks.points`, where the driver tells every plugin that works the awaited set.");
                return Trial();
            }

            // The world: an attempt fails at the second invocation with the step's first transient code, holds at the third, and every
            // other attempt is reached and every resource removed.
            var transient = step.Perform?.Transient.FirstOrDefault() ?? "unavailable";
            AtomicFile.WriteText(_world, new JsonObject
            {
                ["acts"] = new JsonObject
                {
                    ["i-2"] = new JsonObject { ["state"] = WorkStates.Failed, ["code"] = transient },
                    ["i-3"] = new JsonObject { ["hold"] = true },
                },
            }.ToJsonString() + "\n");

            var call = (int n, string revision, TimeSpan deadline) => new WorkPerformCall(
                fixture.Unit, step.Kind, step.Version, $"i-{n}", $"k-{n}", revision, fixture.Inputs, DateTimeOffset.UtcNow + deadline);
            var later = TimeSpan.FromMinutes(10);
            var (r1, r2) = (fixture.Revisions[0], fixture.Revisions[1]);
            var watch = (int generation, bool resync) => new WorkWatchFrame(generation, resync,
                [new WatchedUnit("cycle", "default", Path.Combine(scratch, "root"), "cycle", r1, null, null, [PluginSteps.NameOf(step)])]);

            var life = await StartAsync(1, ct).ConfigureAwait(false);
            if (life is null) return Trial();
            WorkStepFrame? reached;
            try
            {
                // Made due: told the unit awaits the step, and asked its attempt.
                await life.Peer.WatchAsync(watch(1, true), ct).ConfigureAwait(false);
                await life.Peer.PerformAsync(call(1, r1, later), ct).ConfigureAwait(false);
                reached = await life.UntilAsync(Completion("i-1"), ct).ConfigureAwait(false);
                if (!Step("eligible", reached?.Outcome.State == WorkStates.Reached && Receipted(step, reached), reached is null
                        ? $"sent `i-1` and acknowledged, but no outcome came within {Hearing.TotalSeconds:0}s, so the step would wait to its deadline."
                        : reached.Outcome.State != WorkStates.Reached
                            ? $"sent `i-1`, it answered {reached.Outcome.State}{Code(reached)}, where the stand-in world reaches it."
                            : !Receipted(step, reached)
                                ? $"sent `i-1`, it reached without {(step.Evidence is { } schema ? $"a receipt of `{schema}`" : "a receipt")} or named a resource its manifest does not declare: Daoris would hold it."
                                : $"made due and sent `i-1`: acknowledged, then reached{Receipt(reached)}."))
                {
                    return await EndAsync(life).ConfigureAwait(false);
                }

                // Delivered again: the same key, the same invocation.
                await life.Peer.PerformAsync(call(1, r1, later), ct).ConfigureAwait(false);
                var again = await life.UntilAsync(heard => Completion("i-1")(heard) && !ReferenceEquals(heard, reached), ct).ConfigureAwait(false);
                var acts = Effects().Count(line => line == "act k-1");
                Step("duplicate", acts == 1 && Same(life.Heard.Where(Completion("i-1")), reached), acts != 1
                    ? $"sent `i-1` again under its key, and its work was done {acts} times: a redelivery must never do it twice."
                    : !Same(life.Heard.Where(Completion("i-1")), reached)
                        ? "sent `i-1` again, and its outcome came back different: Daoris would hear two events of one attempt."
                        : again is null
                            ? "sent `i-1` again under its key: acknowledged, and its work done once."
                            : "sent `i-1` again under its key: acknowledged, its work done once, its outcome sent again under the id it first had.");

                // A failure the platform reports, and its reconciliation.
                await life.Peer.PerformAsync(call(2, r1, later), ct).ConfigureAwait(false);
                var failed = await life.UntilAsync(Completion("i-2"), ct).ConfigureAwait(false);
                var reconciled = await life.Peer.ReconcileStepAsync(Reconcile(call(2, r1, later)), ct).ConfigureAwait(false);
                var retried = step.Perform?.Transient.Contains(failed?.Outcome.Code ?? "", StringComparer.Ordinal) == true;
                Step("failure", failed?.Outcome is { State: WorkStates.Failed } && failed.Outcome.Code == transient && reconciled.State == ReconcileStates.Failed,
                    failed is null
                        ? $"sent `i-2`, whose platform fails, and no outcome came within {Hearing.TotalSeconds:0}s."
                        : failed.Outcome.State != WorkStates.Failed || failed.Outcome.Code != transient
                            ? $"sent `i-2`, whose platform fails `{transient}`, and it answered {failed.Outcome.State}{Code(failed)}."
                            : reconciled.State != ReconcileStates.Failed
                                ? $"sent `i-2`, it failed `{transient}`, and asked after it, said {reconciled.State}: Daoris would act on that, not on the failure."
                                : $"sent `i-2`, its platform failing: it failed `{transient}`, {(retried ? "which its step tries again" : "which stops the step for the person")}, and said so again when asked.");

                // A timeout: the deadline passes with the work still going, which never reads as success.
                var deadline = fixture.Deadline;
                await life.Peer.PerformAsync(call(3, r1, deadline), ct).ConfigureAwait(false);
                await Task.Delay(deadline, ct).ConfigureAwait(false);
                var timed = life.Heard.FirstOrDefault(Completion("i-3"));
                var running = await life.Peer.ReconcileStepAsync(Reconcile(call(3, r1, deadline)), ct).ConfigureAwait(false);
                Step("timeout", timed is null && running.State == ReconcileStates.Running, timed is not null
                    ? $"sent `i-3`, whose platform never answers, and it said {timed.Outcome.State}{Code(timed)} anyway."
                    : running.State != ReconcileStates.Running
                        ? $"sent `i-3`, whose platform never answers: past its deadline, asked after it, it said {running.State}, not running."
                        : $"sent `i-3`, whose platform never answers: past its {deadline.TotalSeconds:0}s deadline nothing was reached, and asked after it, it said running.");
            }
            catch (DriverException error)
            {
                Step("cycle", false, error.Message);
                return await EndAsync(life).ConfigureAwait(false);
            }

            // A restart: the process ended with `i-3` still at its work, and started again on the same data.
            await EndAsync(life, silently: true).ConfigureAwait(false);
            life = await StartAsync(2, ct).ConfigureAwait(false);
            if (life is null) return Trial();
            try
            {
                await life.Peer.WatchAsync(watch(2, true), ct).ConfigureAwait(false);
                var replayed = await life.UntilAsync(Completion("i-1"), ct).ConfigureAwait(false);
                var done = await life.Peer.ReconcileStepAsync(Reconcile(call(1, r1, later)), ct).ConfigureAwait(false);
                var lost = await life.Peer.ReconcileStepAsync(Reconcile(call(3, r1, later)), ct).ConfigureAwait(false);
                await life.Peer.PerformAsync(call(1, r1, later), ct).ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromMilliseconds(500), ct).ConfigureAwait(false);
                var acts = Effects().Count(line => line == "act k-1");
                Step("restart", replayed is not null && Same([replayed], reached) && done.State == ReconcileStates.Done && lost.State != ReconcileStates.Running && acts == 1,
                    replayed is null
                        ? $"started again and told to resync, it sent no outcome of `i-1` within {Hearing.TotalSeconds:0}s."
                        : !Same([replayed], reached)
                            ? "started again, it sent `i-1`'s outcome under another id or with other words: Daoris would hear a second event."
                            : done.State != ReconcileStates.Done
                                ? $"started again and asked after `i-1`, it said {done.State}, where it had reached."
                                : lost.State == ReconcileStates.Running
                                    ? "started again and asked after `i-3`, which the process before it began, it said running: only the platform can say."
                                    : acts != 1
                                        ? $"started again and sent `i-1` once more, it did the work again ({acts} times in all)."
                                        : $"started again: resynced `i-1` under its id, said done of it and {lost.State} of `i-3`, which the process before it began, and did no work twice.");

                // A new revision's attempt: a new key, its own work, its own outcome, never the old one's.
                await life.Peer.PerformAsync(call(4, r2, later), ct).ConfigureAwait(false);
                var fresh = await life.UntilAsync(Completion("i-4"), ct).ConfigureAwait(false);
                var once = Effects().Count(line => line == "act k-4");
                Step("stale", fresh?.Outcome.State == WorkStates.Reached && once == 1 && fresh.Outcome.Evidence?.Ref != reached!.Outcome.Evidence?.Ref,
                    fresh is null
                        ? $"sent `i-4` at `{r2}`, and no outcome came within {Hearing.TotalSeconds:0}s."
                        : once != 1
                            ? $"sent `i-4` at `{r2}` under a new key, and its work was done {once} times."
                            : fresh.Outcome.Evidence?.Ref == reached!.Outcome.Evidence?.Ref
                                ? $"sent `i-4` at `{r2}`, it answered with `i-1`'s receipt: evidence of `{r1}` is stale at `{r2}`."
                                : $"sent `i-4` at `{r2}` under a new key: its own work, reached{Receipt(fresh)}, never `{r1}`'s.");

                // What `i-1` made, removed, and removed again.
                if (reached!.Outcome.Resources.FirstOrDefault() is { } made)
                {
                    var cleanup = new WorkCleanupCall("c-1", made.Id, "ck-1");
                    var first = await life.Peer.CleanupAsync(cleanup, ct).ConfigureAwait(false);
                    var second = await life.Peer.CleanupAsync(cleanup, ct).ConfigureAwait(false);
                    var removals = Effects().Count(line => line == $"remove {made.Id}");
                    Step("cleanup", first.Disposition == CleanupDispositions.Removed && second.Disposition == CleanupDispositions.AlreadyGone && removals == 1,
                        $"asked to remove `{made.Id}` twice under one key: {first.Disposition}, then {second.Disposition}"
                        + (removals == 1 ? ", removed once." : $", removed {removals} times.")
                        + (second.Disposition == CleanupDispositions.AlreadyGone ? "" : " A repeated cleanup is already-gone."));
                }
                else
                {
                    Step("cleanup", true, "`i-1` named nothing it made, so there was nothing to remove.");
                }
            }
            catch (DriverException error)
            {
                Step("cycle", false, error.Message);
            }

            return await EndAsync(life).ConfigureAwait(false);
        }

        /// <summary>The plugin started, its world taken and its handshake made; null with the step that says why not.</summary>
        private async Task<Life?> StartAsync(int generation, CancellationToken ct)
        {
            PluginTrialProcess process;
            try
            {
                process = await PluginTrialProcess.StartAsync(entry, told, noRepositoryAt: scratch, ct, world: _world).ConfigureAwait(false);
            }
            catch (DriverException error)
            {
                Step("start", false, $"{error.Message} So the driver would not start it either.");
                return null;
            }
            catch (Win32Exception error)
            {
                Step("start", false, $"`{entry.Manifest.Hooks?.Command.FirstOrDefault()}` could not start: {error.Message}. So the driver would not start it either.");
                return null;
            }

            var life = new Life(process, entry, Id, line => { lock (_noise) _noise.Add(line); }, line => { lock (_said) _said.Add(line); });
            var name = generation == 1 ? "handshake" : "handshake again";
            try
            {
                await life.Peer.InitializeAsync(told, entry.Data, entry.Manifest.Hooks!.Points, ct).ConfigureAwait(false);
            }
            catch (DriverException error)
            {
                Step(name, false, $"{error.Message} So the driver would not start it.");
                await EndAsync(life, silently: true).ConfigureAwait(false);
                return null;
            }

            if (!PluginFolders.HasFile(_world + ".taken"))
            {
                Step("world", false, $"it did not take the kit's stand-in world ({World}), so an attempt would reach its platform: nothing was sent. "
                    + "A plugin made by the kit takes it in work.mjs.");
                await EndAsync(life, silently: true).ConfigureAwait(false);
                return null;
            }

            if (!life.Peer.Capabilities.Contains(WorkWire.Capability, StringComparer.Ordinal) || !life.Peer.Points.Contains(HookPoints.Perform, StringComparer.Ordinal))
            {
                Step(name, false, $"it does not name `{HookPoints.Perform}` and `{WorkWire.Capability}` at its handshake, so the driver never asks it to perform.");
                await EndAsync(life, silently: true).ConfigureAwait(false);
                return null;
            }

            Step(name, true, $"generation {generation}: took the stand-in world, listens on {string.Join(", ", life.Peer.Points)} and names `{WorkWire.Capability}`.");
            return life;
        }

        /// <summary>Told to go; and, unless <paramref name="silently"/>, the shutdown and stdout said as a trial says them.</summary>
        private async Task<PluginTrial> EndAsync(Life life, bool silently = false)
        {
            await life.Peer.DisposeAsync().ConfigureAwait(false);
            var left = await life.Process.ExitedAsync(HookProcess.Grace).ConfigureAwait(false);
            if (!left) life.Process.Kill();
            await life.QuietAsync().ConfigureAwait(false);
            life.Process.Dispose();
            if (silently) return Trial();

            Step("shutdown", left, left
                ? "left when told."
                : $"did not leave within {HookProcess.Grace.TotalSeconds:0}s of `shutdown`, so it was ended here, as the driver ends a plugin that stays.");
            var noise = Snapshot(_noise);
            var unread = life.Unread;
            Step("stdout", noise.Count == 0 && unread.Count == 0, noise.Count > 0
                ? $"it wrote {noise.Count} line(s) on stdout that are not frames, the first `{TextBounds.Clip(noise[0], 80, keep: 77)}` — stdout is the wire."
                : unread.Count > 0
                    ? $"it sent {unread.Count} event(s) the driver does not read, the first broken at `{unread[0]}`: Daoris refuses each one."
                    : "held nothing but frames, and every event read as the driver reads it.");
            return Trial();
        }

        private List<string> Effects() =>
            PluginFolders.TextIfThere(_world + ".effects") is { } text ? [.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries)] : [];

        private static WorkReconcileCall Reconcile(WorkPerformCall call) => new(call.Unit, call.Step, call.Invocation, call.IdempotencyKey, call.SubjectRevision);

        private static Func<WorkStepFrame, bool> Completion(string invocation) => heard => heard.Invocation == invocation;

        /// <summary>Whether every event heard is the first one again: the same id and the same words, which Daoris takes once.</summary>
        private static bool Same(IEnumerable<WorkStepFrame> heard, WorkStepFrame? first) =>
            first is not null && heard.All(each => WorkWire.Step(each).ToJsonString() == WorkWire.Step(first).ToJsonString());

        /// <summary>Whether a reached outcome carries the receipt its step declares, and names only resources its manifest declares.</summary>
        private bool Receipted(NodeDeclaration step, WorkStepFrame heard) =>
            (step.Evidence is not { } schema || heard.Outcome.Evidence?.Ref?.StartsWith(schema + ":", StringComparison.Ordinal) == true)
            && heard.Outcome.Resources.All(made => entry.Manifest.Resources.Any(declared => declared.Schema == made.Schema));

        private static string Receipt(WorkStepFrame heard) =>
            (heard.Outcome.Evidence?.Ref is { } reference ? $" on `{reference}`" : "")
            + (heard.Outcome.Resources.Count > 0 ? $", making {string.Join(", ", heard.Outcome.Resources.Select(made => $"`{made.Id}`"))}" : "");

        private static string Code(WorkStepFrame heard) => heard.Outcome.Code is { } code ? $" `{code}`" : "";

        private static List<string> Snapshot(List<string> lines)
        {
            lock (lines) return [.. lines];
        }
    }

    /// <summary>One life of the plugin's process: its peer, the events it sent read as the driver reads them, and its words.</summary>
    private sealed class Life
    {
        private readonly List<WorkStepFrame> _heard = [];
        private readonly List<string> _unread = [];
        private readonly Task _stderr;

        public Life(PluginTrialProcess process, PluginEntry entry, string id, Action<string> noise, Action<string> said)
        {
            Process = process;
            Peer = new HookPeer(process.Output, process.Input, id, noise, HookPeer.DefaultPatience, HookProcess.FrameLimit(entry));
            Peer.Listen(frame =>
            {
                lock (_heard)
                {
                    string? field = null;
                    if (frame is { } @params && WorkWire.ReadStep(@params, out field) is { } step) _heard.Add(step);
                    else _unread.Add(frame is null ? "its length" : field ?? "params");
                }
            });
            _stderr = RelayAsync(process.Error, said);
        }

        public PluginTrialProcess Process { get; }

        public HookPeer Peer { get; }

        public IReadOnlyList<WorkStepFrame> Heard
        {
            get
            {
                lock (_heard) return [.. _heard];
            }
        }

        public IReadOnlyList<string> Unread
        {
            get
            {
                lock (_heard) return [.. _unread];
            }
        }

        /// <summary>The first event heard that <paramref name="matches"/>, waiting up to the hearing's bound; null where none came.</summary>
        public async Task<WorkStepFrame?> UntilAsync(Func<WorkStepFrame, bool> matches, CancellationToken ct)
        {
            var clock = Stopwatch.StartNew();
            while (true)
            {
                if (Heard.FirstOrDefault(matches) is { } found) return found;
                if (clock.Elapsed >= Hearing || Process.HasExited) return null;
                await Task.Delay(50, ct).ConfigureAwait(false);
            }
        }

        public Task QuietAsync() => Task.WhenAny(_stderr, Task.Delay(500, CancellationToken.None));

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
}
