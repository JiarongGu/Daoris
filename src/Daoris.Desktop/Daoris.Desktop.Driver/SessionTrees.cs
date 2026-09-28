namespace Daoris.Driver;

/// <param name="Path">Where the tree is — what the session record carries and the lock keys on.</param>
/// <param name="Branch">The session's own branch, never reused; git itself refuses a second use.</param>
/// <param name="BasedOn">Which point the branch grew from, spelled for a person — the canonical line, or HEAD with the reason.</param>
/// <param name="Sentence">The whole act in one sentence, price included — for whoever is watching.</param>
/// <param name="GrewFrom">The chain step's branch it grew from (CHAIN2), when it did — null for the canonical line.</param>
public sealed record TreeOpened(string Path, string Branch, string BasedOn, string Sentence, string? GrewFrom = null);

/// <param name="Removed">Whether the tree is gone. False is a refusal, not a failure.</param>
/// <param name="Message">What happened, or what would have been lost and how to mean it.</param>
public sealed record TreeRemoval(bool Removed, string Message);

/// <summary>
/// What came of asking to merge a session tree into the canonical line (D51 rule 6). A refusal is an
/// ANSWER — the checkout is busy, there is nothing to merge, the branches conflict — and each names
/// what the person would do about it.
/// </summary>
public sealed record TreeMerge(bool Merged, string Message);

/// <param name="Path">Where it is.</param>
/// <param name="Workspace">Whose circle, read from the layout Daoris itself chose.</param>
/// <param name="Repository">Whose repository, same.</param>
/// <param name="Branch">The session branch checked out in it.</param>
public sealed record SessionTree(string Path, string Workspace, string Repository, string Branch);

/// <summary>
/// The worktree half of D51: a repository may have more than one working tree, and the extra ones are
/// Daoris's to open and account for — <c>&lt;home&gt;/trees/&lt;workspace&gt;/&lt;repository&gt;/&lt;name&gt;</c>,
/// each a real linked worktree of the registered root on a branch of its own.
/// </summary>
/// <remarks>
/// <para><b>Daoris owns the location; git owns the contents</b> (D51 rule 2) — the same arrangement as
/// a credential profile (D49 §4), and for the same reason: a directory Daoris created is one it may
/// reason about, and one scattered beside somebody's checkout is not. That is also why
/// <see cref="RemoveAsync"/> refuses anything outside the trees home outright, whatever git would say.</para>
///
/// <para><b>Nothing merges itself and nothing deletes itself</b> (rules 6–7). Opening never touches
/// the root's own state; removal refuses while the tree holds uncommitted changes or commits the
/// canonical line has not taken, naming what would be lost — and the person may say it again with
/// force, meaning it.</para>
///
/// <para><b>The name is minted here, not borrowed from the session id</b>, because the order is
/// tree → open → spawn: the ledger's open needs the tree path for the lock, so the id it would borrow
/// does not exist yet. Short and random, like the id itself; the record ties the two together.</para>
/// </remarks>
public sealed class SessionTrees(string home)
{
    /// <summary>Every tree this machine's Daoris has opened lives under here, and only here.</summary>
    public string TreesRoot => Path.Combine(home, "trees");

    /// <summary>
    /// Open a fresh tree for a session in <paramref name="root"/>. Throws <see cref="DriverException"/>
    /// with git's own words when it cannot — before any record exists, which is the caller's contract.
    /// </summary>
    /// <param name="from">
    /// A branch to grow from instead of the canonical line — the one a chain's previous step landed on
    /// in this repository (CHAIN2), so the next step's tree holds the unmerged work it builds on or
    /// checks. One that no longer exists falls back to the canonical line, and the answer says so.
    /// </param>
    public async Task<TreeOpened> OpenAsync(
        string root, string repository, string workspace, CancellationToken ct = default, string? from = null)
    {
        // The root must BE the top of its own working tree — proven, not assumed. Git resolves a
        // repository by walking UP from wherever it is asked, so a root that is not one (a
        // mis-registered path, a folder inside some other checkout) would silently grow worktrees and
        // branches on whatever repository CONTAINS it. The first run of this class's own test suite
        // did exactly that, to this repository — which is why this check exists and refuses loudly.
        var (topCode, toplevel, topErr) = await WorkingTree.GitAsync(
            root, ["rev-parse", "--show-toplevel"], ct).ConfigureAwait(false);
        if (topCode != 0)
        {
            throw new DriverException(
                $"`{repository}`'s registered root is not a git repository ({FirstLine(topErr)}) — "
                + "a session tree is a linked worktree, and there is nothing here to link it to.");
        }

        if (!SamePath(toplevel.Trim(), root))
        {
            throw new DriverException(
                $"`{repository}`'s registered root {root} is INSIDE the repository at {toplevel.Trim()} "
                + "rather than being one — refusing to grow a worktree on a repository the registration "
                + "does not name. Re-run `daoris connect` from the actual root.");
        }

        // The repository's line (WSR2): what the person set for it or its workspace, else the
        // checkout's guess, else the root's HEAD — and the answer SAYS which, because a branch grown
        // from the wrong point is invisible until merge time.
        var line = await LineAsync(root, repository, workspace, ct).ConfigureAwait(false);
        var basedOn = line.Branch is not null
            ? CanonicalLine.Describe(line)
            : "the root's HEAD (no canonical line is declared)";
        var start = line.Branch is { } set ? await StartOfAsync(root, set, line, repository, ct).ConfigureAwait(false) : "HEAD";

        // A chain's previous step's branch, where it still stands (CHAIN2). Session branches are refs
        // of the root's own repository, so a linked worktree grows from one like any other branch.
        string? grewFrom = null;
        if (from is { Length: > 0 })
        {
            var (known, _, _) = await WorkingTree.GitAsync(
                root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{from}"], ct).ConfigureAwait(false);
            if (known == 0)
            {
                start = grewFrom = from;
                basedOn = $"`{from}`, the branch the step before it landed on (not merged yet)";
            }
            else
            {
                basedOn += $" — `{from}`, the branch the step before it landed on, is gone";
            }
        }

        var name = $"s-{Guid.NewGuid().ToString("N")[..8]}";
        var branch = $"daoris/{name}";
        var path = Path.Combine(TreesRoot, workspace, repository, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // 🔴 Long paths (2026-09-27): a tree's prefix is longer than the root's, so a repository whose
        // deepest file fits under its root can cross Windows' 260 characters here. The first real
        // workspace did, and every tick's `worktree add` failed. `WorkingTree.GitAsync` says
        // `core.longpaths` on every call since, removal and status included (2026-09-28).
        var (code, _, stderr) = await WorkingTree.GitAsync(
            root, ["worktree", "add", "-b", branch, path, start], ct)
            .ConfigureAwait(false);
        if (code != 0)
        {
            // 🔴 `worktree add -b` makes the branch before it checks anything out, so a failure used to
            // leave one behind on every tick: sixteen on the first real workspace. It is this call's own,
            // made a moment ago under a name nothing else uses, so it goes, and the repository is left
            // as it was found.
            await WorkingTree.GitAsync(root, ["branch", "-D", branch], ct).ConfigureAwait(false);
            throw new DriverException(
                $"could not open a session tree for `{repository}` — git said: {Failure(stderr)}");
        }

        return new(
            path, branch, basedOn,
            $"opened a session tree at {path} on `{branch}`, from {basedOn} — a fresh tree holds "
            + "nothing git does not track: no installed dependencies, no build outputs. The "
            + "repository's own setup cost is paid here, and in exchange the root's uncommitted work "
            + "holds nothing.",
            grewFrom);
    }

    /// <summary>
    /// Merge a session tree's commits into the canonical line, in the repository's own root (D51
    /// rule 6, design §5).
    /// </summary>
    /// <remarks>
    /// <para><b>Nothing merges itself.</b> This is only ever the person pressing it — D37 would permit
    /// automating a local, reversible act, and it stays a press because it is <i>where their
    /// verification lands</i>.</para>
    ///
    /// <para>🔴 <b>This is the one place Daoris writes into a checkout it did not create</b>, so every
    /// guard `reaching-in` names is here and each refuses rather than repairing. The tree must be
    /// under the trees home (a checkout is never ours to merge FROM); the root must be <b>clean</b>,
    /// because somebody's work in flight is exactly what that document was written from; the root must
    /// already be <b>on</b> the canonical line, because switching a branch in a checkout we do not own
    /// is the same trespass in a smaller form; and every check is made <b>immediately before</b> the
    /// merge in this one call, because "it was clean when I looked" expires the moment you look away.
    /// Assume another session is working in that repository right now — it very often is.</para>
    ///
    /// <para><b>A conflict aborts and reports</b>, leaving the root exactly as it was. Nothing here
    /// resolves anything, forces anything or removes anything: the tree survives a merge whether it
    /// succeeded or not, and discarding it stays a separate act the person asks for.</para>
    /// </remarks>
    public async Task<TreeMerge> MergeAsync(string path, CancellationToken ct = default)
    {
        var full = Path.GetFullPath(path);
        if (!full.StartsWith(Path.GetFullPath(TreesRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return new(false, $"{path} is not a session tree — this merges only trees Daoris opened, "
                + $"under {TreesRoot}.");
        }

        if (!Directory.Exists(full))
        {
            return new(false, $"there is no tree at {path}.");
        }

        var (rootCode, commonDir, rootErr) = await WorkingTree.GitAsync(
            full, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct).ConfigureAwait(false);
        if (rootCode != 0)
        {
            return new(false, $"{path} is not a working tree git recognises: {FirstLine(rootErr)}");
        }

        var root = Path.GetDirectoryName(commonDir.Trim())!;
        var (_, branchOut, _) = await WorkingTree.GitAsync(
            full, ["rev-parse", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
        var branch = branchOut.Trim();
        if (branch is "" or "HEAD")
        {
            return new(false, $"the tree at {path} is not on a branch, so there is nothing to merge.");
        }

        // Uncommitted work in the SESSION tree would silently not travel. Saying "merged" while
        // leaving it behind is the worst answer available: the person believes the work moved.
        var (_, treeDirty, _) = await WorkingTree.GitAsync(full, ["status", "--porcelain"], ct)
            .ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(treeDirty))
        {
            var count = treeDirty.Trim().Split('\n').Length;
            return new(false,
                $"the session's tree has uncommitted work — {count} path(s) — which a merge would "
                + "leave behind. Commit it in the tree first, or decide it is not wanted.");
        }

        var (workspace, repository) = OwnerOf(full);
        var line = await LineAsync(root, repository, workspace, ct).ConfigureAwait(false);
        if (line.Branch is not { } canonical)
        {
            return new(false, "this repository has no canonical line git can name, and none is set, so "
                + "there is nowhere to merge to. Set one with `daoris driver line`.");
        }

        // 🔴 Compared where git has the line, and a comparison git could not make is not "nothing to
        // merge": a line only origin has made `log` fail, and its empty output said the work was there.
        var against = await ComparableAsync(root, canonical, ct).ConfigureAwait(false) ?? canonical;
        var (aheadCode, ahead, aheadErr) = await WorkingTree.GitAsync(
            root, ["log", "--oneline", $"{against}..{branch}"], ct).ConfigureAwait(false);
        if (aheadCode != 0)
        {
            return new(false, $"git cannot compare `{branch}` with `{canonical}` here: {FirstLine(aheadErr)} "
                + "Fetch it, or set another line with `daoris driver line`.");
        }

        if (string.IsNullOrWhiteSpace(ahead))
        {
            // An empty merge commit would say work moved when none did.
            return new(false, $"`{canonical}` already holds everything on `{branch}` — nothing to merge.");
        }

        // 🔴 The root's state, read as late as possible. A tree-state observation is a fact about a
        // moment, and the whole premise here is that somebody else may be working in this repository.
        var (clean, detail) = await WorkingTree.CleanAsync(root, ct).ConfigureAwait(false);
        if (!clean)
        {
            return new(false,
                $"the repository's own checkout is not clean ({detail}), and merging into somebody's "
                + "work in flight is exactly what Daoris does not do. Deal with it there, then merge.");
        }

        var (_, onOut, _) = await WorkingTree.GitAsync(
            root, ["rev-parse", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
        var on = onOut.Trim();
        if (!string.Equals(on, canonical, StringComparison.Ordinal))
        {
            return new(false,
                $"the repository's checkout is on `{on}`, not {CanonicalLine.Describe(line)}. Daoris does "
                + $"not switch a branch in a checkout it did not create — put it on `{canonical}`, then merge.");
        }

        // `--no-ff` so the merge is one commit a person can read and revert as a unit, and `--no-edit`
        // so nothing opens an editor on a machine nobody is sitting at.
        var (mergeCode, _, mergeErr) = await WorkingTree.GitAsync(
            root, ["merge", "--no-ff", "--no-edit", branch], ct).ConfigureAwait(false);
        if (mergeCode != 0)
        {
            // Leave the root as it was found. Nothing here resolves a conflict: the person does that
            // where the context is, and a half-merged checkout is worse than an honest refusal.
            await WorkingTree.GitAsync(root, ["merge", "--abort"], ct).ConfigureAwait(false);
            return new(false,
                $"git would not merge `{branch}` into `{canonical}`: {FirstLine(mergeErr)} "
                + "The merge was aborted and the checkout is as it was.");
        }

        var landed = ahead.Trim().Split('\n').Length;
        return new(true, $"merged `{branch}` into `{canonical}` — {landed} commit(s).{TreeStays}");
    }

    /// <summary>Whether <paramref name="path"/> is a tree this home opened — the only kind a landing rule reaches.</summary>
    public bool Holds(string path) =>
        Path.GetFullPath(path).StartsWith(Path.GetFullPath(TreesRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// What a press on this tree would do under its repository's landing rule (WSR1, D87): merge into
    /// the line, or make the branch the pattern names for this session — said before the press.
    /// </summary>
    public async Task<LandingPlan> PlanAsync(string path, LandingSubject subject, CancellationToken ct = default)
    {
        var full = Path.GetFullPath(path);
        var (workspace, repository) = OwnerOf(full);
        var landing = LandingRules.Choose(Config(), repository, workspace);
        if (landing.Rule.Form == LandingForm.Branch)
        {
            return new(LandingForm.Branch, LandingRules.Expand(landing.Rule.Pattern!, NamesOf(subject, repository)), landing.Source);
        }

        var (code, commonDir, _) = Directory.Exists(full)
            ? await WorkingTree.GitAsync(full, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct).ConfigureAwait(false)
            : (1, "", "");
        var line = code == 0
            ? (await LineAsync(Path.GetDirectoryName(commonDir.Trim())!, repository, workspace, ct).ConfigureAwait(false)).Branch
            : null;
        return new(LandingForm.Merge, line ?? "", landing.Source);
    }

    /// <summary>What a landing says of the tree it leaves — dropped when a tidy removed it.</summary>
    private const string TreeStays = " The tree is still there; discard it when you are done with it.";

    /// <summary>
    /// The press on a reviewed session (WSR1, D87): its work lands as its repository's rule says —
    /// merged into the line (<see cref="MergeAsync"/>), or put on a new branch for the person to push.
    /// </summary>
    public async Task<TreeLanding> LandAsync(string path, LandingSubject subject, CancellationToken ct = default)
    {
        var full = Path.GetFullPath(path);
        var (workspace, repository) = OwnerOf(full);
        var landing = LandingRules.Choose(Config(), repository, workspace);
        TreeLanding landed;
        if (landing.Rule.Form != LandingForm.Branch)
        {
            var merged = await MergeAsync(path, ct).ConfigureAwait(false);
            landed = new(merged.Merged, merged.Message);
        }
        else
        {
            landed = await BranchAsync(full, workspace, repository,
                LandingRules.Expand(landing.Rule.Pattern!, NamesOf(subject, repository)), ct).ConfigureAwait(false);
        }

        // The tidy the person's rule asked for (D88): the tree and its branch go once the work lands, behind
        // the same proof an unforced removal makes — never forced.
        if (!landed.Landed || !landing.Rule.Tidy) return landed;
        var tidied = await RemoveAsync(path, force: false, ct).ConfigureAwait(false);
        return landed with
        {
            // 🔴 The sentence that the tree stays goes when the tidy removed it: the message said both
            // (found landing AR-2202, 2026-09-29).
            Message = tidied.Removed
                ? landed.Message.Replace(TreeStays, "", StringComparison.Ordinal) + $" Tidied, as the rule says: {tidied.Message}"
                : landed.Message + $" The rule says to tidy, and the tree stays: {tidied.Message}",
        };
    }

    /// <summary>
    /// How many commits on <paramref name="revision"/> no branch of the person's holds (D88) — a local
    /// branch outside <c>daoris/</c>, or a remote-tracking one. Zero is landed; null is git unable to say.
    /// </summary>
    public static async Task<int?> UnlandedAsync(string cwd, string revision, CancellationToken ct = default)
    {
        var (code, log, _) = await UnlandedLogAsync(cwd, revision, ct).ConfigureAwait(false);
        return code != 0 ? null : log.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
    }

    private static Task<(int Code, string Stdout, string Stderr)> UnlandedLogAsync(string cwd, string revision, CancellationToken ct) =>
        WorkingTree.GitAsync(cwd,
            ["log", "--oneline", revision, "--not", "--exclude=daoris/*", "--branches", "--exclude=*/daoris/*", "--remotes"], ct);

    /// <summary>
    /// Every session branch in these repositories, with what it holds (D88) — the list a person reads
    /// before the clean-up is pressed. Nothing is changed.
    /// </summary>
    /// <param name="inUse">The trees sessions still running or waiting name — kept whatever they hold.</param>
    public async Task<IReadOnlyList<SweepItem>> SweepPlanAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        CancellationToken ct = default)
    {
        var busy = new HashSet<string>(inUse.Select(Normal), StringComparer.OrdinalIgnoreCase);
        var items = new List<SweepItem>();
        foreach (var (repository, workspace, root) in repositories)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;
            var (code, refs, _) = await WorkingTree.GitAsync(
                root, ["for-each-ref", "--format=%(refname:short)", "refs/heads/daoris/"], ct).ConfigureAwait(false);
            if (code != 0) continue;

            var worktrees = await WorktreesAsync(root, ct).ConfigureAwait(false);
            var line = (await LineAsync(root, repository, RemoteTarget.Workspace(workspace), ct).ConfigureAwait(false)).Branch;
            foreach (var branch in refs.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                items.Add(await JudgeAsync(
                    root, repository, RemoteTarget.Workspace(workspace), branch, worktrees.GetValueOrDefault(branch), line, busy, ct)
                    .ConfigureAwait(false));
            }
        }

        return items;
    }

    /// <summary>
    /// The clean-up (D88): every session branch the proof clears goes, with its tree, and the rest are
    /// kept and named. 🔴 Each is judged again right before it goes — a list is a fact about a moment.
    /// </summary>
    /// <param name="only">The branches the person saw listed to go, as <c>repository:branch</c>; null for every one the proof clears now.</param>
    public async Task<IReadOnlyList<SweepResult>> SweepAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        IReadOnlySet<string>? only = null, CancellationToken ct = default)
    {
        var known = repositories.ToList();
        var busy = new HashSet<string>(inUse.Select(Normal), StringComparer.OrdinalIgnoreCase);
        var results = new List<SweepResult>();
        foreach (var item in await SweepPlanAsync(known, inUse, ct).ConfigureAwait(false))
        {
            if (only is not null && !only.Contains($"{item.Repository}:{item.Branch}"))
            {
                continue;
            }

            if (!item.Removable)
            {
                results.Add(new(item, false, "kept"));
                continue;
            }

            var root = known.First(r => string.Equals(r.Repository, item.Repository, StringComparison.OrdinalIgnoreCase)).Root!;
            var worktrees = await WorktreesAsync(root, ct).ConfigureAwait(false);
            var line = (await LineAsync(root, item.Repository, item.Workspace, ct).ConfigureAwait(false)).Branch;
            var now = await JudgeAsync(root, item.Repository, item.Workspace, item.Branch, worktrees.GetValueOrDefault(item.Branch), line, busy, ct)
                .ConfigureAwait(false);
            if (!now.Removable)
            {
                results.Add(new(now, false, "it changed since the list, and is kept"));
                continue;
            }

            if (now.Tree is { } tree)
            {
                var (removeCode, _, removeErr) = await WorkingTree.GitAsync(
                    root, ["worktree", "remove", tree], ct).ConfigureAwait(false);
                if (removeCode != 0)
                {
                    results.Add(new(now, false, $"git would not remove its tree: {FirstLine(removeErr)}"));
                    continue;
                }
            }

            // -D, because the proof was made in this call; git's own -d asks only about the checkout's HEAD.
            var (deleteCode, _, deleteErr) = await WorkingTree.GitAsync(root, ["branch", "-D", now.Branch], ct).ConfigureAwait(false);
            results.Add(deleteCode == 0
                ? new(now, true, now.Tree is null ? "removed" : "removed, with its tree")
                : new(now, false, $"git would not delete the branch: {FirstLine(deleteErr)}"));
        }

        return results;
    }

    private static async Task<SweepItem> JudgeAsync(
        string root, string repository, string workspace, string branch, string? tree, string? line,
        HashSet<string> busy, CancellationToken ct)
    {
        SweepItem Item(string kind, int commits = 0, string? where = null, string? detail = null) =>
            new(repository, workspace, branch, tree, kind, commits, where, detail);

        if (tree is not null && busy.Contains(Normal(tree))) return Item(SweepKind.InUse);

        if (tree is not null)
        {
            var (_, dirty, _) = await WorkingTree.GitAsync(tree, ["status", "--porcelain"], ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(dirty))
            {
                return Item(SweepKind.Dirty, detail: $"{dirty.Trim().Split('\n').Length} path(s) uncommitted");
            }
        }

        var (code, log, err) = await UnlandedLogAsync(root, branch, ct).ConfigureAwait(false);
        if (code != 0) return Item(SweepKind.Unlanded, detail: $"git could not tell: {FirstLine(err)}");
        var unlanded = log.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (unlanded.Length > 0) return Item(SweepKind.Unlanded, unlanded.Length, detail: string.Join('\n', unlanded.Take(3)));

        var against = line is null ? null : await ComparableAsync(root, line, ct).ConfigureAwait(false);
        if (against is not null)
        {
            var (_, ahead, _) = await WorkingTree.GitAsync(root, ["rev-list", "--count", $"{against}..{branch}"], ct).ConfigureAwait(false);
            if (ahead.Trim() == "0") return Item(SweepKind.Empty, where: line);
        }

        // Where it landed, for the list: the first branch of the person's that holds its tip.
        var (_, holders, _) = await WorkingTree.GitAsync(
            root, ["branch", "--all", "--contains", branch, "--format=%(refname:short)"], ct).ConfigureAwait(false);
        var where = holders.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(name => !name.StartsWith("daoris/", StringComparison.Ordinal) && !name.Contains("/daoris/", StringComparison.Ordinal));
        var (_, count, _) = await WorkingTree.GitAsync(
            root, ["rev-list", "--count", branch, "--not", against ?? "HEAD"], ct).ConfigureAwait(false);
        return Item(SweepKind.Landed, int.TryParse(count.Trim(), out var n) ? n : 0, where);
    }

    /// <summary>Which tree each branch is checked out in, from git's own list.</summary>
    private static async Task<Dictionary<string, string>> WorktreesAsync(string root, CancellationToken ct)
    {
        var (_, porcelain, _) = await WorkingTree.GitAsync(root, ["worktree", "list", "--porcelain"], ct).ConfigureAwait(false);
        var trees = new Dictionary<string, string>(StringComparer.Ordinal);
        string? path = null;
        foreach (var raw in porcelain.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("worktree ", StringComparison.Ordinal)) path = line["worktree ".Length..];
            else if (line.StartsWith("branch refs/heads/", StringComparison.Ordinal) && path is not null)
            {
                trees[line["branch refs/heads/".Length..]] = Path.GetFullPath(path);
            }
        }

        return trees;
    }

    private static string Normal(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    /// <summary>
    /// Put a session's work on a new branch, from the session's own branch, which grew from the line.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>It writes nothing to the checkout</b>: it makes one ref in the repository and moves no
    /// checkout, so the root may be dirty and on any branch, and is left exactly so. A branch of that
    /// name already there is refused and never moved — it may be somebody's. Nothing pushes (D87).
    /// </remarks>
    private async Task<TreeLanding> BranchAsync(string full, string workspace, string repository, string name, CancellationToken ct)
    {
        if (!Holds(full))
        {
            return new(false, $"{full} is not a session tree — this lands only trees Daoris opened, under {TreesRoot}.");
        }

        if (!Directory.Exists(full))
        {
            return new(false, $"there is no tree at {full}.");
        }

        var (rootCode, commonDir, rootErr) = await WorkingTree.GitAsync(
            full, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct).ConfigureAwait(false);
        if (rootCode != 0)
        {
            return new(false, $"{full} is not a working tree git recognises: {FirstLine(rootErr)}");
        }

        var root = Path.GetDirectoryName(commonDir.Trim())!;
        var (_, branchOut, _) = await WorkingTree.GitAsync(full, ["rev-parse", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
        var branch = branchOut.Trim();
        if (branch is "" or "HEAD")
        {
            return new(false, $"the tree at {full} is not on a branch, so there is nothing to land.");
        }

        // Uncommitted work would not travel with the branch — the same reason the merge door gives.
        var (_, dirty, _) = await WorkingTree.GitAsync(full, ["status", "--porcelain"], ct).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(dirty))
        {
            return new(false,
                $"the session's tree has uncommitted work — {dirty.Trim().Split('\n').Length} path(s) — which a "
                + "branch would leave behind. Commit it in the tree first, or decide it is not wanted.");
        }

        var line = (await LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
        var against = line is null ? "HEAD" : await ComparableAsync(root, line, ct).ConfigureAwait(false) ?? line;
        var (logCode, ahead, logErr) = await WorkingTree.GitAsync(
            root, ["log", "--oneline", $"{against}..{branch}"], ct).ConfigureAwait(false);
        if (logCode != 0)
        {
            return new(false, $"git cannot compare `{branch}` with `{line ?? "HEAD"}` here: {FirstLine(logErr)} "
                + "Fetch it, or set another line with `daoris driver line`.");
        }

        if (string.IsNullOrWhiteSpace(ahead))
        {
            return new(false, $"`{branch}` holds nothing `{line ?? "HEAD"}` does not — nothing to land.");
        }

        if (!BranchName.IsValid(name))
        {
            return new(false, $"the landing rule names `{name}` for this session, which is not a branch name git "
                + "would take. Change the pattern with `daoris driver landing`.");
        }

        var (exists, _, _) = await WorkingTree.GitAsync(
            root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{name}"], ct).ConfigureAwait(false);
        if (exists == 0)
        {
            return new(false, $"`{name}` is already a branch in `{repository}`, and Daoris does not move a branch it "
                + "did not make. Rename or delete it there, or change the pattern with `daoris driver landing`.");
        }

        var (code, _, err) = await WorkingTree.GitAsync(root, ["branch", name, branch], ct).ConfigureAwait(false);
        if (code != 0)
        {
            return new(false, $"git would not make `{name}`: {FirstLine(err)}");
        }

        var count = ahead.Trim().Split('\n').Length;
        return new(true,
            $"put the work on `{name}` — {count} commit(s) from `{line ?? "HEAD"}`. Push it and open the pull request "
            + $"from there: `git push -u origin {name}`. Nothing was merged and the checkout was not touched.{TreeStays}",
            name);
    }

    private DriverConfig Config() => DriverConfig.Load(Path.Combine(home, "driver.json"));

    /// <summary>What a pattern is expanded from: the quest, or the session where there is none; the title's words.</summary>
    private static LandingNames NamesOf(LandingSubject subject, string repository) =>
        new(subject.Quest ?? subject.Session, LandingRules.Slug(subject.Title), repository, subject.Session);

    /// <summary>
    /// Remove a session tree — <b>and only a session tree</b>: anything outside the trees home is
    /// somebody's checkout, and is refused before git is asked anything.
    /// </summary>
    /// <param name="force">The person saying it again, meaning it: work and branch go too.</param>
    public async Task<TreeRemoval> RemoveAsync(string path, bool force = false, CancellationToken ct = default)
    {
        var full = Path.GetFullPath(path);
        if (!full.StartsWith(Path.GetFullPath(TreesRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return new(false, $"{path} is not a session tree — this removes only trees Daoris opened, "
                + $"under {TreesRoot}. A checkout is never a side effect's to delete.");
        }

        if (!Directory.Exists(full))
        {
            return new(false, $"there is no tree at {path}.");
        }

        // The main root, from git itself: the common dir is the main repository's .git, wherever the
        // registered root is — nothing here guesses a layout it does not own.
        var (rootCode, commonDir, rootErr) = await WorkingTree.GitAsync(
            full, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct).ConfigureAwait(false);
        if (rootCode != 0)
        {
            return new(false, $"{path} is not a working tree git recognises: {FirstLine(rootErr)}");
        }

        var root = Path.GetDirectoryName(commonDir.Trim())!;
        var (_, branchOut, _) = await WorkingTree.GitAsync(
            full, ["rev-parse", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
        var branch = branchOut.Trim();

        if (!force)
        {
            // Nothing deletes itself (D51 rule 7). Two different kinds of work can be lost here, and
            // the refusal names the kind, because they are fixed differently: uncommitted changes are
            // committed or abandoned IN the tree; unmerged commits are merged FROM it (SURF6's surface).
            var (_, dirty, _) = await WorkingTree.GitAsync(full, ["status", "--porcelain"], ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(dirty))
            {
                var count = dirty.Trim().Split('\n').Length;
                return new(false,
                    $"the tree at {path} has uncommitted work — {count} path(s). Removing it would "
                    + "destroy that work; commit it there or say it again with --force, meaning it.");
            }

            var (workspace, repository) = OwnerOf(full);
            var canonical = (await LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch ?? "HEAD";
            // Landed (D88): a branch of the person's holds every commit — the line, a branch the branch form
            // made, one they pushed. Asked of the tree's own HEAD, so a detached tree is judged by what it
            // holds. 🔴 A comparison git could not make keeps the tree: an empty answer is not "landed".
            var (logCode, unmerged, logErr) = await UnlandedLogAsync(full, "HEAD", ct).ConfigureAwait(false);
            if (logCode != 0)
            {
                return new(false,
                    $"Daoris cannot tell whether the work on `{branch}` is landed: {FirstLine(logErr)} "
                    + "The tree stays; say it again with --force to discard it.");
            }

            if (!string.IsNullOrWhiteSpace(unmerged))
            {
                return new(false,
                    $"the tree at {path} holds commits `{canonical}` has not taken, and no other branch of yours holds "
                    + $"them:\n{unmerged.Trim()}\nLand them from the review, or say it again with --force to discard them.");
            }
        }

        // A tree past Windows' 260 characters is removed with long paths, which every git call here says:
        // without them git let go of the tree, then failed deleting its files (2026-09-28).
        var removeArgs = force
            ? new[] { "worktree", "remove", "--force", full }
            : ["worktree", "remove", full];
        var (removeCode, _, removeErr) = await WorkingTree.GitAsync(root, removeArgs, ct).ConfigureAwait(false);
        if (removeCode != 0)
        {
            return new(false, $"git would not remove the tree: {FirstLine(removeErr)}");
        }

        // The branch goes with its tree — where this call's proof cleared it (D88) or the person forced it.
        // -D either way: git's own -d asks only whether the checkout's HEAD holds it, which kept every
        // branch the branch form had landed. A branch left behind would resurrect "never reused" as a pile.
        if (branch is not "" and not "HEAD")
        {
            await WorkingTree.GitAsync(root, ["branch", "-D", branch], ct).ConfigureAwait(false);
        }

        return new(true, $"removed the session tree at {path} (branch `{branch}`).");
    }

    /// <summary>
    /// Every session tree on this machine, read from the layout itself — the directory is the
    /// registry, the same argument the credential profiles made (D49 §4).
    /// </summary>
    public async Task<IReadOnlyList<SessionTree>> ListAsync(CancellationToken ct = default)
    {
        var found = new List<SessionTree>();
        if (!Directory.Exists(TreesRoot)) return found;

        foreach (var workspace in Directory.EnumerateDirectories(TreesRoot))
        foreach (var repository in Directory.EnumerateDirectories(workspace))
        foreach (var tree in Directory.EnumerateDirectories(repository))
        {
            var (code, branch, _) = await WorkingTree.GitAsync(
                tree, ["rev-parse", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
            found.Add(new(
                tree,
                Path.GetFileName(workspace),
                Path.GetFileName(repository),
                code == 0 ? branch.Trim() : "(unreadable)"));
        }

        return found;
    }

    /// <summary>
    /// The repository's line (WSR2), from the choices beside this home, read per call: the person may
    /// set it between one tree and the next, and a cached answer would grow the second from the old line.
    /// </summary>
    private Task<Line> LineAsync(string root, string repository, string workspace, CancellationToken ct) =>
        CanonicalLine.ResolveAsync(root, repository, workspace, Config(), ct);

    /// <summary>
    /// Where a tree on <paramref name="branch"/> grows from: the branch itself where this checkout has
    /// it, its copy on <c>origin</c> where only that exists, and a refusal for a line set to a branch
    /// the checkout has nowhere — a tree grown from the wrong point is invisible until merge time.
    /// </summary>
    private static async Task<string> StartOfAsync(string root, string branch, Line line, string repository, CancellationToken ct)
    {
        var (local, _, _) = await WorkingTree.GitAsync(
            root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}"], ct).ConfigureAwait(false);
        if (local == 0) return branch;
        if (line.Source == LineSource.Checkout) return branch;

        var (remote, _, _) = await WorkingTree.GitAsync(
            root, ["rev-parse", "--verify", "--quiet", $"refs/remotes/origin/{branch}"], ct).ConfigureAwait(false);
        if (remote == 0) return $"origin/{branch}";

        throw new DriverException(
            $"{CanonicalLine.Describe(line)} is `{repository}`'s line, and its checkout has no such branch, here "
            + $"or on origin. Fetch it, or set another with `daoris driver line {repository} <branch>`.");
    }

    /// <summary>
    /// The name git compares <paramref name="branch"/> by here: the local branch, else origin's copy,
    /// else null — a line this checkout has nowhere.
    /// </summary>
    private static async Task<string?> ComparableAsync(string root, string branch, CancellationToken ct)
    {
        foreach (var (reference, name) in new[] { ($"refs/heads/{branch}", branch), ($"refs/remotes/origin/{branch}", $"origin/{branch}") })
        {
            var (code, _, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", reference], ct).ConfigureAwait(false);
            if (code == 0) return name;
        }

        return null;
    }

    /// <summary>A tree's workspace and repository, read from the layout Daoris itself chose (`trees/&lt;workspace&gt;/&lt;repository&gt;/&lt;name&gt;`).</summary>
    private (string Workspace, string Repository) OwnerOf(string full)
    {
        var parts = Path.GetRelativePath(TreesRoot, full).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Length >= 3 ? (parts[0], parts[1]) : ("", "");
    }

    private static string FirstLine(string text)
    {
        var trimmed = text.Trim();
        var newline = trimmed.IndexOf('\n');
        return newline < 0 ? trimmed : trimmed[..newline].Trim();
    }

    /// <summary>
    /// git's reason, not its progress. `worktree add` says *"Preparing worktree (new branch …)"*
    /// before anything can fail, and a refusal that quoted the first line quoted that. The last
    /// `fatal:` or `error:` line is why; failing those, the last line said.
    /// </summary>
    private static string Failure(string stderr)
    {
        var lines = stderr.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
        return lines.LastOrDefault(line => line.StartsWith("fatal:", StringComparison.Ordinal))
               ?? lines.LastOrDefault(line => line.StartsWith("error:", StringComparison.Ordinal))
               ?? lines.LastOrDefault()
               ?? "nothing";
    }

    /// <summary>
    /// One filesystem location under two spellings: git prints forward slashes on every platform, the
    /// caller passes whatever the registry holds, and Windows compares blind to case.
    /// </summary>
    private static bool SamePath(string left, string right) => string.Equals(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)).Replace('\\', '/'),
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)).Replace('\\', '/'),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
