using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>
/// The trees half of <see cref="HelpFacts"/>: each repository's part of what the ended sessions' trees hold, its
/// judgement and its reading, and the door that reads the Sessions list's look with those trees merged in. It owns no state
/// but the one wait for the parts' first judgement of each facts' life; the topic names and the parts live on <see cref="HelpFacts"/>.
/// </summary>
internal static class HelpTreeFacts
{
    /// <summary>How many of a part's trees are read at once: each start still passes the background's places.</summary>
    public const int AtOnce = 4;

    /// <summary>
    /// The longest a door reading the trees waits for the parts' first judgement, once in the facts' life: a part's walk,
    /// two at once, against the 24.9 s the whole walk took on a loaded machine.
    /// </summary>
    public static readonly TimeSpan First = TimeSpan.FromSeconds(10);

    /// <summary>The one wait for the parts' first judgement of each facts' life, which every door then awaits.</summary>
    private static readonly ConditionalWeakTable<MachineFacts, Task> FirstTrees = new();

    /// <summary>
    /// What <paramref name="subject"/>'s part of the trees reads: the look, and whose sessions' trees are its, a
    /// repository's by its records' name, the rest's those of every repository with no checkout here. Null for a repository the
    /// answers do not name with a checkout here.
    /// </summary>
    public static HelpTreesPart? PartOf(HelpMachineWhole whole, IKnowledgeSessionLook look, string subject)
    {
        var checkouts = HelpFacts.Checkouts(whole);
        if (subject == HelpFacts.NoCheckout) return new HelpTreesPart(null, checkouts, look);
        return whole.Snapshot.Repositories.FirstOrDefault(known => string.Equals(known.Repository, subject, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(known.Root)) is { } checkout
            ? new HelpTreesPart(checkout.Repository, checkouts, look)
            : null;
    }

    /// <summary>
    /// Which trees <paramref name="part"/> judges from <paramref name="look"/>: those its sessions would put to review or offer to
    /// land, whether or not this home holds them, which the judge asks. A part is judged again where they move.
    /// </summary>
    public static string Basis(HelpMachineWhole whole, IKnowledgeSessionLook look, string part) => PartOf(whole, look, part) is { } of
        ? string.Join('\n', KnowledgeWorkOrders.Current.TreesToJudge(look, _ => true, of.Sessions).Select(KnowledgeWorkOrders.Current.Normal).Order(StringComparer.OrdinalIgnoreCase))
        : "";

    /// <summary>
    /// One part of the trees through <paramref name="trees"/>, from the look and the service's answers as the facts hold them: null
    /// before either, or where the answers no longer name the repository with a checkout here, so it keeps no fact.
    /// </summary>
    public static async Task<object?> JudgeAsync(
        MachineFacts facts, string subject, Func<HelpTreesPart, CancellationToken, Task<HelpTrees?>> trees, CancellationToken ct)
    {
        var current = facts.Current;
        if (current.Value<HelpMachineWhole>(HelpFacts.Machine, HelpFacts.Whole) is not { } whole
            || current.Value<IKnowledgeSessionLook>(HelpFacts.Sessions, HelpFacts.Whole) is not { } look
            || PartOf(whole, look, subject) is not { } part)
        {
            return null;
        }

        return await trees(part, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// One part of the trees read from <paramref name="home"/>, as the module's judge reads it: each of the part's
    /// sessions' trees that would be put to review or offered to land judged once with git, and no other tree.
    /// <paramref name="times"/> takes the walk's time and how many trees it asked, where a caller says them.
    /// </summary>
    /// <remarks>
    /// A tree's status and proof are kept while their inputs stand (<see cref="TreeReadMemo"/>);
    /// <see cref="AtOnce"/> changed trees at once. Only a tree whose folder is still here, and not one moved aside under
    /// the trees home's <c>.tidied</c>, is walked or counted (<see cref="TreeFolders.Walkable"/>); the rest are asked, holding nothing.
    /// </remarks>
    public static async Task<HelpTrees> ReadAsync(string home, HelpTreesPart part, CancellationToken ct, LookTimes? times = null)
    {
        return await KnowledgeWorkplace.Current.ReadTreesAsync(home, part, ct, times).ConfigureAwait(false);
    }

    /// <summary>
    /// One part of the trees through <paramref name="judge"/>, a stand-in where a test hands one: each tree <paramref name="held"/> takes
    /// that the part's sessions would put to review or offer to land, judged once, with the trees it asked.
    /// </summary>
    /// <param name="atOnce">How many trees are judged at once; one after another unless a caller says more.</param>
    /// <param name="walkable">
    /// Which of the asked trees are judged and counted: every one unless a caller says; the rest are asked, holding nothing.
    /// </param>
    public static async Task<HelpTrees> ReadAsync(
        HelpTreesPart part, Func<string, bool> held, Func<string, CancellationToken, Task<TreeWork?>> judge, CancellationToken ct,
        LookTimes? times = null, int atOnce = 1, Func<string, bool>? walkable = null)
    {
        // The part's own trees alone: a look kept with every tree judged carries none of them in.
        var look = part.Look.WithTrees(HelpTrees.None.Trees);
        times ??= new LookTimes();
        var asked = KnowledgeWorkOrders.Current.TreesToJudge(look, held, part.Sessions);
        var walked = walkable is null ? held : tree => held(tree) && walkable(tree);
        times.Trees = walkable is null ? asked.Count : asked.Count(walkable);
        var judged = await times.TimeAsync("trees", () => KnowledgeWorkOrders.Current.JudgeAsync(look, walked, judge, part.Sessions, ct, atOnce)).ConfigureAwait(false);
        return new HelpTrees(judged.Trees) { Asked = asked.Select(KnowledgeWorkOrders.Current.Normal).ToHashSet(StringComparer.OrdinalIgnoreCase) };
    }

    /// <summary>
    /// <paramref name="look"/>, read by a door without its trees (the Sessions list's), with each tree it would put
    /// to review or offer to land as the parts judged it (<see cref="Judged"/>), so a window's tick, and every other window's, starts
    /// no git. Never a tree's state that is never judged: one no part asked is walked here through <paramref name="walk"/>, as the door
    /// walked every tree before, and the facts' look is judged again at once where it is behind the door's, so its part asks it next.
    /// </summary>
    /// <remarks>
    /// <para><b>Waited for once.</b> Before the parts' first judgement every tree is one no part asked: the door waits for it, at most
    /// <paramref name="first"/>, once in the facts' life, each door then asking awaiting the one wait, so the windows opening with the
    /// shell do not each walk every tree beside the parts. Past it, what is still not judged is walked, <see cref="AtOnce"/> at once; a
    /// repository registered later, whose part is judged as the answers name it, is walked until it is, with no wait.</para>
    ///
    /// <para><b>As fresh as the parts.</b> A part is judged on its repository's <c>refs</c> wake, its rest holding a burst to one judgement
    /// each half minute (<see cref="HelpFacts.TreesRules"/>); at once as the look lands with its trees moved; on the configuration; and at its
    /// deadline. Where the facts never started (a terminal, a test), every tree is walked, as before.</para>
    /// </remarks>
    /// <param name="held">Whether a path is a tree this home opened (<see cref="SessionTrees.Holds"/>), as the parts read it.</param>
    /// <param name="walk"><see cref="SessionTrees.WorkAsync"/>, git's walk of one tree; a test hands its own.</param>
    /// <param name="only">The sessions asked about: their trees alone.</param>
    /// <param name="first">The wait for the parts' first judgement; <see cref="First"/> unless a test says another.</param>
    public static async Task<IKnowledgeSessionLook> WithTreesAsync(
        MachineFacts facts, IKnowledgeSessionLook look, Func<string, bool> held, Func<string, CancellationToken, Task<TreeWork?>> walk,
        IReadOnlyCollection<string>? only = null, TimeSpan? first = null, CancellationToken ct = default)
    {
        var asked = KnowledgeWorkOrders.Current.TreesToJudge(look, held, only);
        var read = Judged(facts.Current, asked);
        if (read.NotJudged.Count > 0 && facts.Started)
        {
            var bound = first ?? First;
            await FirstTrees.GetValue(facts, started => FirstAsync(started, bound)).WaitAsync(ct).ConfigureAwait(false);
            read = Judged(facts.Current, asked);
            // A session ended after the facts' look: their look judged again at once, which reads the door's kept a moment ago.
            if (read.NotJudged.Count > 0 && Behind(facts.Current, held, read.NotJudged)) _ = facts.JudgedAsync(HelpFacts.Sessions, HelpFacts.Whole, CancellationToken.None);
        }

        var trees = new Dictionary<string, TreeWork>(look.Trees, StringComparer.OrdinalIgnoreCase);
        foreach (var (tree, work) in read.Trees) trees[tree] = work;
        // A few at once, as a part reads its own: one after another, each walk's git queued behind the first look's held a
        // start's Sessions list for the sum of them.
        var walked = new TreeWork?[read.NotJudged.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, read.NotJudged.Count), new ParallelOptions { MaxDegreeOfParallelism = AtOnce, CancellationToken = ct },
            async (at, token) => walked[at] = await walk(read.NotJudged[at], token).ConfigureAwait(false)).ConfigureAwait(false);
        for (var at = 0; at < walked.Length; at++)
        {
            if (walked[at] is { } work) trees[KnowledgeWorkOrders.Current.Normal(read.NotJudged[at])] = work;
        }

        return look.WithTrees(trees);
    }

    /// <summary>
    /// Each of <paramref name="asked"/> as the parts judged it: the work of a tree a part asked, keyed by
    /// <see cref="KnowledgeWorkOrders.Current.Normal"/>, none for one it asked and git could not speak for, and the trees no part asked.
    /// </summary>
    public static HelpTreesJudged Judged(MachineFactsSnapshot facts, IReadOnlyList<string> asked)
    {
        var parts = facts.Subjects(HelpFacts.Trees).Select(subject => facts.Value<HelpTrees>(HelpFacts.Trees, subject)).OfType<HelpTrees>().ToList();
        var trees = new Dictionary<string, TreeWork>(StringComparer.OrdinalIgnoreCase);
        var notJudged = new List<string>();
        foreach (var tree in asked)
        {
            var key = KnowledgeWorkOrders.Current.Normal(tree);
            if (parts.FirstOrDefault(part => part.Asked.Contains(key)) is not { } part) notJudged.Add(tree);
            else if (part.Trees.TryGetValue(key, out var work)) trees[key] = work;
        }

        return new HelpTreesJudged(trees, notJudged);
    }

    /// <summary>Until the look and every part the answers name were judged, or <paramref name="bound"/> passed.</summary>
    private static async Task FirstAsync(MachineFacts facts, TimeSpan bound)
    {
        var read = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Landed(MachineFactsSnapshot snapshot)
        {
            if (Read(snapshot)) read.TrySetResult();
        }

        facts.Changed += Landed;
        try
        {
            Landed(facts.Current);
            await read.Task.WaitAsync(bound).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Not read within the wait: each door walks what is still not judged, as before, and none waits again.
        }
        finally
        {
            facts.Changed -= Landed;
        }
    }

    /// <summary>Whether the look and every part of the trees the service's answers name were judged, as <see cref="HelpFacts.Read"/> reads them.</summary>
    private static bool Read(MachineFactsSnapshot facts) =>
        facts.Value<HelpMachineWhole>(HelpFacts.Machine, HelpFacts.Whole) is { } whole && HelpFacts.SessionsOf(facts, whole) is not null;

    /// <summary>
    /// Whether the facts' look is behind a door's: every part judged, and one of <paramref name="notJudged"/> a tree it would not
    /// judge. One it names is its part's to ask, which the look landing asked at once.
    /// </summary>
    private static bool Behind(MachineFactsSnapshot facts, Func<string, bool> held, IReadOnlyList<string> notJudged)
    {
        if (!Read(facts) || facts.Value<IKnowledgeSessionLook>(HelpFacts.Sessions, HelpFacts.Whole) is not { } look) return false;
        var named = KnowledgeWorkOrders.Current.TreesToJudge(look, held).Select(KnowledgeWorkOrders.Current.Normal).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return notJudged.Any(tree => !named.Contains(KnowledgeWorkOrders.Current.Normal(tree)));
    }
}
