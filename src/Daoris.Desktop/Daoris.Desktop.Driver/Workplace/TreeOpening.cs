using System.Globalization;
using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>A fresh tree opened for a session: the checkout's root proven, the branch made from where the line stands, the tree's record begun.</summary>
internal sealed class TreeOpening(TreeContext context)
{
    private string TreesRoot => context.TreesRoot;
    private SessionBranches Grown => context.Grown;

    /// <summary>
    /// Open a fresh tree for a session in <paramref name="root"/>. Throws <see cref="DriverException"/>
    /// with git's own words when it cannot — before any record exists, which is the caller's contract.
    /// </summary>
    /// <param name="from">
    /// A branch to grow from instead of the canonical line — the one a chain's previous step landed on
    /// in this repository, so the next step's tree holds the unmerged work it builds on or
    /// checks. One that no longer exists falls back to <paramref name="landed"/>, then to the canonical line, and the
    /// answer says so.
    /// </param>
    /// <param name="landed">
    /// The branch the chain's step before landed on, tried when <paramref name="from"/> is gone:
    /// a rule's tidy removes that session branch at its landing, and a step grown from the line could not move the chain's
    /// branch on. With <paramref name="partOf"/>, the branch that part's work landed on.
    /// </param>
    /// <param name="partOf">
    /// For a further part of a session: the earlier part whose tree it would have gone into, gone
    /// since its landing. The tree grows from <paramref name="landed"/> where that branch stands, else from the line, and the
    /// opening says which and whose tree is gone. Without <paramref name="from"/>: a part grows from where its work landed.
    /// </param>
    public async Task<TreeOpened> OpenAsync(
        string root, string repository, string workspace, CancellationToken ct = default, string? from = null, string? landed = null,
        string? partOf = null)
    {
        // The root must BE the top of its own working tree — proven, not assumed. Git resolves a
        // repository by walking UP from wherever it is asked, so a root that is not one (a
        // mis-registered path, a folder inside some other checkout) would silently grow worktrees and
        // branches on whatever repository CONTAINS it. The first run of this class's own test suite
        // did exactly that, to this repository: this check refuses loudly.
        // Asked so that a git that fails and says nothing is reported by what it did (code, time, folder, arguments).
        var top = await AskAsync(root, ["rev-parse", "--show-toplevel"], ct).ConfigureAwait(false);
        if (top.Code != 0)
        {
            throw new DriverException(
                $"`{repository}`'s registered root is not a git repository ({top.FirstWords()}) — "
                + "a session's tree is one git links to its checkout, and there is nothing here to link it to.");
        }

        if (!SamePath(top.Stdout.Trim(), root))
        {
            throw new DriverException(
                $"`{repository}`'s registered root {root} is INSIDE the repository at {top.Stdout.Trim()} "
                + "rather than being one — refusing to grow a tree on a repository the registration "
                + "does not name. Re-run `daoris repository connect` from the actual root.");
        }

        // The repository's line: what the person set for it or its workspace, else the
        // checkout's guess, else the root's HEAD — and the answer SAYS which, because a branch grown
        // from the wrong point is invisible until merge time.
        var line = await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false);
        var basedOn = line.Branch is not null
            ? CanonicalLine.Describe(line)
            : "the root's HEAD (no canonical line is declared)";
        var start = line.Branch is { } set ? await StartOfAsync(root, set, line, repository, ct).ConfigureAwait(false) : "HEAD";

        // A chain's previous step's branch, where it still stands. Session branches are refs
        // of the root's own repository, so a linked worktree grows from one like any other branch.
        string? grewFrom = null;
        if (from is { Length: > 0 })
        {
            var (known, _, _) = await WorkingTree.GitAsync(
                root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{from}"], ct).ConfigureAwait(false);
            var (chain, _, _) = known != 0 && landed is { Length: > 0 } && BranchName.IsValid(landed)
                ? await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{landed}"], ct).ConfigureAwait(false)
                : (1, "", "");
            if (known == 0)
            {
                start = grewFrom = from;
                basedOn = $"`{from}`, the branch the step before it landed on (not merged yet)";
            }
            else if (chain == 0 && landed is not null)
            {
                start = grewFrom = landed;
                basedOn = $"`{landed}`, the branch its chain landed on (not merged yet) — `{from}`, the branch the step before it "
                    + "worked on, is gone";
            }
            else
            {
                basedOn += $" — `{from}`, the branch the step before it landed on, is gone";
            }
        }
        else if (partOf is not null)
        {
            // A further part whose session's tree went at its landing: the branch that work landed on, where it stands.
            var gone = $"the tree session `{partOf}` worked in is gone";
            var (stands, _, _) = landed is { Length: > 0 } && BranchName.IsValid(landed)
                ? await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{landed}"], ct).ConfigureAwait(false)
                : (1, "", "");
            if (stands == 0 && landed is { } standing)
            {
                start = grewFrom = standing;
                basedOn = $"`{standing}`, the branch its session's work landed on (not merged yet) — {gone}";
            }
            else
            {
                basedOn += landed is { Length: > 0 }
                    ? $" — {gone}, and `{landed}`, the branch its work landed on, is gone too"
                    : $" — {gone}, and no branch holds its work here";
            }
        }

        var name = $"s-{Guid.NewGuid().ToString("N")[..8]}";
        var branch = $"daoris/{name}";
        var path = Path.Combine(TreesRoot, workspace, repository, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // A folder there before the add is never this open's to delete.
        var ours = !Directory.Exists(path);

        string? unrecorded = null;
        var dependencies = DependencyCopy.None;
        var inputs = LocalCopy.None;
        var refusedBound = false;
        try
        {
            // 🔴 Long paths: a tree's prefix is longer than the root's, so a repository whose
            // deepest file fits under its root can cross Windows' 260 characters here. The first real
            // workspace did, and every tick's `worktree add` failed. `WorkingTree.GitAsync` says
            // `core.longpaths` on every call, removal and status included.
            // Locked while it opens, the lock's reason naming this process, so a later look tells an open a kill cut
            // off from a person's lock (TreeStrays); let go of below, once where its branch started is recorded.
            var added = await AskAsync(
                root, ["worktree", "add", "--lock", "--reason", TreeOpenings.LockReason(Environment.ProcessId), "-b", branch, path, start], ct)
                .ConfigureAwait(false);
            if (added.Code != 0)
            {
                throw new DriverException(
                    $"could not open a session tree for `{repository}` — git said: {added.Failure()}");
            }

            // A tree git binds to another checkout (a shared `core.worktree` under worktree-specific
            // configuration) is let go of, unused, before a record, a copy or a session is told of it: its agent's own git would act
            // on that checkout. One start of the tree's own, asked once; the undo below takes the folder and the branch.
            if (await TreeBinding.RefusalAsync(path, ct).ConfigureAwait(false) is { } bound)
            {
                refusedBound = true;
                throw new DriverException($"could not open a session tree for `{repository}` — {bound}.");
            }

            // Where the branch started, the moment it exists: bringing it up to date after the line moves cuts
            // here, so a chain's step replays only its own commits once the step before's are on the line.
            var (headCode, head, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}"], ct)
                .ConfigureAwait(false);
            if (headCode == 0)
            {
                try
                {
                    // The branch work unit's id, written once here; every later record of the branch keeps it.
                    Grown.Record(new GrownBranch(repository, workspace, branch, line.Branch, head.Trim(), grewFrom, DateTimeOffset.UtcNow)
                    {
                        Unit = GrownBranch.NewUnit(),
                    });
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    unrecorded = error.Message;
                }
            }

            // The dependencies the checkout installed at this tree's lockfile, copied in while the tree is still locked as
            // opening, so a cut-off open's undo takes a half copy with the tree. Copied, never linked.
            dependencies = await TreeDependencies.CopyAsync(root, path, ct).ConfigureAwait(false);
            // The machine-local files its gates read, as the checkout's gates file declares them, copied read-only under
            // the same lock, so its gates run in the tree the checks they run in the checkout.
            inputs = await TreeLocalInputs.CopyAsync(root, path, ct).ConfigureAwait(false);

            var unlocked = await AskAsync(root, ["worktree", "unlock", path], ct).ConfigureAwait(false);
            if (unlocked.Code != 0)
            {
                throw new DriverException(
                    $"could not open a session tree for `{repository}` — git would not let go of the lock it opened it under: {unlocked.Failure()}");
            }
        }
        catch
        {
            // 🔴 `worktree add -b` makes the branch before it checks anything out, so a failure would leave one behind on
            // every tick. And a cancel as the app closes cuts an open off inside the add,
            // leaving a half-checked-out tree, locked, and its branch. Whatever cut it off, the branch
            // and the tree are this call's own, made a moment ago under a name nothing else uses and handed to nobody, so they
            // go, and the repository is left as it was found. A kill leaves them for the look (TreeStrays).
            await TreeOpenings.UndoAsync(root, path, branch, ours, unused: refusedBound).ConfigureAwait(false);
            throw;
        }

        // A tree opened is Daoris's own ref move; its repository is watched from here, and judged at once (§B4).
        RefMoves.Mine(context.Home, repository, workspace, root);

        // What it grew from as a fact: the branch it started at, never the sentence spelling it for a person.
        var opening = OpeningOf(path, branch, basedOn, grewFrom ?? line.Branch ?? "HEAD", unrecorded, dependencies, inputs);
        var told = string.Join(" ", new[] { dependencies.Told, inputs.Told }.OfType<string>());
        return new(path, branch, basedOn, opening.Note, grewFrom) { Opening = opening, Dependencies = told.Length == 0 ? null : told };
    }

    /// <summary>
    /// The opening's sentence, price included, with its codes (the language design §4 rows 25–26): its branch and the
    /// branch it started at as facts, never its path, which only the English names and the cleaner cuts where it travels. Where
    /// its checkout's dependencies were copied in, its own code says them; each project that copied none says why. The
    /// machine-local files its gates read, copied in read-only, are a line after those, and each declared one not copied
    /// says why.
    /// </summary>
    /// <param name="basedOn">What it grew from, spelled for a person.</param>
    /// <param name="startedAt">The branch it started at, or <c>HEAD</c>: the fact the page says.</param>
    /// <param name="unrecorded">Why where its branch started is not recorded, in the program's words; null where it is.</param>
    /// <param name="dependencies">What was copied of the checkout's dependencies, and why not the rest; none by default.</param>
    /// <param name="inputs">What was copied of the machine-local inputs the checkout's gates file declares, and why not the rest; none by default.</param>
    internal static Noted OpeningOf(
        string path, string branch, string basedOn, string startedAt, string? unrecorded, DependencyCopy? dependencies = null,
        LocalCopy? inputs = null)
    {
        dependencies ??= DependencyCopy.None;
        inputs ??= LocalCopy.None;
        // 🔴 A stored shape is never changed in place: `started.tree` still says no installed dependencies, as every opening
        // recorded earlier does, and a tree its checkout's were copied into says it under a code of its own.
        var opening = dependencies.Copied.Count > 0
            ? Noted.Of(
                StartNoteCodes.StartedTreeCopied,
                $"opened a session tree at {path} on `{branch}`, from {basedOn} — a fresh tree holds what git tracks, and here the "
                + $"dependencies your checkout installed at the same lockfile, copied in: {dependencies.Listed}. `npm ci` is not "
                + "needed there unless a lockfile changes; the rest of the repository's own setup cost, build outputs included, is "
                + "paid here, and in exchange the root's uncommitted work holds nothing.",
                ("branch", branch), ("basedOn", startedAt), ("copied", (IReadOnlyList<string>)[.. dependencies.Copied]))
            : Noted.Of(
                StartNoteCodes.StartedTree,
                $"opened a session tree at {path} on `{branch}`, from {basedOn} — a fresh tree holds "
                + "nothing git does not track: no installed dependencies, no build outputs. The "
                + "repository's own setup cost is paid here, and in exchange the root's uncommitted work "
                + "holds nothing.",
                ("branch", branch), ("basedOn", startedAt));
        foreach (var each in dependencies.Declined) opening = opening.Then(" ", each.Line());
        if (inputs.Line() is { } copied) opening = opening.Then(" ", copied);
        foreach (var each in inputs.Declined) opening = opening.Then(" ", each.Line());
        if (unrecorded is null) return opening;

        var line = Noted.Of(
            StartNoteCodes.StartedTreeUnrecorded,
            $"Daoris could not record where its branch started ({unrecorded}), so bringing it up to date "
            + "will cut where its work first differs from the line.");
        return opening.Then(" ", unrecorded.Length == 0 ? line : line.Also(NotePart.Said(unrecorded, NoteBy.Program)));
    }

    /// <summary>
    /// Where a tree on <paramref name="branch"/> grows from: the branch itself where this checkout has
    /// it, its copy on <c>origin</c> where only that exists, and a refusal for a line set to a branch
    /// the checkout has nowhere — a tree grown from the wrong point is invisible until merge time.
    /// </summary>
    private static async Task<string> StartOfAsync(string root, string branch, Line line, string repository, CancellationToken ct)
    {
        // The checkout's own guess is the branch either way (its answer is the same where it stands or not), so git is
        // not asked whether it does; a line the person set is the one checked.
        if (line.Source == LineSource.Checkout) return branch;
        var (local, _, _) = await WorkingTree.GitAsync(
            root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}"], ct).ConfigureAwait(false);
        if (local == 0) return branch;

        var (remote, _, _) = await WorkingTree.GitAsync(
            root, ["rev-parse", "--verify", "--quiet", $"refs/remotes/origin/{branch}"], ct).ConfigureAwait(false);
        if (remote == 0) return $"origin/{branch}";

        throw new DriverException(
            $"{CanonicalLine.Describe(line)} is `{repository}`'s line, and its checkout has no such branch, here "
            + $"or on origin. Fetch it, or set another with `daoris place line {repository} <branch>`.");
    }

    // The callers' calls, one line each: each part's doc is on its class.
}
