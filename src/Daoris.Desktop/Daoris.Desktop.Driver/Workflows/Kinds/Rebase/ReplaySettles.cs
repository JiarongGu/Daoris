using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeRefs;

namespace Daoris.Driver;

/// <summary>
/// The replay that settles what the repository's records say may be settled: a work's commits replayed onto a target in a tree of Daoris's
/// own made for it under the trees home and removed after, each stop settled by the generated files, the declared backlog by its rows, the
/// declared row tables, union's joins and headings and the line rules the person accepted, and nothing else. The <c>rebase</c> step and a
/// landing's replay are its callers; neither keeps a copy.
/// </summary>
internal sealed class ReplaySettles(TreeContext context, PassLeftovers leftovers)
{
    /// <summary>Where a replay stopped on a file no rule settles: the commit it was applying and the files left conflicted, before it was aborted.</summary>
    internal sealed record Stop(string Commit, IReadOnlyList<string> Paths);

    /// <summary>What a landing's replay made: the new tip, or null and git's words, and whether those name a conflict.</summary>
    internal sealed record KeptReplay(string? Now, string Words, bool Conflict)
    {
        /// <summary>The generated files it took at the line's copy where only those conflicted, in the order met.</summary>
        public IReadOnlyList<string> Generated { get; init; } = [];

        /// <summary>Those written again from the replayed tree by the writers the person accepted; none where none ran.</summary>
        public GeneratedWriters.Written Written { get; init; } = GeneratedWriters.Written.None;

        /// <summary>The declared backlog it settled by the merge base where it conflicted; null where it settled none.</summary>
        public BacklogMerge.Settled? Backlog { get; init; }

        /// <summary>The declared row tables it settled the same way where they conflicted; none where it settled none.</summary>
        public IReadOnlyList<BacklogMerge.Settled> Tables { get; init; } = [];

        /// <summary>The joins union left that it set apart, in a commit on top; none where it set none.</summary>
        public IReadOnlyList<UnionJoins.Set> Joined { get; init; } = [];

        /// <summary>The headings union kept twice that it kept once, in a commit on top (<see cref="UnionOnce"/>).</summary>
        public IReadOnlyList<UnionOnce.Kept> Once { get; init; } = [];

        /// <summary>What holding the backlog to its rows settled where git had merged it clean, said (<see cref="BacklogHeld"/>).</summary>
        public string Departed { get; init; } = "";

        /// <summary>The files it settled by a line rule the owner accepted for integration, with their writers; none where none.</summary>
        public IReadOnlyList<LineRules.Settled> Lined { get; init; } = [];

        /// <summary>What those writers wrote again from the replayed tree, in a commit on top; nothing run where it settled none.</summary>
        public GeneratedWriters.Written LineWritten { get; init; } = GeneratedWriters.Written.None;

        /// <summary>Where a conflict stopped it; null where it made a tip, or failed with no stop git could name.</summary>
        public Stop? StoppedAt { get; init; }
    }

    /// <summary>
    /// The commits a landing keeps replayed onto <paramref name="onto"/>, in order, in a tree of Daoris's own made for it under the
    /// trees home and removed after: one <c>git rebase --onto</c> per stretch of <paramref name="commits"/> it keeps, each onto the last one's
    /// new tip. A commit that becomes empty is dropped by git; a conflict aborts it, and nothing is moved. A conflict
    /// only in files the repository marks generated is not the session's, so those take the line's copy and the replay goes on
    /// (<see cref="GeneratedPaths"/>); one in any other file still aborts it. Where it took any, the writers the person
    /// accepted for their folders write them again from the replayed tree, in a commit on top (<see cref="GeneratedWriters"/>).
    /// The backlog the line declares at <paramref name="onto"/>, conflicted alone or beside generated files, is settled by
    /// its merge base as the merge tool settles it (<see cref="BacklogMerge"/>); a base line both sides changed still aborts it.
    /// A file of a line rule the owner accepted for integration, declared at <paramref name="onto"/>, is settled by its
    /// pattern and written again by its writer in a commit on top (<see cref="LineRules"/>); a writer that fails refuses the replay.
    /// </summary>
    /// <param name="commits">Every commit beyond the line, oldest first, which <paramref name="kept"/> is a subset of.</param>
    internal async Task<KeptReplay> ReplayKeptAsideAsync(
        string root, string workspace, string repository, string onto, IReadOnlyList<LandingCommit> commits, IReadOnlyList<LandingCommit> kept,
        CancellationToken ct)
    {
        if (kept.Count == 0) return new(onto, "", false);
        var keeps = kept.Select(each => each.Sha).ToHashSet(StringComparer.Ordinal);
        var stretches = BranchReplays.Stretches([.. commits.Select(each => each.Sha)], commits.Select(each => each.Sha).Where(sha => !keeps.Contains(sha)).ToHashSet(StringComparer.Ordinal));

        var config = Config();
        // The line rules the owner accepted for integration, read from the accepted blob alone where it is the one at `onto`.
        var lines = config is null ? [] : await LineRules.AcceptedAsync(config, root, repository, onto, ct).ConfigureAwait(false);
        var (aside, notMade) = await OpenAsideAsync(root, workspace, repository, stretches[0].To, ct).ConfigureAwait(false);
        if (aside is null) return new(null, notMade, false);
        try
        {
            var made = await RebaseStretchesAsync(aside, onto, stretches, switchFirst: false, ct, landing: new BacklogMerge.Stops(onto, lines)).ConfigureAwait(false);
            if (made.Now is null) return made;
            var holding = await BacklogHeld.HoldAsync(aside, onto, $"{commits[0].Sha}^", onto, commits[^1].Sha, made.Now, commit: true, ct).ConfigureAwait(false);
            if (holding.Refused is { } unheld) return new(null, $"the replayed backlog does not hold its rows.{unheld}", true);
            if (holding.Said.Length > 0) made = made with { Now = await CommitOfAsync(aside, "HEAD", ct).ConfigureAwait(false) ?? made.Now, Departed = holding.Said };

            // What union joined in the replay is set apart as the merge tool sets it, in a commit on top.
            var joined = await UnionJoins.SetApartAsync(aside, onto, commits[^1].Sha, ct).ConfigureAwait(false);
            if (joined.Count > 0) made = made with { Now = await CommitOfAsync(aside, "HEAD", ct).ConfigureAwait(false) ?? made.Now, Joined = joined };
            var once = await UnionOnce.KeepOnceAsync(aside, onto, $"{commits[0].Sha}^", commits[^1].Sha, ct).ConfigureAwait(false);
            if (once.Count > 0) made = made with { Now = await CommitOfAsync(aside, "HEAD", ct).ConfigureAwait(false) ?? made.Now, Once = once };

            if (made.Generated.Count > 0)
            {
                var writers = config is null ? [] : await GeneratedWriters.AcceptedAsync(config, root, repository, workspace, onto, ct).ConfigureAwait(false);
                var written = await GeneratedWriters.WriteAsync(aside, writers, made.Generated, ct).ConfigureAwait(false);
                made = written.Paths.Count == 0
                    ? made with { Written = written }
                    : made with { Now = await CommitOfAsync(aside, "HEAD", ct).ConfigureAwait(false) ?? made.Now, Written = written };
            }

            if (made.Lined.Count == 0) return made;
            // Each file a line rule settled is written again by its accepted writer. No rule keeps one side's copy, so a
            // writer that fails refuses the replay as the conflict it is, for the session to settle in its own tree.
            var lineWriters = made.Lined.Select(each => new GeneratedWriters.Writer(each.Path, each.Run)).ToList();
            var lineWritten = await GeneratedWriters.WriteAsync(aside, lineWriters, [.. made.Lined.Select(each => each.Path)], ct).ConfigureAwait(false);
            if (lineWritten.Failed is { } failed) return new(null, LineRules.Unwritten(made.Lined, failed), true);
            return lineWritten.Paths.Count == 0
                ? made with { LineWritten = lineWritten }
                : made with { Now = await CommitOfAsync(aside, "HEAD", ct).ConfigureAwait(false) ?? made.Now, LineWritten = lineWritten };
        }
        finally
        {
            // Daoris's own tree, made a moment ago for this one replay and holding nothing of anyone's.
            await CloseAsideAsync(root, aside).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A tree of Daoris's own for one replay, under the trees home, detached at <paramref name="at"/>: what a
    /// replay a kill cut off left in the repository's trees moved aside first (<see cref="PassLeftovers"/>), then the
    /// tree added locked under words naming this process for as long as the replay runs, so the next replay tells a tree in use
    /// from a leftover. Its path, or null and git's words.
    /// </summary>
    /// <remarks>
    /// Under the trees home, where Daoris's trees live, and like every git call here with long paths on: the first real
    /// post-merge run, done by hand in a tree made elsewhere without them, could not check out the repository's
    /// deepest files.
    /// </remarks>
    internal async Task<(string? Aside, string Words)> OpenAsideAsync(string root, string workspace, string repository, string at, CancellationToken ct)
    {
        var trees = Path.Combine(context.TreesRoot, workspace, repository);
        try
        {
            await leftovers.ClearReplaysAsync(root, trees, DateTimeOffset.UtcNow, TreeOpenings.Running, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            // A leftover that could not be cleared is the next replay's to try again, never a reason to refuse this one.
        }

        var aside = Path.Combine(trees, $"{BranchReplays.Prefix}{Guid.NewGuid().ToString("N")[..8]}");
        Directory.CreateDirectory(trees);
        var (added, _, addErr) = await WorkingTree.GitAsync(
            root, ["worktree", "add", "--quiet", "--detach", "--lock", "--reason", PassLeftovers.HeldBy(Environment.ProcessId), aside, at], ct)
            .ConfigureAwait(false);
        if (added != 0) return (null, $"Daoris could not make a tree to replay it in: {Failure(addErr)}");

        // Where the repository's configuration binds its linked trees to another checkout, a rebase in this one would
        // rewrite that checkout's files, so nothing is replayed. This tree is Daoris's own, made a moment ago and holding nothing, so it goes
        // as every replay's own tree goes: git's removal names it by its record, never through it.
        if (await TreeBinding.RefusalAsync(aside, ct).ConfigureAwait(false) is { } bound)
        {
            await CloseAsideAsync(root, aside).ConfigureAwait(false);
            return (null, $"Daoris let go of the tree it made to replay it in, unused: {bound}.");
        }

        return (aside, "");
    }

    /// <summary>
    /// A replay's own tree removed, its lock with it (<c>--force</c> twice), whatever the replay's token says, since a tree left
    /// locked by a process that still runs would wait for that process to end. Daoris's own tree, made a moment ago
    /// for this one replay and holding nothing of anyone's: its commits are in the repository, so removing it loses nothing.
    /// </summary>
    internal static Task CloseAsideAsync(string root, string aside) =>
        WorkingTree.GitAsync(root, ["worktree", "remove", "--force", "--force", aside], CancellationToken.None);

    /// <summary>
    /// The person's choices a landing's replay reads its acceptances from, the generated folders' writers and the line rules';
    /// null where the driver's settings could not be read, and the replay runs no writer, as it would with none accepted.
    /// </summary>
    private DriverConfig? Config()
    {
        try
        {
            return context.Config();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return null;
        }
    }

    /// <summary>
    /// <paramref name="stretches"/> replayed in <paramref name="tree"/>, detached, each onto the last one's new tip, starting at
    /// <paramref name="onto"/>: the new tip, or null and git's words, and whether those name a conflict. Each run is taken up detached
    /// at its last commit first (the first only where <paramref name="switchFirst"/>: a tree opened there already stands on it).
    /// </summary>
    /// <param name="landing">
    /// A landing's replay, with the backlog's settling over its stops: a stop whose every conflicted file the
    /// repository marks generated, or is the declared backlog that its base settles, takes the line's copy of the generated ones and the
    /// merge of the backlog, and goes on; the answer names them. Null stops at any conflict, as the replay aside does.
    /// </param>
    internal static async Task<KeptReplay> RebaseStretchesAsync(
        string tree, string onto, IReadOnlyList<(string From, string To)> stretches, bool switchFirst, CancellationToken ct,
        BacklogMerge.Stops? landing = null)
    {
        var current = onto;
        var generated = new List<string>();
        for (var at = 0; at < stretches.Count; at++)
        {
            if (at > 0 || switchFirst)
            {
                var (moved, _, moveErr) = await WorkingTree.GitAsync(tree, ["switch", "--quiet", "--detach", stretches[at].To], ct).ConfigureAwait(false);
                if (moved != 0) return new(null, $"git would not take up its next commits to replay: {Failure(moveErr)}", false);
            }

            if (landing is null)
            {
                var (plainFailed, plainWords) = await BranchReplays.RebaseInAsync(tree, current, stretches[at].From, ct).ConfigureAwait(false);
                if (plainFailed) return new(null, plainWords, plainWords.StartsWith("CONFLICT", StringComparison.Ordinal));
            }
            else
            {
                var (failed, words, stop) = await RebaseTakingGeneratedAsync(tree, current, stretches[at].From, generated, landing, ct).ConfigureAwait(false);
                if (failed) return new(null, words, words.StartsWith("CONFLICT", StringComparison.Ordinal)) { StoppedAt = stop };
            }

            current = await CommitOfAsync(tree, "HEAD", ct).ConfigureAwait(false) ?? current;
        }

        return new(current, "", false) { Generated = generated, Backlog = landing?.Settled, Tables = landing?.Tables ?? [], Lined = landing?.Lined ?? [] };
    }

    /// <summary>
    /// <see cref="BranchReplays.RebaseInAsync"/>'s replay, where a stop whose every conflicted file the repository marks generated
    /// (<see cref="GeneratedPaths"/>) is not the session's conflict: those files take the line's side, the side the replay
    /// builds on, and the replay goes on, a commit left empty by it skipped as git would drop one. Any other stop aborts with git's
    /// <c>CONFLICT</c> lines, the commit it was applying and its conflicted files read first. The files taken are added to
    /// <paramref name="generated"/>. The backlog the line declares, conflicted alone or beside generated files, is settled by its merge
    /// base in <paramref name="backlog"/>, which sums what each stop settled; a base line both sides changed aborts the replay as any conflict does.
    /// </summary>
    private static async Task<(bool Failed, string Words, Stop? Stop)> RebaseTakingGeneratedAsync(
        string tree, string onto, string upstream, List<string> generated, BacklogMerge.Stops backlog, CancellationToken ct)
    {
        var (code, output, err) = await WorkingTree.GitAsync(
            tree, ["-c", "rebase.updateRefs=false", "-c", "rebase.autoStash=false", "rebase", "--onto", onto, upstream],
            environment: null, SyncBounds.Replay, ct).ConfigureAwait(false);
        string[] paths = [];
        // Each stop is one commit; a bound on them keeps a replay that never settles from going round for ever.
        for (var stops = 0; code != 0 && stops < GeneratedPaths.StopsAtMost; stops++)
        {
            var (listed, unmerged, _) = await WorkingTree.GitAsync(tree, ["--no-optional-locks", "-c", "core.quotePath=false", "diff", "--name-only", "--diff-filter=U"], ct)
                .ConfigureAwait(false);
            paths = unmerged.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (listed != 0 || paths.Length == 0) break;
            // The backlog is asked of only where the generated files alone do not settle the stop, so a replay that meets none reads no
            // manifest; its other files must all be generated, and its own conflict must settle by its base.
            // The declared row tables are settled by the merge base as the backlog is.
            var settled = new HashSet<string>(StringComparer.Ordinal);
            if (await GeneratedPaths.AllAsync(tree, paths, ct).ConfigureAwait(false) is not true)
            {
                var declared = await backlog.DeclaredAsync(tree, ct).ConfigureAwait(false);
                var mine = paths.Where(declared.Settles).ToArray();
                var others = paths.Where(path => !declared.Settles(path)).ToArray();
                if (mine.Length == 0
                    || (others.Length > 0 && await GeneratedPaths.AllAsync(tree, others, ct).ConfigureAwait(false) is not true))
                {
                    break;
                }

                var all = true;
                foreach (var path in mine)
                {
                    if (!await backlog.SettleAsync(tree, path, ct).ConfigureAwait(false))
                    {
                        all = false;
                        break;
                    }

                    settled.Add(path);
                }

                if (!all) break;
            }

            foreach (var path in paths.Where(path => !settled.Contains(path)))
            {
                // The line's side, `--ours` in a rebase; a path the line removed goes, as it does there.
                var (taken, _, _) = await WorkingTree.GitAsync(tree, ["checkout", "--ours", "--", path], ct).ConfigureAwait(false);
                await WorkingTree.GitAsync(tree, taken == 0 ? ["add", "--", path] : ["rm", "--quiet", "--cached", "--force", "--", path], ct)
                    .ConfigureAwait(false);
                if (!generated.Contains(path, StringComparer.Ordinal)) generated.Add(path);
            }

            paths = [];
            // Nothing left of the commit once its generated files are the line's: skipped, as a commit that becomes empty is dropped.
            var (same, _, _) = await WorkingTree.GitAsync(tree, ["--no-optional-locks", "diff", "--cached", "--quiet", "HEAD"], ct).ConfigureAwait(false);
            (code, output, err) = await WorkingTree.GitAsync(
                tree, same == 0 ? ["rebase", "--skip"] : ["-c", "core.editor=:", "rebase", "--continue"], environment: null, SyncBounds.Replay, ct)
                .ConfigureAwait(false);
        }

        if (code == 0) return (false, "", null);
        var stopped = paths.Length > 0 && await CommitOfAsync(tree, "REBASE_HEAD", ct).ConfigureAwait(false) is { } at ? new Stop(at, paths) : null;
        await WorkingTree.GitAsync(tree, ["rebase", "--abort"], ct).ConfigureAwait(false);
        var conflicts = (output + "\n" + err).Split('\n')
            .Select(each => each.Trim())
            .Where(each => each.StartsWith("CONFLICT", StringComparison.Ordinal))
            .ToList();
        return (true, (conflicts.Count > 0 ? LineHolds.Joined(conflicts) : Failure(err.Length > 0 ? err : output)) + backlog.Refused, stopped);
    }
}
