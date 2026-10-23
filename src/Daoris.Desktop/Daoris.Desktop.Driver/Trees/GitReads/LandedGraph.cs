namespace Daoris.Driver;

/// <summary>
/// A landed or gone session's review as nodes of Review's graph (GITAPP1a-ii, the git-app design P1.2's last row): the reads
/// <see cref="LandedReviews.LandedReviewAsync"/> makes, the same commands in the same checkout, each started once what it needs has
/// answered. The range read and the content proof run programs (a textconv, a diff) and are Π; the ref reads run beside each other.
/// </summary>
/// <remarks>
/// 🔴 <b>Reads, and never writes</b>, as <see cref="LandedReviews"/> says; and <b>the checkout is proven first</b>: the branch's tip is
/// read beside that proof, speculatively, and used only where the checkout is a repository's top.
/// </remarks>
internal sealed class LandedGraph(ReviewGraph graph, TreeContext context, string root, LandedBranch entry, bool speculative, CancellationToken ct)
{
    private readonly List<ReviewNode> _made = [];

    /// <summary>The nodes it made: the standing review, asked before D decides its use, marks them used or wasted as one.</summary>
    internal IReadOnlyList<ReviewNode> Made => _made;

    /// <summary>
    /// <see cref="LandedReviews.LandedReviewAsync"/>'s answer, read as a graph; <paramref name="speculative"/> where its use is decided
    /// later (the standing review beside D).
    /// </summary>
    internal static async Task<(LandedReview Review, IReadOnlyList<ReviewNode> Made)> ReadAsync(
        ReviewGraph graph, TreeContext context, string? root, LandedBranch entry, bool changes, bool speculative, CancellationToken ct)
    {
        if (root is null) return (new(entry, LandedState.NoCheckout), []);
        var reads = new LandedGraph(graph, context, root, entry, speculative, ct);
        return (await reads.ReviewAsync(changes).ConfigureAwait(false), reads.Made);
    }

    private (ReviewNode Node, Task<T> Answer) Run<T>(string name, ReviewNode?[] needs, Func<Task<T>> read, bool ahead = false)
    {
        var run = graph.Run(name, needs, read, speculative || ahead, GitBudget.Landed);
        lock (_made) _made.Add(run.Node);
        return run;
    }

    private Task<(int Code, string Stdout, string Stderr)> Git(IReadOnlyList<string> arguments) => WorkingTree.GitAsync(root, arguments, ct);

    private async Task<LandedReview> ReviewAsync(bool changes)
    {
        var valid = WorkingTree.IsCommitId(entry.Tip) && BranchName.IsValid(entry.Branch);
        var top = Run("LT", [], () => WorkingTree.IsTopLevelAsync(root, ct));
        // The tip, beside the checkout's proof: today's next read wherever the checkout is a top and the branch is not recorded gone.
        var tip = valid && entry.GoneAt is null
            ? Run("Ltip", [], () => Git(["rev-parse", "--verify", "--quiet", $"refs/heads/{entry.Branch}^{{commit}}"]), ahead: true)
            : default;
        if (!await top.Answer.ConfigureAwait(false))
        {
            tip.Node?.Use(false);
            return new(entry, LandedState.NoCheckout);
        }

        // The record's words become git's arguments, so each must be what it says it is.
        if (!valid) return new(entry, LandedState.NotOurs);
        if (entry.GoneAt is not null) return new(entry, LandedState.Gone, Reads: await ReadsAsync(top.Node).ConfigureAwait(false));

        tip.Node!.Use(true);
        var (tipCode, tipOut, _) = await tip.Answer.ConfigureAwait(false);
        if (tipCode != 0) return new(entry, LandedState.Gone, Reads: await ReadsAsync(tip.Node).ConfigureAwait(false));

        var at = tipOut.Trim();
        var ours = Run("Lours", [tip.Node], () => Git(["merge-base", "--is-ancestor", entry.Tip, at]));
        if ((await ours.Answer.ConfigureAwait(false)).Code != 0) return new(entry, LandedState.NotOurs);
        if (!changes) return new(entry, LandedState.Standing);

        var (from, fromNode) = await FromAsync(at, tip.Node).ConfigureAwait(false);
        if (from is null)
        {
            return new(entry, LandedState.Standing, Detail: $"git found no point where `{entry.Branch}` left its line, so there is no range to read.");
        }

        // DiffBetweenAsync's read: its top is the checkout's, proven above; the range runs textconv, so it is Π.
        WorkingTree.TreeDiff? diff = null;
        if (WorkingTree.IsCommitId(from) && WorkingTree.IsCommitId(at))
        {
            diff = await graph.ProgramAsync(
                "Lrange", [top.Node, fromNode],
                () => WorkingTree.RangeAsync(root, from, $"{from}..{at}", WorkingTree.BetweenRest(from, at), WorkingTree.ReadGitAsync, ct),
                GitBudget.Landed).ConfigureAwait(false);
        }

        return diff is null
            ? new(entry, LandedState.Standing, Detail: $"git could not read `{entry.Branch}`'s changes from {TextBounds.ShortCommit(from, 8)} in the repository's checkout.")
            : new(entry, LandedState.Standing, diff);
    }

    /// <summary>
    /// <c>LandedFromAsync</c>'s order (<see cref="ReviewChoices.LandedFrom"/>): the recorded start while the branch holds it, its full
    /// id read beside that question; else where the branch leaves the line.
    /// </summary>
    private async Task<(string? From, ReviewNode? Node)> FromAsync(string tip, ReviewNode tipNode)
    {
        if (entry.From is { } recorded && WorkingTree.IsCommitId(recorded))
        {
            var held = Run("Lfrom", [tipNode], () => Git(["merge-base", "--is-ancestor", recorded, tip]));
            var full = Run("Lfull", [], () => Git(["rev-parse", "--verify", "--quiet", $"{recorded}^{{commit}}"]), ahead: true);
            var heldCode = (await held.Answer.ConfigureAwait(false)).Code;
            full.Node.Use(heldCode == 0);
            if (ReviewChoices.LandedFrom(heldCode, (await full.Answer.ConfigureAwait(false)).Stdout) is { } commit) return (commit, full.Node);
        }

        var (line, lineNode) = await LineAsync([tipNode]).ConfigureAwait(false);
        if (line is null) return (null, lineNode);
        var (against, againstNode) = await ComparableAsync(line, [lineNode ?? tipNode]).ConfigureAwait(false);
        if (against is null) return (null, againstNode);
        var fork = Run("Lfork", [againstNode, tipNode], () => Git(["merge-base", against, tip]));
        var (code, found, _) = await fork.Answer.ConfigureAwait(false);
        return (code == 0 && found.Trim() is { Length: > 0 } commit2 ? commit2 : null, fork.Node);
    }

    /// <summary>The line in this checkout, as <see cref="TreeContext.LineAsync"/> names it, else the landing's record of it.</summary>
    private async Task<(string? Line, ReviewNode? Node)> LineAsync(ReviewNode?[] needs)
    {
        var made = graph.Nodes.Count;
        var (line, node) = await ReviewGraphReads.GuessAsync(
            graph, context, root, entry.Repository, RemoteTarget.Workspace(entry.Workspace), needs, ct, GitBudget.Landed).ConfigureAwait(false);
        lock (_made) _made.AddRange(graph.Nodes.Skip(made).Where(each => each.Budget == GitBudget.Landed && each.Name.StartsWith('G')));
        return (line ?? entry.Line, node);
    }

    /// <summary>
    /// <c>ComparableAsync</c>'s order: the line here, else origin's, where git has it; both asked together (origin's speculatively).
    /// </summary>
    private async Task<(string? Reference, ReviewNode Node)> ComparableAsync(string line, ReviewNode?[] needs)
    {
        var refs = ReviewRanges.LineRefs(line);
        var here = Run("Lc1", needs, () => Git(["rev-parse", "--verify", "--quiet", refs[0] + "^{commit}"]));
        var origin = Run("Lc2", needs, () => Git(["rev-parse", "--verify", "--quiet", refs[1] + "^{commit}"]), ahead: true);
        if ((await here.Answer.ConfigureAwait(false)).Code == 0)
        {
            origin.Node.Use(false);
            return (refs[0], here.Node);
        }

        origin.Node.Use(true);
        return ((await origin.Answer.ConfigureAwait(false)).Code == 0 ? refs[1] : null, origin.Node);
    }

    /// <summary><c>ReadsAsync</c>: whether a gone branch's work reads on the line, the content proof (Π) on its forms.</summary>
    private async Task<LandedReads> ReadsAsync(ReviewNode need)
    {
        var held = Run("Lcommit", [need], () => Git(["cat-file", "-e", $"{entry.Tip}^{{commit}}"]));
        if ((await held.Answer.ConfigureAwait(false)).Code != 0) return new(LandedReads.CommitsGone, null, [], null);

        var (line, lineNode) = await LineAsync([held.Node]).ConfigureAwait(false);
        var forms = new List<LandedProofs.LineForm>();
        ReviewNode?[] proofNeeds = [held.Node, lineNode];
        if (line is not null)
        {
            // Both forms are asked today whatever the first answers, so both run together and neither is speculative.
            var asked = ReviewRanges.LineRefs(line)
                .Zip([line, $"origin/{line}"], (reference, name) => (reference, name, read: Run(
                    "Lform", [held.Node, lineNode], () => Git(["rev-parse", "--verify", "--quiet", $"{reference}^{{commit}}"]))))
                .ToArray();
            foreach (var (reference, name, read) in asked)
            {
                var (code, sha, _) = await read.Answer.ConfigureAwait(false);
                if (LandedProofs.FormOf(name, reference, code, sha) is { } form) forms.Add(form);
            }

            proofNeeds = [held.Node, .. asked.Select(each => each.read.Node)];
        }

        var proof = await graph.ProgramAsync(
            "Lprove", proofNeeds, () => LandedProofs.ProveAsync(root, entry.Tip, line, forms, ct), GitBudget.Landed).ConfigureAwait(false);
        return new(proof.Kind, proof.Where, proof.Kind == LandedKind.Differs ? proof.Files : [], proof.Detail);
    }
}
