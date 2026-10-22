using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>One quest a close names (FABRIC-CLOSE1b), with the submission it judged: what the close door checks is still current.</summary>
public sealed record QuestCloseItem(string Id, string Machine, long Sequence);

/// <summary>A <c>quest.close</c> attempt's close as its record keeps it (§E5 step 1): the token, the quests named, and a refusal's words.</summary>
public sealed record QuestCloseCall(string Token, IReadOnlyList<QuestCloseItem> Quests)
{
    /// <summary>The door's words when it refused the close, kept so the refusal is said as it came; null otherwise.</summary>
    public string? Words { get; init; }

    public bool Equals(QuestCloseCall? other) =>
        other is not null && Token == other.Token && Words == other.Words && Quests.SequenceEqual(other.Quests);

    public override int GetHashCode() => HashCode.Combine(Token, Quests.Count, Words);
}

/// <summary>What the close door answered (<see cref="QuestCloseAnswer.Outcome"/>).</summary>
public static class QuestCloseOutcomes
{
    /// <summary>Every quest named is Done by this close: made now, or made already under the same key.</summary>
    public const string Closed = "closed";

    /// <summary>A check refused it, now or when it was first sent under this key, or the key went with another close: nothing was written.</summary>
    public const string Refused = "refused";

    /// <summary>
    /// The refusal's code for a key the door never took before it was asked what became of it (FABRIC-CLOSE1u): that close never ran, and the
    /// key is withdrawn, so it never runs. Its close is judged again on what is there now, under a new key.
    /// </summary>
    public const string Withdrawn = "withdrawn";

    /// <summary>
    /// The checks the close door names a refusal by (the service's <c>QuestCloseCodes</c>): only a refusal naming one of these is the door's
    /// own, and settles the close (FABRIC-CLOSE1l, the review's finding 7). Any other answer leaves the close unknown. 🔴 Twins: a code the
    /// door adds is added here, or its refusal leaves the close unknown; FABRIC-CLOSE1v added the door's scope checks, a quest of another
    /// repository, of another chain, or bound to another graph.
    /// </summary>
    public static IReadOnlySet<string> Codes { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "malformed", "not-found", "not-bound", "not-held", "closed", "stale", "held", "unsealed", "key-reused", "refused", Withdrawn,
        "other-repository", "not-in-chain", "other-graph",
    };
}

/// <summary>The close door's answer (FABRIC-CLOSE1a's <c>POST /api/quests/{id}/close</c>), as the driver reads it.</summary>
/// <param name="Outcome">One of <see cref="QuestCloseOutcomes"/>.</param>
/// <param name="Code">A refusal's check, as the door names it (<c>stale</c>, <c>held</c>, <c>key-reused</c>, …); null otherwise.</param>
/// <param name="Message">The door's sentence, verbatim.</param>
/// <param name="Quests">The quests named, as they now stand, where the door answered them.</param>
public sealed record QuestCloseAnswer(string Outcome, string? Code, string Message, IReadOnlyList<QuestView> Quests);

/// <summary>The close door, as the closer calls it (FABRIC-CLOSE1b): a port, so the fence is proved without a host.</summary>
public interface IQuestClosePort
{
    /// <exception cref="HttpRequestException">The host did not answer: the close's outcome is unknown, and its door is asked what became of it.</exception>
    Task<QuestCloseAnswer> CloseQuestsAsync(string quest, string key, string token, IReadOnlyList<QuestCloseItem> quests, CancellationToken ct);

    /// <summary>
    /// What became of a close sent with <paramref name="key"/> (FABRIC-CLOSE1u), answered from the door's own record, executing nothing: its
    /// first outcome where the door took the key (closed, or its refusal), else refused <see cref="QuestCloseOutcomes.Withdrawn"/>, the key
    /// withdrawn in the same transaction so a send of it still on its way is refused too.
    /// </summary>
    /// <exception cref="HttpRequestException">The host did not answer: the close stays unknown, and is asked after again.</exception>
    Task<QuestCloseAnswer> CloseOutcomeAsync(string quest, string key, string token, IReadOnlyList<QuestCloseItem> quests, CancellationToken ct);
}

/// <summary>What a close node may do now (<see cref="QuestClosing.Judge"/>).</summary>
/// <param name="Named">The quests it closes, its chain's in this repository oldest first, each with its current submission; empty when it waits.</param>
/// <param name="Waits">Why nothing is sent now, or null.</param>
/// <param name="Overridden">Every quest it would close is Done already: by its chain's close, or by the person (§E5's override).</param>
public sealed record QuestCloseJudged(IReadOnlyList<QuestCloseItem> Named, string? Waits, bool Overridden);

/// <summary>
/// The judgements a <c>quest.close</c> node rests on (FABRIC-CLOSE1b; the fabric events design §E5): whether a graph closes, the close token,
/// and which quests a close names. Pure.
/// </summary>
/// <remarks>
/// <b>The chain closes at its last quest here</b> (D173's CLOSE1b note, choice 2): a quest whose own steps still name this repository waits,
/// and the last one's close names every quest of the chain in this repository that is not Done yet, oldest first, in one call (§E5: all of
/// them or none). A quest the person closed is left as it is.
/// </remarks>
public static class QuestClosing
{
    /// <summary>The sender an attempt of Daoris's own close names: never a plugin's, so no plugin's frame is taken about it.</summary>
    public const string Sender = NodeKinds.QuestClose;

    /// <summary>The sender a close node's attempt reached on its chain's close names (FABRIC-CLOSE1l): never sent, reconciled or said.</summary>
    public const string Chained = NodeKinds.QuestClose + "/chain";

    /// <summary>The history entry a graph ended by the person's own done keeps: what says it once.</summary>
    public const string Overridden = "overridden";

    /// <summary>The history entry a close found closed only in part keeps, with the quest it names: what says it once.</summary>
    public const string Partial = "partially-closed";

    /// <summary>
    /// The history entry the person's word taken back on a close whose submission moved keeps (FABRIC-CLOSE1q4), with the quest and the
    /// word and the attempt (<c>word-taken-back #q7 stop 1</c>): what says it once on the page, where its close is no longer listed as held.
    /// </summary>
    public const string TakenBack = "word-taken-back";

    private const string Done = "Done";

    private const string Taken = "Taken";

    /// <summary>Whether a graph holds <c>quest.close</c>: a closure graph (§E5).</summary>
    public static bool Holds(NodeGraph? graph) => graph?.Nodes.Any(node => node.Kind == NodeKinds.QuestClose) == true;

    /// <summary>The close token (§E5 step 1): the unit, its revision, the graph's digest and the prerequisite acceptances' ids.</summary>
    public static string Token(string unit, string? revision, string graphDigest, IReadOnlyList<string> receipts)
    {
        var text = new StringBuilder("quest-close\n").Append(unit).Append('\n').Append(revision ?? "").Append('\n').Append(graphDigest).Append('\n');
        foreach (var receipt in receipts.Order(StringComparer.Ordinal)) text.Append(receipt).Append('\n');
        return "ctk-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..32];
    }

    /// <summary>Which quests the close of <paramref name="quest"/>'s unit names now, or why it waits, or that every one is Done already.</summary>
    public static QuestCloseJudged Judge(QuestView quest, IReadOnlyDictionary<string, QuestView> quests)
    {
        // FABRIC-CLOSE1l, the review's finding 1: the whole ancestry is walked, through other repositories' steps, so an A → B → A chain's last
        // A closes with its first; an ancestor not read leaves the chain unjudged, never judged without it.
        var (chain, unread) = Lineage(quest, quests);
        if (unread is not null) return new([], $"#{unread}, which #{quest.Id}'s chain grew from, was not read, so the chain is not judged whole", false);
        var open = chain.Where(each => each.Status != Done).ToList();
        if (open.Count == 0) return new([], null, true);
        // FABRIC-CLOSECASE1: "the same repository" is the service's rule, blind to case, as its NextStep and close door read it.
        if (quest.Status != Done && quest.Then.Any(step => string.Equals(step.To, quest.To, StringComparison.OrdinalIgnoreCase)))
        {
            return new([], $"#{quest.Id}'s chain has a step in this repository still to come, and closes at its last", false);
        }

        foreach (var each in open)
        {
            if (!string.Equals(each.Closure, quest.Closure, StringComparison.Ordinal))
            {
                return new([], $"#{each.Id}, of the same chain here, is not bound to this graph, so it closes by its own end first", false);
            }

            if (each.Status != Taken) return new([], $"#{each.Id} is {each.Status}", false);
            if (each.Submission is null) return new([], $"#{each.Id} has no submission yet: its taker says `ready` when its work is done", false);
            if (each.Held) return new([], $"#{each.Id}'s submission is held ({each.Hold ?? "for the person"})", false);
        }

        return new([.. open.Select(each => new QuestCloseItem(each.Id, each.Submission!.Machine, each.Submission.Sequence))], null, false);
    }

    /// <summary>
    /// The quest and every quest of its chain in this repository it grew from, oldest first, walked through every repository's steps; and
    /// the first ancestor named and not read, or null.
    /// </summary>
    public static (List<QuestView> Chain, string? Unread) Lineage(QuestView quest, IReadOnlyDictionary<string, QuestView> quests)
    {
        var chain = new List<QuestView> { quest };
        var seen = new HashSet<string>(StringComparer.Ordinal) { quest.Id };
        for (var at = quest; at.Parent is { Length: > 0 } parent && seen.Add(parent);)
        {
            if (!quests.TryGetValue(parent, out var before)) return (chain, parent);
            if (string.Equals(before.To, quest.To, StringComparison.OrdinalIgnoreCase)) chain.Insert(0, before);
            at = before;
        }

        return (chain, null);
    }

    /// <summary>
    /// A closing quest's revision (FABRIC-CLOSE1l, the review's findings 5 and 8): what its close judges, as one digest of every quest of its
    /// chain here (its moment, its status, its binding, its submission and its hold) and the tip of each branch their sessions worked. A
    /// commit, a new submission, or a hold gained or released anywhere in the chain moves it, which lapses every acceptance bound to the
    /// revision before (WIRE1e), a close refused on it included, so a fresh close is made under a fresh key.
    /// </summary>
    /// <param name="tips">The branches' tips git answered this judgement (FABRIC-CLOSE1p, <see cref="NodeUnitsRead.Tips"/>); null, each branch's recorded revision.</param>
    public static string Revision(
        QuestView quest, IReadOnlyDictionary<string, QuestView> quests, IReadOnlyList<WorkUnit> units, IReadOnlyDictionary<string, string?>? tips = null)
    {
        var (chain, unread) = Lineage(quest, quests);
        var text = new StringBuilder("quest-close-revision\n").Append(unread ?? "").Append('\n');

        // Closed, every quest of it here: what it closed on is all that stays, so nothing after its end (a landing moving a branch) reads as
        // new work; its Done itself moves it once, and the close's node is reached again on that close.
        if (unread is null && chain.All(each => each.Status == Done))
        {
            foreach (var each in chain) text.Append(each.Id).Append('\t').Append(each.Submission is { } closed ? $"{closed.Machine}/{closed.Sequence}" : "").Append('\n');
            return "crv-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..32];
        }

        foreach (var each in chain)
        {
            text.Append(each.Id).Append('\t').Append(each.Status).Append('\t').Append(each.Updated is { } moved ? Moments.Write(moved) : "")
                .Append('\t').Append(each.Closure ?? "").Append('\t').Append(each.Submission is { } submitted ? $"{submitted.Machine}/{submitted.Sequence}" : "")
                .Append('\t').Append(each.Held ? each.Hold ?? "held" : "").Append('\n');
        }

        // FABRIC-CLOSE1p: each branch's tip as git answered this judgement; a record's revision is its kept end fact's, which a commit does not move.
        foreach (var branch in Worked(chain, units).Select(unit => $"{unit.Id}\t{(tips is null ? unit.Subject.Revision : Tip(unit, tips) ?? "unread")}")
                     .Order(StringComparer.Ordinal))
        {
            text.Append(branch).Append('\n');
        }

        return "crv-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..32];
    }

    /// <summary>
    /// The close attempts kept on <paramref name="records"/> that name <paramref name="quest"/> at the submission it stands on now
    /// (<see cref="NamesCurrent"/>): the only ones whose close may speak for it.
    /// </summary>
    public static IEnumerable<NodeAttempt> Naming(IEnumerable<NodeRecord> records, QuestView quest) =>
        records.SelectMany(record => record.Attempts).Where(attempt => NamesCurrent(attempt, quest));

    /// <summary>
    /// Whether a close attempt names <paramref name="quest"/> at its current submission (FABRIC-CLOSE1q): its id, and the machine and
    /// sequence of the operation that made the submission. The same commit made ready again is another submission, so a close kept for
    /// the one before (its id, its commit, a sequence of another machine's) is never its authority. A quest with no submission has none.
    /// </summary>
    public static bool NamesCurrent(NodeAttempt attempt, QuestView quest) =>
        quest.Submission is { } current
        && attempt.Close?.Quests.Any(named => string.Equals(named.Id, quest.Id, StringComparison.Ordinal)
                                              && string.Equals(named.Machine, current.Machine, StringComparison.Ordinal)
                                              && named.Sequence == current.Sequence) == true;

    /// <summary>
    /// The first quest a close attempt names whose submission is its current one no more (FABRIC-CLOSE1q2): read, and standing on another
    /// submission or on none, since the person reopened it or a later <c>ready</c> replaced it; null where each one read still stands on the
    /// submission named. A quest the read does not hold is not judged.
    /// </summary>
    public static QuestCloseItem? Moved(NodeAttempt attempt, IReadOnlyDictionary<string, QuestView> quests) =>
        attempt.Close?.Quests.FirstOrDefault(named => quests.TryGetValue(named.Id, out var quest) && !NamesCurrent(attempt, quest));

    /// <summary>
    /// Whether a close attempt is unknown and offered to the person (FABRIC-CLOSE1u): its door answered neither the close nor the question of
    /// what became of its key, so it is asked after at each pass and never sent again by itself, and its node is still on it. The person may
    /// stop its work, or ask again now, with a held close's words (<see cref="HeldResolutions.CloseUnkept"/>).
    /// </summary>
    public static bool Unanswered(NodeRecord record, NodeAttempt attempt) =>
        attempt is { Plugin: Sender, State: AttemptStates.Unknown, Close: not null }
        && record.Statuses.GetValueOrDefault(attempt.Node) is { } status && status.Attempt == attempt.Attempt
        && status.State is NodeStates.Active or NodeStates.Unknown;

    /// <summary>
    /// Whether a close attempt is the settled one of a close found closed only in part (FABRIC-CLOSE1j; §E5, <see cref="Partial"/>), whose node
    /// is live again: the newest on its node, which a <see cref="Partial"/> entry of its record names, and nothing in flight on it. Its node
    /// would close the lost quest over the winner's take once that take submits, so the person is offered its stop.
    /// </summary>
    public static bool Partly(NodeRecord record, NodeAttempt attempt) =>
        attempt is { Plugin: Sender or Chained, State: AttemptStates.Settled }
        && record.Attempts.LastOrDefault(each => each.Node == attempt.Node) is { } newest && newest.Attempt == attempt.Attempt
        && record.History.Any(entry => entry.Was.Node == attempt.Node && entry.Why.StartsWith(Partial + " #", StringComparison.Ordinal))
        && NodeStates.Live.Contains(WorkPerformer.StatusOf(record, attempt.Node).State);

    /// <summary>
    /// The first branch <paramref name="quest"/>'s chain worked whose tip git did not answer this judgement (FABRIC-CLOSE1p), or null: such a
    /// close waits, never judged on a revision that could not be read. Null without <paramref name="tips"/>, where the records alone are judged.
    /// </summary>
    public static string? Unread(
        QuestView quest, IReadOnlyDictionary<string, QuestView> quests, IReadOnlyList<WorkUnit> units, IReadOnlyDictionary<string, string?>? tips)
    {
        if (tips is null) return null;
        return Worked(Lineage(quest, quests).Chain, units).OrderBy(unit => unit.Id, StringComparer.Ordinal).FirstOrDefault(unit => Tip(unit, tips) is null)
            is { } unread
            ? unread.Subject.Artifact ?? unread.Id
            : null;
    }

    /// <summary>
    /// The tip of each branch a bound quest's chain worked (FABRIC-CLOSE1p; §E5 step 1), by the branch unit's id: a commit, empty where git says
    /// the branch is not there, null where git did not say (its repository has no checkout here, or the read failed). One
    /// <see cref="RefSnapshot"/> per repository answers every branch there, taken for this judgement alone: a tip is a mutable fact, so the next
    /// judgement reads again (LANDGIT1), and the read right before a close is its own. No quest bound to a closure graph, no git.
    /// </summary>
    /// <param name="take">The snapshot of a checkout (the test seam); null, <see cref="RefSnapshot.Take(string)"/>.</param>
    internal static async Task<IReadOnlyDictionary<string, string?>> TipsAsync(
        IReadOnlyList<WorkUnit> units, IReadOnlyDictionary<string, string> roots, CancellationToken ct, Func<string, RefSnapshot>? take = null)
    {
        var quests = units.Select(unit => unit.Records.Quest).OfType<QuestView>()
            .GroupBy(quest => quest.Id, StringComparer.Ordinal).ToDictionary(same => same.Key, same => same.First(), StringComparer.Ordinal);
        // A chain wholly closed is judged on its quests alone (Revision), so a closed quest the service still lists reads no git.
        var chains = quests.Values.Where(quest => quest.Closure is { Length: > 0 }).Select(quest => Lineage(quest, quests))
            .Where(lineage => lineage.Unread is not null || lineage.Chain.Any(each => each.Status != Done)).SelectMany(lineage => lineage.Chain).ToList();
        var tips = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var repository in Worked(chains, units).DistinctBy(unit => unit.Id).GroupBy(unit => unit.Owner.Repository ?? "", StringComparer.OrdinalIgnoreCase))
        {
            var snapshot = RootOf(roots, repository.Key) is { } root ? (take ?? (checkout => RefSnapshot.Take(checkout)))(root) : null;
            foreach (var unit in repository)
            {
                tips[unit.Id] = snapshot is null ? null
                    : unit.Subject.Artifact is not { Length: > 0 } branch ? ""
                    : await snapshot.CommitAsync("refs/heads/" + branch, ct).ConfigureAwait(false) is (true, var commit) ? commit ?? ""
                    : null;
            }
        }

        return tips;
    }

    /// <summary>The branch units the quests of <paramref name="chain"/> produced: each one a session serving it worked.</summary>
    private static IEnumerable<WorkUnit> Worked(IEnumerable<QuestView> chain, IReadOnlyList<WorkUnit> units)
    {
        var ids = chain.Select(each => QuestUnits.IdOf(each.Id)).ToHashSet(StringComparer.Ordinal);
        return units.Where(unit => unit.Kind == WorkUnitKind.Branch && unit.Links.Any(link => link.Relation == WorkRelation.ProducedBy && ids.Contains(link.Unit)));
    }

    /// <summary>What git answered of <paramref name="unit"/>'s branch, the commit or empty where it is gone; null where it was not read.</summary>
    private static string? Tip(WorkUnit unit, IReadOnlyDictionary<string, string?> tips) => tips.TryGetValue(unit.Id, out var tip) ? tip : null;

    /// <summary>A repository's checkout here, by its name as written, else blind to case as the inventory groups repositories; null where none.</summary>
    private static string? RootOf(IReadOnlyDictionary<string, string> roots, string repository) =>
        roots.TryGetValue(repository, out var root) && root.Length > 0 ? root
        : roots.FirstOrDefault(each => string.Equals(each.Key, repository, StringComparison.OrdinalIgnoreCase) && each.Value.Length > 0).Value;
}

/// <summary>
/// Daoris's own <c>quest.close</c> (FABRIC-CLOSE1b; the fabric events design §E5's fence): the attempt written with its key and token before
/// the close is sent, the door's answer kept on it, and a timeout or a restart sending the same close again.
/// </summary>
/// <remarks>
/// <para><b>The fence</b> (§E5, D173's CLOSE1b note, choice 1): step 1 is one locked edit that judges the start on the record as the evaluation
/// left it and writes the attempt with its key and token; step 2 is the door's one transaction; step 3 is one locked edit that keeps the answer
/// on that attempt, only while it still waits for it. Nothing is awaited inside an edit (PERFORM1h). What holds the driver's facts still between
/// steps 1 and 3 is the lease's one writer and the pass's one-at-a-time: no other writer edits the unit's record meanwhile.</para>
/// <para><b>Step 4:</b> an unanswered close is unknown, and the next pass sends the same close again, its key, token and quests unchanged: the
/// door answers its first outcome, a refusal included, and writes nothing twice.</para>
/// </remarks>
public sealed class QuestCloser(NodeRecordFiles records, IQuestClosePort port, Func<DateTimeOffset> clock)
{
    /// <summary>A refused close is never a transient failure: a new close is made under a new key once what refused it moves.</summary>
    public const string Refused = "refused";

    private readonly List<string> _said = [];

    /// <summary>What the closer said since it was last asked: each refusal in the door's words.</summary>
    public IReadOnlyList<string> TakeSaid()
    {
        var said = _said.ToList();
        _said.Clear();
        return said;
    }

    /// <summary>The fence's steps 1 to 3 on the start the evaluation proposed: the attempt written first, then the close sent, then its answer kept.</summary>
    public async Task<WorkPerformed> StartAsync(NodeDeclaration node, WireStart start, NodeStatus evaluated, IReadOnlyList<QuestCloseItem> named, CancellationToken ct)
    {
        if (start.Move.Binding is not { } binding) return new("", start.Node, PerformedWhat.Refused, NodeRefusal.NoBinding);
        var unit = binding.Unit;
        if (node.Kind != NodeKinds.QuestClose || named.Count == 0) return new(unit, node.Id, PerformedWhat.Refused, WorkPerformer.NotDeclared);
        var now = clock();
        var (refused, started) = records.Edit<(NodeAttempt? Attempt, string? Refused)>(unit, record =>
        {
            var held = WorkPerformer.StatusOf(record, node.Id);
            if (!WorkPerformer.Same(held, evaluated)) return (null, (null, WorkPerformer.Moved));
            var moved = NodeLifecycle.Apply(node, held, start.Move);
            if (!moved.Moved) return (null, (null, moved.Refused));
            var call = new WorkPerformCall(unit, node.Kind, node.Version, WorkPerformer.Random("i-", 12), WorkPerformer.Random("k-", 16), binding.SubjectRevision ?? "",
                [.. binding.Inputs.OrderBy(input => input.Key, StringComparer.Ordinal).Select(input => input.Value.Ref)], now + WorkPerformer.Timeout(node));
            var attempt = new NodeAttempt(node.Id, moved.Status.Attempt, QuestClosing.Sender, call, AttemptStates.Recorded)
            {
                RecordedAt = now,
                Close = new QuestCloseCall(QuestClosing.Token(unit, binding.SubjectRevision, binding.GraphDigest, binding.PrerequisiteReceipts), [.. named]),
            };
            var fired = record.Fired.Concat(start.Firings.Select(firing => firing.Key)).Distinct(StringComparer.Ordinal).ToList();
            return (WorkPerformer.Kept(record, moved, "start", now) with { Attempts = [.. record.Attempts, attempt], Fired = fired }, (attempt, null));
        });
        if (refused is not null) return new(unit, node.Id, PerformedWhat.Refused, refused);
        if (started.Attempt is not { } recorded) return new(unit, node.Id, PerformedWhat.Refused, started.Refused);
        return await AnswerAsync(unit, recorded, node, PerformedWhat.Sent, ask: false, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Step 4: every close attempt whose outcome is not established (written and never answered, or unanswered) is asked after, each question
    /// taking one of <paramref name="sends"/>. Asked whatever graph governs its unit now: a close in flight is reconciled.
    /// </summary>
    /// <remarks>
    /// FABRIC-CLOSE1u (the 2026-10-18 review of the close path): never sent again blind. A close written before a crash, never delivered, is
    /// judged on the revision it was written on, and its key's idempotency protects an outcome already kept, not a key never used, so a resend
    /// after the branch moved would close on obsolete evidence. Its door is asked what became of the key, answering from its own record and
    /// executing nothing: a key it took is settled from the outcome it kept; a key it never took is withdrawn there, in the same transaction,
    /// and its attempt is settled as never started, its node judged again on what is there now (<see cref="Withdrawn"/>), so a close is made
    /// again only through the fresh fence, under a new key. An unanswered question leaves the close unknown, offered to the person.
    /// </remarks>
    public async Task<IReadOnlyList<WorkPerformed>> ReconcileAsync(WorkSends sends, CancellationToken ct)
    {
        var done = new List<WorkPerformed>();
        foreach (var record in records.All().Records)
        {
            foreach (var attempt in record.Attempts.Where(Waiting).ToList())
            {
                if (!sends.Take()) return done;
                done.Add(await AnswerAsync(record.Unit, attempt, Declaration(attempt), PerformedWhat.Redelivered, ask: true, ct).ConfigureAwait(false));
            }
        }

        return done;
    }

    /// <summary>
    /// The person's word on each held close (FABRIC-CLOSE1q), and on each unknown one (FABRIC-CLOSE1u, <see cref="QuestClosing.Unanswered"/>),
    /// applied before any close is asked after (<see cref="ReconcileAsync"/>): a close's recovery, never a plugin's <i>it happened</i> or <i>it
    /// did not</i>, since its outcome is its door's answer and no word of the person's stands in for it, and a new close is never made. The word
    /// is cleared as it is applied, so only one not applied yet is ever written.
    /// </summary>
    /// <remarks>
    /// <para><b><c>record</c></b>: the door's answer kept on the attempt (it closed the quest) recorded again through the one table, its key as
    /// the evidence. Refused again, the attempt stays held with the new refusal; its node moved off, it settles as a stale one's (§D3).</para>
    /// <para><b><c>resend</c></b>: the attempt waits for its answer again, so its door is asked within this pass what became of its key, its
    /// token and named submissions; the door answers its first outcome and writes nothing (FABRIC-CLOSE1u: never the close sent again).</para>
    /// <para><b><c>stop</c></b>, offered apart: every live node of its work stopped by the person, and the close's answer kept as it is (a
    /// kept one settles the attempt, its receipt kept; one not kept is asked for again with its key, and lands as a stale one's). The quest's
    /// Done, if the door wrote it, stands.</para>
    /// <para>A plugin's word recorded on a close before this build, or <c>record</c> with nothing kept, is taken back, saying why.</para>
    /// <para><b>A close for a submission reopened or replaced since</b> (FABRIC-CLOSE1q2, <see cref="QuestClosing.Moved"/>, judged on
    /// <paramref name="quests"/>): <c>record</c> and <c>stop</c> are taken back, saying why. Its answer closes none of the work there is now,
    /// and a stop through it would stop the next submission's: its nodes already on that work, or the close node itself, since a stop is the
    /// person's word for the work and not for a revision (<see cref="NodeStates.Bound"/>), so the next submission's close would never run.
    /// The nodes it was bound to lapse with its revision. <c>resend</c> asks after its key alone, as before.</para>
    /// </remarks>
    /// <param name="declared">The declaration governing each unit's node now.</param>
    /// <param name="quests">The quests the pass read, by id; null, no close is judged moved.</param>
    public IReadOnlyList<WorkPerformed> Resolve(Func<string, string, NodeDeclaration?> declared, IReadOnlyDictionary<string, QuestView>? quests = null)
    {
        var done = new List<WorkPerformed>();
        foreach (var read in records.All().Records)
        {
            if (!read.Attempts.Any(Worded)) continue;
            var lines = new List<string>();
            var (refused, resolved) = records.Edit<List<WorkPerformed>>(read.Unit, current =>
            {
                lines.Clear();
                var said = new List<WorkPerformed>();
                var record = current;
                foreach (var attempt in current.Attempts.Where(Worded))
                {
                    var (next, result) = Recover(record, attempt, declared, quests, lines);
                    record = next;
                    said.Add(result);
                }

                return (ReferenceEquals(record, current) ? null : record, said);
            });
            if (refused is not null) done.Add(new(read.Unit, "", PerformedWhat.Refused, refused));
            else
            {
                done.AddRange(resolved!);
                _said.AddRange(lines);
            }
        }

        return done;
    }

    /// <summary>Whether any close attempt holds the person's word not applied yet: the pass that applies it reads the quests afresh (FABRIC-CLOSE1q2).</summary>
    public bool Worded() => records.All().Records.Any(record => record.Attempts.Any(Worded));

    /// <summary>A close attempt holding a word to apply. FABRIC-CLOSE1u: an unknown close takes the person's word as a held one does, before it is asked after.</summary>
    // FABRIC-CLOSE1j: a close found closed only in part takes the person's stop on its settled attempt (QuestClosing.Partly).
    private static bool Worded(NodeAttempt attempt) =>
        attempt is { Plugin: QuestClosing.Sender, State: AttemptStates.Held or AttemptStates.Unknown, Resolution: not null, Close: not null }
            or { Plugin: QuestClosing.Sender or QuestClosing.Chained, State: AttemptStates.Settled, Resolution: HeldResolutions.Stop };

    /// <summary>One word on a held close applied (<see cref="Resolve"/>): the record after it, and what it came to; a word taken back says why in <paramref name="lines"/>.</summary>
    private (NodeRecord Next, WorkPerformed Result) Recover(
        NodeRecord record, NodeAttempt attempt, Func<string, string, NodeDeclaration?> declared, IReadOnlyDictionary<string, QuestView>? quests, List<string> lines)
    {
        var unit = record.Unit;
        var word = attempt.Resolution!;
        var node = declared(unit, attempt.Node) ?? Declaration(attempt);
        var cleared = attempt with { Resolution = null, ResolvedAt = null };

        // FABRIC-CLOSE1j: the stop of a close found closed only in part. Its answer stands for the quests it kept, so the attempt stays settled,
        // and its work's live nodes are stopped: the lost quest's work is the winner's, never closed here over its take (§E5).
        if (attempt.State == AttemptStates.Settled)
        {
            var stopped = new List<string>();
            record = Stopped(record, node, declared, stopped);
            if (stopped.Count > 0) lines.Add($"work  {unit}: stopped by you, as its close was found closed only in part: {string.Join(", ", stopped)}.");
            return (WorkPerformer.With(record, attempt, cleared), new(unit, node.Id, PerformedWhat.Resolved, word));
        }

        if (WorkPerformer.WordRefused(attempt, word) is { } code)
        {
            return (WorkPerformer.With(record, attempt, cleared with { Code = code }), new(unit, node.Id, PerformedWhat.Refused, code));
        }

        // FABRIC-CLOSE1q2: S1's close never records on, nor stops, the work of S2 at its commit; the nodes bound to S1 lapse with its revision.
        if (word != HeldResolutions.Resend && quests is not null && QuestClosing.Moved(attempt, quests) is { } moved)
        {
            lines.Add($"work  {unit}: your `{word}` on its close ({node.Id}, attempt {attempt.Attempt}) was not taken: #{moved.Id}'s submission "
                + $"{moved.Machine}/{moved.Sequence}, which that close names, was reopened or replaced since, so the work there is now is never that close's to "
                + (word == HeldResolutions.Record ? "record" : "stop")
                + (attempt.State == AttemptStates.Unknown ? "; its door is still asked what became of it." : "; say resend to ask its door what became of it."));
            // FABRIC-CLOSE1q4: kept in its history too, said on the page once its close is no longer listed (its node moved off, before
            // this pass ends or later); while it is listed, its held row says it (submission-moved).
            var why = $"{QuestClosing.TakenBack} #{moved.Id} {word} {attempt.Attempt}";
            record = record with { History = [.. record.History, new NodeHistory(clock(), why, WorkPerformer.StatusOf(record, node.Id))] };
            return (WorkPerformer.With(record, attempt, cleared with { Code = WorkPerformer.SubmissionMoved }),
                new(unit, node.Id, PerformedWhat.Refused, WorkPerformer.SubmissionMoved));
        }

        if (word == HeldResolutions.Resend)
        {
            // FABRIC-CLOSE1q4: the person's resend answers a word of theirs taken back on it, which is said no more.
            var answered = $" {attempt.Attempt}";
            record = record with
            {
                History = [.. record.History.Where(entry => !(entry.Was.Node == node.Id && entry.Why.StartsWith(QuestClosing.TakenBack + " #", StringComparison.Ordinal)
                                                              && entry.Why.EndsWith(answered, StringComparison.Ordinal)))],
            };
            return (WorkPerformer.With(record, attempt, cleared with { State = AttemptStates.Unknown }), new(unit, node.Id, PerformedWhat.Resolved, word));
        }

        if (word == HeldResolutions.Record)
        {
            var (next, result) = Answered(record, cleared, node, closed: true, null, attempt.Close);
            return (next, result.What == PerformedWhat.Held ? result with { What = PerformedWhat.Refused } : new(unit, node.Id, PerformedWhat.Resolved, word));
        }

        // Stop, apart from the close's answer: its work's live nodes stopped by the person, whatever the close comes to.
        record = Stopped(record, node, declared, []);
        if (attempt.Outcome != WorkStates.Reached)
        {
            return (WorkPerformer.With(record, attempt, cleared with { State = AttemptStates.Unknown }), new(unit, node.Id, PerformedWhat.Resolved, word));
        }

        var after = WorkPerformer.StatusOf(record, node.Id);
        record = WorkPerformer.With(record, attempt, cleared with { State = AttemptStates.Settled, Code = null });
        return (record with { Statuses = WorkPerformer.With(record.Statuses, NodeLifecycle.Reconciled(after, attempt.Attempt)) }, new(unit, node.Id, PerformedWhat.Resolved, word));
    }

    /// <summary>The record with every live node of its work stopped by the person, each kept in its history and named in <paramref name="stopped"/>.</summary>
    private NodeRecord Stopped(NodeRecord record, NodeDeclaration node, Func<string, string, NodeDeclaration?> declared, List<string> stopped)
    {
        var now = clock();
        foreach (var (id, status) in record.Statuses.Where(each => NodeStates.Live.Contains(each.Value.State)).ToList())
        {
            if ((id == node.Id ? node : declared(record.Unit, id)) is not { } live) continue;
            var moved = NodeLifecycle.Apply(live, status, new NodeMove(NodeStates.Stopped, NodeCause.Person));
            record = WorkPerformer.Kept(record, moved, "person-stop", now);
            if (moved.Moved) stopped.Add(id);
        }

        return record;
    }

    /// <summary>A close node's declaration from its attempt: a close in flight is answered whatever graph governs its unit now.</summary>
    private static NodeDeclaration Declaration(NodeAttempt attempt) => new(attempt.Node, NodeKinds.QuestClose, attempt.Call.StepVersion, NodeReach.Performed);

    /// <summary>
    /// The chain's close reached on a quest it closed (D173's CLOSE1b note, choice 2): a chain quest's own close node, proposed to start once its
    /// quest is Done by the close its chain's last quest made, is started and reached in one locked edit on that close's key, never sent.
    /// </summary>
    public WorkPerformed ReachByChain(NodeDeclaration node, WireStart start, NodeStatus evaluated, NodeAttempt chained)
    {
        if (start.Move.Binding is not { } binding) return new("", start.Node, PerformedWhat.Refused, NodeRefusal.NoBinding);
        var unit = binding.Unit;
        var now = clock();
        var (refused, result) = records.Edit(unit, record =>
        {
            var held = WorkPerformer.StatusOf(record, node.Id);
            if (!WorkPerformer.Same(held, evaluated)) return (null, new WorkPerformed(unit, node.Id, PerformedWhat.Refused, WorkPerformer.Moved));
            var begun = NodeLifecycle.Apply(node, held, start.Move);
            if (!begun.Moved) return (null, new WorkPerformed(unit, node.Id, PerformedWhat.Refused, begun.Refused));
            var reached = NodeLifecycle.Apply(node, begun.Status, Reach(node, begun.Status, chained.Call.IdempotencyKey));
            if (!reached.Moved) return (null, new WorkPerformed(unit, node.Id, PerformedWhat.Refused, reached.Refused));
            var fired = record.Fired.Concat(start.Firings.Select(firing => firing.Key)).Distinct(StringComparer.Ordinal).ToList();

            // FABRIC-CLOSE1l: a node with attempts of its own (the close's own node, started again once its quest's Done moved its revision)
            // keeps one for the attempt started here, settled on the chain's close and never sent, so its record still agrees with itself
            // (each node's attempts end at its status's) and reads; marked as the chain's, so nothing reconciles or says it.
            var attempts = record.Attempts.Any(attempt => attempt.Node == node.Id)
                ? [.. record.Attempts, chained with
                {
                    Node = node.Id, Attempt = begun.Status.Attempt, Plugin = QuestClosing.Chained, State = AttemptStates.Settled, RecordedAt = now,
                    Outcome = WorkStates.Reached, Code = null, Call = chained.Call with { Unit = unit, Invocation = WorkPerformer.Random("i-", 12) },
                }]
                : record.Attempts;
            return (WorkPerformer.Kept(WorkPerformer.Kept(record, begun, "start", now), reached, "chain", now) with { Fired = fired, Attempts = attempts },
                new WorkPerformed(unit, node.Id, PerformedWhat.Applied, WorkStates.Reached));
        });
        return refused is null ? result! : new(unit, node.Id, PerformedWhat.Refused, refused);
    }

    /// <summary>Whether a close attempt waits for its door's answer: written and never answered, or unanswered.</summary>
    private static bool Waiting(NodeAttempt attempt) =>
        attempt.Plugin == QuestClosing.Sender && attempt.Close is not null && attempt.State is AttemptStates.Recorded or AttemptStates.Unknown;

    /// <summary>
    /// One send of a close, its key, token and quests as written (step 2), or, with <paramref name="ask"/>, the question of what became of it
    /// (FABRIC-CLOSE1u); and the answer kept on its attempt after the await (step 3), only while that attempt still waits for it. Unanswered,
    /// the attempt and its node are unknown, and it is asked after again.
    /// </summary>
    private async Task<WorkPerformed> AnswerAsync(string unit, NodeAttempt attempt, NodeDeclaration node, string what, bool ask, CancellationToken ct)
    {
        var close = attempt.Close!;
        var invocation = attempt.Call.Invocation;
        QuestCloseAnswer answer;
        try
        {
            var key = attempt.Call.IdempotencyKey;
            answer = ask
                ? await port.CloseOutcomeAsync(close.Quests[^1].Id, key, close.Token, close.Quests, ct).ConfigureAwait(false)
                : await port.CloseQuestsAsync(close.Quests[^1].Id, key, close.Token, close.Quests, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (Unanswered(error, ct))
        {
            var now = clock();
            var (unsent, _) = records.Edit<bool>(unit, record =>
            {
                if (record.Invoked(invocation) is not { State: AttemptStates.Recorded or AttemptStates.Unknown } current) return (null, false);
                var status = WorkPerformer.StatusOf(record, node.Id);
                if (status.State == NodeStates.Active && status.Attempt == current.Attempt)
                {
                    record = WorkPerformer.Kept(record, NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Unknown, NodeCause.Daoris) { Attempt = current.Attempt }), "unknown", now);
                }

                return (WorkPerformer.With(record, current, current with { State = AttemptStates.Unknown }), true);
            });
            return new(unit, node.Id, PerformedWhat.Unknown, unsent ?? PerformedWhat.Unanswered);
        }

        var withdrawn = answer is { Outcome: QuestCloseOutcomes.Refused, Code: QuestCloseOutcomes.Withdrawn };
        var (refused, result) = records.Edit(unit, record => record.Invoked(invocation) is { State: AttemptStates.Recorded or AttemptStates.Unknown } current
            ? withdrawn ? Withdrawn(record, current, node) : Outcome(record, current, node, answer)
            : (null, new WorkPerformed(unit, node.Id, PerformedWhat.NotTaken, WorkPerformer.Moved)));
        if (refused is not null) return new(unit, node.Id, PerformedWhat.Unknown, refused);
        if (withdrawn && result!.What != PerformedWhat.NotTaken)
        {
            _said.Add($"work  {unit}: its close was never taken by its door, so its key is withdrawn and it is judged again on what is there now.");
        }
        else if (result is { What: PerformedWhat.Applied, Code: WorkStates.Failed })
        {
            _said.Add($"work  {unit}: its close was not made ({answer.Code ?? answer.Outcome}): {answer.Message}");
        }

        return result!.What == PerformedWhat.Applied && what == PerformedWhat.Redelivered ? result with { What = PerformedWhat.Applied } : result;
    }

    /// <summary>
    /// A close whose door never took its key (FABRIC-CLOSE1u): it never ran, and the door withdrew the key as it answered, so it never will. The
    /// attempt is settled as never started, its key kept as the one withdrawn, and a node still on it is invalidated, its attempt superseded:
    /// the next evaluation judges it again on what is there now, and where it is still eligible makes a new close, under a new key, through the
    /// fresh fence. A node moved off it is left as it stands, the gate the attempt held lifted (§D3).
    /// </summary>
    private (NodeRecord Next, WorkPerformed Result) Withdrawn(NodeRecord record, NodeAttempt attempt, NodeDeclaration node)
    {
        var now = clock();
        var status = WorkPerformer.StatusOf(record, node.Id);
        var result = new WorkPerformed(record.Unit, node.Id, PerformedWhat.Stale);
        if (status.Attempt == attempt.Attempt && status.State is NodeStates.Active or NodeStates.Unknown)
        {
            var superseded = NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Invalidated, NodeCause.Daoris));
            if (superseded.Moved)
            {
                record = WorkPerformer.Kept(record, superseded, QuestCloseOutcomes.Withdrawn, now);
                result = result with { What = PerformedWhat.Applied, Code = NodeStates.Invalidated };
            }
        }

        var after = WorkPerformer.StatusOf(record, node.Id);
        record = WorkPerformer.With(record, attempt, attempt with
        {
            State = AttemptStates.Settled, Outcome = ReconcileStates.NotStarted, Code = QuestCloseOutcomes.Withdrawn, Resolution = null, ResolvedAt = null,
        });
        return (record with { Statuses = WorkPerformer.With(record.Statuses, NodeLifecycle.Reconciled(after, attempt.Attempt)) }, result);
    }

    /// <summary>
    /// The door's answer applied to its attempt's node through the one table: closed is the reach, on the close's key as its evidence; refused
    /// is the failure, with the door's code and its words kept. An answer that is neither never reaches here: the client throws it as unanswered
    /// (FABRIC-CLOSE1l). An attempt its node has moved off is settled as a stale one's, lifting the gate it held (§D3).
    /// </summary>
    private (NodeRecord? Next, WorkPerformed Result) Outcome(NodeRecord record, NodeAttempt attempt, NodeDeclaration node, QuestCloseAnswer answer)
    {
        var closed = answer.Outcome == QuestCloseOutcomes.Closed;
        return Answered(record, attempt with { Deliveries = attempt.Deliveries + 1 }, node, closed, closed ? null : answer.Code ?? Refused,
            closed ? attempt.Close : attempt.Close! with { Words = answer.Message });
    }

    /// <summary>
    /// A door's answer applied to its attempt's node (<see cref="Outcome"/>, and the person's <c>record</c>): settled with it where it binds or
    /// where the node moved off the attempt; held where the one table refuses it, the answer kept on the attempt and any word taken back, so
    /// the person's next word is asked afresh and never applied twice (FABRIC-CLOSE1q).
    /// </summary>
    private (NodeRecord Next, WorkPerformed Result) Answered(NodeRecord record, NodeAttempt attempt, NodeDeclaration node, bool closed, string? code, QuestCloseCall? close)
    {
        var unit = record.Unit;
        var now = clock();
        var outcome = closed ? WorkStates.Reached : WorkStates.Failed;
        var answered = attempt with { Outcome = outcome, Close = close, Resolution = null, ResolvedAt = null };
        var settled = answered with { State = AttemptStates.Settled, Code = code };
        var status = WorkPerformer.StatusOf(record, node.Id);
        if (status.Attempt != attempt.Attempt || status.State is not (NodeStates.Active or NodeStates.Unknown))
        {
            var stale = WorkPerformer.With(record, attempt, settled);
            return (stale with { Statuses = WorkPerformer.With(stale.Statuses, NodeLifecycle.Reconciled(status, attempt.Attempt)) }, new(unit, node.Id, PerformedWhat.Stale));
        }

        var move = closed
            ? Reach(node, status, attempt.Call.IdempotencyKey)
            : new NodeMove(NodeStates.Failed, NodeCause.Authority) { Source = NodeSource.CloseDoor, Attempt = attempt.Attempt, Code = code };
        var moved = NodeLifecycle.Apply(node, status, move);
        if (!moved.Moved)
        {
            var (held, result) = WorkPerformer.Hold(record, answered, moved.Refused!);
            return (held!, result);
        }

        return (WorkPerformer.With(WorkPerformer.Kept(record, moved, outcome, now), attempt, settled), new(unit, node.Id, PerformedWhat.Applied, outcome));
    }

    /// <summary>The close's reach (§E2: the close door's write is its authority): its binding, with the close's key as its evidence.</summary>
    private static NodeMove Reach(NodeDeclaration node, NodeStatus status, string key) => new(NodeStates.Reached, NodeCause.Authority)
    {
        Source = NodeSource.CloseDoor,
        Attempt = status.Attempt,
        Acceptance = status.Binding is { } binding
            ? binding with { Evidence = new NodePort(node.Evidence ?? node.Kind, "close:" + key, binding.SubjectRevision, null) }
            : null,
    };

    /// <summary>A failure of the call itself, not the loop ending: the close's outcome is unknown, and it is sent again, never made anew.</summary>
    private static bool Unanswered(Exception error, CancellationToken ct) =>
        !ct.IsCancellationRequested
        && error is HttpRequestException or IOException or TimeoutException or TaskCanceledException or DriverException or JsonException;
}

/// <summary>Whether a quest was bound to its closure graph at its take, and what is said (<see cref="ClosureBinding.AtTakeAsync"/>).</summary>
public sealed record ClosureBound(bool Bound, string Said);

/// <summary>
/// The closure graphs this machine bound a quest to, kept by digest beside the node records (FABRIC-CLOSE1l; the fabric events design §E5's
/// cutover: "removing `quest.close` from a graph changes nothing for runs already bound"). A quest bound to a graph since edited or removed is
/// closed by the graph it was bound to, read from here; a home that never bound one keeps nothing, and nothing here is read.
/// </summary>
public static class ClosureGraphs
{
    /// <summary>The folder the kept graphs sit in, beside the node records (<see cref="NodeRecordFiles.FolderOf"/>).</summary>
    public static string FolderOf(string home) => DaorisHome.Entry(home, HomeEntry.Workflows, "closures");

    /// <summary>Whether this home keeps any closure graph: one stat, so a home that never bound a quest reads no record.</summary>
    public static bool Any(string home) => Directory.Exists(FolderOf(home));

    /// <summary>Keeps the graph <paramref name="json"/> spells under its digest, once; a graph whose digest is not that is never kept.</summary>
    public static void Keep(string home, string digest, string json)
    {
        if (!IsDigest(digest) || File.Exists(PathOf(home, digest))) return;
        if (NodeGraphs.Read(json).Graph is not { } graph || NodeGraphs.Digest(graph) != digest || !QuestClosing.Holds(graph)) return;
        Directory.CreateDirectory(FolderOf(home));
        AtomicFile.WriteText(PathOf(home, digest), json.ReplaceLineEndings("\n"));
    }

    /// <summary>The graph kept under <paramref name="digest"/>, or null where none is, or it does not read as that digest.</summary>
    public static NodeGraph? Read(string home, string digest)
    {
        if (!IsDigest(digest) || !File.Exists(PathOf(home, digest))) return null;
        try
        {
            return NodeGraphs.Read(File.ReadAllText(PathOf(home, digest))).Graph is { } graph && NodeGraphs.Digest(graph) == digest ? graph : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Drops each kept graph <paramref name="needed"/> says nothing needs, and the folder once it is empty (FABRIC-CLOSE1m), so a home that
    /// no longer binds a quest is not kept armed by it. A graph written within <paramref name="grace"/> of <paramref name="now"/> stays: it is
    /// kept before the service binds its quest (<see cref="ClosureBinding.AtTakeAsync"/>), and no read shows that quest bound yet.
    /// </summary>
    public static void Prune(string home, Func<string, bool> needed, DateTimeOffset now, TimeSpan grace)
    {
        if (!Directory.Exists(FolderOf(home))) return;
        try
        {
            foreach (var path in Directory.GetFiles(FolderOf(home), "*.json"))
            {
                var digest = Path.GetFileNameWithoutExtension(path);
                if (!IsDigest(digest) || needed(digest) || now - new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero) < grace) continue;
                File.Delete(path);
            }

            if (!Directory.EnumerateFileSystemEntries(FolderOf(home)).Any()) Directory.Delete(FolderOf(home));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left for the next pass: a graph kept is a graph that closes, never a lost one.
        }
    }

    private static string PathOf(string home, string digest) => Path.Combine(FolderOf(home), digest + ".json");

    /// <summary>A digest as <see cref="NodeGraphs.Digest"/> writes it, so no name the service answers is read as a path.</summary>
    private static bool IsDigest(string digest) =>
        digest.StartsWith("gph-", StringComparison.Ordinal) && digest.Length > 4 && digest[4..].All(char.IsAsciiHexDigitLower);
}

/// <summary>
/// The runtime's last fresh read of the quests, kept beside the node records as <c>workflows/quests.json</c> (FABRIC-CLOSE1q5), so the
/// terminal, which has no runtime, judges a held close as the page does (<see cref="QuestClosing.Moved"/>). Only what <c>Moved</c> needs is
/// kept: per quest, its current submission's machine and sequence, or null for a quest with none. Never under <c>units/</c>, where any file
/// not a unit's record is reported unreadable. A read left by a closed desktop is kept and used: a submission that moved stays moved (its
/// identity is machine and sequence), so a stale read can only miss a move, which the pass still refuses, never invent one.
/// </summary>
public static class QuestReads
{
    /// <summary>The file, beside <c>units/</c> and <c>closures/</c>.</summary>
    public static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.Workflows, "quests.json");

    /// <summary>Keeps <paramref name="quests"/>' submissions, atomically; a failed write keeps nothing and says nothing.</summary>
    public static void Keep(string home, IReadOnlyDictionary<string, QuestView> quests)
    {
        var path = PathOf(home);
        try
        {
            var body = new System.Text.Json.Nodes.JsonObject();
            foreach (var (id, quest) in quests.OrderBy(each => each.Key, StringComparer.Ordinal))
            {
                body[id] = quest.Submission is { } submission
                    ? new System.Text.Json.Nodes.JsonObject { ["machine"] = submission.Machine, ["sequence"] = submission.Sequence }
                    : null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            AtomicFile.WriteText(path, new System.Text.Json.Nodes.JsonObject { ["quests"] = body }.ToJsonString().ReplaceLineEndings("\n"));
            HomeWrites.Mine(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>The kept read as stand-ins <see cref="QuestClosing.Moved"/> judges, or null where none is kept or it does not read.</summary>
    public static IReadOnlyDictionary<string, QuestView>? Read(string home)
    {
        try
        {
            if (!File.Exists(PathOf(home))) return null;
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(PathOf(home)));
            if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object
                || !document.RootElement.TryGetProperty("quests", out var kept) || kept.ValueKind != System.Text.Json.JsonValueKind.Object) return null;
            var read = new Dictionary<string, QuestView>(StringComparer.Ordinal);
            foreach (var each in kept.EnumerateObject())
            {
                QuestSubmissionView? submission = null;
                if (each.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    if (!each.Value.TryGetProperty("machine", out var machine) || machine.ValueKind != System.Text.Json.JsonValueKind.String
                        || !each.Value.TryGetProperty("sequence", out var sequence) || !sequence.TryGetInt64(out var number)) return null;
                    submission = new QuestSubmissionView(machine.GetString()!, number, null, null);
                }
                else if (each.Value.ValueKind != System.Text.Json.JsonValueKind.Null) return null;
                read[each.Name] = new QuestView(each.Name, "", "", "", "", "") { Submission = submission };
            }

            return read;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>Deletes the kept read, at a disarm; nothing kept is nothing to delete.</summary>
    public static void Delete(string home)
    {
        try
        {
            File.Delete(PathOf(home));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>The service's two doors a bind reads and writes (FABRIC-CLOSE1b): the handshake, and the bind itself. A port, so a bind is proved without a host.</summary>
public interface IClosureBindPort
{
    /// <summary>Whether the local host knows a closure graph's operations (<c>bound</c>, <c>submitted</c>) and doors; false from a host before them.</summary>
    Task<bool> KnowsClosureAsync(CancellationToken ct);

    /// <summary>
    /// Whether the local host judges a bind in a workspace synced through a remote by the remote's handshake (FABRIC-CLOSE1i), refusing it
    /// where a machine of it has not declared it knows a bound quest; false from a host before it, which is asked no such bind.
    /// </summary>
    Task<bool> KnowsRemoteClosureAsync(CancellationToken ct) => Task.FromResult(false);

    /// <summary>Names <paramref name="graph"/> for the take of <paramref name="id"/> by <paramref name="session"/>, whose take alone binds it (FABRIC-CLOSE1o).</summary>
    Task<(bool Ok, string Message)> BindQuestAsync(string id, string graph, string session, CancellationToken ct);
}

/// <summary>
/// The bind at the take (FABRIC-CLOSE1b; §E5's cutover): a quest whose work cycle holds <c>quest.close</c> is bound to that graph when its
/// session opens, before the session can say <c>done</c>, and only where every machine of its circle and the host run a build that knows a
/// bound quest (FABRIC-CLOSE1e's compatibility).
/// </summary>
/// <remarks>
/// D173's CLOSE1b note, choice 4: the local host is asked (<c>/api/status</c>'s <c>closure</c>). A circle synced through a remote is bound
/// only by a host that judges the remote's handshake (<c>closeRemote</c>, FABRIC-CLOSE1i), whose bind refuses it, naming the machines,
/// until every machine of the workspace declared it knows a bound quest; a host before it is asked nothing there, and its quests close by
/// their done as today.
/// </remarks>
public static class ClosureBinding
{
    /// <summary>
    /// What a session's start owes its quest's closure graph (FABRIC-CLOSE1l, folding CLOSE1f and CLOSE1g): a quest bound already says
    /// `ready` (a resume, a carry-on), asking nothing; an open quest's first start names its graph for the take
    /// (<see cref="AtTakeAsync"/>), and says `ready` only where the host took that; a quest taken unbound keeps today's done. Null where no
    /// closure graph governs the quest and it is bound to none: nothing is asked, kept or said (the owner's install).
    /// </summary>
    /// <param name="session">The session opening, whose take alone binds the graph named for it (FABRIC-CLOSE1o); none names nothing.</param>
    /// <param name="run">The binding of the run the quest's work is, as the start's bind kept it before this take (FABRIC-CUTOVER1g).</param>
    public static async Task<ClosureBound?> AtStartAsync(
        IClosureBindPort port, DriverConfig config, QuestView quest, string workspace, bool remote, string? home, CancellationToken ct,
        string? session = null, WorkflowRunBinding? run = null)
    {
        if (quest.Closure is { Length: > 0 } bound) return new(true, $"#{quest.Id} closes through its graph `{bound}`: its session says `ready`.");
        return quest.Status == "Open" ? await AtTakeAsync(port, config, quest, workspace, remote, ct, home, session, run).ConfigureAwait(false) : null;
    }

    /// <summary>The bind for a quest just taken, or null where no closure graph governs it: nothing is asked, nothing changes.</summary>
    /// <param name="run">
    /// The binding of the run the quest's work is (FABRIC-CUTOVER1g, D190's binding moment; the cutover design §8): the take names the graph
    /// its run is bound to. Under Current, or with no binding (a quest whose id names no run), Current's work cycle, as before; under a named
    /// version, none, so a schema-1 run's quest closes by its done as schema 1 reads it. A schema-2 run's take names its version's graph where
    /// that holds <c>quest.close</c> from FABRIC-CUTOVER1h, and none until then.
    /// </param>
    public static async Task<ClosureBound?> AtTakeAsync(
        IClosureBindPort port, DriverConfig config, QuestView quest, string workspace, bool remote, CancellationToken ct, string? keepIn = null,
        string? session = null, WorkflowRunBinding? run = null)
    {
        if (run is { IsCurrent: false }) return null;
        var home = NodeGraphHomes.Work(config, quest.To, workspace, WorkUnitKind.Quest);
        if (!QuestClosing.Holds(home.Graph)) return null;
        const string AsToday = "so it closes by its done, as today.";
        if (!home.Readable)
        {
            return new(false, $"#{quest.Id} is not bound to its closure graph, which does not hold ({home.Problems[0].Code}), {AsToday}");
        }

        var digest = NodeGraphs.Digest(home.Graph!);
        bool knows;
        try
        {
            // FABRIC-CLOSE1i: through a remote, the host judges the workspace's handshake in its bind, which names a machine not declared.
            knows = !remote || await port.KnowsRemoteClosureAsync(ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException or IOException)
        {
            knows = false;
        }

        if (!knows)
        {
            return new(false, $"#{quest.Id} is not bound to its closure graph `{digest}`: its workspace `{workspace}` syncs through a remote, and "
                              + $"the service here cannot tell whether every machine sharing it runs a build that knows a bound quest (it is older "
                              + $"than this driver), {AsToday}");
        }

        try
        {
            knows = await port.KnowsClosureAsync(ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException or IOException)
        {
            knows = false;
        }

        if (!knows)
        {
            return new(false, $"#{quest.Id} is not bound to its closure graph `{digest}`: the service here does not know a bound quest (it is older "
                              + $"than this driver), {AsToday}");
        }

        // FABRIC-CLOSE1o: named for one session, whose take alone binds it; a start that names none would bind nobody's take.
        if (string.IsNullOrWhiteSpace(session))
        {
            return new(false, $"#{quest.Id} is not bound to its closure graph `{digest}`: no session was named for its take, {AsToday}");
        }

        // FABRIC-CLOSE1l: kept by its digest before it is named, so the quest is closed by this graph whatever the choices say later.
        if (keepIn is not null && home.Cycle is { } json) ClosureGraphs.Keep(keepIn, digest, json);
        bool ok;
        string message;
        try
        {
            (ok, message) = await port.BindQuestAsync(quest.Id, digest, session, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException or IOException
                                      || (error is TaskCanceledException && !ct.IsCancellationRequested))
        {
            // Unanswered: its session is told `done`. Were the graph named after all, the take binds it, and the service refuses that done
            // naming `ready`, so nothing closes past the graph.
            (ok, message) = (false, $"the service did not answer ({error.Message}).");
        }

        return ok
            ? new(true, $"#{quest.Id} closes through its graph `{digest}`: its session says `ready`, and the graph writes its Done at the work's end.")
            : new(false, $"#{quest.Id} is not bound to its closure graph `{digest}`: {message} It closes by its done, as today.");
    }

    /// <summary>
    /// What the loop's line says at a session's end whose start named its quest's graph for the take, where that take left the quest
    /// unbound (FABRIC-CLOSE1o: a bind expired, a remote since, another session's take): said once, at that end, since a later start reads
    /// the quest taken and unbound and names nothing. Null everywhere else.
    /// </summary>
    /// <param name="why">
    /// What the take's record says it made of the graph (CLOSETAKE1, FABRIC-CLOSE1o-ii: <c>expired</c>, <c>replaced</c> or <c>remote</c>); the
    /// line names it. None where the record says nothing (another session's take, a host from before the field): the line leaves it to the answer.
    /// </param>
    public static string? UnboundAtEnd(ClosureBound? named, QuestView? after, string? why = null) =>
        named is { Bound: true } && after is { Status: "Taken" or "Done", Closure: null or "" }
            ? $" #{after.Id}'s closure graph was named for its take, and its take did not bind it ("
              + (why switch
              {
                  "expired" => "its naming expired before the take came",
                  "replaced" => "the quest was replaced since it was named",
                  "remote" => "its workspace syncs through a remote, and not every machine sharing it has declared it knows a bound quest",
                  _ => "its answer said why",
              }) + "), so it closes by its done."
            : null;
}
