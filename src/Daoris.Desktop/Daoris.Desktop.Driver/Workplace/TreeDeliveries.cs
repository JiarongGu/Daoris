using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The delivery a landing performs (LANDNODE1b2; the land node design §3 step 4): the store and the claim its phases are written under. The
/// landing reads its intent from the store, and the intent chooses the method, the branch, the line and the pinned tip; no live rule does.
/// </summary>
public sealed record TreeDelivery(DeliveryStore Store, DeliveryClaim Claim)
{
    /// <summary>The test seam: run with a phase's name after its effect and before its outcome is written. Null in production.</summary>
    internal Func<string, Task>? Seam { get; init; }

    /// <summary>
    /// Whether the standing automatic authority still stands (LANDNODE1h; the land node design §3), asked of an automatic delivery just before
    /// its first effect: a refusal in words, or null where it stands. Null judges nothing, and an automatic delivery with none lands nothing.
    /// </summary>
    public Func<DeliveryRecord, string?>? Standing { get; init; }

    /// <summary>
    /// Written by a door from Current's live rule a moment before it lands (LANDNODE1c3, <see cref="CurrentDeliveries"/>): the landing keeps
    /// that rule whole, its <c>integrate</c> and switch among it, and no land node names a template, so its commits say today's words.
    /// </summary>
    public bool Live { get; init; }
}

/// <summary>The delivery one landing performs, once entered: its reading, and this process's hold on it until the landing ends.</summary>
internal sealed class DeliveryEntry : IDisposable
{
    public TreeDelivering? Delivering { get; set; }

    public IDisposable? Performing { get; set; }

    public void Dispose() => Performing?.Dispose();
}

/// <summary>
/// The deliveries this process is landing or settling now (LANDNODE1b3), one at a time each, by home and key: a restart's settlement never
/// writes beside a landing of the delivery here, and a second starter of it lands only once the first has ended.
/// </summary>
internal static class DeliveryRuns
{
    private static readonly ConcurrentDictionary<string, byte> Here = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The delivery held by this process until the answer is disposed; null where this process holds it already.</summary>
    public static IDisposable? Hold(string home, string key)
    {
        var name = $"{Path.TrimEndingDirectorySeparator(Path.GetFullPath(home))}|{key}";
        return Here.TryAdd(name, 0) ? new Held(name) : null;
    }

    private sealed class Held(string name) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0) Here.TryRemove(name, out _);
        }
    }
}

/// <summary>
/// A landing performing its delivery (LANDNODE1b2): the intent read once, and each git effect's entry written ahead of it and its outcome
/// after, read back and never assumed: <i>prepared</i> from the start of the landing's judgement to the commit its ref is written at,
/// <i>ref written</i>, <i>recorded</i>. The branch's ref goes through one <see cref="RefTransaction"/> under the checkout's lock held alone
/// (GITLOCK1), inside the tree's landing hold (LANDRACE1) the landing took first; a merge's line is moved by the promotion's effect, which
/// takes that lock itself. The landing's ownership is its landing record: no act-journal mark is made. A delivery a restart settled by phase
/// (LANDNODE1b3) goes on from its next phase: a done preparation is not written ahead again, and a ref it wrote is read, never written twice.
/// </summary>
internal sealed class TreeDelivering
{
    private static readonly Regex FullId = new("^(?:[0-9a-f]{40}|[0-9a-f]{64})$", RegexOptions.CultureInvariant);

    /// <summary>The git phases, in their order: what a landing writes ahead, and goes on from after a restart.</summary>
    internal static IReadOnlyList<string> GitPhases { get; } = [DeliveryPhases.Prepared, DeliveryPhases.RefWritten, DeliveryPhases.Recorded];

    private readonly TreeDelivery _delivery;
    private readonly string _home;
    private readonly string _repository;
    private readonly HashSet<string> _done = new(StringComparer.Ordinal);
    private readonly string? _wrote;
    private string? _open;

    private TreeDelivering(TreeDelivery delivery, DeliveryRecord record, IReadOnlyList<DeliveryPhase> phases, string home, string repository, bool squash)
    {
        (_delivery, Record, _home, _repository, Squash) = (delivery, record, home, repository, squash);
        foreach (var each in phases.Where(each => each.Outcome == DeliveryOutcomes.Done)) _done.Add(each.Phase);
        _wrote = phases.LastOrDefault(each => each.Phase == DeliveryPhases.RefWritten && each.Outcome == DeliveryOutcomes.Done)?.Evidence;
    }

    /// <summary>The git phase this landing goes on from: the first not done (the land node design §3, a restart's rule 1).</summary>
    public string? Next => GitPhases.FirstOrDefault(each => !_done.Contains(each));

    /// <summary>Where this delivery's own ref write left <c>refs/heads/<paramref name="name"/></c>, read back done before it stopped; null where it wrote none.</summary>
    public string? Wrote(string name) => string.Equals(name, Intent.Branch, StringComparison.Ordinal) ? _wrote : null;

    /// <summary>The delivery as the store held it when the landing began.</summary>
    public DeliveryRecord Record { get; }

    public DeliveryIntent Intent => Record.Intent;

    /// <summary>Whether a door wrote it from Current's live rule (<see cref="TreeDelivery.Live"/>).</summary>
    public bool Live => _delivery.Live;

    /// <summary>The message its commits say, rendered from a land node's template; null under Current, whose commits say today's words.</summary>
    public string? Template => Live ? null : Intent.Message;

    /// <summary>Whether its resolved options squash the work into one commit.</summary>
    public bool Squash { get; }

    /// <summary>Who accepted it, as the landing record keeps it: the standing automatic authority, or the person.</summary>
    public string Accepted => Record.Authorization.Authority == DeliveryAuthorities.Automatic ? AcceptedBy.Auto : AcceptedBy.Person;

    /// <summary>
    /// Why the delivery's authority no longer holds, or null: the person's Accept stands as recorded; the standing automatic authority is
    /// judged again, here, before anything is written ahead.
    /// </summary>
    public string? Unauthorized() =>
        Record.Authorization.Authority != DeliveryAuthorities.Automatic ? null
        : _delivery.Standing is null ? "nothing judges whether its automatic authority still stands"
        : _delivery.Standing(Record);

    /// <summary>The preparation's intended effect, as its entry says it.</summary>
    public string Preparing => Intent.Method == DeliveryMethods.Merge
        ? $"merge {Intent.Tip} into `{Intent.Line}`"
        : $"{Intent.Method}: `{Intent.Branch}` at {Intent.Tip}" + (Squash ? ", squashed" : "");

    /// <summary>
    /// The delivery <paramref name="delivery"/>'s claim names, read for a landing of <paramref name="repository"/>'s tree; or why it does not
    /// land: none, held, settled or claimed by another run, of another repository, an intent or option this build does not land, or phases a
    /// landing cannot go on from (<see cref="Standing"/>).
    /// </summary>
    public static (TreeDelivering? Delivering, string? Refused) Read(TreeDelivery delivery, string home, string repository)
    {
        var record = delivery.Store.Delivery(delivery.Claim.Key);
        var intent = record?.Intent;
        var why = record switch
        {
            null => "the store holds no such delivery",
            { Held: true } => "it was written by a newer Daoris, which this build reads and never lands",
            { Unresolved: false } => $"it is settled ({record.Settled})",
            _ when record.ClaimRun != delivery.Claim.Run || record.ClaimGeneration != delivery.Claim.Generation =>
                $"run {record.ClaimRun} holds it at generation {record.ClaimGeneration}",
            _ when !string.Equals(intent!.Repository, repository, StringComparison.OrdinalIgnoreCase) => $"it lands `{intent.Repository}`, not `{repository}`",
            _ when !DeliveryMethods.All.Contains(intent.Method, StringComparer.Ordinal) => $"`{intent.Method}` is no method this build lands",
            _ when !FullId.IsMatch(intent.Tip) => $"its tip `{intent.Tip}` is no full commit id",
            _ when intent.Method != DeliveryMethods.Merge && !(intent.Branch is { } named && BranchName.IsValid(named)) =>
                $"its branch `{intent.Branch}` is no branch name git would take",
            _ => null,
        };

        var squash = record is null ? false : SquashOf(record.Authorization.Options);
        why ??= squash switch
        {
            null => "its resolved options do not read as options, so whether it squashes is unknown",
            true when intent!.Method == DeliveryMethods.Integrate => "an integrate is never squashed, since its history is the line's",
            true when intent!.Method == DeliveryMethods.Merge => "a squashed merge is not made by this build, and a merge commit nobody chose would be",
            _ => null,
        };

        var phases = why is null ? delivery.Store.Phases(delivery.Claim.Key) : [];
        why ??= Standing(intent!, phases);

        return why is null
            ? (new TreeDelivering(delivery, record!, phases, home, repository, squash!.Value), null)
            : (null, $"delivery {delivery.Claim.Key[..12]}: {why}");
    }

    /// <summary>
    /// Why no landing goes on from these phases now, or null: an entry a newer build wrote, one written ahead without its outcome or read as
    /// unknown, every git effect recorded already, or a merge its line holds, which a restart's settlement records (<see cref="DeliveryReconciler"/>).
    /// </summary>
    private static string? Standing(DeliveryIntent intent, IReadOnlyList<DeliveryPhase> phases)
    {
        if (phases.FirstOrDefault(each => !DeliveryPhases.All.Contains(each.Phase, StringComparer.Ordinal)
                || each.Outcome is { } outcome && !DeliveryOutcomes.All.Contains(outcome, StringComparer.Ordinal)) is { } newer)
        {
            return $"its entry {newer.Ordinal} was written by a newer Daoris, which lands it";
        }

        // The plugin's phases (LANDNODE1e) are read by DispatchAsync, which reconciles a dispatch left standing and never sends it again.
        var plugin = intent.Plugin is not null && intent.Method is DeliveryMethods.Push or DeliveryMethods.PullRequest;
        bool Theirs(DeliveryPhase each) => plugin && each.Phase is DeliveryPhases.PluginDispatched or DeliveryPhases.PluginAnswered;
        if (phases.FirstOrDefault(each => each.Open && !Theirs(each)) is { } open)
        {
            return $"its `{open.Phase}` entry was written ahead at {Moments.Write(open.BeganAt)} and has no outcome: a restart settles it by phase";
        }

        if (phases.FirstOrDefault(each => each.Outcome == DeliveryOutcomes.Unknown && !Theirs(each)) is { } unknown)
        {
            return $"its `{unknown.Phase}` was read as unknown, which the person settles before any later phase";
        }

        var done = phases.Where(each => each.Outcome == DeliveryOutcomes.Done).Select(each => each.Phase).ToHashSet(StringComparer.Ordinal);
        if (GitPhases.All(done.Contains))
        {
            return plugin ? null : "its effects are recorded, and a restart's settlement delivers it";
        }

        return intent.Method == DeliveryMethods.Merge && done.Contains(DeliveryPhases.RefWritten)
            ? $"`{intent.Line}` holds its merge, which a restart's settlement records"
            : null;
    }

    /// <summary>
    /// What a landing says that waited for its tree behind its delivery's other starter, which delivered it meanwhile (a press and a pass at
    /// once): where the work is, and that nothing more was landed. Kept as <see cref="AutoLandingCode.Already"/>, as LANDRACE1's is.
    /// </summary>
    public static TreeLanding Meanwhile(DeliveryRecord delivered)
    {
        var intent = delivered.Intent;
        var merge = intent.Method == DeliveryMethods.Merge;
        return new TreeLanding(false,
            $"its delivery {delivered.Key[..12]} was delivered while this landing waited for its tree: its work at {TextBounds.ShortCommit(intent.Tip, 8)} is "
            + (merge ? $"merged into `{intent.Line}`" : $"on `{intent.Branch}`") + ". Nothing more was landed.", merge ? null : intent.Branch)
        {
            Refusal = AutoLandingCode.Already,
        };
    }

    /// <summary>
    /// The landing as its delivery lands it: the method's form and squash the intent's; of the rule only what setup keeps (its tidy), the plugin
    /// only of a push or a pull request, and that the intent's, told through <see cref="DispatchAsync"/>; no switch and no integrate, since the
    /// delivery's authorization and the next node hold those. Under Current (<see cref="Live"/>) the intent was read from that very rule a
    /// moment ago, so the rule stands whole, its <c>integrate</c> the line's policy and its record's provenance (LANDNODE1c3).
    /// </summary>
    public Landing Over(Landing live) => Live ? live with { Rule = live.Rule with { Squash = Squash } } :
        new(live.Rule with
        {
            Form = Intent.Method == DeliveryMethods.Merge ? LandingForm.Merge : LandingForm.Branch,
            Pattern = null, AutoAccept = false, Squash = Squash, Integrate = null,
            Plugin = Intent.Method is DeliveryMethods.Push or DeliveryMethods.PullRequest ? Intent.Plugin : null,
        }, live.Source);

    /// <summary>
    /// The plugin told this delivery (LANDNODE1e; the land node design §3 step 4 and the restart ladder): the frame carries the delivery key where
    /// the plugin declares one and the mode of its method, <c>push</c> only to a plugin that declares it. <i>Plugin dispatched</i> is written
    /// ahead of the send and <i>plugin answered</i> after the answer; where a dispatch stands without its answer, or the reply is lost, the
    /// plugin is never sent again: a remote-tracking ref proves a push (never a pull request), <c>work/state</c> asked with the key proves a pull
    /// request, and anything else is read as unknown, for the person's hand-off. A keyless plugin is reconciled by the ref alone.
    /// </summary>
    public async Task<PluginLanding> DispatchAsync(IWorkplacePluginWire wire, LandingFrame frame, CancellationToken ct)
    {
        var plugin = Intent.Plugin!;
        var declares = wire.LandDeclared(plugin);
        var mode = Intent.Method == DeliveryMethods.Push ? LandingModes.Push : LandingModes.PullRequest;
        PluginLanding Refused(string why) => new(plugin, false, null, why, Failed: true);
        if (mode == LandingModes.Push && !declares.Modes.Contains(LandingModes.Push, StringComparer.Ordinal))
        {
            return Refused($"plugin `{plugin}` does not declare the `push` mode, so nothing was sent to it: the branch stands, and its manifest names the mode once it serves it.");
        }

        var sent = frame with { Key = declares.Key ? Record.Key : null, Mode = mode };
        var phases = _delivery.Store.Phases(Record.Key);
        if (phases.LastOrDefault(each => each.Phase == DeliveryPhases.PluginAnswered && each.Outcome == DeliveryOutcomes.Done) is { Evidence: { } kept }
            && JsonSerializer.Deserialize<PluginLanding>(kept) is { } already)
        {
            _done.Add(DeliveryPhases.PluginAnswered);
            return already;
        }

        // An answer cut while being written is withdrawn, and the dispatch before it is reconciled like any without its answer.
        if (phases.FirstOrDefault(each => each.Open && each.Phase == DeliveryPhases.PluginAnswered) is not null)
        {
            End(DeliveryPhases.PluginAnswered, DeliveryOutcomes.Withdrawn, null, "its entry stood without an outcome");
        }

        var dispatch = phases.LastOrDefault(each => each.Phase == DeliveryPhases.PluginDispatched);
        if (dispatch is { Outcome: DeliveryOutcomes.Unknown })
        {
            return Refused($"its dispatch to `{plugin}` was read as unknown, which the person settles (a hand-off sends it again).");
        }

        if (dispatch is null || dispatch.Outcome == DeliveryOutcomes.Withdrawn)
        {
            if (Begin(DeliveryPhases.PluginDispatched, JsonSerializer.Serialize(new { key = sent.Key, mode, tip = Intent.Tip })) is { } unbegun)
            {
                return Refused(unbegun);
            }

            var said = await wire.LandAsync(plugin, sent, ct).ConfigureAwait(false);
            await SeamAsync(DeliveryPhases.PluginDispatched).ConfigureAwait(false);
            if (!said.Failed)
            {
                End(DeliveryPhases.PluginDispatched, said.Pushed ? DeliveryOutcomes.Done : DeliveryOutcomes.Withdrawn, null, said.Message);
                return said.Pushed ? Answered(said) : said;
            }
        }

        return await ReconcileAsync(wire, sent, declares, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// <see cref="DeliveryReconciler.Plugin"/> (LANDNODE1e): a delivery whose git phases are done settled by its plugin phases at a restart.
    /// Nothing is ever sent: a dispatch standing without its answer is reconciled as <see cref="DispatchAsync"/> does, one never sent is left
    /// to its landing or the person's hand-off, and a plugin answer proved settles it delivered.
    /// </summary>
    public static Func<DeliveryRecord, DeliveryClaim, CancellationToken, Task<DeliveryReconciled>> PluginSettlement(
        DeliveryStore store, string home, IWorkplacePluginWire wire, Func<DeliveryRecord, string?> checkout) =>
        async (record, claim, ct) =>
        {
            DeliveryReconciled Said(string standing, string said) => new(record.Key, standing, said) { Claim = claim };
            var intent = record.Intent;
            if (intent.Plugin is null || intent.Method is not (DeliveryMethods.Push or DeliveryMethods.PullRequest))
            {
                return Said(DeliveryStandings.Plugin, "it names no plugin phase");
            }

            if (checkout(record) is not { } root) return Said(DeliveryStandings.Held, $"`{intent.Repository}`'s checkout is not here, so its plugin phases are read at the next look");
            var delivering = new TreeDelivering(new TreeDelivery(store, claim), record, store.Phases(record.Key), home, intent.Repository, squash: false);
            var frame = new LandingFrame(intent.Repository, "", root, intent.Branch!, intent.Line, null, null, "", []);
            var settled = await delivering.SettleAsync(wire, frame, ct).ConfigureAwait(false);
            if (settled is null) return Said(DeliveryStandings.Plugin, "its plugin was not sent it yet: its landing or the person's hand-off sends it");
            if (!settled.Pushed) return Said(DeliveryStandings.Unknown, settled.Message);
            store.Settle(claim, DeliverySettlements.Delivered, $"{intent.Method} of {intent.Tip} delivered, settled by phase");
            return Said(DeliveryStandings.Delivered, $"its plugin phases are done: {settled.Message}");
        };

    /// <summary>The plugin phases read without sending: null where nothing was dispatched, else the reconciliation's answer.</summary>
    public async Task<PluginLanding?> SettleAsync(IWorkplacePluginWire wire, LandingFrame frame, CancellationToken ct)
    {
        var plugin = Intent.Plugin!;
        var declares = wire.LandDeclared(plugin);
        var mode = Intent.Method == DeliveryMethods.Push ? LandingModes.Push : LandingModes.PullRequest;
        var sent = frame with { Key = declares.Key ? Record.Key : null, Mode = mode };
        var phases = _delivery.Store.Phases(Record.Key);
        if (phases.LastOrDefault(each => each.Phase == DeliveryPhases.PluginAnswered && each.Outcome == DeliveryOutcomes.Done) is { Evidence: { } kept }
            && JsonSerializer.Deserialize<PluginLanding>(kept) is { } already)
        {
            return already;
        }

        if (phases.FirstOrDefault(each => each.Open && each.Phase == DeliveryPhases.PluginAnswered) is not null)
        {
            End(DeliveryPhases.PluginAnswered, DeliveryOutcomes.Withdrawn, null, "its entry stood without an outcome");
        }

        var dispatch = phases.LastOrDefault(each => each.Phase == DeliveryPhases.PluginDispatched);
        if (dispatch is null || dispatch.Outcome == DeliveryOutcomes.Withdrawn) return null;
        if (dispatch.Outcome == DeliveryOutcomes.Unknown) return new PluginLanding(plugin, false, null, dispatch.Said ?? "its dispatch was read as unknown", Failed: true);
        return await ReconcileAsync(wire, sent, declares, ct).ConfigureAwait(false);
    }

    private async Task<PluginLanding> ReconcileAsync(IWorkplacePluginWire wire, LandingFrame sent, PluginLandDeclares declares, CancellationToken ct)
    {
        var plugin = Intent.Plugin!;
        PluginLanding? found = null;
        if (sent.Mode == LandingModes.Push)
        {
            var (code, at, _) = await WorkingTree.GitAsync(sent.Root, ["rev-parse", "--verify", "--quiet", $"refs/remotes/origin/{sent.Branch}"], ct).ConfigureAwait(false);
            if (code == 0 && string.Equals(at.Trim(), Intent.Tip, StringComparison.OrdinalIgnoreCase))
            {
                found = new(plugin, true, null, $"origin's remote-tracking `{sent.Branch}` holds {Intent.Tip}, so the push was made");
            }
        }
        else if (declares.Key)
        {
            var entry = new LandedBranch(sent.Repository, sent.Workspace, sent.Branch, sent.Base, Intent.Tip, sent.Session, sent.Quest, sent.Title, DateTimeOffset.UtcNow)
            {
                Plugin = plugin,
            };
            var frame = new StateFrame(sent.Repository, sent.Workspace, sent.Root, sent.Branch, Intent.Line, sent.PullRequest, Intent.Tip, sent.Origin, sent.Key);
            var asked = await wire.AskStatesAsync([new StateAsk(entry, plugin, frame)], ct).ConfigureAwait(false);
            if (asked.Count == 1 && asked[0].Answer is { PullRequest: { } address, State: PullRequestStates.Open or PullRequestStates.Completed })
            {
                found = new(plugin, true, address, $"`work/state` with the delivery key found its pull request {address}");
            }
        }

        if (found is null)
        {
            var said = $"`{plugin}` was told this delivery and its answer was not kept, and nothing read proves what it did; it is not sent again, and the person's hand-off sends it";
            End(DeliveryPhases.PluginDispatched, DeliveryOutcomes.Unknown, null, said);
            return new PluginLanding(plugin, false, null, said + ".", Failed: true);
        }

        End(DeliveryPhases.PluginDispatched, DeliveryOutcomes.Done, found.PullRequest, found.Message);
        return Answered(found);
    }

    private PluginLanding Answered(PluginLanding said)
    {
        if (Begin(DeliveryPhases.PluginAnswered, "the plugin's answer kept") is not null) return said;
        End(DeliveryPhases.PluginAnswered, DeliveryOutcomes.Done, JsonSerializer.Serialize(said), said.Message);
        return said;
    }

    /// <summary>Write the phase's entry ahead of its effect; the store's refusal in its words where it may not be.</summary>
    public string? Begin(string phase, string intended)
    {
        try
        {
            _delivery.Store.Begin(_delivery.Claim, phase, intended);
        }
        catch (DeliveryRefusedException refused)
        {
            return $"delivery {refused.Key[..12]}: {refused.Why}";
        }

        _open = phase;
        return null;
    }

    /// <summary>
    /// The landing's branch ref written from its preparation, answered in the shape of git's so the caller's words stay: the checkout held alone,
    /// the preparation done at <paramref name="value"/>, the ref's entry written ahead, one ref transaction, the ref read back and its outcome
    /// written. Where another holds the checkout nothing is written, and the preparation stands for the landing's refusal to withdraw.
    /// </summary>
    /// <param name="old">The tip judged, which the ref moves from; null makes it where it is absent; equal to <paramref name="value"/>, it is verified there.</param>
    public async Task<(int Code, string Stdout, string Stderr)> WriteAsync(string root, string reference, string? old, string value, CancellationToken ct)
    {
        // LANDNODE1b3: the ref this delivery wrote and read back before it stopped is read again where it stands, and never written twice.
        if (_done.Contains(DeliveryPhases.RefWritten))
        {
            var (known, at) = await StandsAsync(root, reference, ct).ConfigureAwait(false);
            return known && at is not null && string.Equals(at, _wrote, StringComparison.OrdinalIgnoreCase) && string.Equals(at, value, StringComparison.OrdinalIgnoreCase)
                ? (0, "", "")
                : (1, "", $"`{reference}` stands at {at ?? "nothing"}, not at {_wrote}, where this delivery wrote it before it stopped");
        }

        using var held = TreeLock.TryWriting(_home, root, _repository, TreeLock.Landing);
        if (held is null) return (1, "", $"{TreeLock.Held(_repository, TreeLock.HolderOf(_home, root))}, so the branch was not written; accept again once it is done");

        var (transaction, intended) = old is null ? (new RefTransaction().Create(reference, value), $"`{reference}` made at {value}")
            : old == value ? (new RefTransaction().Verify(reference, value), $"`{reference}` standing at {value}")
            : (new RefTransaction().Update(reference, old, value), $"`{reference}` moved from {old} to {value}");
        await SeamAsync(DeliveryPhases.Prepared).ConfigureAwait(false);
        End(DeliveryPhases.Prepared, DeliveryOutcomes.Done, value, $"the commit `{reference}` is written at is {value}");
        if (Begin(DeliveryPhases.RefWritten, intended) is { } refused) return (1, "", refused);

        var run = await transaction.RunAsync(root, ct).ConfigureAwait(false);
        await SeamAsync(DeliveryPhases.RefWritten).ConfigureAwait(false);
        var (read, stands) = await StandsAsync(root, reference, ct).ConfigureAwait(false);
        // Git's refusal moved nothing; no answer is told by the ref itself.
        var outcome = run.Done || (run.Unanswered && read && stands == value) ? DeliveryOutcomes.Done
            : !run.Unanswered || (read && stands == old) ? DeliveryOutcomes.Withdrawn
            : DeliveryOutcomes.Unknown;
        End(DeliveryPhases.RefWritten, outcome, read ? stands ?? "absent" : null, outcome == DeliveryOutcomes.Done ? intended : run.Said);
        return outcome == DeliveryOutcomes.Done ? (0, "", "") : (1, "", run.Said);
    }

    /// <summary>A merge's press about to move the line: the preparation done at the commit merged, the line's entry written ahead.</summary>
    public async Task<string?> BeginMergeAsync(string line, string commit)
    {
        await SeamAsync(DeliveryPhases.Prepared).ConfigureAwait(false);
        // A merge going on after its press left its request waiting was prepared before (LANDNODE1c3).
        if (_open == DeliveryPhases.Prepared) End(DeliveryPhases.Prepared, DeliveryOutcomes.Done, commit, $"the commit merged into `{line}` is {commit}");
        return Begin(DeliveryPhases.RefWritten, $"`refs/heads/{line}` to hold {commit}");
    }

    /// <summary>
    /// What the merge's press came to, read from the line: done where it holds the commit, withdrawn where the press refused and it does not,
    /// unknown where git cannot say or it merged and the line does not hold it. A merge keeps no landing record of its own: the line holding
    /// the commit, through the integration's request the press settled, is its record, and that is what its <i>recorded</i> reads.
    /// </summary>
    public async Task MergedAsync(string root, string line, string commit, string? refused, CancellationToken ct)
    {
        await SeamAsync(DeliveryPhases.RefWritten).ConfigureAwait(false);
        var (_, tip) = await StandsAsync(root, $"refs/heads/{line}", ct).ConfigureAwait(false);
        var (code, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", commit, $"refs/heads/{line}"], ct).ConfigureAwait(false);
        var outcome = code == 0 ? DeliveryOutcomes.Done : code == 1 && refused is not null ? DeliveryOutcomes.Withdrawn : DeliveryOutcomes.Unknown;
        End(DeliveryPhases.RefWritten, outcome, tip, outcome == DeliveryOutcomes.Done ? $"`{line}` holds {commit}" : refused ?? $"`{line}` does not hold {commit}");
        if (outcome != DeliveryOutcomes.Done || Begin(DeliveryPhases.Recorded, $"`{line}` recorded holding {commit}") is not null) return;
        await SeamAsync(DeliveryPhases.Recorded).ConfigureAwait(false);
        End(DeliveryPhases.Recorded, DeliveryOutcomes.Done, tip, $"the integration's request is settled and `{line}` holds the commit at {tip}");
    }

    /// <summary>
    /// The landing record written by <paramref name="write"/> (the landing's own write and words), its entry ahead and its outcome read back by
    /// <paramref name="read"/>: done where the record holds <paramref name="branch"/> at <paramref name="tip"/>, else withdrawn. Refused ahead,
    /// nothing is recorded, and the landing says so.
    /// </summary>
    public async Task<TreeLanding> RecordAsync(TreeLanding landed, string branch, string tip, Func<TreeLanding> write, Func<LandedBranch?> read)
    {
        if (Begin(DeliveryPhases.Recorded, $"`{branch}` recorded at {tip}") is { } refused)
        {
            return landed with { Message = landed.Message + $" Its delivery did not record it, so its clean-up will not judge it: {refused}." };
        }

        var written = write();
        await SeamAsync(DeliveryPhases.Recorded).ConfigureAwait(false);
        var holds = read() is { } entry && string.Equals(entry.Tip, tip, StringComparison.OrdinalIgnoreCase);
        End(DeliveryPhases.Recorded, holds ? DeliveryOutcomes.Done : DeliveryOutcomes.Withdrawn, holds ? $"{LandedBranches.FileName}: `{branch}` at {tip}" : null,
            holds ? $"the landing record holds `{branch}` at {tip}" : $"the landing record does not hold `{branch}` at {tip}");
        return written;
    }

    /// <summary>
    /// The landing ended as <paramref name="landed"/> says: a preparation still standing is withdrawn with the landing's words, a refusal having
    /// made nothing, and the delivery stays unresolved, to be tried again or stopped; it is settled delivered once its method's reaching phase
    /// is done, so a push and a pull request stay open for their plugin's phases.
    /// </summary>
    public TreeLanding Close(TreeLanding landed)
    {
        if (_open == DeliveryPhases.Prepared)
        {
            // Landed with its preparation standing is a landing that wrote its ref by no phase: read as neither.
            End(DeliveryPhases.Prepared, landed.Landed ? DeliveryOutcomes.Unknown : DeliveryOutcomes.Withdrawn, null, landed.Message);
        }

        if (_done.Contains(DeliveryPhases.ReachOf(Intent.Method)))
        {
            _delivery.Store.Settle(_delivery.Claim, DeliverySettlements.Delivered, $"{Intent.Method} of {Intent.Tip} delivered");
        }

        return landed;
    }

    private void End(string phase, string outcome, string? evidence, string said)
    {
        _delivery.Store.End(_delivery.Claim, phase, outcome, evidence, said);
        _open = null;
        if (outcome == DeliveryOutcomes.Done) _done.Add(phase);
    }

    private Task SeamAsync(string phase) => _delivery.Seam?.Invoke(phase) ?? Task.CompletedTask;

    /// <summary>
    /// Where <paramref name="reference"/> stands: read and absent (null), read at a commit, or not read. Asked as its commit, the one spelling
    /// the landing reads a branch by (LANDGIT1c).
    /// </summary>
    private static async Task<(bool Read, string? At)> StandsAsync(string root, string reference, CancellationToken ct)
    {
        var (code, at, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"{reference}^{{commit}}"], ct).ConfigureAwait(false);
        return code switch { 0 => (true, at.Trim()), 1 => (true, null), _ => (false, null) };
    }

    /// <summary>The <c>squash</c> of the resolved options: false where they name none, null where they are not an object or it is no boolean.</summary>
    internal static bool? SquashOf(string options)
    {
        try
        {
            using var document = JsonDocument.Parse(options);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (!document.RootElement.TryGetProperty("squash", out var squash)) return false;
            return squash.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => null };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
