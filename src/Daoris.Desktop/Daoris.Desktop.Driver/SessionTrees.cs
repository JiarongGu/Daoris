namespace Daoris.Driver;

/// <param name="Path">Where the tree is — what the session record carries and the lock keys on.</param>
/// <param name="Branch">The session's own branch, never reused; git itself refuses a second use.</param>
/// <param name="BasedOn">Which point the branch grew from, spelled for a person — the canonical line, or HEAD with the reason.</param>
/// <param name="Sentence">The whole act in one sentence, price included — for whoever is watching.</param>
public sealed record TreeOpened(string Path, string Branch, string BasedOn, string Sentence);

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
    public async Task<TreeOpened> OpenAsync(
        string root, string repository, string workspace, CancellationToken ct = default)
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

        // WSP4's resolution, reused rather than re-derived: the repository's canonical line where it
        // declared one, the root's HEAD otherwise — and the answer SAYS which, because a branch grown
        // from the wrong point is invisible until merge time.
        var canonical = await WorkingTree.DefaultBranchAsync(root, ct).ConfigureAwait(false);
        var basedOn = canonical is not null
            ? $"the canonical line `{canonical}`"
            : "the root's HEAD (no canonical line is declared)";

        var name = $"s-{Guid.NewGuid().ToString("N")[..8]}";
        var branch = $"daoris/{name}";
        var path = Path.Combine(TreesRoot, workspace, repository, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // 🔴 Long paths, for this one command (2026-09-27): a tree's prefix is longer than the root's, so
        // a repository whose deepest file fits under its root can cross Windows' 260 characters here.
        // The first real workspace did, and every tick's `worktree add` failed. Said on the command
        // line, so nothing in the repository's own configuration changes.
        var (code, _, stderr) = await WorkingTree.GitAsync(
            root, ["-c", "core.longpaths=true", "worktree", "add", "-b", branch, path, canonical ?? "HEAD"], ct)
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
            + "holds nothing.");
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

        var canonical = await WorkingTree.DefaultBranchAsync(root, ct).ConfigureAwait(false);
        if (canonical is null)
        {
            return new(false, "this repository has no canonical line git can name, so there is "
                + "nowhere to merge to.");
        }

        var (_, ahead, _) = await WorkingTree.GitAsync(
            root, ["log", "--oneline", $"{canonical}..{branch}"], ct).ConfigureAwait(false);
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
                $"the repository's checkout is on `{on}`, not `{canonical}`. Daoris does not switch a "
                + "branch in a checkout it did not create — put it on the canonical line, then merge.");
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
        return new(true,
            $"merged `{branch}` into `{canonical}` — {landed} commit(s). The tree is still there; "
            + "discard it when you are done with it.");
    }

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

            var canonical = await WorkingTree.DefaultBranchAsync(root, ct).ConfigureAwait(false) ?? "HEAD";
            var (_, unmerged, _) = await WorkingTree.GitAsync(
                root, ["log", "--oneline", $"{canonical}..{branch}"], ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(unmerged))
            {
                return new(false,
                    $"the tree at {path} holds commits `{canonical}` has not taken:\n{unmerged.Trim()}\n"
                    + "Merge them from the root, or say it again with --force to discard them.");
            }
        }

        var removeArgs = force
            ? new[] { "worktree", "remove", "--force", full }
            : ["worktree", "remove", full];
        var (removeCode, _, removeErr) = await WorkingTree.GitAsync(root, removeArgs, ct).ConfigureAwait(false);
        if (removeCode != 0)
        {
            return new(false, $"git would not remove the tree: {FirstLine(removeErr)}");
        }

        // The branch goes with its tree — -d where the pre-check proved it safe, -D where the person
        // forced it. A branch left behind would resurrect "never reused" as a growing pile of names.
        if (branch is not "" and not "HEAD")
        {
            await WorkingTree.GitAsync(root, ["branch", force ? "-D" : "-d", branch], ct).ConfigureAwait(false);
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
