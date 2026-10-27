using System.Text;

namespace Daoris.Driver;

/// <summary>
/// Review's read of a session as a graph of today's reads (GITAPP1a-ii, D202 part one, the git-app design P1.1-P1.4): the same
/// commands with the same arguments as today's chain (<see cref="SessionReviews"/>' oracle), the same choices by today's code
/// (<see cref="ReviewChoices"/>), and the same answer, field by field; only when each start is made changes.
/// </summary>
/// <remarks>
/// <para>🔴 <b>Π, the reads that can run a configured program, are one chain in today's order</b>, one at a time and never
/// speculative: W → S → U₁ … Uᵣ → M → D → the landed review's range → the offer, each where today's chain would ask it. A textconv,
/// a clean filter or a merge driver is a process with state git does not see, so asking it earlier, beside another or needlessly could
/// change an answer (Codex R4-1). The rest (T, P, K, E, K′, G1-G3, B1, B2, N↑, N↓, I, H, the landed review's ref reads) read refs and
/// the commit graph only, and run at once and ahead; a speculative one only where its command is the one today's chain runs if the
/// condition goes that way, its answer used only there.</para>
///
/// <para><b>Nothing kept</b>: no cache, no refs observation, no watch. Every open runs its graph whole (part two's keeping, read
/// later). The route's guards and its fallbacks' precedence are the chain's, in the same order.</para>
/// </remarks>
internal static class ReviewGraphReads
{
    /// <summary>
    /// The untracked window (P1.3): files started ahead of the fold. 1 in part one, since each untracked read runs the clean filter and
    /// textconv (Π); wider is part two's, once its context shows no program applies to the untracked paths.
    /// </summary>
    internal const int Window = 1;

    /// <summary>The review of <paramref name="session"/>, as <see cref="SessionReviews"/>' chain answers it, read as a graph.</summary>
    internal static async Task<SessionReview?> ReadAsync(
        ReviewGraph graph, string session, string? tree, string? baseCommit, SessionTrees trees,
        Func<string, CancellationToken, Task<string?>> checkoutOf, CancellationToken ct, int window = Window)
    {
        try
        {
            return await ReadCoreAsync(graph, session, tree, baseCommit, trees, checkoutOf, ct, window).ConfigureAwait(false);
        }
        finally
        {
            // Nothing outlives the open: every node started has answered before the answer, or the failure, is given.
            await graph.SettledAsync().ConfigureAwait(false);
        }
    }

    private static async Task<SessionReview?> ReadCoreAsync(
        ReviewGraph graph, string session, string? tree, string? baseCommit, SessionTrees trees,
        Func<string, CancellationToken, Task<string?>> checkoutOf, CancellationToken ct, int window)
    {
        var context = trees.Context;
        // What decides the path and costs no git: the landing, the tree's folder, whose tree it is, the record's base.
        var landing = trees.Recorded.Landing(session);
        var gone = TreeFolders.TreeGone(tree);
        var own = !gone && SessionReviews.OwnTree(trees, tree);
        var ranged = !gone && !string.IsNullOrWhiteSpace(baseCommit);
        var checkout = landing is null ? null : new Lazy<Task<string?>>(() => checkoutOf(landing.Repository, ct));

        // T: asked once, answering both W's (WorkingTree.cs:328) and D's (:302).
        var top = !gone && (own || ranged)
            ? graph.Run("T", [], () => WorkingTree.IsTopLevelAsync(tree!, WorkingTree.ReadGitAsync, ct))
            : default;
        // H: speculative from the start, used where D answered or on the dirty-only path; its failure thrown only there.
        var head = !gone && (own || ranged)
            ? graph.Run("H", [], () => WorkingTree.GitAsync(tree!, WorkingTree.BranchAsk, ct), speculative: true)
            : default;
        // The range's reads outside Π, under way while the uncommitted part is read.
        var range = ranged && context.Holds(tree!) && Directory.Exists(tree) ? new RangeReads(graph, context, tree!, baseCommit!, ct) : null;
        // The standing landing's review (changes: false) reads refs only: speculative, used where D answers.
        var standing = ranged && landing is not null ? graph.Track(StandingAsync(graph, context, checkout!, landing, ct)) : null;

        // Π, in today's order: the uncommitted part first (REVIEWDIRTY1-a), never for a tree that is not this home's own (D49 §3).
        var dirty = own ? await UncommittedAsync(graph, tree!, top, window, ct).ConfigureAwait(false) : null;

        if (ranged)
        {
            var (from, against, behind, sigma) = range is null
                // A tree outside this home keeps the record's word, with no line and no start (ReviewRanges.cs:37).
                ? (baseCommit!, null, Task.FromResult<LineBehind?>(null), Array.Empty<ReviewNode>())
                : await range.AnswerAsync().ConfigureAwait(false);
            var reads = new ReviewRange(from, null, against).Reads;
            WorkingTree.TreeDiff? diff = null;
            if (!string.IsNullOrWhiteSpace(reads) && await top.Answer.ConfigureAwait(false))
            {
                diff = await graph.ProgramAsync(
                    "D", [top.Node, .. sigma],
                    () => WorkingTree.RangeAsync(tree!, reads, $"{reads}..HEAD", WorkingTree.CommittedRest, WorkingTree.ReadGitAsync, ct))
                    .ConfigureAwait(false);
            }

            if (diff is not null)
            {
                var answer = new ReviewRange(from, await behind.ConfigureAwait(false), against);
                diff = diff with { Base = answer.From };
                LandedReview? held = null;
                if (standing is not null)
                {
                    var (review, made) = await standing.ConfigureAwait(false);
                    foreach (var node in made) node.Use(true);
                    held = review;
                }

                head.Node!.Use(true);
                var (code, stdout, stderr) = await head.Answer.ConfigureAwait(false);
                var branch = ReviewChoices.Branch(code, stdout, stderr);
                LandOffer? offer = null;
                if (landing is null && answer.Behind is { Ahead: > 0 } && own)
                {
                    offer = await graph.ProgramAsync(
                        "Offer", [head.Node, range?.Counts],
                        async () => await trees.WorkAsync(tree!, ct).ConfigureAwait(false) is { } work ? LandOffer.Of(tree!, work) : null,
                        GitBudget.Offer).ConfigureAwait(false);
                }

                return new(diff, FromBranch: false, held, TreeGone: false, dirty, answer.Behind, branch, offer);
            }
        }

        if (standing is not null)
        {
            foreach (var node in (await standing.ConfigureAwait(false)).Made) node.Use(false);
        }

        if (landing is not null)
        {
            var (review, _) = await LandedGraph.ReadAsync(
                graph, context, await checkout!.Value.ConfigureAwait(false), landing, changes: true, speculative: false, ct).ConfigureAwait(false);
            head.Node?.Use(false);
            return new(review.Changes, FromBranch: true, review, gone);
        }

        if (dirty is { Files.Count: > 0 })
        {
            head.Node!.Use(true);
            var (code, stdout, stderr) = await head.Answer.ConfigureAwait(false);
            return new(new WorkingTree.TreeDiff([], null, ""), FromBranch: false, null, TreeGone: false, dirty, TreeBranch: ReviewChoices.Branch(code, stdout, stderr));
        }

        head.Node?.Use(false);
        return null;
    }

    /// <summary>The standing landing's review (changes: false), all of it speculative: used where D answers, else wasted.</summary>
    private static async Task<(LandedReview Review, IReadOnlyList<ReviewNode> Made)> StandingAsync(
        ReviewGraph graph, TreeContext context, Lazy<Task<string?>> checkout, LandedBranch landing, CancellationToken ct) =>
        await LandedGraph.ReadAsync(graph, context, await checkout.Value.ConfigureAwait(false), landing, changes: false, speculative: true, ct)
            .ConfigureAwait(false);

    /// <summary>
    /// W → S → the untracked window (<c>UncommittedDiffAsync</c>'s reads, WorkingTree.cs:326-370), each in Π: null where T says no top,
    /// W fails or S fails, and then no window opens.
    /// </summary>
    private static async Task<WorkingTree.TreeDiff?> UncommittedAsync(
        ReviewGraph graph, string tree, (ReviewNode Node, Task<bool> Answer) top, int window, CancellationToken ct)
    {
        if (!await top.Answer.ConfigureAwait(false)) return null;
        var tracked = await graph.ProgramAsync(
            "W", [top.Node],
            () => WorkingTree.RangeAsync(tree, "HEAD", "HEAD", WorkingTree.UncommittedRest, WorkingTree.ReadGitAsync, ct)).ConfigureAwait(false);
        if (tracked is null) return null;

        var (code, status) = await graph.ProgramAsync("S", [], async () =>
        {
            var text = new StringBuilder();
            var exit = await WorkingTree.ReadGitAsync(tree, WorkingTree.UncommittedStatus, piece => { text.Append(piece.Span); return true; }, ct)
                .ConfigureAwait(false);
            return (exit, text.ToString());
        }).ConfigureAwait(false);
        if (code != 0) return null;

        var fold = new UntrackedFold(tracked);
        await UntrackedWindow.FoldAsync(
            ReviewChoices.UntrackedPaths(status), fold, window,
            // At w = 1 each read is Π, after the one before; a wider window (part two's) reads beside each other after S.
            (path, token) => window <= 1
                ? graph.ProgramAsync("U", [], () => WorkingTree.UntrackedReadAsync(tree, path, WorkingTree.ReadGitAsync, token), GitBudget.Untracked)
                : graph.Run("U", [graph.Nodes.Last(node => node.Name == "S")], () => WorkingTree.UntrackedReadAsync(tree, path, WorkingTree.ReadGitAsync, token),
                    budget: GitBudget.Untracked).Answer,
            ct).ConfigureAwait(false);
        return fold.End(WorkingTree.UncommittedRest);
    }

    /// <summary>
    /// The range's reads outside Π (<c>ReviewRangeAsync</c>'s, ReviewRanges.cs:35-82), started at once: K, E and K′ for the record's
    /// base; P, the line (set, or git's guess G1-G3), B1 and B2; N↑ and N↓ beside B; I where two candidates are distinct. M, which
    /// runs a merge driver, is asked by the Π chain (<see cref="AnswerAsync"/>).
    /// </summary>
    private sealed class RangeReads
    {
        private readonly ReviewGraph _graph;
        private readonly string _full;
        private readonly string _recorded;
        private readonly CancellationToken _ct;
        private readonly Task<(string? Record, ReviewNode?[] Nodes)> _record;
        private readonly Task<(string? Line, ReviewNode? Node)> _line;
        private readonly Task<(string? Here, string? Origin, List<string> Known, ReviewNode?[] Nodes)> _bases;
        private readonly Task<LineBehind?> _behind;
        private readonly TaskCompletionSource<ReviewNode?> _counts = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal RangeReads(ReviewGraph graph, TreeContext context, string tree, string recorded, CancellationToken ct)
        {
            _graph = graph;
            _full = Path.GetFullPath(tree);
            _recorded = recorded;
            _ct = ct;
            var (workspace, repository) = context.OwnerOf(_full);
            _record = graph.Track(RecordAsync(context, repository));
            _line = graph.Track(LineAsync(context, repository, workspace));
            var bases = new TaskCompletionSource<(string? Here, string? Origin, List<string> Known, ReviewNode?[] Nodes)>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _bases = bases.Task;
            _behind = graph.Track(BehindAsync(bases));
            _select = graph.Track(SelectAsync());
        }

        private readonly Task<(string From, (string Line, string Record)? Merge, Task<LineBehind?> Behind, ReviewNode[] Sigma, ReviewNode? Independent)> _select;

        /// <summary>The node whose answer said how far the tree is ahead (the offer's need), where the counts ran.</summary>
        internal ReviewNode? Counts => _counts.Task.IsCompletedSuccessfully ? _counts.Task.Result : null;

        /// <summary>K, then E and K′ beside it: the record's base by <see cref="ReviewChoices.RecordedBase"/>'s order.</summary>
        private async Task<(string? Record, ReviewNode?[] Nodes)> RecordAsync(TreeContext context, string repository)
        {
            var asksRecord = ReviewChoices.Id(_recorded);
            var held = asksRecord ? _graph.Run("K", [], () => Ancestor(_recorded)) : default;
            // E and K′ are used where K says no; where the record is no id they are not speculative at all.
            var branch = _graph.Run("E", [], () => WorkingTree.GitAsync(_full, ["rev-parse", "--abbrev-ref", "HEAD"], _ct), speculative: asksRecord);
            var grownHeld = GrownAsync(context, repository, branch, speculative: asksRecord);

            bool? recordHeld = asksRecord ? await held.Answer.ConfigureAwait(false) : null;
            if (ReviewChoices.RecordedBase(_recorded, recordHeld, null, null) is { } record)
            {
                // The record held: E and K′ are wasted, and nothing waits for them but the open's end.
                branch.Node.Use(false);
                _graph.Track(WastedAsync(grownHeld));
                return (record, [held.Node]);
            }

            branch.Node.Use(true);
            var (grown, node, grownAnswer) = await grownHeld.ConfigureAwait(false);
            node?.Use(true);
            return (ReviewChoices.RecordedBase(_recorded, recordHeld, grown, grownAnswer), [held.Node, branch.Node, node]);
        }

        private static async Task WastedAsync(Task<(string? Grown, ReviewNode? Node, bool? Held)> grownHeld) =>
            (await grownHeld.ConfigureAwait(false)).Node?.Use(false);

        private async Task<(string? Grown, ReviewNode? Node, bool? Held)> GrownAsync(
            TreeContext context, string repository, (ReviewNode Node, Task<(int Code, string Stdout, string Stderr)> Answer) branch, bool speculative)
        {
            var (_, abbrev, _) = await branch.Answer.ConfigureAwait(false);
            if (ReviewChoices.Grown(context.Grown, repository, abbrev) is not { } grown) return (null, null, null);
            var (node, held) = _graph.Run("K′", [branch.Node], () => Ancestor(grown), speculative);
            return (grown, node, await held.ConfigureAwait(false));
        }

        private async Task<bool> Ancestor(string commit) =>
            (await WorkingTree.GitAsync(_full, ["merge-base", "--is-ancestor", commit, "HEAD"], _ct).ConfigureAwait(false)).Code == 0;

        /// <summary>P, and the line: what is set, else git's guess in the checkout P names (G1-G3 together, today's order).</summary>
        private async Task<(string? Line, ReviewNode? Node)> LineAsync(TreeContext context, string repository, string workspace)
        {
            var common = _graph.Run("P", [], () => WorkingTree.GitAsync(_full, ["rev-parse", "--path-format=absolute", "--git-common-dir"], _ct));
            _common = common.Node;
            var set = CanonicalLine.Choose(context.Config(), repository, workspace, guess: null).Branch;
            var (code, commonDir, _) = await common.Answer.ConfigureAwait(false);
            if (code != 0) return (null, common.Node);
            if (set is not null) return (set, common.Node);
            var root = Path.GetDirectoryName(commonDir.Trim())!;
            var (line, node) = await GuessAsync(_graph, context, root, repository, workspace, [common.Node], _ct).ConfigureAwait(false);
            return (line, node ?? common.Node);
        }

        private ReviewNode? _common;

        /// <summary>
        /// B1 and B2 once the line is known, with N↑ and N↓ beside them on both refs, speculatively. The counts are used where both B
        /// answered; else asked again on the refs that did, as today, and the first two wasted.
        /// </summary>
        private async Task<LineBehind?> BehindAsync(TaskCompletionSource<(string? Here, string? Origin, List<string> Known, ReviewNode?[] Nodes)> bases)
        {
            try
            {
                var (line, lineNode) = await _line.ConfigureAwait(false);
                if (line is null)
                {
                    bases.TrySetResult((null, null, [], []));
                    _counts.TrySetResult(null);
                    return null;
                }

                var refs = ReviewRanges.LineRefs(line);
                var needs = new[] { lineNode };
                var here = _graph.Run("B1", needs, () => WorkingTree.GitAsync(_full, ["merge-base", "HEAD", refs[0]], _ct));
                var origin = _graph.Run("B2", needs, () => WorkingTree.GitAsync(_full, ["merge-base", "HEAD", refs[1]], _ct));
                var up = _graph.Run("N↑", needs, () => WorkingTree.GitAsync(_full, ["rev-list", "--count", .. refs, "--not", "HEAD"], _ct), speculative: true);
                var down = _graph.Run("N↓", needs, () => WorkingTree.GitAsync(_full, ["rev-list", "--count", "HEAD", "--not", .. refs], _ct), speculative: true);

                var (hereCode, hereOut, _) = await here.Answer.ConfigureAwait(false);
                var (originCode, originOut, _) = await origin.Answer.ConfigureAwait(false);
                var hereBase = ReviewChoices.Shared(hereCode, hereOut);
                var originBase = ReviewChoices.Shared(originCode, originOut);
                var known = new List<string>();
                if (hereBase is not null) known.Add(refs[0]);
                if (originBase is not null) known.Add(refs[1]);
                bases.TrySetResult((hereBase, originBase, known, [here.Node, origin.Node]));

                if (known.Count == 0)
                {
                    up.Node.Use(false);
                    down.Node.Use(false);
                    _counts.TrySetResult(null);
                    return null;
                }

                if (known.Count == refs.Length)
                {
                    up.Node.Use(true);
                    down.Node.Use(true);
                    _counts.TrySetResult(down.Node);
                    var (bc, b, _) = await up.Answer.ConfigureAwait(false);
                    var (ac, a, _) = await down.Answer.ConfigureAwait(false);
                    return ReviewChoices.Behind(line, bc, b, ac, a);
                }

                // One ref answered: today's counts are on that one alone, asked again; the pair asked on both is wasted.
                up.Node.Use(false);
                down.Node.Use(false);
                var basesNodes = new[] { here.Node, origin.Node };
                var again = _graph.Run("N↑", basesNodes, () => WorkingTree.GitAsync(_full, ["rev-list", "--count", .. known, "--not", "HEAD"], _ct));
                var againDown = _graph.Run("N↓", basesNodes, () => WorkingTree.GitAsync(_full, ["rev-list", "--count", "HEAD", "--not", .. known], _ct));
                _counts.TrySetResult(againDown.Node);
                var (bc2, b2, _) = await again.Answer.ConfigureAwait(false);
                var (ac2, a2, _) = await againDown.Answer.ConfigureAwait(false);
                return ReviewChoices.Behind(line, bc2, b2, ac2, a2);
            }
            catch (Exception error)
            {
                bases.TrySetException(error);
                _counts.TrySetResult(null);
                throw;
            }
        }

        /// <summary>
        /// Σ, then M in Π where the line's base and the record's are incomparable (REVIEWBASE3): the range as <c>ReviewRangeAsync</c>
        /// answers it, and the nodes D needs.
        /// </summary>
        internal async Task<(string From, string? Against, Task<LineBehind?> Behind, ReviewNode[] Sigma)> AnswerAsync()
        {
            var (from, merge, behind, sigma, independentNode) = await _select.ConfigureAwait(false);
            string? against = null;
            if (merge is var (lineBase, recordBase))
            {
                // REVIEWBASE3: a configured merge driver runs, so it waits its turn in Π.
                against = await _graph.ProgramAsync(
                    "M", [independentNode],
                    async () =>
                    {
                        var (code, output, _) = await WorkingTree.GitAsync(_full, ["merge-tree", "--write-tree", lineBase, recordBase], _ct)
                            .ConfigureAwait(false);
                        return ReviewChoices.Merged(code, output);
                    }).ConfigureAwait(false);
                sigma = [.. sigma, _graph.Nodes.Last(node => node.Name == "M")];
            }

            return (from, against, behind, sigma);
        }

        /// <summary>Σ outside Π, started with the rest: K, K′, B and I answered, the base chosen; the merge M would read, where incomparable.</summary>
        private async Task<(string From, (string Line, string Record)? Merge, Task<LineBehind?> Behind, ReviewNode[] Sigma, ReviewNode? Independent)> SelectAsync()
        {
            var (record, recordNodes) = await _record.ConfigureAwait(false);
            var (here, origin, _, baseNodes) = await _bases.ConfigureAwait(false);
            var (line, _) = await _line.ConfigureAwait(false);
            var candidates = ReviewBase.Candidates(here, origin, record);
            var sigma = new List<ReviewNode?>([.. recordNodes, .. baseNodes, _common]);
            IReadOnlyList<string>? maximal = null;
            ReviewNode? independentNode = null;
            if (candidates.Count >= 2)
            {
                var independent = _graph.Run(
                    "I", [.. recordNodes, .. baseNodes],
                    () => WorkingTree.GitAsync(_full, ["merge-base", "--independent", .. candidates], _ct));
                var (code, named, _) = await independent.Answer.ConfigureAwait(false);
                maximal = ReviewChoices.Independent(code, named);
                independentNode = independent.Node;
                sigma.Add(independentNode);
            }

            var (from, merge) = ReviewChoices.Choose(candidates, maximal, record, _recorded);
            // The counts are the answer's, never D's need: D starts without them.
            return (from, merge, line is null ? Task.FromResult<LineBehind?>(null) : _behind, [.. sigma.OfType<ReviewNode>()], independentNode);
        }
    }

    /// <summary>
    /// The line of a checkout as <see cref="TreeContext.LineAsync"/> answers it: what is set, else git's guess, its three questions
    /// asked together (G1, then G2 and G3 beside it, speculative) and chosen in <c>DefaultBranchAsync</c>'s order. Within a look, or
    /// under a test's seam, asked as today.
    /// </summary>
    internal static async Task<(string? Line, ReviewNode? Node)> GuessAsync(
        ReviewGraph graph, TreeContext context, string root, string repository, string workspace, ReviewNode?[] needs, CancellationToken ct,
        GitBudget budget = GitBudget.Review)
    {
        var set = CanonicalLine.Choose(context.Config(), repository, workspace, guess: null);
        if (set.Branch is not null) return (set.Branch, null);
        if (!context.GuessesApart)
        {
            var asked = graph.Run("G", needs, () => context.LineAsync(root, repository, workspace, ct), budget: budget);
            return ((await asked.Answer.ConfigureAwait(false)).Branch, asked.Node);
        }

        var origin = graph.Run("G1", needs, () => WorkingTree.GitAsync(root, WorkingTree.OriginHeadAsk, ct), budget: budget);
        var main = graph.Run("G2", needs, () => WorkingTree.GitAsync(root, ReviewChoices.ConventionalAsk("main"), ct), speculative: true, budget);
        var master = graph.Run("G3", needs, () => WorkingTree.GitAsync(root, ReviewChoices.ConventionalAsk("master"), ct), speculative: true, budget);
        var (originCode, originOut, _) = await origin.Answer.ConfigureAwait(false);
        if (ReviewChoices.Guess(originCode, originOut, null, null) is { } pointed)
        {
            main.Node.Use(false);
            master.Node.Use(false);
            return (pointed, origin.Node);
        }

        main.Node.Use(true);
        var mainCode = (await main.Answer.ConfigureAwait(false)).Code;
        if (ReviewChoices.Guess(originCode, originOut, mainCode, null) is { } guessed)
        {
            master.Node.Use(false);
            return (guessed, main.Node);
        }

        master.Node.Use(true);
        var masterCode = (await master.Answer.ConfigureAwait(false)).Code;
        return (ReviewChoices.Guess(originCode, originOut, mainCode, masterCode), master.Node);
    }
}

/// <summary>
/// The untracked window (GITAPP1a-ii, the git-app design P1.3): opened only after W and S answered; at most <c>w</c> files started
/// and not yet folded, a slot given back only as the ordered fold passes a file; stopped by the fold once the budget is spent, each
/// file started ahead of it then stopped and counted wasted. At w = 1 it is today's loop exactly.
/// </summary>
internal static class UntrackedWindow
{
    /// <summary>Fold <paramref name="paths"/> in order through <paramref name="fold"/>, reading at most <paramref name="window"/> ahead.</summary>
    /// <returns>The reads started ahead of the fold and stopped unused: the wasted ones, at most w − 1.</returns>
    internal static async Task<int> FoldAsync(
        IReadOnlyList<string> paths, UntrackedFold fold, int window,
        Func<string, CancellationToken, Task<(string? Patch, int? Added)>> read, CancellationToken ct)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var ahead = new Queue<(string Path, Task<(string? Patch, int? Added)> Read)>();
        var next = 0;
        var wasted = 0;
        for (var at = 0; at < paths.Count; at++)
        {
            // Fill the window up to w files started and not yet folded, while the budget is unspent.
            while (!fold.Spent && ahead.Count < Math.Max(1, window) && next < paths.Count)
            {
                var path = paths[next++];
                ahead.Enqueue((path, read(path, stop.Token)));
            }

            if (ahead.Count == 0)
            {
                // The budget is spent: every later file is listed with no read.
                fold.Drop(paths[at]);
                continue;
            }

            var (taken, answer) = ahead.Dequeue();
            var (patch, added) = await answer.ConfigureAwait(false);
            fold.Take(taken, patch, added);
            if (!fold.Spent || ahead.Count == 0) continue;

            // The fold reached the budget: each read started ahead of it is stopped and counted wasted; its file is listed with no patch.
            await stop.CancelAsync().ConfigureAwait(false);
            foreach (var (path, started) in ahead)
            {
                wasted++;
                try
                {
                    await started.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                }

                fold.Drop(path);
                at++;
            }

            ahead.Clear();
        }

        return wasted;
    }
}
