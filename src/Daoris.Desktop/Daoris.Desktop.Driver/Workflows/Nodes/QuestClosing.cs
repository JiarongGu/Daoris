using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

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
