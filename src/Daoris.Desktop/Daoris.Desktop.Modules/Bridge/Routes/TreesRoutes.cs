using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// What a session's tree holds and what becomes of it, the page's `bridge/trees.ts` (MOD5): the review's
/// diff (SURF6), the files a composer offers (CONV4d), a file's preview (D111), discard (D51), landing
/// (WSR1) and the hand-off (WSR5b).
/// </summary>
public sealed class TreesRoutes(DriverLoop loop)
{
    private readonly DriverLoop _loop = loop;

    /// <summary>
    /// What a landing changes (STATEHUB1b, D169): the session, every plan and gate (the line it merged into is what each
    /// session's plan judges against, LANDSYNC1, so `landing` is never only its own), its review, its hand-off, the clean-up's
    /// list, and which repositories hold Daoris's branches.
    /// </summary>
    private const StateScopes Lands = StateScopes.Sessions | StateScopes.Landing | StateScopes.Diff | StateScopes.HandOff
        | StateScopes.Sweep | StateScopes.TreesSync;

    /// <summary>What discarding a session's tree changes: as a landing, but for the branch a plugin could take, which it keeps.</summary>
    private const StateScopes Discards = Lands & ~StateScopes.HandOff;

    /// <summary>What a press at the second opinion's gate changes: every plan and gate, and the sessions whose reviewer it moved.</summary>
    private const StateScopes Gates = StateScopes.Sessions | StateScopes.Landing;

    // What a session actually did (SURF6, design §5). The evidence string says that work
    // happened; this says what it was. Desktop-only by construction, like the console: it is
    // read off a checkout, and only the machine holding one can answer at all.
    /// <summary>
    /// One session's landed work, as a diff (SURF6).
    /// </summary>
    /// <remarks>
    /// <para><b>Read-only, and that is the whole of this route.</b> It runs `git diff` in the tree the
    /// record names and returns what git said. Nothing here writes, merges or removes anything — the
    /// acts that do are the person's and are their own routes, so a surface that only shows the work
    /// cannot accidentally change it.</para>
    ///
    /// <para><b>Unreviewable is INFORMATION, not a failure</b> (D48 §6's class): a record mirrored
    /// from another machine names no tree here, a record made before the base commit was written has
    /// no range, and a tree that has been discarded is gone. None of those is a fault, and each has a
    /// different sentence, so the page can say which.</para>
    ///
    /// <para><b>A landed session reads as landed</b> (REVIEW2, D113). Where this machine's landing record holds
    /// the session's landing, the answer says where its work landed — the branch, when, the pull request a
    /// plugin opened — and where that branch stands now. The tree is read first while it is here, since it
    /// holds what the session did and anything it did after its landing; once it is gone, the changes are
    /// read from the landed branch in the repository's own checkout, from where its work grew from, and a
    /// branch gone since is said with whether its work reads on the line. Every read there is of refs and
    /// objects: the person's checkout is never touched.</para>
    /// </remarks>
    [DriverRoute("SESSION_DIFF")]
    private async Task<object?> DiffAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");

        var service = _loop.Service ?? throw NotReady();

        var (tree, baseCommit) = await service.SessionGroundAsync(id, cancellationToken);
        var trees = new SessionTrees(_loop.Home);
        // This machine's landing of the session, standing or a trace (D113): where its work went.
        var landing = trees.Recorded.Landing(id);
        var gone = TreeFolders.TreeGone(tree);

        // What the session's OWN tree holds that no commit does, read apart (REVIEWDIRTY1-a, D164 point 4); never for a conversation in
        // the person's checkout, whose uncommitted work is not knowably the session's (D49 §3).
        var dirty = gone || !OwnTree(trees, tree) ? null : await WorkingTree.UncommittedDiffAsync(tree!, cancellationToken);

        if (!gone && !string.IsNullOrWhiteSpace(baseCommit))
        {
            // The review measures from the newest commit the work shares with the line (REVIEWBASE1, D164's note): a tree that took
            // in newer line commits (WSR6's replay, a merge, a rebase, the line checked out) would otherwise show the line's own
            // changes as the session's work. It says how far the latest line it knows is past the tree, and fetches nothing.
            var range = await trees.ReviewRangeAsync(tree!, baseCommit, cancellationToken);
            // A chain's step that merged the line reads from a merge of the line's base and its parent's tip (REVIEWBASE3), a tree
            // and no commit: the answer still names the commit it is measured from.
            var diff = await WorkingTree.DiffAsync(tree!, range.Reads, cancellationToken) is { } read ? read with { Base = range.From } : null;
            if (diff is not null)
            {
                var standing = landing is null
                    ? null
                    : await trees.LandedReviewAsync(await CheckoutOfAsync(service, landing.Repository, cancellationToken), landing, changes: false, cancellationToken);
                // HEAD's own name, never the layout's (`SessionTrees.BranchNowAsync` falls back to it where detached, REVIEWBASE4). Git
                // just read the range from this HEAD, so a refusal to name it is a fault, thrown as `BranchAsync` throws it.
                var branch = await WorkingTree.BranchAsync(tree!, cancellationToken);
                // ACCEPTFOOT1: the driver's own offer to land this tree (LAND4: D88's proof and SQUASHTIDY1b's content hold), read where
                // the work is beyond the line and nothing landed it, so an open conversation is offered exactly what a press would land.
                var offer = landing is null && range.Behind is { Ahead: > 0 } && OwnTree(trees, tree)
                    && await trees.WorkAsync(tree!, cancellationToken) is { } work
                    ? LandOffer.Of(tree!, work)
                    : null;
                return ReviewAnswer(
                    id, diff, ReviewSource.Tree, standing, treeGone: false, uncommitted: dirty, behind: range.Behind, treeBranch: branch, offer: offer);
            }
        }

        if (landing is not null)
        {
            var review = await trees.LandedReviewAsync(
                await CheckoutOfAsync(service, landing.Repository, cancellationToken), landing, changes: true, cancellationToken);
            return ReviewAnswer(id, review.Changes, ReviewSource.Branch, review, treeGone: gone);
        }

        // Nothing committed is readable (no base, or a range git cannot read), but the tree holds uncommitted work: that is the review,
        // rather than a refusal that says there is nothing to see.
        if (dirty is { Files.Count: > 0 })
        {
            return ReviewAnswer(
                id, new WorkingTree.TreeDiff([], null, ""), ReviewSource.Tree, null, treeGone: false, uncommitted: dirty,
                treeBranch: await WorkingTree.BranchAsync(tree!, cancellationToken));
        }

        throw Unreviewable(id, tree, baseCommit);
    }

    /// <summary>Whether <paramref name="tree"/> is one this home opened for a session, which ran in it alone (D49 §3); a person's checkout is not.</summary>
    private static bool OwnTree(SessionTrees trees, string? tree)
    {
        if (string.IsNullOrWhiteSpace(tree)) return false;
        try
        {
            return trees.Holds(tree);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>
    /// The process a session's own tree runs under (PROCESS1e, D164's amendment point 3), for its page's *Details*: how its work is
    /// committed and how it lands, each with where it was set. Read from the driver file and the tree's place alone: no git, so a
    /// page may ask it for every session it shows.
    /// </summary>
    /// <remarks>
    /// Only a tree this home made is told a process (PROCESS1d's guard, <see cref="SessionTrees.Holds"/>): a session in the person's
    /// checkout, or whose record names no tree here, answers none, as its start told it none. The rules are as resolved now, which a
    /// press of Accept reads; a rule changed since the session's start was not told to it.
    /// </remarks>
    [DriverRoute("SESSION_PROCESS")]
    private async Task<object?> ProcessAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var service = _loop.Service ?? throw NotReady();

        var (tree, _) = await service.SessionGroundAsync(id, cancellationToken);
        return new { Session = id, Process = SessionProcess(DriverConfig.Load(_loop.ConfigPath), new SessionTrees(_loop.Home), tree) };
    }

    /// <summary>
    /// What <c>SESSION_PROCESS</c> says of a tree: its repository's commit rule and landing rule as <c>LINES</c> says each, with where
    /// it was set, the repository and workspace read from the tree's place; null where the tree is not one this home made.
    /// </summary>
    public static object? SessionProcess(DriverConfig config, SessionTrees trees, string? tree)
    {
        if (!OwnTree(trees, tree)) return null;
        var (workspace, repository) = trees.Owner(tree!);
        if (repository.Length == 0) return null;

        var commits = CommitRules.Resolve(config, repository, workspace);
        var landing = LandingRules.Choose(config, repository, workspace);
        return new
        {
            Commits = new { Repository = repository, Workspace = workspace, Rule = LinesRoutes.CommitWire(commits.Rule), commits.Source },
            Landing = new
            {
                Repository = repository,
                Workspace = workspace,
                landing.Rule.Form,
                landing.Rule.Pattern,
                landing.Rule.Tidy,
                landing.Rule.Plugin,
                landing.Rule.AutoAccept,
                landing.Rule.Key,
                landing.Rule.Squash,
                landing.Source,
            },
        };
    }

    /// <summary>The landing plan's count of uncommitted paths in the session's own tree: 0 where clean or not asked (REVIEWDIRTY1-a).</summary>
    public static int UncommittedCount(int? held) => held ?? 0;

    /// <summary>Where a review's files are read from (REVIEW2): the session's own tree, or its landed branch once the tree cannot be read.</summary>
    public static class ReviewSource
    {
        public const string Tree = "tree";
        public const string Branch = "branch";
    }

    /// <summary>
    /// What a review becomes on the wire: the files and the range they are measured from, where they are read from,
    /// and — for a landed session (REVIEW2, D113) — where its work landed, where that branch stands, and whether the
    /// review reads as landed, which is what the page's acts follow. Public, as <see cref="FileAnswer"/> is, so the
    /// shape is tested without a service.
    /// </summary>
    /// <remarks>Never a machine path: the checkout the branch was read in is the registry's, and the page has no use for it.</remarks>
    public static object ReviewAnswer(
        string session, WorkingTree.TreeDiff? diff, string source, LandedReview? landed, bool treeGone, WorkingTree.TreeDiff? uncommitted = null,
        LineBehind? behind = null, string? treeBranch = null, LandOffer? offer = null) => new
    {
        Session = session,
        Base = diff?.Base ?? "",
        diff?.Truncated,
        Files = (diff?.Files ?? []).Select(file => new
        {
            file.Path,
            file.Status,
            file.Added,
            file.Removed,
            file.Patch,
        }).ToArray(),
        Source = source,
        // The session's own tree's uncommitted work (REVIEWDIRTY1-a, D164 point 4), apart from the committed list: null where the
        // source is not that tree or nothing is uncommitted. A shape added, so an answer without it reads as it did.
        Uncommitted = source == ReviewSource.Tree && uncommitted is { Files.Count: > 0 }
            ? new
            {
                Files = uncommitted.Files.Select(file => new { file.Path, file.Status, file.Added, file.Removed, file.Patch }).ToArray(),
                Truncated = uncommitted.Truncated is not null,
            }
            : null,
        // How the session's own tree stands against the latest line known here (REVIEWBASE1): the line's name, the commits it has that
        // the tree does not, and the tree's beyond it. Null where the source is not that tree or the line could not be read; a shape added.
        Behind = source == ReviewSource.Tree && behind is not null ? new { behind.Line, behind.Commits, behind.Ahead } : null,
        // The branch the read tree's HEAD stands on (REVIEWBASE4, D164's note), which the page's head names over the record's guess: the
        // agent may have switched it (TREEFOLLOW1) or deleted its `daoris/` branch and detached. Null where detached, and where the
        // source is a landed branch, whose tree was not read; a shape added.
        TreeBranch = source == ReviewSource.Tree ? treeBranch : null,
        // What the driver would land of the tree (ACCEPTFOOT1, LAND4's offer as `SESSION_GROUPS` says it): null where it offers none, the
        // work not beyond the line or landed, and on a landed branch's review; a shape added.
        Offer = source == ReviewSource.Tree && offer is not null ? new { offer.Branch, offer.Tree, offer.Commits, offer.Uncommitted } : null,
        Landed = landed is null ? null : new
        {
            landed.Entry.Branch,
            landed.Entry.Repository,
            landed.Entry.Line,
            LandedAt = landed.Entry.LandedAt == DateTimeOffset.MinValue
                ? null
                : Moments.Write(landed.Entry.LandedAt),
            landed.Entry.Plugin,
            landed.Entry.Pushed,
            landed.Entry.PullRequest,
            // Who accepted it (LAND2b, D145 point 4): `person`, `auto`, or null for a landing from before it was kept.
            landed.Entry.AcceptedBy,
            landed.State,
            AsLanded = landed.ReadsAsLanded(treeGone),
            Reads = landed.Reads is { } reads ? new { reads.Kind, reads.Where, reads.Files, reads.Detail } : null,
            Removed = landed.Entry.RemovedAs is { } kind
                ? new
                {
                    Kind = kind,
                    Where = landed.Entry.RemovedOn,
                    At = Moments.Write(landed.Entry.GoneAt),
                }
                : null,
            landed.Detail,
        },
    };

    /// <summary>Why a review with no landing to read instead has nothing to show, as the refusal the page renders — each its own code.</summary>
    public static Shenora.Core.Ipc.ShenoraException Unreviewable(string session, string? tree, string? baseCommit)
    {
        if (string.IsNullOrWhiteSpace(tree))
        {
            return Refusals.Because(
                Refusals.SessionNotReviewable,
                "this session's record names no working tree on this machine, so there is nothing "
                + "here to diff. That is what a record looks like when it travelled from the machine "
                + "that did the work.",
                ("session", session));
        }

        if (TreeFolders.TreeGone(tree))
        {
            return Refusals.Because(
                Refusals.SessionTreeGone,
                "this session's tree is gone from this machine — a clean-up after its landing, or a discard, removed it — so "
                + "there is nothing here to diff, and no tree to land or discard.",
                ("session", session));
        }

        if (string.IsNullOrWhiteSpace(baseCommit))
        {
            return Refusals.Because(
                Refusals.SessionNoBase,
                "this session's record does not say which commit its tree stood at when it began, so "
                + "there is no range to measure. Records made before Daoris started writing that down "
                + "keep their evidence line and cannot gain a diff.",
                ("session", session));
        }

        return Refusals.Because(
            Refusals.SessionRangeUnreadable,
            "git could not read that range where the session ran — the tree has moved, been "
            + "discarded, or no longer holds the commit it started from.",
            ("session", session));
    }

    // What a person may `@` in a conversation (CONV4d): the files in the tree the record names.
    // Desktop-only for the diff's reason — it is read off a checkout — and read-only.
    /// <summary>
    /// The files in one session's tree, for the composer's `@` (CONV4d).
    /// </summary>
    /// <remarks>
    /// <b>Unlisted is INFORMATION</b>, in the review's class: a record from another machine names no
    /// tree here, and a tree that is gone or not a repository of its own has nothing git will answer
    /// for. One code for both, because the person's next move is the same — type the path, which both
    /// doors expand as typed (`docs/2026-09-25-message-content-evidence.md`).
    /// </remarks>
    [DriverRoute("SESSION_FILES")]
    private async Task<object?> FilesAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");

        var service = _loop.Service ?? throw NotReady();

        var (tree, _) = await service.SessionGroundAsync(id, cancellationToken);
        var files = string.IsNullOrWhiteSpace(tree)
            ? null
            : await WorkingTree.FilesAsync(tree, cancellationToken);
        if (files is null)
        {
            throw Refusals.Because(
                Refusals.SessionTreeUnlisted,
                "git cannot list this session's tree here — its record names no tree on this machine, or "
                + "the tree is gone or not a repository of its own. A path typed after @ still reaches the agent.",
                ("session", id));
        }

        return new { Session = id, files.Files, files.Unlisted };
    }

    // A file the conversation or the review named, read for the person to look at in the side bar
    // (PREVIEW1, D111). Desktop-only for the diff's reason, and read-only: there is no editor (D55).
    /// <summary>
    /// One file in a session's tree, for its preview (PREVIEW1, D111): the tree the record names, which for a
    /// conversation in the repository's checkout is the checkout.
    /// </summary>
    /// <remarks>
    /// <para><b>Only inside that tree</b> (<see cref="FilePreview"/>): a path outside it, a path through a link
    /// that leads out of it, and a path under `.git` are each refused as a code of their own.</para>
    ///
    /// <para><b>The answer names the file as the page asked for it</b>, relative to the tree, and never the
    /// tree's own path, which is machine-local material the page has no use for.</para>
    ///
    /// <para><b>Once the tree is gone, a landed session's file is read from its landed branch</b> (REVIEW2, D113):
    /// a finished session is usually a landed one, and a tidy takes its tree, so its preview had nothing to read.
    /// The answer names the branch, so the page says the file is as that branch holds it. The no-tree answer is
    /// left for a session with neither its tree nor the landing's branch here.</para>
    /// </remarks>
    [DriverRoute("SESSION_FILE")]
    private async Task<object?> FileAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var path = PayloadHelper.GetRequiredValue<string>(request.Payload, "path");

        var service = _loop.Service ?? throw NotReady();

        var (tree, _) = await service.SessionGroundAsync(id, cancellationToken);
        if (!TreeFolders.TreeGone(tree) || new SessionTrees(_loop.Home).Recorded.Landing(id) is not { } landing)
        {
            return await PreviewAsync(id, tree, path, cancellationToken, _loop.Log);
        }

        var root = await CheckoutOfAsync(service, landing.Repository, cancellationToken);
        return await LandedPreviewAsync(id, root, landing, tree, path, cancellationToken, _loop.Log);
    }

    /// <summary>
    /// A file read for its preview from a session's landed branch (REVIEW2, D113), in the repository's checkout
    /// <paramref name="root"/>, answered as the page receives it or refused as a code. Public, as <see cref="PreviewAsync"/> is.
    /// </summary>
    /// <param name="tree">The session's tree as its record names it, gone now: a path the page sends inside it is the same path on the branch.</param>
    /// <param name="log">As <see cref="PreviewAsync"/>'s: the session and the file relative to the repository, never the branch's name, which carries a title's words.</param>
    public static async Task<object> LandedPreviewAsync(
        string session, string? root, LandedBranch landing, string? tree, string path, CancellationToken cancellationToken, MachineLog? log = null)
    {
        var read = await FilePreview.ReadLandedAsync(root, landing, path, tree, cancellationToken);
        if (read is { Refusal: FilePreviewRefusal.None, File: { } opened })
        {
            log?.Info("preview.opened", ("session", session), ("path", opened.Path));
        }

        return FileAnswer(session, path, read);
    }

    /// <summary>
    /// A file read for its preview in <paramref name="tree"/>, answered as the page receives it or refused as
    /// a code. Public, as <see cref="OptionsAnswer"/> is, so the route's answers are tested without a service.
    /// </summary>
    /// <param name="log">
    /// The machine log a preview that opened is written to (LEFT2, D94), as <c>preview.opened</c>: the session and
    /// the file relative to the tree, never the tree's own path and never the file's words. Null writes none.
    /// </param>
    public static async Task<object> PreviewAsync(
        string session, string? tree, string path, CancellationToken cancellationToken, MachineLog? log = null)
    {
        var read = string.IsNullOrWhiteSpace(tree)
            ? FilePreviewResult.Refused(FilePreviewRefusal.NoTree)
            : await FilePreview.ReadAsync(tree, path, cancellationToken);
        if (read is { Refusal: FilePreviewRefusal.None, File: { } opened })
        {
            log?.Info("preview.opened", ("session", session), ("path", opened.Path));
        }

        return FileAnswer(session, path, read);
    }

    /// <summary>What a preview's reading becomes on the wire: the file, or its refusal, each with its own code.</summary>
    public static object FileAnswer(string session, string path, FilePreviewResult read)
    {
        (string, string)[] named = [("session", session), ("path", path)];
        return read switch
        {
            // The landed branch it was read from, once the tree is gone (REVIEW2); null for the file on disk.
            { Refusal: FilePreviewRefusal.None, File: { } file } => new
            {
                Session = session, file.Path, file.Size, file.Binary, file.Text, file.Truncated, file.Branch,
            },
            { Refusal: FilePreviewRefusal.NotOnBranch } => throw Refusals.Because(
                Refusals.PreviewNotOnBranch,
                $"`{path}` is not a file on `{read.Branch}`, the branch this session's work landed on: it was deleted or moved "
                + "there, or it is a folder or a link.",
                [.. named, ("branch", read.Branch ?? "")]),
            { Refusal: FilePreviewRefusal.Outside } => throw Refusals.Because(
                Refusals.PreviewOutsideTree,
                $"`{path}` is outside this session's tree, so the preview does not read it.",
                named),
            { Refusal: FilePreviewRefusal.LinkLeaves } => throw Refusals.Because(
                Refusals.PreviewLinkLeavesTree,
                $"`{path}` goes through a link that leads out of this session's tree, so the preview does not read it.",
                named),
            { Refusal: FilePreviewRefusal.GitFolder } => throw Refusals.Because(
                Refusals.PreviewGitFolder,
                $"`{path}` is inside `.git`, which is git's own folder rather than the session's work.",
                named),
            { Refusal: FilePreviewRefusal.NoTree } => throw Refusals.Because(
                Refusals.PreviewNoTree,
                "this session's record names no tree on this machine, or the tree is gone and no branch its landing made stands "
                + "here to read instead, so there is no file here to show.",
                named),
            _ => throw Refusals.Because(
                Refusals.PreviewNotAFile,
                $"`{path}` is not a file in this session's tree now: it was deleted or moved, it is a folder, or it cannot be read.",
                named),
        };
    }

    // Discarding a reviewed session's tree (SURF6b, D51 rule 7): the PERSON's act, since nothing
    // deletes itself, so its own route rather than anything the diff route could do as a side effect.
    // Its sibling, the merge alone, is retired (LEFT2): the work lands through `LAND_SESSION_TREE`,
    // which merges where the repository's rule says merge (D87).
    /// <summary>
    /// Discard a session's tree (SURF6b, D51 rule 7).
    /// </summary>
    /// <remarks>
    /// <para><b>A refusal here is an ANSWER, not an error</b> — the tree holds work nobody merged, or
    /// uncommitted work. Each comes back as `{ done: false, message }` with the sentence the tree layer
    /// wrote, exactly as `START_CHAT` does, because the person's next move is different for each and a
    /// code would flatten them into one.</para>
    ///
    /// <para><b>Discard needs `force` said out loud.</b> The unforced call is what produces the
    /// refusal that names what would be lost, so the page asks, shows that sentence, and only then
    /// sends `force`. Destroying work is never a side effect of tidying (D51 rule 7).</para>
    /// </remarks>
    [DriverRoute("DISCARD_SESSION_TREE")]
    [Publishes("DISCARD_SESSION_TREE", Discards, Session = "id")]
    private async Task<object?> DiscardTreeAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var force = Flag(request, "force");

        var service = _loop.Service ?? throw NotReady();

        var (tree, _) = await service.SessionGroundAsync(id, cancellationToken);
        if (string.IsNullOrWhiteSpace(tree))
        {
            throw Refusals.Because(
                Refusals.SessionNotReviewable,
                "this session's record names no working tree on this machine, so there is nothing "
                + "here to discard.",
                ("session", id));
        }

        // The same home the loop derives for the chat runner and the watch: the directory holding
        // `driver.json`. Derived rather than stored twice, so one answer cannot drift from the other.
        var removal = await new SessionTrees(_loop.Home).RemoveAsync(tree, force, cancellationToken);
        _loop.Nudge();
        return new { Session = id, Done = removal.Removed, removal.Message };
    }

    /// <summary>
    /// The branch name the page entered (LANDNAME1a, D145), which the landing takes in place of the rule's; blank or absent is
    /// the rule's, as before. The plan and the press both read it, so what the page shows is what the press does.
    /// </summary>
    public static LandingSubject Entering(LandingSubject subject, IpcRequest request) =>
        subject with { Entered = Optional(request, "branch") };

    // How a reviewed session's work lands (WSR1, D87): what a press WOULD do, said before it —
    // merge into the line, or the branch the rule names for this session — and the press, which
    // applies the repository's rule. It is the one door that lands a session's work.
    /// <summary>
    /// A reviewed session's landing (WSR1, D87): the plan before the press, or the press itself.
    /// </summary>
    /// <remarks>
    /// The pattern is expanded from the session's quest and its title, or for a conversation from the
    /// session and what the person first said — this machine's own record, never the session record's.
    /// A refusal is an answer, as the merge door's are.
    /// </remarks>
    [DriverRoute("LANDING")]
    [DriverRoute("LAND_SESSION_TREE")]
    [Publishes("LAND_SESSION_TREE", Lands, Session = "id")]
    private async Task<object?> LandAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var service = _loop.Service ?? throw NotReady();

        var (tree, _) = await service.SessionGroundAsync(id, cancellationToken);
        if (string.IsNullOrWhiteSpace(tree))
        {
            throw Refusals.Because(
                Refusals.SessionNotReviewable,
                "this session's record names no working tree on this machine, so there is nothing here to land.",
                ("session", id));
        }

        var questId = await service.SessionQuestAsync(id, cancellationToken);
        var records = await service.SessionRecordsAsync(cancellationToken);
        // A rule's plugin says its lines on the console under its name, as a hook's do (D64 §4, D100); what the
        // landing did with it goes to the machine log and the loop's record of its health (PLUGUI1d).
        var trees = new SessionTrees(_loop.Home, new LandingPlugins(
            _loop.Home, say: (plugin, line) => _loop.Output.Append($"plugin:{plugin}", line), log: _loop.Log, health: _loop.Health));

        // The landing gate (XAGENT1f, REVIEWENV1c): the second opinion, then the review. What holds the press says so before it,
        // and the press refuses it.
        // Said to every window while the press is under way (PRESSSTEP1); a plan is no press and says it to nobody.
        PressSteps.Reach(PressSteps.Gate);
        var gate = await trees.GateAsync(tree, questId, new ServiceReviewWorld(service), id, cancellationToken);
        // Named for the chain's first quest (WSR5), and a part of an entry with others for its entry's first part, on the entry's one
        // branch (ACCEPTALL1b, D167's 2026-10-15 amendment point 2): the call every door that lands one session makes (ACCEPTALL1f).
        // A name the person entered still lands as entered.
        var subject = Entering(await trees.OwnSubjectAsync(
            tree, id, questId, records, quest => service.FindQuestAsync(quest, cancellationToken),
            named => _loop.Events.Openings([named]).GetValueOrDefault(named), gate.Process, cancellationToken), request);
        var lead = EntryLead(records, id);

        if (request.Type == "LANDING")
        {
            // How it lands, by the process the gate read (WORKFLOW1f): a named workflow's landing step, or the rule.
            var plan = await trees.PlanAsync(tree, subject, cancellationToken, gate.Process);
            // Accept refuses until the tree's uncommitted paths are committed (D164 point 4): the page says so before the press.
            var uncommitted = UncommittedCount(await trees.UncommittedAsync(tree, cancellationToken));
            // ACCEPTALL1c (D167's amendment point 3): only a part of an entry with others can have been left out.
            return Planned(id, plan, uncommitted, gate, lead is null ? null : LeftOutOfEntry(id));
        }

        // The press answers what it showed beside Accept (the second-agent design §8.2–§8.3): a dispute or commits nobody read,
        // drawn with the gate's token, which the press sends back; a gate that moved since answers nothing.
        gate = await new OpinionPresses(_loop.Home, service).PressedAsync(id, gate, Optional(request, "answers"), tree, questId, cancellationToken);

        // The sessions in use, asked when the rule's tidy reaches the other session branches the work holds (LAND3).
        PressSteps.Reach(PressSteps.Landing);
        // LANDHOLD2: a landing of this tree still under way holds the press for up to five minutes; the page is told it waits.
        using var waiting = PressSteps.WhileWaiting(PressSteps.Landing);
        var landed = await trees.LandAsync(tree, subject, cancellationToken, async token => await LinesRoutes.InUseAsync(service, token), gate: gate,
            joins: subject.Joins);
        // Kept where the conversation is kept, so the landing and the plugin's word outlast the press (D100).
        if (landed.Landed) _loop.Events.Keep(id, LandingRules.Note(landed), line => _loop.Output.Append(id, line));
        if (_loop.Log is { } log && LandingGateWords.Held(id, trees.Owner(tree), gate, landed, ReviewDoors.Screen) is { } held)
        {
            SessionLog.WriteLanding(log, held);
        }

        _loop.Nudge();
        return new
        {
            Session = id, Done = landed.Landed, landed.Message, landed.Branch,
            Plugin = landed.Plugin is { } said
                ? new { Id = said.Plugin, said.Pushed, said.PullRequest, said.Message, said.Failed }
                : null,
            Review = Waits(gate.Review),
            Opinion = Opinion(gate.Opinion),
            Workflow = WorkflowWaits(gate.Workflow),
            // LANDSYNC1: what it left out, and the conflict that refused it with the words the review offers to say to the session.
            LeftOut = LeftOut(landed.LeftOut),
            Conflict = Conflict(landed.Conflict),
        };
    }

    /// <summary>
    /// <c>LANDING</c>'s answer: what a press would do (WSR1, D87), said before it, beside what holds it and how many paths the tree holds
    /// uncommitted (D164 point 4).
    /// </summary>
    /// <param name="leftOutOf">
    /// The branch its line of work landed on where the person left this part out of it (ACCEPTALL1c), so its review says so and leads with
    /// Discard; null where none did.
    /// </param>
    public static object Planned(string session, LandingPlan plan, int uncommitted, LandingGate gate, string? leftOutOf) => new
    {
        // Own (TREEFOLLOW1): the target is the branch the session's agent made, which lands as it stands. Stands (ACCEPTALL1g): the
        // target already stands, an entry's or a chain's branch, so the press moves it on rather than making it.
        Session = session, plan.Form, plan.Target, plan.Source, plan.Plugin, plan.Problem, plan.Own, plan.Stands, Uncommitted = uncommitted,
        Workflow = WorkflowWaits(gate.Workflow), Review = Waits(gate.Review), Opinion = Opinion(gate.Opinion),
        // LANDSYNC1 (D87's 2026-10-15 amendment point 4): what the press would leave out because the line holds it, what it would land,
        // and a conflict it would be refused for; each absent where the line holds none of the tree's commits.
        LeftOut = LeftOut(plan.LeftOut), Lands = plan.Lands?.Select(commit => new { Commit = Short(commit.Sha), commit.Subject }).ToArray(),
        Conflict = Conflict(plan.Conflict),
        LeftOutOf = leftOutOf,
    };

    /// <summary>The part a session's own Accept is named for (ACCEPTALL1b): the library's, <see cref="EntryAccepts.EntryLead"/>.</summary>
    public static SessionRecord? EntryLead(IReadOnlyList<SessionRecord> records, string session) => EntryAccepts.EntryLead(records, session);

    /// <summary>
    /// Where the person left this part out of its entry's Accept (ACCEPTALL1c): the branch its conversation's newest note names, while
    /// nothing accepted it since (<see cref="LandingRules.LeftOutOf"/>). A record that does not read says nothing left out, and the
    /// review offers Accept as before.
    /// </summary>
    private string? LeftOutOfEntry(string id)
    {
        try
        {
            return File.Exists(_loop.Events.PathOf(id)) ? LandingRules.LeftOutOf(_loop.Events.After(id, 0).Events) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return null;
        }
    }

    /// <summary>A landing's left-out commits on the wire (LANDSYNC1): short id, subject, why, and the line's commit with its patch.</summary>
    public static object[]? LeftOut(IReadOnlyList<LeftOutCommit>? leftOut) => leftOut?
        .Select(commit => new { Commit = Short(commit.Sha), commit.Subject, commit.Why, On = commit.On is { } on ? Short(on) : null })
        .ToArray<object>();

    /// <summary>A replay's conflict on the wire (LANDSYNC1): git's lines, and the rebase words the review's press says to the session.</summary>
    public static object? Conflict(LandingConflict? conflict) => conflict is null ? null : new { conflict.Says, conflict.Words };

    private static string Short(string sha) => TextBounds.ShortCommit(sha, 8);

    /// <summary>The workflow gate's own explanation and terminal doors, shared by the preview and the press.</summary>
    public static object? WorkflowWaits(WorkflowGateState? gate) => gate is null || gate.State == WorkflowGateStates.None
        ? null
        : new { gate.State, Holds = !gate.LetsGo, gate.Says, gate.Outside, gate.Lowered, gate.Otherwise,
            Workflow = gate.Process.Binding?.Workflow, Version = gate.Process.Binding?.Version, gate.Process.Binding?.Kind };

    /// <summary>
    /// What the second opinion's gate says beside a landing's plan or press (XAGENT1f, the second-agent design §8.1, §8.5): its
    /// state, whether it holds, whether the rule requires one, the reviewer and its label, the findings and the disputes counted,
    /// the commits unread, why none could be had, and the sentence; and the token a press sends back to answer what it showed.
    /// Null where the level asks no opinion, so a landing no rule touches answers as it did. The page draws it (XAGENT1g).
    /// </summary>
    public static object? Opinion(OpinionGateState gate) => gate.State == OpinionGateStates.None
        ? null
        : new
        {
            gate.State,
            Holds = !gate.LetsGo,
            gate.Required,
            Opinion = gate.Opinion?.Id,
            Reviewer = gate.Opinion?.Adapter,
            gate.Opinion?.Product,
            gate.Opinion?.Maker,
            gate.Opinion?.Label,
            Reviewing = gate.Opinion?.Session,
            Findings = gate.Opinion?.Findings?.Count,
            Disputes = gate.Disputes?.Count,
            gate.Since,
            gate.Code,
            Until = Moments.Write(gate.Until),
            gate.Later,
            Person = gate.Person?.Said,
            Answers = gate.PressAnswers ? gate.Token : null,
            Says = gate.Says,
        };

    // The second opinion's presses (XAGENT1f, the second-agent design §8.5, §9): the screen's half of `daoris-driver opinion`,
    // each the driver's own press (OpinionPresses), so both doors say and keep the same. A refusal is an answer, as a landing's is.
    /// <summary>
    /// The second opinion's gate for a session's work, and the person's presses on it (XAGENT1f): <c>OPINION_GATE</c> reads it;
    /// <c>ASK_OPINION</c> asks a pass the next look starts (<i>Ask now</i>, <i>Try again</i>, <i>Ask again</i>, <i>Ask the same
    /// agent, fresh</i> with <c>sameAgent</c>, or a struck quest's <i>Ask another agent for help</i> with <c>occasion: failure</c>);
    /// <c>OPINION_ANYWAY</c> and <c>OPINION_MYSELF</c> keep the person's answer with their words; <c>STOP_OPINION</c> stops a
    /// reviewer reading.
    /// </summary>
    /// <remarks>
    /// Never Ask Daoris's (§9, D110): asking spends an account at the person's choice, and going on, or reading it themselves, is a
    /// judgement no agent has made. Each answers the gate as it then stands, in <see cref="Opinion"/>'s shape.
    /// </remarks>
    [DriverRoute("OPINION_GATE")]
    [DriverRoute("ASK_OPINION")]
    [DriverRoute("OPINION_ANYWAY")]
    [DriverRoute("OPINION_MYSELF")]
    [DriverRoute("STOP_OPINION")]
    [Publishes("ASK_OPINION", Gates, Session = "id")]
    [Publishes("OPINION_ANYWAY", Gates, Session = "id")]
    [Publishes("OPINION_MYSELF", Gates, Session = "id")]
    [Publishes("STOP_OPINION", Gates)]
    private async Task<object?> OpinionPressAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        var presses = new OpinionPresses(_loop.Home, service);
        if (request.Type == "STOP_OPINION")
        {
            var opinion = PayloadHelper.GetRequiredValue<string>(request.Payload, "opinion");
            var stopped = await presses.StopAsync(opinion, _loop.Processes, cancellationToken);
            _loop.Nudge();
            return new { Opinion = opinion, Done = stopped.Done, stopped.Message };
        }

        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var words = Optional(request, "words");
        var pressed = request.Type switch
        {
            "ASK_OPINION" => await presses.AskAsync(
                id, Optional(request, "reviewer"), Flag(request, "sameAgent"), words,
                Optional(request, "occasion") == "failure" ? "failure" : "asked", cancellationToken),
            "OPINION_ANYWAY" => await presses.AnywayAsync(id, words, ReviewDoors.Screen, cancellationToken),
            "OPINION_MYSELF" => await presses.MyselfAsync(id, words, ReviewDoors.Screen, cancellationToken),
            _ => await presses.GateAsync(id, cancellationToken),
        };
        if (request.Type != "OPINION_GATE") _loop.Nudge();
        return new
        {
            Session = id, Done = pressed.Done, pressed.Message,
            Opinion = pressed.Gate is { } gate ? Opinion(gate.Opinion) : null,
            // What the review's *Second opinion* draws (XAGENT1g, design §9): the findings with their answers, beside the state.
            Detail = pressed.Gate is { } read ? OpinionDetail(read.Opinion, session => _loop.Usage.Of(session)) : null,
        };
    }

    /// <summary>
    /// The second opinion as the review draws it (XAGENT1g; the second-agent design §9): the reviewer, the candidate and whether it
    /// covers what would land, each finding with its weight, place, consequence, reproduction, sureness and proposal beside the
    /// working session's answer and how the driver read it, the recheck's word on it, what it read and could not, the tier, and
    /// the person's answer at the gate. Null where the level asks no opinion.
    /// </summary>
    /// <remarks>
    /// Read from the gate the driver judged, never from the host again: the findings are the reviewer's claims (§6.6), and what
    /// counts of an answer (a fix's commit checked, an unanswered finding) is the driver's reading. The tier is <c>person</c> where
    /// the person read it themselves, <c>none</c> where no agent could read it, and <c>agent</c> otherwise (§10).
    /// </remarks>
    public static object? OpinionDetail(OpinionGateState gate, Func<string, UsageEntry?>? usageOf = null)
    {
        // The reviewer's measured use (XAGENT1g4b, §6.3): read by the reviewer's own session; null where none was measured, never 0.
        object? Usage(OpinionView view) => usageOf?.Invoke(view.Session) is { } entry ? new { entry.Used, entry.Size } : null;
        object Pass<T>(OpinionView view, Func<OpinionFindingView, T> beside) => PassOf(view, Usage(view), beside);
        if (gate.State == OpinionGateStates.None) return null;
        var first = gate.Opinion;
        var disputed = gate.Disputes?.First ?? [];
        var raised = gate.Disputes?.Raised ?? [];
        return new
        {
            Tier = gate.State == OpinionGateStates.Myself ? "person" : first is null ? "none" : "agent",
            // Whether what was read covers what would land (§8.2): commits since it was read say it does not.
            Covers = gate.Since is null ? (bool?)null : gate.Since == 0,
            gate.Passes,
            gate.ToPerson,
            gate.Answered,
            Person = gate.Person is { } word
                ? new { word.Said, At = Moments.Write(word.At), word.Words, word.Door }
                : null,
            First = first is null ? null : Pass(first, finding => new
            {
                Answer = first.AnswerTo(finding.Number) is { } answer
                    ? new { answer.Said, answer.Commit, answer.Evidence, answer.Why }
                    : null,
                // How the driver read the answer as the turn ended (§6.4): what counts, and why it does not count as said.
                Counts = gate.Answers?.Findings.FirstOrDefault(row => row.Finding == finding.Number) is { } row
                    ? new { row.Counts, row.Why, row.Fix }
                    : null,
                Rechecked = gate.Recheck?.Rechecked.GetValueOrDefault(finding.Number),
                Disputed = disputed.Contains(finding.Number),
            }),
            Recheck = gate.Recheck is { } recheck ? Pass(recheck, finding => new
            {
                Answer = (object?)null,
                Counts = (object?)null,
                Rechecked = (string?)null,
                Disputed = raised.Contains(finding.Number),
            }) : null,
        };

        static object PassOf<T>(OpinionView view, object? usage, Func<OpinionFindingView, T> beside) => new
        {
            Usage = usage,
            view.Id, view.Occasion, view.Pass, view.State, view.Why, view.Base, view.Tip, Commits = view.Commits.Count, view.Minutes,
            Reviewer = view.Adapter, view.Product, view.Maker, view.Label, Reviewing = view.Session, view.HandedTo, view.Read, view.Limits,
            Findings = view.Findings?.Select(finding => new
            {
                finding.Number, finding.Weight, finding.Where, finding.Claim, finding.Consequence, finding.Reproduce, finding.Sure,
                finding.Proposal, Beside = beside(finding),
            }).ToArray(),
        };
    }

    /// <summary>
    /// What waits on the person at the second opinion's gate on this machine (XAGENT1g; the second-agent design §9): each work owed
    /// an opinion whose gate holds for the person's answer, oldest first, with the gate as <see cref="Opinion"/> says it. *What needs
    /// you* lists them.
    /// </summary>
    /// <remarks>
    /// <para><b>What waits on the person</b> (<see cref="WaitsOnPerson"/>): a dispute, a required opinion none could be had for, and
    /// commits nobody read where the work lands by itself (D145), since there no press of theirs is coming to answer them.</para>
    ///
    /// <para><b>Read from the look</b> (XAGENT1g3): the gates the driver's last look judged for each opinion owed
    /// (<see cref="DriverLoop.Judged"/>, as every landing door judges them), so a poll runs no git and asks no service. A judgment
    /// is as of that look, at most <see cref="DriverConfig.PollSeconds"/> old; a person's press nudges the loop, so it is not left
    /// waiting a whole poll, and the tick the loop forwards when what waits changes has the page ask again.</para>
    /// </remarks>
    [DriverRoute("OPINION_WAITS")]
    private Task<object?> OpinionWaitsAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        _ = _loop.Service ?? throw NotReady();
        var waits = new List<object>();
        foreach (var judged in _loop.Judged.Latest ?? [])
        {
            if (!WaitsOnPerson(judged.Gate, judged.Auto)) continue;
            waits.Add(new
            {
                judged.Session, judged.Quest, judged.Repository, judged.Workspace,
                Since = Moments.Write(judged.Since),
                judged.Auto,
                Opinion = Opinion(judged.Gate),
            });
        }

        return Task.FromResult<object?>(new { Waits = waits });
    }

    /// <summary>
    /// Whether the second opinion's gate waits on the person's answer (the second-agent design §9's *What needs you*): a dispute, a
    /// required opinion none could be had for, or commits nobody read where the work lands by itself (<paramref name="auto"/>).
    /// </summary>
    public static bool WaitsOnPerson(OpinionGateState gate, bool auto) => gate.State switch
    {
        OpinionGateStates.Disputed => true,
        OpinionGateStates.Unavailable => gate.Required,
        OpinionGateStates.CommitsSince => auto,
        _ => false,
    };

    /// <summary>
    /// What the review's gate says beside a landing's plan or press (REVIEWENV1c, design §3.1): its state, the environment and the
    /// sentence while it holds the work, so the page can say <i>Waits for your review in <c>environment</c></i> where <i>Accept…</i>
    /// would be (REVIEWENV1g); null where nothing waits, so a landing no review touches answers as it did.
    /// </summary>
    public static object? Waits(ReviewGateState review) => review.LetsGo
        ? null
        : new { review.State, review.Decision.Environment, review.Decision.Level, Quest = review.Decision.SetUpStep?.Id, Says = review.Says };

    // A branch a landing made, handed to a landing plugin afterwards (WSR5b): what a press would do,
    // said before it, and the press — `daoris-driver trees hand` is the terminal's door (D50).
    /// <summary>
    /// A session's landed branch handed to a landing plugin after its landing (WSR5b): the plan before the
    /// press, or the press. The branch is the one this session's landing made and recorded; a session whose
    /// landing made none answers no branch, which is information, not a failure.
    /// </summary>
    /// <remarks>
    /// A refusal is an answer, as a landing's is. The checkout is the registry's, since the tree may have been
    /// tidied away; a repository with no checkout here is answered in the same sentence form.
    /// </remarks>
    [DriverRoute("HANDOFF_PLAN")]
    [DriverRoute("HANDOFF")]
    [Publishes("HANDOFF", StateScopes.HandOff | StateScopes.Sweep, Session = "id")]
    private async Task<object?> HandOffAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var named = Optional(request, "plugin");
        var service = _loop.Service ?? throw NotReady();
        var trees = new SessionTrees(_loop.Home, new LandingPlugins(
            _loop.Home, say: (plugin, line) => _loop.Output.Append($"plugin:{plugin}", line), log: _loop.Log, health: _loop.Health));

        // The session's newest landing: a session lands once, and a second press is refused while its branch stands.
        var entry = trees.Recorded.Find(id).FirstOrDefault(each => string.Equals(each.Session, id, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return request.Type == "HANDOFF_PLAN"
                ? new { Session = id, Branch = (string?)null }
                : (object)new { Session = id, Done = false, Message = TreeHand.NotLanded(id), Branch = (string?)null };
        }

        var root = await CheckoutOfAsync(service, entry.Repository, cancellationToken);
        if (root is null)
        {
            var none = $"`{entry.Repository}` has no checkout on this machine, so there is no `{entry.Branch}` here to hand on.";
            return request.Type == "HANDOFF_PLAN"
                ? new { Session = id, Branch = (string?)entry.Branch, entry.Repository, Plugin = (string?)null, Problem = (string?)none, entry.PullRequest, Commits = 0 }
                : (object)new { Session = id, Done = false, Message = none, Branch = (string?)entry.Branch };
        }

        if (request.Type == "HANDOFF_PLAN")
        {
            var plan = await trees.HandPlanAsync(root, entry, named, cancellationToken);
            return new { Session = id, Branch = (string?)plan.Branch, plan.Repository, plan.Plugin, plan.Problem, plan.PullRequest, plan.Commits };
        }

        PressSteps.Reach(PressSteps.HandOff);
        using var waiting = PressSteps.WhileWaiting(PressSteps.HandOff);
        var handed = await HandAsync(_loop, trees, root, entry, named, cancellationToken);
        return new
        {
            Session = id, Done = handed.Handed, handed.Message, Branch = (string?)handed.Branch,
            Plugin = handed.Plugin is { } said
                ? new { Id = said.Plugin, said.Pushed, said.PullRequest, said.Message, said.Failed }
                : null,
        };
    }

    /// <summary>A repository's checkout here, from the registry — or null where it has none on this machine.</summary>
    internal static async Task<string?> CheckoutOfAsync(ServiceClient service, string repository, CancellationToken cancellationToken)
    {
        var root = (await service.RegistryAsync(cancellationToken))
            .FirstOrDefault(row => string.Equals(row.Repository, repository, StringComparison.OrdinalIgnoreCase))?.Root;
        return string.IsNullOrWhiteSpace(root) || !Directory.Exists(root) ? null : root;
    }

    /// <summary>
    /// The hand-off's press, which the review's button and Ask Daoris's card share (WSR5b): the plugin spoken
    /// to, its word kept where the landing's own note is (D100), and the loop told to look again. A static over the loop,
    /// which Help's doors reach by this class's name (PART10f).
    /// </summary>
    internal static async Task<TreeHand> HandAsync(
        DriverLoop loop, SessionTrees trees, string root, LandedBranch entry, string? plugin, CancellationToken cancellationToken)
    {
        var handed = await trees.HandAsync(root, entry, plugin, cancellationToken);
        if (handed.Plugin is not null) loop.Events.Keep(entry.Session, LandingRules.HandNote(handed), line => loop.Output.Append(entry.Session, line));
        loop.Nudge();
        return handed;
    }
}
