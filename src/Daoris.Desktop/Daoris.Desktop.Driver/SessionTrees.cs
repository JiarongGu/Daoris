using System.Globalization;

namespace Daoris.Driver;

/// <param name="Path">Where the tree is — what the session record carries and the lock keys on.</param>
/// <param name="Branch">The session's own branch, never reused; git itself refuses a second use.</param>
/// <param name="BasedOn">Which point the branch grew from, spelled for a person — the canonical line, or HEAD with the reason.</param>
/// <param name="Sentence">The whole act in one sentence, price included — for whoever is watching.</param>
/// <param name="GrewFrom">The chain step's branch it grew from (CHAIN2), when it did — null for the canonical line.</param>
public sealed record TreeOpened(string Path, string Branch, string BasedOn, string Sentence, string? GrewFrom = null)
{
    /// <summary>
    /// <see cref="Sentence"/> with its codes (LANG1a, the language design §4 rows 25–26): the tree's branch and what it grew
    /// from as facts, never its path; and where its start went unrecorded, that line with git's words beside it.
    /// </summary>
    public Noted? Opening { get; init; }
}

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
/// force, meaning it. Commits whose work a squash merge or a cherry-pick holds by content lose nothing,
/// and go unforced saying where that work is (SQUASHTIDY1), the commits themselves kept on a recovery
/// ref the sentence names. A branch goes only while it is the commit judged, and a tree only while
/// nothing in it is uncommitted or ignored and its alone (SQUASHTIDY1c).</para>
///
/// <para><b>The name is minted here, not borrowed from the session id</b>, because the order is
/// tree → open → spawn: the ledger's open needs the tree path for the lock, so the id it would borrow
/// does not exist yet. Short and random, like the id itself; the record ties the two together.</para>
/// </remarks>
/// <param name="plugins">
/// The plugin wire a branch rule's landing speaks (WSR4, D100) — the home's own plugins by default; the
/// shell and the terminal hand one that says the plugin's lines where each says things.
/// </param>
public sealed partial class SessionTrees(string home, LandingPlugins? plugins = null)
{
    private readonly LandingPlugins _plugins = plugins ?? new LandingPlugins(home);

    /// <summary>
    /// The test seam (SQUASHTIDY1c): run with the branch's name between a judgement and the delete it cleared, where a test
    /// moves the branch as a session or a person could. Production sets none.
    /// </summary>
    internal Func<string, Task>? BeforeDeleting { get; init; }

    /// <summary>The branches this machine's landings made (WSR5) — the only ones the clean-up and the hand-off act on.</summary>
    public LandedBranches Recorded => new(home);

    /// <summary>Where this machine's session branches started (WSR6) — what bringing one up to date cuts at.</summary>
    public SessionBranches Grown => new(home);

    /// <summary>Every tree this machine's Daoris has opened lives under here, and only here.</summary>
    public string TreesRoot => Path.Combine(home, "trees");

    /// <summary>
    /// Open a fresh tree for a session in <paramref name="root"/>. Throws <see cref="DriverException"/>
    /// with git's own words when it cannot — before any record exists, which is the caller's contract.
    /// </summary>
    /// <param name="from">
    /// A branch to grow from instead of the canonical line — the one a chain's previous step landed on
    /// in this repository (CHAIN2), so the next step's tree holds the unmerged work it builds on or
    /// checks. One that no longer exists falls back to <paramref name="landed"/>, then to the canonical line, and the
    /// answer says so.
    /// </param>
    /// <param name="landed">
    /// The branch the chain's step before landed on (LAND2c, D82 as D145 amends it), tried when <paramref name="from"/> is gone:
    /// a rule's tidy removes that session branch at its landing, and a step grown from the line could not move the chain's
    /// branch on.
    /// </param>
    public async Task<TreeOpened> OpenAsync(
        string root, string repository, string workspace, CancellationToken ct = default, string? from = null, string? landed = null)
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
                + "a session's tree is one git links to its checkout, and there is nothing here to link it to.");
        }

        if (!SamePath(toplevel.Trim(), root))
        {
            throw new DriverException(
                $"`{repository}`'s registered root {root} is INSIDE the repository at {toplevel.Trim()} "
                + "rather than being one — refusing to grow a tree on a repository the registration "
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

        // Where the branch started (WSR6), the moment it exists: bringing it up to date after the line moves cuts
        // here, so a chain's step replays only its own commits once the step before's are on the line.
        string? unrecorded = null;
        var (headCode, head, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}"], ct)
            .ConfigureAwait(false);
        if (headCode == 0)
        {
            try
            {
                Grown.Record(new GrownBranch(repository, workspace, branch, line.Branch, head.Trim(), grewFrom, DateTimeOffset.UtcNow));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                unrecorded = error.Message;
            }
        }

        // What it grew from as a fact (LANG1a): the branch it started at, never the sentence spelling it for a person.
        var opening = OpeningOf(path, branch, basedOn, grewFrom ?? line.Branch ?? "HEAD", unrecorded);
        return new(path, branch, basedOn, opening.Note, grewFrom) { Opening = opening };
    }

    /// <summary>
    /// The opening's sentence, price included, with its codes (LANG1a, the language design §4 rows 25–26): its branch and the
    /// branch it started at as facts, never its path, which only the English names and the cleaner cuts where it travels.
    /// </summary>
    /// <param name="basedOn">What it grew from, spelled for a person.</param>
    /// <param name="startedAt">The branch it started at, or <c>HEAD</c>: the fact the page says.</param>
    /// <param name="unrecorded">Why where its branch started was not recorded (WSR6), in the program's words; null where it was.</param>
    internal static Noted OpeningOf(string path, string branch, string basedOn, string startedAt, string? unrecorded)
    {
        var opening = Noted.Of(
            NoteCodes.StartedTree,
            $"opened a session tree at {path} on `{branch}`, from {basedOn} — a fresh tree holds "
            + "nothing git does not track: no installed dependencies, no build outputs. The "
            + "repository's own setup cost is paid here, and in exchange the root's uncommitted work "
            + "holds nothing.",
            ("branch", branch), ("basedOn", startedAt));
        if (unrecorded is null) return opening;

        var line = Noted.Of(
            NoteCodes.StartedTreeUnrecorded,
            $"Daoris could not record where its branch started ({unrecorded}), so bringing it up to date "
            + "will cut where its work first differs from the line.");
        return opening.Then(" ", unrecorded.Length == 0 ? line : line.Also(NotePart.Said(unrecorded, NoteBy.Program)));
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
        // The line moved: the registry follows it at the next look (WSSETUP5, D124 §3.1), whichever door pressed this.
        RegistryFollowing.Moved(home, repository, DateTimeOffset.UtcNow);
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
            // Who pushes it, said before the press, and what would refuse the press where something would (D100).
            var plugin = landing.Rule.Plugin;
            return new(LandingForm.Branch, LandingRules.Expand(landing.Rule.Pattern!, NamesOf(subject, repository)), landing.Source,
                plugin, plugin is null ? null : _plugins.Problem(plugin), landing.Rule.AutoAccept);
        }

        var (code, commonDir, _) = Directory.Exists(full)
            ? await WorkingTree.GitAsync(full, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct).ConfigureAwait(false)
            : (1, "", "");
        var line = code == 0
            ? (await LineAsync(Path.GetDirectoryName(commonDir.Trim())!, repository, workspace, ct).ConfigureAwait(false)).Branch
            : null;
        return new(LandingForm.Merge, line ?? "", landing.Source);
    }

    /// <summary>
    /// The review's gate for this tree (REVIEWENV1c, D154 point 7; <see cref="ReviewGate.ReadAsync"/>): the chain its quest is a step
    /// of, the review rule standing for the tree's repository here, and git's ancestry from the tree's <c>HEAD</c>. Every door that
    /// lands asks it and hands it to <see cref="LandAsync"/>; a conversation, with no quest, has nothing to wait for.
    /// </summary>
    public Task<ReviewGateState> ReviewAsync(string path, string? quest, IReviewWorld world, CancellationToken ct = default)
    {
        var full = Path.GetFullPath(path);
        var (workspace, repository) = OwnerOf(full);
        return ReviewGate.ReadAsync(world, Config(), full, repository, workspace, quest, ct);
    }

    /// <summary>What a landing says of the tree it leaves — dropped when a tidy removed it.</summary>
    private const string TreeStays = " The tree is still there; discard it when you are done with it.";

    /// <summary>
    /// The press on a reviewed session (WSR1, D87): its work lands as its repository's rule says —
    /// merged into the line (<see cref="MergeAsync"/>), or put on a new branch for the person to push, or
    /// for the plugin the rule names to push and open the pull request from (WSR4, D100). A chain's later
    /// done moves the branch its first landing made on instead, and grows that pull request (LAND2c, D149).
    /// </summary>
    /// <remarks>
    /// 🔴 <b>The plugin is spoken to after the branch exists, and never instead of it.</b> A plugin the
    /// press cannot use — gone, off, landing nothing — refuses the press before anything is made; one that
    /// fails once the branch is made leaves the branch standing, and the sentence says its step failed and
    /// how the person does it by hand. Nothing here pushes: the plugin's process does.
    /// <para>🔴 <b>Work the line already holds by content is never landed again</b> (SQUASHTIDY1f): where a squash merge or a
    /// cherry-pick put the tree's commits on the line or a branch of the person's, as the session's head reads it
    /// (<see cref="DiscardOfferAsync"/>, SQUASHTIDY1b), every door is refused here, before the review's gate and before anything
    /// is made, in the head's clause and with <see cref="AutoLandingCode.Carried"/>: a merge would copy the work onto the line
    /// again, or conflict with it, and a branch would carry it to a second pull request. Its discard is the way.</para>
    /// </remarks>
    /// <param name="inUse">
    /// The trees sessions still running or waiting name, asked when the rule's tidy reaches the other session branches the
    /// landed work holds (LAND3); null keeps every one of those that still has a tree, since nobody asked.
    /// </param>
    /// <param name="acceptedBy">
    /// Who accepts it (LAND2b, D145), kept on the landing record: the person's press, the default, or the rule's switch at the
    /// quest's done. 🔴 One thing differs for <see cref="AcceptedBy.Auto"/>: a plugin that cannot land work here does not stop
    /// the branch, since nobody is there to fix it, so the branch is made and recorded and the push is not tried.
    /// </param>
    /// <param name="gate">
    /// The landing gate for this tree (<see cref="GateAsync"/>), read once by the door that lands it: the second opinion
    /// (XAGENT1f, D155 point 9) and then D154's look (REVIEWENV1c, D154 point 7). The first part that does not let go refuses before
    /// anything is made, merge or branch, an advance among them, with its sentence and its code
    /// (<see cref="AutoLandingCode.Opinion"/>, then <see cref="AutoLandingCode.Unreviewed"/>); what let each go is kept on the
    /// landing record. Null is a door that asked none.
    /// </param>
    public async Task<TreeLanding> LandAsync(
        string path, LandingSubject subject, CancellationToken ct = default, Func<CancellationToken, Task<IReadOnlySet<string>>>? inUse = null,
        string acceptedBy = AcceptedBy.Person, LandingGate? gate = null)
    {
        var full = Path.GetFullPath(path);
        // SQUASHTIDY1f: the head's own judgement, asked first, since a second opinion or a review of work already on the line
        // lands nothing either.
        if (await DiscardOfferAsync(full, ct).ConfigureAwait(false) is { } carried)
        {
            return new(false, carried.NotLanded) { Refusal = AutoLandingCode.Carried };
        }

        // One gate, in a fixed order (the second-agent design §7): the second opinion, then the person's review where it runs. A
        // merge rule waits as a branch rule does, and an advance waits the same way.
        if (gate?.Refusal is { } refused) return refused;
        var review = gate?.Review;

        var (workspace, repository) = OwnerOf(full);
        var landing = LandingRules.Choose(Config(), repository, workspace);
        // 🔴 Read again here, not only where the look chose it (LAND2b): a rule changed between the two must never let a landing
        // at done merge into the person's checkout with no press (D145 point 1, D51 rule 6), nor land under a switch now off.
        if (acceptedBy == AcceptedBy.Auto && landing.Rule is not { Form: LandingForm.Branch, AutoAccept: true })
        {
            return new(false, "its repository's rule no longer accepts automatically, so nothing was landed: your Accept lands it.")
            {
                Refusal = AutoLandingCode.Off,
            };
        }

        TreeLanding landed;
        if (landing.Rule.Form != LandingForm.Branch)
        {
            var merged = await MergeAsync(path, ct).ConfigureAwait(false);
            landed = new(merged.Merged, merged.Message);
        }
        else
        {
            var plugin = landing.Rule.Plugin;
            var unready = plugin is null ? null : _plugins.Problem(plugin);
            if (unready is not null && acceptedBy != AcceptedBy.Auto)
            {
                return new(false, $"{unready} Nothing was landed: the rule hands the branch to that plugin, so fix the "
                    + "plugin or the rule (`daoris driver landing`), then accept again.");
            }

            var branched = await BranchAsync(full, workspace, repository,
                LandingRules.Expand(landing.Rule.Pattern!, NamesOf(subject, repository)), handedOn: plugin is not null, ct).ConfigureAwait(false);
            landed = branched.Landing;
            // Recorded the moment it exists (WSR5): the branch is Daoris's to judge and to hand on only
            // because this line wrote it down, at the commit it was made at.
            var tip = branched.Commits.Count > 0 ? branched.Commits[^1].Sha : null;
            if (landed.Landed && tip is not null && landed.AdvancedFrom is { } advancedFrom)
            {
                // Moved on (LAND2c): the entry's tip is the new commit, and the advance is kept with who accepted it (D143).
                landed = Remember(landed, () => Recorded.Advanced(repository, landed.Branch!,
                    new LandedAdvance(advancedFrom, tip, DateTimeOffset.UtcNow, subject.Session)
                    {
                        AcceptedBy = acceptedBy, Review = review?.Landing, Opinion = gate?.Opinion.Landing,
                    }));
            }
            else if (landed.Landed && tip is not null)
            {
                // Where its work grew from (WSR6): the session branch's start, so bringing it up to date replays only its own.
                var from = branched.Source is { } source ? Grown.Of(repository, source)?.From : null;
                landed = Remember(landed, () => Recorded.Record(new LandedBranch(
                    repository, workspace, landed.Branch!, branched.Base, tip, subject.Session, subject.Quest, subject.Title,
                    DateTimeOffset.UtcNow)
                {
                    From = from,
                    // Who accepted it, and the rule as it stood (LAND2b, D145 point 6): a choice keeps its facts (D143).
                    AcceptedBy = acceptedBy,
                    Rule = new LandedRule(plugin, landing.Rule.AutoAccept, landing.Source),
                    // The review that let it go (REVIEWENV1c, design §3.5): reviewed with its set-up's commit, or skipped.
                    Review = review?.Landing,
                    // The second opinion that let it go (XAGENT1f, design §8.6), or why it landed without one.
                    Opinion = gate?.Opinion.Landing,
                }));
            }

            if (landed.Landed && unready is not null)
            {
                // At done (D145 point 2): the branch is the half Daoris owns, so it stands, and the hand-off pushes it later (D102).
                landed = landed with
                {
                    Unready = unready,
                    Message = landed.Message.Replace(TreeStays, "", StringComparison.Ordinal) + $" {Sentence(unready)} So its push was not "
                        + $"tried: the branch stands, and `daoris-driver trees hand {subject.Session}` hands it to `{plugin}` once it can "
                        + "land work here." + TreeStays,
                };
            }
            else if (landed.Landed && plugin is not null)
            {
                // An advance grows the pull request its first landing opened (LAND2c, D149 point 4): the plugin is told it, so it
                // pushes and opens no second one, and is told who accepted the work, so its description says so truly.
                var open = landed.AdvancedFrom is null ? null : Recorded.Of(repository, landed.Branch!) is { Pushed: true } entry ? entry.PullRequest : null;
                var said = await _plugins.LandAsync(plugin, new LandingFrame(
                    repository, workspace, branched.Root, landed.Branch!, branched.Base, subject.Title, subject.Quest,
                    subject.Session, branched.Commits, open, acceptedBy), ct).ConfigureAwait(false);
                landed = landed with
                {
                    Plugin = said,
                    // The plugin's word goes before the sentence about the tree, which a tidy may take out.
                    Message = landed.Message.Replace(TreeStays, "", StringComparison.Ordinal) + " " + Said(said, landed.Branch!) + TreeStays,
                };
                if (said is { Pushed: true, Failed: false } && tip is not null)
                {
                    landed = Remember(landed, () => Recorded.Pushed(repository, landed.Branch!, said, tip));
                }
            }
        }

        // The tidy the person's rule asked for (D88): the tree and its branch go once the work lands, behind
        // the same proof an unforced removal makes — never forced.
        if (!landed.Landed || !landing.Rule.Tidy) return landed;
        // Read before the tree goes (LAND3): the repository it belongs to and the branch it was on.
        var (factsCode, commonDir, _) = await WorkingTree.GitAsync(full, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct)
            .ConfigureAwait(false);
        var (_, pressedOut, _) = await WorkingTree.GitAsync(full, ["rev-parse", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
        var tidied = await RemoveAsync(path, force: false, ct).ConfigureAwait(false);
        landed = landed with
        {
            // 🔴 The sentence that the tree stays goes when the tidy removed it: the message said both
            // (found landing TK-2202, 2026-09-29).
            Message = tidied.Removed
                ? landed.Message.Replace(TreeStays, "", StringComparison.Ordinal) + $" Cleaned up, as the rule says: {tidied.Message}"
                : landed.Message + $" The rule says to clean up, and the tree stays: {tidied.Message}",
        };
        if (factsCode != 0) return landed with { Tidied = [] };

        // LAND3: every other session branch the landed work holds goes too — a chain's earlier step, whose commits rode
        // into this one, stayed behind on the owner's install. The ref is the branch made, or the line a merge went into.
        var root = Path.GetDirectoryName(commonDir.Trim())!;
        var into = landing.Rule.Form == LandingForm.Branch
            ? landed.Branch
            : (await LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
        if (into is null) return landed with { Tidied = [] };
        var held = await TidyHeldAsync(root, repository, workspace, $"refs/heads/{into}", pressedOut.Trim(), inUse, ct).ConfigureAwait(false);
        return landed with { Message = landed.Message + TidiedSaid(held, into), Tidied = held };
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

    /// <summary>
    /// How many commits a session tree's HEAD holds beyond its repository's line (LAND2b): zero is a done that made no commits,
    /// which lands nothing; null is git unable to say, or no line it can name, and the landing then decides.
    /// </summary>
    public async Task<int?> AheadOfLineAsync(string tree, CancellationToken ct = default)
    {
        var full = Path.GetFullPath(tree);
        if (!Holds(full) || !Directory.Exists(full)) return null;
        var (code, commonDir, _) = await WorkingTree.GitAsync(full, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct)
            .ConfigureAwait(false);
        if (code != 0) return null;
        var root = Path.GetDirectoryName(commonDir.Trim())!;
        var (workspace, repository) = OwnerOf(full);
        var line = (await LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
        var against = line is null ? null : await ComparableAsync(root, line, ct).ConfigureAwait(false);
        if (against is null) return null;
        var (countCode, count, _) = await WorkingTree.GitAsync(full, ["rev-list", "--count", $"{against}..HEAD"], ct).ConfigureAwait(false);
        return countCode == 0 && int.TryParse(count.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var ahead) ? ahead : null;
    }

    /// <summary>A tree's workspace and repository, read from the layout this home chose (<c>trees/&lt;workspace&gt;/&lt;repository&gt;/&lt;name&gt;</c>).</summary>
    public (string Workspace, string Repository) Owner(string tree) => OwnerOf(Path.GetFullPath(tree));

    private static Task<(int Code, string Stdout, string Stderr)> UnlandedLogAsync(string cwd, string revision, CancellationToken ct) =>
        WorkingTree.GitAsync(cwd,
            ["log", "--oneline", revision, "--not", "--exclude=daoris/*", "--branches", "--exclude=*/daoris/*", "--remotes"], ct);

    /// <summary>
    /// Every session branch in these repositories, with what it holds (D88) — the list a person reads
    /// before the clean-up is pressed. Nothing is changed.
    /// </summary>
    /// <param name="inUse">The trees sessions still running or waiting name — kept whatever they hold.</param>
    /// <param name="only">The branches to judge, as <c>repository:branch</c>; null for every one. The look's own tidy (AUTOTIDY1) judges its few.</param>
    public async Task<IReadOnlyList<SweepItem>> SweepPlanAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        CancellationToken ct = default, IReadOnlySet<string>? only = null)
    {
        var busy = new HashSet<string>(inUse.Select(Normal), StringComparer.OrdinalIgnoreCase);
        var items = new List<SweepItem>();
        foreach (var (repository, workspace, root) in repositories)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;
            var (code, refs, _) = await WorkingTree.GitAsync(
                root, ["for-each-ref", "--format=%(refname:short)", "refs/heads/daoris/"], ct).ConfigureAwait(false);
            if (code != 0) continue;

            var named = refs.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(branch => only is null || only.Contains($"{repository}:{branch}"))
                .ToList();
            // A list git could not give is never read as "no tree here" (AUTOTIDY1): every branch is kept, saying why.
            if (await ReadWorktreesAsync(root, ct).ConfigureAwait(false) is not { } worktrees)
            {
                items.AddRange(named.Select(branch => new SweepItem(
                    repository, RemoteTarget.Workspace(workspace), branch, null, SweepKind.Unlanded, 0, null, $"git could not tell: {ListUnread}")
                {
                    Unread = true,
                }));
                continue;
            }

            var line = (await LineAsync(root, repository, RemoteTarget.Workspace(workspace), ct).ConfigureAwait(false)).Branch;
            // The completed pull requests the record keeps, confirmed here (PLUGHOOK1a): nothing is asked at a list or a press.
            var carriers = await CarriersAsync(root, repository, line, ct).ConfigureAwait(false);
            foreach (var branch in named)
            {
                items.Add((await JudgeAsync(
                    root, repository, RemoteTarget.Workspace(workspace), branch, worktrees.GetValueOrDefault(branch), line, busy, carriers, ct)
                    .ConfigureAwait(false)).Item);
            }
        }

        return items;
    }

    /// <summary>
    /// The clean-up (D88): every session branch the proof clears goes, with its tree, and the rest are
    /// kept and named. 🔴 Each is judged again right before it goes — a list is a fact about a moment.
    /// </summary>
    /// <param name="only">The branches the person saw listed to go, as <c>repository:branch</c>; null for every one the proof clears now.</param>
    public Task<IReadOnlyList<SweepResult>> SweepAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        IReadOnlySet<string>? only = null, CancellationToken ct = default) =>
        SweepAsync(repositories, inUse, only, byItself: false, ct);

    /// <summary>
    /// The clean-up's one path, for both doors (AUTOTIDY1): the person's press takes every row the proof clears
    /// (<see cref="SweepItem.Removable"/>) and removes its tree; the look's own tidy takes the empty alone
    /// (<see cref="GoesByItself"/>), asks its last guards (<see cref="LastLookAsync"/>), and moves the tree's folder aside
    /// rather than deleting it (<see cref="MoveAsideAsync"/>). The judgement, the judgement again, and the delete at the commit
    /// judged are the same for both.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>A destructive step is never cancelled halfway</b> (AUTOTIDY1): once a branch's removal begins, each git command is
    /// awaited to its end whatever <paramref name="ct"/> says, so a closing loop never lets go of the repository's hold while
    /// git is still removing. Cancellation is heard between branches.
    /// </remarks>
    private async Task<IReadOnlyList<SweepResult>> SweepAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        IReadOnlySet<string>? only, bool byItself, CancellationToken ct)
    {
        var known = repositories.ToList();
        var busy = new HashSet<string>(inUse.Select(Normal), StringComparer.OrdinalIgnoreCase);
        Func<SweepItem, bool> goes = byItself ? GoesByItself : item => item.Removable;
        var results = new List<SweepResult>();
        foreach (var item in await SweepPlanAsync(known, inUse, ct, only).ConfigureAwait(false))
        {
            if (only is not null && !only.Contains($"{item.Repository}:{item.Branch}"))
            {
                continue;
            }

            if (!goes(item))
            {
                results.Add(new(item, false, "kept"));
                continue;
            }

            var root = known.First(r => string.Equals(r.Repository, item.Repository, StringComparison.OrdinalIgnoreCase)).Root!;
            if (await ReadWorktreesAsync(root, ct).ConfigureAwait(false) is not { } worktrees)
            {
                results.Add(new(item with { Unread = true, Detail = ListUnread }, false, $"{ListUnread}, so it is kept") { Kept = TidyKept.Unread });
                continue;
            }

            var line = (await LineAsync(root, item.Repository, item.Workspace, ct).ConfigureAwait(false)).Branch;
            var carriers = item.Kind == SweepKind.Carried ? await CarriersAsync(root, item.Repository, line, ct).ConfigureAwait(false) : [];
            // The commit it was judged at (SQUASHTIDY1c): the one its delete must still find, and the one a pull request's answer
            // removed, for the record of it (PLUGHOOK1a, design §2.5).
            var (now, judged) = await JudgeAsync(
                    root, item.Repository, item.Workspace, item.Branch, worktrees.GetValueOrDefault(item.Branch), line, busy, carriers, ct)
                .ConfigureAwait(false);
            if (!goes(now) || judged is null)
            {
                results.Add(new(now, false, "it changed since the list, and is kept"));
                continue;
            }

            if (byItself && await LastLookAsync(root, now, ct).ConfigureAwait(false) is { } last)
            {
                results.Add(new(now, false, last.Sentence) { Kept = last.Code });
                continue;
            }

            // From here each step removes, or finishes a removal: none is cancelled halfway.
            var done = CancellationToken.None;

            // Held by content (SQUASHTIDY1c): its commits are kept on a ref of their own before anything goes.
            string? kept = null;
            if (now.HeldBy is not null)
            {
                var (reference, why) = await KeepDiscardedAsync(root, now.Branch, judged, done).ConfigureAwait(false);
                if (reference is null)
                {
                    results.Add(new(now, false, $"Daoris could not keep its commits on a ref of their own ({why}), so it is kept"));
                    continue;
                }

                kept = reference;
            }

            string? left = null;
            string? movedTo = null;
            if (now.Tree is { } tree && byItself)
            {
                // The look never deletes a tree's folder: a write that lands after its last look lands in the folder moved aside.
                var moved = await MoveAsideAsync(root, now, done).ConfigureAwait(false);
                if (!moved.Gone)
                {
                    results.Add(new(now, false, moved.Sentence) { Kept = moved.Code });
                    continue;
                }

                movedTo = moved.To;
            }
            else if (now.Tree is { } pressed)
            {
                var (removeCode, _, removeErr) = await WorkingTree.GitAsync(
                    root, ["worktree", "remove", pressed], done).ConfigureAwait(false);
                if (removeCode != 0)
                {
                    // A tree git let go of whose folder something holds open: the tree is gone all the same.
                    var (letGo, why) = await LetGoAsync(root, pressed, done).ConfigureAwait(false);
                    if (!letGo)
                    {
                        await DropKeptAsync(root, now.Branch, kept, judged, done).ConfigureAwait(false);
                        results.Add(new(now, false, $"git would not remove its tree: {FirstLine(removeErr)}"));
                        continue;
                    }

                    left = why;
                }
            }

            // Only while it is still the commit judged (SQUASHTIDY1c); git's own -d asks only about the checkout's HEAD.
            var stays = await DeleteJudgedAsync(root, now.Branch, judged, done).ConfigureAwait(false);
            if (stays is not null)
            {
                await DropKeptAsync(root, now.Branch, kept, judged, done).ConfigureAwait(false);
                var treeSaid = movedTo is not null ? $"; its tree's folder was moved aside to {movedTo}"
                    : now.Tree is null ? "" : "; its tree was removed";
                results.Add(new(now, false, stays == MovedSince
                    ? $"{MovedSince}, and is kept" + treeSaid
                    : $"git would not delete the branch: {stays}" + (movedTo is null ? "" : treeSaid))
                {
                    Kept = byItself ? TidyKept.Refused : null,
                    MovedTo = movedTo,
                });
                continue;
            }

            var removed = movedTo is not null ? $"removed; its tree's folder was moved aside to {movedTo}"
                : (now.Tree is null ? "removed" : "removed, with its tree") + (left is null ? "" : $". {left}");
            results.Add(new(now, true, kept is null ? removed : $"{Sentence(removed)} {ContentHold.KeptAt(now.Branch, kept)}")
            {
                MovedTo = movedTo,
            });
            if (now.CarriedBy is { } carrier)
            {
                try
                {
                    Recorded.Carried(carrier, new CarriedBranch(now.Branch, judged, _plugins.Now, CarriedBy.CleanUp));
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    // The branch went on a confirmed answer; only the note of it on the record is lost.
                }
            }
        }

        return results;
    }

    /// <summary>
    /// One session branch judged (D88, PLUGHOOK1a, SQUASHTIDY1), and the commit it was judged at: every question is asked of
    /// that one id, read once, and a removal deletes the branch only while it is still that commit (SQUASHTIDY1c). Null where
    /// git could not read it.
    /// </summary>
    /// <param name="carriers">The completed pull requests the record keeps that carried work here (PLUGHOOK1a); a branch they carried goes.</param>
    private async Task<(SweepItem Item, string? Tip)> JudgeAsync(
        string root, string repository, string workspace, string branch, string? tree, string? line,
        HashSet<string> busy, IReadOnlyList<PullRequestCarrier> carriers, CancellationToken ct)
    {
        // The ignored paths its tree shares with the checkout, on every row once read (AUTOTIDY1): the look's own tidy keeps such a tree.
        var shared = 0;
        SweepItem Item(string kind, int commits = 0, string? where = null, string? detail = null) =>
            new(repository, workspace, branch, tree, kind, commits, where, detail) { IgnoredShared = shared };

        if (tree is not null && busy.Contains(Normal(tree))) return (Item(SweepKind.InUse), null);

        // A registration whose folder is gone holds nothing on disk; git cannot be asked in it, and its removal says so.
        if (tree is not null && Directory.Exists(tree))
        {
            // Everything no commit holds, whatever git's settings hide (SQUASHTIDY1c): what a removal would destroy.
            var holds = await HoldsAsync(tree, root, ct).ConfigureAwait(false);
            if (holds is null) return (Item(SweepKind.Dirty, detail: "git could not say what its tree holds") with { Unread = true }, null);
            shared = holds.Shared.Count;
            if (holds.Uncommitted.Count > 0) return (Item(SweepKind.Dirty, detail: $"{holds.Uncommitted.Count} path(s) uncommitted"), null);
            if (holds.Ignored.Count > 0) return (Item(SweepKind.Dirty, detail: holds.IgnoredSaid), null);
        }

        var (tipCode, tipOut, tipErr) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}^{{commit}}"], ct)
            .ConfigureAwait(false);
        if (tipCode != 0) return (Item(SweepKind.Unlanded, detail: $"git could not tell: {FirstLine(tipErr)}") with { Unread = true }, null);
        var tip = tipOut.Trim();

        var (code, log, err) = await UnlandedLogAsync(root, tip, ct).ConfigureAwait(false);
        if (code != 0) return (Item(SweepKind.Unlanded, detail: $"git could not tell: {FirstLine(err)}") with { Unread = true }, null);
        var unlanded = log.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (unlanded.Length > 0)
        {
            // PLUGHOOK1a: a squash left these commits on no branch of the person's, and a completed pull request carried them.
            // A branch checked out outside the trees home is the person's to move, so it is never taken on the platform's word.
            if ((tree is null || Holds(tree)) && await CarrierOfAsync(root, tip, carriers, ct).ConfigureAwait(false) is { } carrier)
            {
                return (Item(SweepKind.Carried, unlanded.Length, carrier.Entry.Branch, string.Join('\n', unlanded.Take(3))) with { CarriedBy = carrier.Entry }, tip);
            }

            // SQUASHTIDY1: the review's Discard's proof by content, so the list offers what that door would take unforced. Its
            // work is where the row says, and the row is landed. Not a branch checked out outside the trees home, as above.
            if ((tree is null || Holds(tree))
                && await HeldByContentAsync(root, tip, line, LandedHere(repository), ct).ConfigureAwait(false) is { } held)
            {
                return (Item(SweepKind.Landed, unlanded.Length, held.Where, string.Join('\n', unlanded.Take(3))) with { HeldBy = held }, tip);
            }

            return (Item(SweepKind.Unlanded, unlanded.Length, detail: string.Join('\n', unlanded.Take(3))), tip);
        }

        var against = line is null ? null : await ComparableAsync(root, line, ct).ConfigureAwait(false);
        if (against is not null)
        {
            var (_, ahead, _) = await WorkingTree.GitAsync(root, ["rev-list", "--count", $"{against}..{tip}"], ct).ConfigureAwait(false);
            if (ahead.Trim() == "0")
            {
                // Whether it made commits since it grew (AUTOTIDY1): a tree that never moved is a conversation's place (D137).
                var worked = Grown.Of(repository, branch) is { } grown && !string.Equals(grown.From, tip, StringComparison.OrdinalIgnoreCase);
                return (Item(SweepKind.Empty, where: line) with { Worked = worked }, tip);
            }
        }

        // Where it landed, for the list: the first branch of the person's that holds its tip.
        var (_, holders, _) = await WorkingTree.GitAsync(
            root, ["branch", "--all", "--contains", tip, "--format=%(refname:short)"], ct).ConfigureAwait(false);
        var where = holders.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(name => !name.StartsWith("daoris/", StringComparison.Ordinal) && !name.Contains("/daoris/", StringComparison.Ordinal));
        var (_, count, _) = await WorkingTree.GitAsync(
            root, ["rev-list", "--count", tip, "--not", against ?? "HEAD"], ct).ConfigureAwait(false);
        return (Item(SweepKind.Landed, int.TryParse(count.Trim(), out var n) ? n : 0, where), tip);
    }

    /// <summary>What the branch form made, and what a plugin is told of it (D100): where, from which line, which commits — and the session branch it was made from.</summary>
    private sealed record Branched(
        TreeLanding Landing, string Root = "", string? Base = null, IReadOnlyList<LandingCommit>? Carried = null, string? Source = null)
    {
        public IReadOnlyList<LandingCommit> Commits => Carried ?? [];

        public static implicit operator Branched(TreeLanding refused) => new(refused);
    }

    /// <summary>
    /// Put a session's work on a new branch, from the session's own branch, which grew from the line.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>It writes nothing to the checkout</b>: it makes one ref in the repository and moves no
    /// checkout, so the root may be dirty and on any branch, and is left exactly so. A branch of that
    /// name already there is refused and never moved — it may be somebody's. Nothing pushes (D87).
    /// </remarks>
    /// <param name="handedOn">A plugin pushes it next (D100), so the sentence does not tell the person to.</param>
    private async Task<Branched> BranchAsync(
        string full, string workspace, string repository, string name, bool handedOn, CancellationToken ct)
    {
        if (!Holds(full))
        {
            return new TreeLanding(false, $"{full} is not a session tree — this lands only trees Daoris opened, under {TreesRoot}.");
        }

        if (!Directory.Exists(full))
        {
            return new TreeLanding(false, $"there is no tree at {full}.");
        }

        var (rootCode, commonDir, rootErr) = await WorkingTree.GitAsync(
            full, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct).ConfigureAwait(false);
        if (rootCode != 0)
        {
            return new TreeLanding(false, $"{full} is not a working tree git recognises: {FirstLine(rootErr)}");
        }

        var root = Path.GetDirectoryName(commonDir.Trim())!;
        var (_, branchOut, _) = await WorkingTree.GitAsync(full, ["rev-parse", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
        var branch = branchOut.Trim();
        if (branch is "" or "HEAD")
        {
            return new TreeLanding(false, $"the tree at {full} is not on a branch, so there is nothing to land.");
        }

        // Uncommitted work would not travel with the branch — the same reason the merge door gives.
        var (_, dirty, _) = await WorkingTree.GitAsync(full, ["status", "--porcelain"], ct).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(dirty))
        {
            return new TreeLanding(false,
                $"the session's tree has uncommitted work — {dirty.Trim().Split('\n').Length} path(s) — which a "
                + "branch would leave behind. Commit it in the tree first, or decide it is not wanted.")
            { Refusal = AutoLandingCode.Uncommitted };
        }

        var line = (await LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
        var against = line is null ? "HEAD" : await ComparableAsync(root, line, ct).ConfigureAwait(false) ?? line;
        // Oldest first, each commit's id and subject: what a plugin writes a pull request from (D100).
        var (logCode, ahead, logErr) = await WorkingTree.GitAsync(
            root, ["log", "--reverse", "--format=%H%x09%s", $"{against}..{branch}"], ct).ConfigureAwait(false);
        if (logCode != 0)
        {
            return new TreeLanding(false, $"git cannot compare `{branch}` with `{line ?? "HEAD"}` here: {FirstLine(logErr)} "
                + "Fetch it, or set another line with `daoris driver line`.");
        }

        if (string.IsNullOrWhiteSpace(ahead))
        {
            return new TreeLanding(false, $"`{branch}` holds nothing `{line ?? "HEAD"}` does not — nothing to land.")
            { Refusal = AutoLandingCode.Nothing };
        }

        if (!BranchName.IsValid(name))
        {
            return new TreeLanding(false, $"the landing rule names `{name}` for this session, which is not a branch name git "
                + "would take. Change the pattern with `daoris driver landing`.");
        }

        var commits = ahead.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry => entry.Split('\t', 2))
            .Select(parts => new LandingCommit(parts[0], parts.Length > 1 ? parts[1] : ""))
            .ToList();

        var (exists, standing, _) = await WorkingTree.GitAsync(
            root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{name}^{{commit}}"], ct).ConfigureAwait(false);
        if (exists == 0)
        {
            // A chain's later done moves the chain's branch on (LAND2c, D149): only one Daoris made, as a fast-forward.
            return await AdvanceAsync(root, repository, name, standing.Trim(), branch, line, commits, handedOn, ct).ConfigureAwait(false);
        }

        var (code, _, err) = await WorkingTree.GitAsync(root, ["branch", name, branch], ct).ConfigureAwait(false);
        if (code != 0)
        {
            return new TreeLanding(false, $"git would not make `{name}`: {FirstLine(err)}");
        }

        var pushIt = handedOn ? "" : $" Push it and open the pull request from there: `git push -u origin {name}`.";
        return new Branched(
            new TreeLanding(true,
                $"put the work on `{name}` — {commits.Count} commit(s) from `{line ?? "HEAD"}`.{pushIt} Nothing was merged "
                + $"and the checkout was not touched.{TreeStays}",
                name),
            root, line, commits, branch);
    }

    /// <summary>
    /// Move a standing branch on to the session's work (LAND2c, D145 §3, D149): the chain's branch, which an earlier done
    /// made, grows by this session's commits, so a ticket has one branch and one pull request.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>Only a fast-forward of a branch Daoris made, and never forced.</b> It must be the record's (D102), standing at
    /// the recorded tip, checked out in no working tree, with its work not already on the line, and the session's tip must grow
    /// from it with something new (<see cref="LandingAdvance"/>). Then git moves it by compare-and-swap from the tip judged
    /// (<c>update-ref</c>), so a branch moved in between is refused by git itself. Like the branch form, it writes one ref and
    /// touches no checkout.</para>
    ///
    /// <para><b>A branch whose pull request was merged is not moved on</b> (D149 point 3): D102's proof on the recorded tip, the
    /// same one the clean-up and the hand-off make, finds its work on the line, and commits added now would ride no pull
    /// request.</para>
    /// </remarks>
    /// <param name="standsAt">The commit the branch stands at, read a moment ago.</param>
    /// <param name="branch">The session's own branch.</param>
    /// <param name="commits">What the branch would hold beyond the line once moved: what a plugin is told, as at a first landing.</param>
    private async Task<Branched> AdvanceAsync(
        string root, string repository, string name, string standsAt, string branch, string? line,
        IReadOnlyList<LandingCommit> commits, bool handedOn, CancellationToken ct)
    {
        var recorded = Recorded.Of(repository, name);
        var (_, headOut, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}^{{commit}}"], ct)
            .ConfigureAwait(false);
        var head = headOut.Trim();

        // Each fact read only where the one before it allowed an advance, in the order the refusals are said.
        string? checkedOut = null;
        string? completed = null;
        var descends = false;
        var newer = 0;
        if (recorded is not null && string.Equals(standsAt, recorded.Tip, StringComparison.OrdinalIgnoreCase))
        {
            checkedOut = (await WorktreesAsync(root, ct).ConfigureAwait(false)).GetValueOrDefault(name);
            if (checkedOut is null)
            {
                var proof = await ProveAsync(root, recorded.Tip, line, await LineFormsAsync(root, line, ct).ConfigureAwait(false), ct)
                    .ConfigureAwait(false);
                completed = proof.Kind is LandedKind.Merged or LandedKind.OnLine ? proof.Where : null;
            }

            if (checkedOut is null && completed is null && head.Length > 0)
            {
                var (ancestor, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", recorded.Tip, head], ct)
                    .ConfigureAwait(false);
                descends = ancestor == 0;
                if (descends)
                {
                    var (_, count, _) = await WorkingTree.GitAsync(root, ["rev-list", "--count", $"{recorded.Tip}..{head}"], ct)
                        .ConfigureAwait(false);
                    newer = int.TryParse(count.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0;
                }
            }
        }

        if (LandingAdvance.Refusal(name, repository, branch, new AdvanceFacts(recorded, standsAt, checkedOut, descends, newer, completed))
            is { } refused)
        {
            return refused;
        }

        // Compare-and-swap from the tip judged: git refuses where the branch moved since, and nothing is forced.
        var (moved, _, moveErr) = await WorkingTree.GitAsync(
            root, ["update-ref", "-m", $"daoris: {branch} moves {name} on", $"refs/heads/{name}", head, standsAt], ct).ConfigureAwait(false);
        if (moved != 0)
        {
            return new TreeLanding(false, $"git would not move `{name}` on from {standsAt[..Math.Min(8, standsAt.Length)]}: "
                + $"{FirstLine(moveErr)} It may have moved meanwhile; nothing was changed.")
            { Refusal = AutoLandingCode.Exists };
        }

        var pushIt = handedOn ? "" : $" Push it to grow its pull request: `git push origin {name}`.";
        return new Branched(
            new TreeLanding(true,
                $"moved `{name}` on from {standsAt[..Math.Min(8, standsAt.Length)]} to this session's work — {newer} more commit(s), "
                + $"{commits.Count} from `{line ?? "HEAD"}` in all.{pushIt} Nothing was merged and the checkout was not touched.{TreeStays}",
                name)
            { AdvancedFrom = standsAt },
            root, line, commits, branch);
    }

    /// <summary>
    /// What the plugin's step came to, in the landing's words (D100). Anything short of a push says the
    /// branch stands and how the person pushes it themselves.
    /// </summary>
    private static string Said(PluginLanding said, string branch)
    {
        var byHand = $"The branch stands — push it and open the pull request yourself: `git push -u origin {branch}`.";
        if (said.Failed) return $"Plugin `{said.Plugin}`'s step failed — {Sentence(said.Message)} {byHand}";
        if (!said.Pushed) return $"Plugin `{said.Plugin}` did not push it: {Sentence(said.Message)} {byHand}";

        var opened = said.PullRequest is { } pr && !said.Message.Contains(pr, StringComparison.Ordinal) ? $" The pull request: {pr}" : "";
        return $"Plugin `{said.Plugin}`: {Sentence(said.Message)}{opened}";
    }

    /// <summary>A plugin's words closed as a sentence, so the one after it does not run on.</summary>
    private static string Sentence(string words) =>
        words.TrimEnd() is var trimmed && trimmed.Length > 0 && ".!?".Contains(trimmed[^1]) ? trimmed : $"{trimmed}.";

    /// <summary>
    /// The person's choices, from the file the loop and the planner read (CONFIGSEAM1): this home's <c>driver.json</c> alone
    /// ignored an override's landing and review rules, so the work merged into the line while the planner sat a review for it.
    /// </summary>
    private DriverConfig Config() => DriverConfig.Load(DriverConfig.ResolvePath(home));

    /// <summary>What a pattern is expanded from: the quest, or the session where there is none; the quest's name's words (LANDNAME1).</summary>
    private static LandingNames NamesOf(LandingSubject subject, string repository) =>
        new(subject.Quest ?? subject.Session, subject.Slug, repository, subject.Session);

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
        var detached = branch is "" or "HEAD";
        // The name a recovery ref is kept under: the branch, or for a detached tree the one its layout names.
        var named = detached ? BranchOfTree(full)?.Branch ?? "HEAD" : branch;
        ContentHold? held = null;
        string? judged = null;
        string? kept = null;

        if (!force)
        {
            // Nothing deletes itself (D51 rule 7). Two different kinds of work can be lost here, and
            // the refusal names the kind, because they are fixed differently: uncommitted changes are
            // committed or abandoned IN the tree; unmerged commits are merged FROM it (SURF6's surface).
            // Asked so no setting hides one (SQUASHTIDY1c): an untracked file `status.showUntrackedFiles=no` hides,
            // and an ignored one only this tree holds, which `git worktree remove` deletes without asking.
            var holds = await HoldsAsync(full, root, ct).ConfigureAwait(false);
            if (holds is null)
            {
                return new(false, $"Daoris cannot tell what the tree at {path} holds beyond its commits. "
                    + "The tree stays; say it again with --force to discard it.");
            }

            if (holds.Uncommitted.Count > 0)
            {
                return new(false,
                    $"the tree at {path} has uncommitted work — {holds.Uncommitted.Count} path(s). Removing it would "
                    + "destroy that work; commit it there or say it again with --force, meaning it.");
            }

            if (holds.Ignored.Count > 0)
            {
                return new(false,
                    $"the tree at {path} holds {holds.IgnoredSaid}. Removing it would destroy them; move them somewhere "
                    + "of yours, or say it again with --force, meaning it.");
            }

            // The commit judged, read once (SQUASHTIDY1c): every question below is asked of it, and the branch goes only while
            // it is still that commit.
            var (tipCode, tipOut, tipErr) = await WorkingTree.GitAsync(full, ["rev-parse", "--verify", "--quiet", "HEAD^{commit}"], ct)
                .ConfigureAwait(false);
            if (tipCode != 0)
            {
                return new(false,
                    $"Daoris cannot tell whether the work on `{branch}` is landed: {FirstLine(tipErr)} "
                    + "The tree stays; say it again with --force to discard it.");
            }

            judged = tipOut.Trim();
            var (workspace, repository) = OwnerOf(full);
            var line = (await LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
            var canonical = line ?? "HEAD";
            // Landed (D88): a branch of the person's holds every commit — the line, a branch the branch form
            // made, one they pushed. Asked of the tree's own HEAD, so a detached tree is judged by what it
            // holds. 🔴 A comparison git could not make keeps the tree: an empty answer is not "landed".
            var (logCode, unmerged, logErr) = await UnlandedLogAsync(full, judged, ct).ConfigureAwait(false);
            if (logCode != 0)
            {
                return new(false,
                    $"Daoris cannot tell whether the work on `{branch}` is landed: {FirstLine(logErr)} "
                    + "The tree stays; say it again with --force to discard it.");
            }

            // SQUASHTIDY1: a squash merge or a cherry-pick holds the work under commits of its own, which ancestry cannot see;
            // the content can. Work held nowhere still refuses, and force stays its door.
            if (!string.IsNullOrWhiteSpace(unmerged)
                && (held = await HeldByContentAsync(full, judged, line, LandedHere(repository), ct).ConfigureAwait(false)) is null)
            {
                return new(false,
                    $"the tree at {path} holds commits `{canonical}` has not taken, and no other branch of yours holds "
                    + $"them:\n{unmerged.Trim()}\nLand them from the review, or say it again with --force to discard them.");
            }

            // SQUASHTIDY1c: the proof compared files, so the commits themselves are kept on a ref of their own before anything goes.
            if (held is not null)
            {
                var (reference, why) = await KeepDiscardedAsync(root, named, judged, ct).ConfigureAwait(false);
                if (reference is null)
                {
                    return new(false, $"Daoris could not keep the commits on `{branch}` on a ref of their own ({why}), so nothing "
                        + "was removed. Say it again with --force to discard them.");
                }

                kept = reference;
            }
        }

        // A tree past Windows' 260 characters is removed with long paths, which every git call here says:
        // without them git let go of the tree, then failed deleting its files (2026-09-28).
        var removeArgs = force
            ? new[] { "worktree", "remove", "--force", full }
            : ["worktree", "remove", full];
        var (removeCode, _, removeErr) = await WorkingTree.GitAsync(root, removeArgs, ct).ConfigureAwait(false);
        string? left = null;
        if (removeCode != 0)
        {
            // Git lets go of a tree before its folder, and a folder something holds open stays (the first real
            // post-merge run): then the tree is gone and only its folder is left, which is said plainly.
            var (letGo, why) = await LetGoAsync(root, full, ct).ConfigureAwait(false);
            if (!letGo)
            {
                if (judged is not null) await DropKeptAsync(root, branch, kept, judged, ct).ConfigureAwait(false);
                return new(false, $"git would not remove the tree: {FirstLine(removeErr)}");
            }

            left = why;
        }

        // The branch goes with its tree — where this call's proof cleared it (D88), only while it is still the commit judged
        // (SQUASHTIDY1c), or where the person forced it. Never git's own -d, which asks only whether the checkout's HEAD holds
        // it and kept every branch the branch form had landed. A branch left behind would resurrect "never reused" as a pile.
        string? stays = null;
        if (!detached)
        {
            if (judged is null)
            {
                var (deleted, _, deleteErr) = await WorkingTree.GitAsync(root, ["branch", "-D", branch], ct).ConfigureAwait(false);
                stays = deleted == 0 ? null : $"git would not delete it: {FirstLine(deleteErr)}";
            }
            else
            {
                stays = await DeleteJudgedAsync(root, branch, judged, ct).ConfigureAwait(false);
                if (stays is not null) await DropKeptAsync(root, branch, kept, judged, ct).ConfigureAwait(false);
            }

            // The record drops what is gone (LAND3): a name no branch has is never judged by a start it no longer has.
            if (stays is null) Forget(OwnerOf(full).Repository, branch);
        }

        if (stays is not null)
        {
            return new(true, $"removed the session tree at {path}, and kept its branch `{branch}`: {stays}"
                + (stays == MovedSince ? ", so it holds work this removal did not judge." : ".") + (left is null ? "" : $" {left}"));
        }

        // How the work was found held, where ancestry could not say (SQUASHTIDY1), and where its commits stay (SQUASHTIDY1c): the
        // sentence the review's Discard shows as it is.
        return new(true, $"removed the session tree at {path} (branch `{branch}`)" + (held is null ? "." : $": {held.Said}.")
            + (left is null ? "" : $" {left}") + (kept is null ? "" : $" {ContentHold.KeptAt(named, kept)}"));
    }

    /// <summary>
    /// Every session tree on this machine, read from the layout itself — the directory is the
    /// registry, the same argument the credential profiles made (D49 §4).
    /// </summary>
    public async Task<IReadOnlyList<SessionTree>> ListAsync(CancellationToken ct = default)
    {
        var found = new List<SessionTree>();
        if (!Directory.Exists(TreesRoot)) return found;

        // The folders the look moved aside are no trees (AUTOTIDY1): git no longer knows them as such.
        foreach (var workspace in Directory.EnumerateDirectories(TreesRoot).Where(folder => Path.GetFileName(folder) != SetAsideFolder))
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
    /// The reference git compares <paramref name="branch"/> by here, by its full name: the local branch, else origin's copy,
    /// else null — a line this checkout has nowhere. Never the short name (AUTOTIDY1): git reads a tag of that name first.
    /// </summary>
    private static async Task<string?> ComparableAsync(string root, string branch, CancellationToken ct)
    {
        foreach (var reference in new[] { $"refs/heads/{branch}", $"refs/remotes/origin/{branch}" })
        {
            var (code, _, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", reference], ct).ConfigureAwait(false);
            if (code == 0) return reference;
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
