using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;
using static Daoris.Desktop.GitAnswers;

namespace Daoris.Desktop;

/// <summary>
/// Git's reads and the working-tree acts, the page's `bridge/git.ts`, for Repositories' Branches, History and Changes tabs: each the
/// driver's own, through the git the agents run. The ref acts' doors are here and their bodies <see cref="GitRefDesk"/>'s.
/// </summary>
public sealed class GitRoutes(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;

    // One reader for the module's life, so its answers, kept by whole commit ids (D147 §4.2), outlive a call: a page asking the
    // same commit twice starts no second git.
    private readonly GitReads _gitReads = new();

    private readonly ActRuns _acts = context.GitActs;

    private readonly GitRefDesk _refs = new(context);

    // The list, read as the terminal's `git branches` reads it. 🔴 No machine path leaves: a branch's tree by its folder's name,
    // and a sentence that named a root names the repository.
    [DriverRoute("GIT_BRANCHES", PipeClass.Open)]
    private async Task<object?> GitBranchesAsync(GitBranchesRead read, CancellationToken cancellationToken)
    {
        var named = read.Repository;
        var service = _loop.Service ?? throw NotReady();
        var rows = LinesRoutes.Checkouts(await service.RegistryAsync(cancellationToken).ConfigureAwait(false), named, read.Workspace);

        IReadOnlyList<SessionRecord>? sessions = null;
        var unread = false;
        try
        {
            sessions = await service.SessionRecordsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException)
        {
            unread = true;
        }

        var scope = read.All ?SyncScope.Everything : named is not null ? SyncScope.Named([named]) : SyncScope.Held;
        var list = await GitBranches.ListAsync(rows, DriverConfig.Load(_loop.ConfigPath), _loop.Home, sessions, scope, cancellationToken)
            .ConfigureAwait(false);

        string?[] RootsOf(RepositoryBranches each) => [.. rows
            .Where(row => string.Equals(row.Repository, each.Repository, StringComparison.Ordinal)
                && string.Equals(RemoteTarget.Workspace(row.Workspace), each.Workspace, StringComparison.Ordinal))
            .Select(row => row.Root)];
        // GITAUTH1e: a repository's Setup asks for its remotes with what serves each (the git-auth design §7.3), beside the list.
        var remotes = new Dictionary<(string, string), IReadOnlyList<GitRemoteRow>>();
        if (read.Remotes && (named is not null || read.Workspace is not null))
        {
            foreach (var each in list.Repositories)
            {
                if (RootsOf(each).FirstOrDefault(root => !string.IsNullOrEmpty(root) && Directory.Exists(root)) is { } root)
                {
                    remotes[(each.Repository, each.Workspace)] = await GitRemoteRows.ReadAsync(
                        _loop.Home, root, each.Repository, each.Workspace == RemoteTarget.DefaultWorkspace ? null : each.Workspace, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        return new
        {
            Repositories = list.Repositories.Select(each => new
            {
                each.Repository, each.Workspace, each.Line, each.Fetch, each.Holds, each.Missing, each.Commands,
                Remotes = remotes.TryGetValue((each.Repository, each.Workspace), out var rows2) ? rows2 : null,
                Problem = WithoutRoots(each.Problem, RootsOf(each), each.Repository),
                Branches = each.Branches.Select(branch => new
                {
                    branch.Name, branch.Kind, branch.Commit, branch.At, branch.Subject, branch.Ahead, branch.Behind,
                    Tree = branch.Worktree is { Length: > 0 } tree ? Path.GetFileName(tree.TrimEnd('/', '\\')) : null,
                    branch.Session, branch.Landed,
                }).ToArray(),
            }).ToArray(),
            list.Apart,
            SessionsUnread = unread,
        };
    }

    // The checkout's current branch, for the composer's tree chip (NEW1e-a): `{ branch }`, null where HEAD is detached. Its own
    // route, since the list costs every branch. The checkout is found as GitAnsweredAsync finds it; 🔴 no path in any answer or
    // sentence (D47 §4), and the git failure's words name none either.
    [DriverRoute("CHECKOUT_BRANCH", PipeClass.Open)]
    private async Task<object?> CheckoutBranchAsync(GitCheckoutRead read, CancellationToken cancellationToken)
    {
        var repository = read.Repository ?? throw new DriverException("a Git read names its repository.");
        var service = _loop.Service ?? throw NotReady();
        var rows = LinesRoutes.Checkouts(await service.RegistryAsync(cancellationToken).ConfigureAwait(false), repository, read.Workspace);
        if (rows.FirstOrDefault(row => Directory.Exists(row.Root)).Root is not { } root)
        {
            throw Refusals.Because(Refusals.GitNoCheckout, $"{repository} has no checkout on this machine, so there is no branch to name.");
        }

        try
        {
            return new { Branch = await WorkingTree.BranchAsync(root, cancellationToken).ConfigureAwait(false) };
        }
        catch (DriverException error)
        {
            throw new DriverException(WithoutRoots(error.Message, [root], repository) ?? "git could not name this checkout's branch.");
        }
    }

    // A graph page from the tips named (a branch's whole commit ids, resolved by the list), the commits drawn in date order.
    [DriverRoute("GIT_GRAPH", PipeClass.Open)]
    private async Task<object?> GitGraphAsync(GitGraphRead read, CancellationToken cancellationToken)
    {
        List<string> tips = [.. read.Tips ?? []];
        return await GitAnsweredAsync(read.Repository, read.Workspace, cancellationToken, (root, token) =>
            _gitReads.GraphAsync(root, tips, GitPage(read.Page), read.SinceParted, token));
    }

    // A commit's page: its fields, whole message and changes against its first parent.
    [DriverRoute("GIT_COMMIT", PipeClass.Open)]
    private async Task<object?> GitCommitAsync(GitCommitRead read, CancellationToken cancellationToken) =>
        await GitAnsweredAsync(read.Repository, read.Workspace, cancellationToken, (root, token) =>
            _gitReads.CommitAsync(root, read.Commit ?? "", token));

    // A file's history at a commit, following renames, a page at a time.
    [DriverRoute("GIT_HISTORY", PipeClass.Open)]
    private async Task<object?> GitHistoryAsync(GitHistoryRead read, CancellationToken cancellationToken) =>
        await GitAnsweredAsync(read.Repository, read.Workspace, cancellationToken, (root, token) =>
            _gitReads.HistoryAsync(root, read.Commit ?? "", read.Path ?? "", GitPage(read.Page), token));

    // A file as a commit holds it, each line with the commit that last changed it.
    [DriverRoute("GIT_BLAME", PipeClass.Open)]
    private async Task<object?> GitBlameAsync(GitBlameRead read, CancellationToken cancellationToken) =>
        await GitAnsweredAsync(read.Repository, read.Workspace, cancellationToken, (root, token) =>
            _gitReads.BlameAsync(root, read.Commit ?? "", read.Path ?? "", token));

    // Two commits compared: the commits only one side holds, bounded (D147 §2.6: not paged), and the changes.
    [DriverRoute("GIT_COMPARE", PipeClass.Open)]
    private async Task<object?> GitCompareAsync(GitCompareRead read, CancellationToken cancellationToken) =>
        await GitAnsweredAsync(read.Repository, read.Workspace, cancellationToken, (root, token) =>
            _gitReads.CompareAsync(root, read.Left ?? "", read.Right ?? "", read.Everything, token));

    // The Changes tab's read, three fresh observations of the checkout, kept nowhere across opens. Told by event: it starts git.
    [DriverRoute("GIT_CHANGES", PipeClass.Open)]
    [ByEvent("GIT_CHANGES", Untold = "the checkout's index, tree and untracked files, which no scope tells")]
    private async Task<object?> GitChangesAsync(GitCheckoutRead read, CancellationToken cancellationToken) =>
        await GitAnsweredAsync(read.Repository, read.Workspace, cancellationToken, (root, token) => GitChanges.ReadAsync(root, token));

    // One file's hunks on a side, with the ids a press must still find equal.
    [DriverRoute("GIT_HUNKS", PipeClass.Open)]
    [ByEvent("GIT_HUNKS", Untold = "the checkout's index and the file's bytes, which no scope tells")]
    private async Task<object?> GitHunksAsync(GitHunksRead read, CancellationToken cancellationToken) =>
        await GitAnsweredAsync(read.Repository, read.Workspace, cancellationToken, (root, token) =>
            GitHunks.ReadAsync(root, read.Path ?? "", read.Side ?? "", token));

    // Stage and unstage a file, hunks or lines of it, and Undo of such an act: each one act of the journal under the checkout's
    // lock, on the registered checkout, never a session's tree. A refusal is data (`done: false`), never thrown.
    [DriverRoute("GIT_STAGE", PipeClass.Window)]
    [Owner("GIT_STAGE", Repository = "repository", Workspace = "workspace")]
    [Publishes("GIT_STAGE", StateScopes.Checkouts)]
    private Task<object?> GitStageAsync(GitStageAct act, CancellationToken cancellationToken) => StageOrUnstageAsync(StageVerbs.Stage, act, cancellationToken);

    [DriverRoute("GIT_UNSTAGE", PipeClass.Window)]
    [Owner("GIT_UNSTAGE", Repository = "repository", Workspace = "workspace")]
    [Publishes("GIT_UNSTAGE", StateScopes.Checkouts)]
    private Task<object?> GitUnstageAsync(GitStageAct act, CancellationToken cancellationToken) => StageOrUnstageAsync(StageVerbs.Unstage, act, cancellationToken);

    private async Task<object?> StageOrUnstageAsync(string verb, GitStageAct act, CancellationToken cancellationToken)
    {
        var (context, refused) = await WorkContextAsync("a stage", act.Repository, act.Workspace, cancellationToken).ConfigureAwait(false);
        if (context is null) return refused;
        var picks = new List<HunkPick>();
        foreach (var pick in act.Picks ?? [])
        {
            var lines = pick.Lines is { ValueKind: JsonValueKind.Array } each
                ? each.EnumerateArray().Where(line => line.ValueKind == JsonValueKind.Number).Select(line => line.GetInt32()).ToList()
                : null;
            picks.Add(new HunkPick(pick.Id ?? "", lines));
        }

        var press = new StagePress(verb, act.Path ?? "", act.Head, act.Index ?? "", act.Source ?? "", picks);
        // The act is not the window's to cancel once its first effect begins; the watch ends only a wait before it.
        return Staged(await new StageActs(new ActJournal(_loop.Home)).RunAsync(context, press, cancellationToken).ConfigureAwait(false), context.Repository, context.Checkout);
    }

    [DriverRoute("GIT_UNDO", PipeClass.Window)]
    [Owner("GIT_UNDO", Repository = "repository", Workspace = "workspace")]
    [Publishes("GIT_UNDO", StateScopes.Checkouts)]
    private async Task<object?> GitUndoAsync(GitActTarget target, CancellationToken cancellationToken)
    {
        var (context, refused) = await WorkContextAsync("a stage", target.Repository, target.Workspace, cancellationToken).ConfigureAwait(false);
        if (context is null) return refused;
        var journal = new ActJournal(_loop.Home);
        var undone = target.Act ?? "";
        // GITAPP2j: a resolve and a mark are undone by Resolve's own rules, which stand in a merge or a rebase.
        return Staged(
            journal.Act(undone)?.Kind is ResolveVerbs.Resolve or ResolveVerbs.Mark
                ? await new ResolveActs(journal).UndoAsync(context, undone, cancellationToken).ConfigureAwait(false)
                : await new StageActs(journal).UndoAsync(context, undone, cancellationToken).ConfigureAwait(false),
            context.Repository, context.Checkout);
    }

    // A file in conflict read by its stages, one conflict at a time. Told by event: it starts git.
    [DriverRoute("GIT_CONFLICT", PipeClass.Open)]
    [ByEvent("GIT_CONFLICT", Untold = "the checkout's index stages and the file's bytes, which no scope tells")]
    private async Task<object?> GitConflictAsync(GitConflictRead read, CancellationToken cancellationToken) =>
        await GitAnsweredAsync(read.Repository, read.Workspace, cancellationToken, (root, token) => GitConflicts.ReadAsync(root, read.Path ?? "", token));

    // *Resolve* (ours, theirs or both per conflict) and *Mark resolved*, each one act of the journal; Undo of either is `GIT_UNDO`.
    [DriverRoute("GIT_RESOLVE", PipeClass.Window)]
    [Owner("GIT_RESOLVE", Repository = "repository", Workspace = "workspace")]
    [Publishes("GIT_RESOLVE", StateScopes.Checkouts)]
    private Task<object?> GitResolveAsync(GitResolveAct act, CancellationToken cancellationToken) => ResolveOrMarkAsync(ResolveVerbs.Resolve, act, cancellationToken);

    [DriverRoute("GIT_MARK_RESOLVED", PipeClass.Window)]
    [Owner("GIT_MARK_RESOLVED", Repository = "repository", Workspace = "workspace")]
    [Publishes("GIT_MARK_RESOLVED", StateScopes.Checkouts)]
    private Task<object?> GitMarkResolvedAsync(GitResolveAct act, CancellationToken cancellationToken) => ResolveOrMarkAsync(ResolveVerbs.Mark, act, cancellationToken);

    private async Task<object?> ResolveOrMarkAsync(string verb, GitResolveAct act, CancellationToken cancellationToken)
    {
        var (context, refused) = await WorkContextAsync("a resolve", act.Repository, act.Workspace, cancellationToken).ConfigureAwait(false);
        if (context is null) return refused;
        var picks = (act.Picks ?? []).Select(pick => new ResolvePick(pick.Id ?? "", pick.Take ?? "")).ToList();
        var press = new ResolvePress(verb, act.Path ?? "", act.Head, act.Base ?? "", act.Ours ?? "", act.Theirs ?? "", act.Worktree ?? "", picks);
        return Staged(await new ResolveActs(new ActJournal(_loop.Home)).RunAsync(context, press, cancellationToken).ConfigureAwait(false), context.Repository, context.Checkout);
    }

    /// <summary>An act's answer as the page reads it: whether it is done, its act (the id Undo takes) and the driver's sentence, with no machine path.</summary>
    private static object Staged(ActRun run, string repository, string? root) => new
    {
        Done = !run.Refused && run.Outcome == ActOutcomes.Done,
        run.Refused,
        run.Act,
        run.Outcome,
        Said = WithoutRoots(run.Said, [root], repository),
    };

    // What a commit or an amend would rest on, read fresh. Told by event: it starts git.
    [DriverRoute("GIT_COMMIT_PLAN", PipeClass.Open)]
    [ByEvent("GIT_COMMIT_PLAN", Untold = "the checkout's index, HEAD and hooks, which no scope tells")]
    private async Task<object?> GitCommitPlanAsync(GitCommitPlanRead read, CancellationToken cancellationToken) =>
        await GitAnsweredAsync(read.Repository, read.Workspace, cancellationToken, (root, token) =>
            new CommitActs(new ActJournal(_loop.Home)).PlanAsync(root, read.Verb == CommitVerbs.Amend ? CommitVerbs.Amend : CommitVerbs.Commit, token));

    // *Commit* and *Amend* of the staged changes, one commit step of the journal; the repository's hooks run and are never skipped.
    [DriverRoute("GIT_MAKE_COMMIT", PipeClass.Window)]
    [Owner("GIT_MAKE_COMMIT", Repository = "repository", Workspace = "workspace")]
    [Publishes("GIT_MAKE_COMMIT", StateScopes.Checkouts | StateScopes.Driver)]
    private Task<object?> GitMakeCommitAsync(GitCommitAct act, CancellationToken cancellationToken) => CommitOrAmendAsync(CommitVerbs.Commit, act, cancellationToken);

    [DriverRoute("GIT_AMEND", PipeClass.Window)]
    [Owner("GIT_AMEND", Repository = "repository", Workspace = "workspace")]
    [Publishes("GIT_AMEND", StateScopes.Checkouts | StateScopes.Driver)]
    private Task<object?> GitAmendAsync(GitCommitAct act, CancellationToken cancellationToken) => CommitOrAmendAsync(CommitVerbs.Amend, act, cancellationToken);

    private async Task<object?> CommitOrAmendAsync(string verb, GitCommitAct act, CancellationToken cancellationToken)
    {
        var (context, refused) = await WorkContextAsync("a commit", act.Repository, act.Workspace, cancellationToken).ConfigureAwait(false);
        if (context is null) return refused;
        var press = new CommitPress(verb, act.Message ?? "", act.Head, act.Tree ?? "");
        return Acted(await new CommitActs(new ActJournal(_loop.Home)).RunAsync(context, press, cancellationToken).ConfigureAwait(false), context);
    }

    // What a switch to a branch would rest on, read fresh. Told by event: it starts git.
    [DriverRoute("GIT_SWITCH_PLAN", PipeClass.Open)]
    [ByEvent("GIT_SWITCH_PLAN", Untold = "the checkout's HEAD, branches, index and the other trees, which no scope tells")]
    private async Task<object?> GitSwitchPlanAsync(GitSwitchPlanRead read, CancellationToken cancellationToken) =>
        await GitAnsweredAsync(read.Repository, read.Workspace, cancellationToken, (root, token) =>
            new SwitchActs(new ActJournal(_loop.Home)).PlanAsync(root, read.Branch ?? "", token));

    // *Switch* of the registered checkout to a local branch, never forced; undone by pressing Switch again on the branch it left.
    [DriverRoute("GIT_SWITCH", PipeClass.Window)]
    [Owner("GIT_SWITCH", Repository = "repository", Workspace = "workspace")]
    [Publishes("GIT_SWITCH", StateScopes.Checkouts | StateScopes.Driver)]
    private async Task<object?> GitSwitchAsync(GitSwitchAct act, CancellationToken cancellationToken)
    {
        var (context, refused) = await WorkContextAsync("an act", act.Repository, act.Workspace, cancellationToken).ConfigureAwait(false);
        if (context is null) return refused;
        var press = new SwitchPress(act.Branch ?? "", act.From, act.Head ?? "");
        return Acted(await new SwitchActs(new ActJournal(_loop.Home)).RunAsync(context, press, cancellationToken).ConfigureAwait(false), context);
    }

    // What a stash Save would rest on, the shared stash's tip among it. Told by event.
    [DriverRoute("GIT_STASH_PLAN", PipeClass.Open)]
    [ByEvent("GIT_STASH_PLAN", Untold = "the checkout's index, tree, untracked files and the shared stash, which no scope tells")]
    private async Task<object?> GitStashPlanAsync(GitCheckoutRead read, CancellationToken cancellationToken) =>
        await GitAnsweredAsync(read.Repository, read.Workspace, cancellationToken, (root, token) =>
            new StashActs(new ActJournal(_loop.Home)).PlanAsync(root, token));

    // Stash *Save*: `refs/stash` is shared with every linked tree, so a stash that moved since the plan refuses the press.
    [DriverRoute("GIT_STASH", PipeClass.Window)]
    [Owner("GIT_STASH", Repository = "repository", Workspace = "workspace")]
    [Publishes("GIT_STASH", StateScopes.Checkouts | StateScopes.Driver)]
    private async Task<object?> GitStashAsync(GitStashAct act, CancellationToken cancellationToken)
    {
        var (context, refused) = await WorkContextAsync("an act", act.Repository, act.Workspace, cancellationToken).ConfigureAwait(false);
        if (context is null) return refused;
        var press = new StashPress(act.Untracked, act.Message, act.Head ?? "", act.Stash);
        return Acted(await new StashActs(new ActJournal(_loop.Home)).RunAsync(context, press, cancellationToken).ConfigureAwait(false), context);
    }

    // Stash *Apply* by the stash commit's id, never its ordinal and never `pop`; a conflict stays for Resolve and the stash is kept.
    [DriverRoute("GIT_STASH_APPLY", PipeClass.Window)]
    [Owner("GIT_STASH_APPLY", Repository = "repository", Workspace = "workspace")]
    [Publishes("GIT_STASH_APPLY", StateScopes.Checkouts | StateScopes.Driver)]
    private async Task<object?> GitStashApplyAsync(GitStashApplyAct act, CancellationToken cancellationToken)
    {
        var (context, refused) = await WorkContextAsync("an act", act.Repository, act.Workspace, cancellationToken).ConfigureAwait(false);
        if (context is null) return refused;
        var press = new StashApplyPress(act.Id ?? "", act.Head ?? "");
        return Acted(await new StashActs(new ActJournal(_loop.Home)).ApplyAsync(context, press, cancellationToken).ConfigureAwait(false), context);
    }

    /// <summary>The context of a working-tree act on the repository's registered checkout, or the answer that there is none.</summary>
    private async Task<(StageContext? Context, object? Refused)> WorkContextAsync(
        string act, string? named, string? workspace, CancellationToken cancellationToken)
    {
        var repository = named ?? throw new DriverException($"{act} names its repository.");
        var service = _loop.Service ?? throw NotReady();
        var rows = LinesRoutes.Checkouts(await service.RegistryAsync(cancellationToken).ConfigureAwait(false), repository, workspace);
        if (rows.FirstOrDefault(row => Directory.Exists(row.Root)).Root is not { } root)
        {
            return (null, Staged(new ActRun(null, null, $"{repository} has no checkout on this machine: nothing was changed.", Refused: true), repository, null));
        }

        return (new StageContext(repository, root, () => DriverConfig.Load(_loop.ConfigPath).MayChangeCheckout(repository), token => BranchTidying.BesideAsync(service, token)), null);
    }

    /// <summary>An act's answer as the page reads it, with the sentence's notes and no machine path.</summary>
    private static object Acted(ActRun run, StageContext context) => new
    {
        Done = !run.Refused && run.Outcome == ActOutcomes.Done,
        run.Refused,
        run.Act,
        run.Outcome,
        Said = WithoutRoots(run.Said, [context.Checkout], context.Repository),
        Notes = run.Notes is null ? null : WithoutRoots(run.Notes, [context.Checkout], context.Repository),
    };

    // *New branch* and *New tag* at a commit, one `create` act of the journal; a ref act writes no working tree. A terminal's verb is
    // the same route asked for its words (`said`). A refusal is data; `act` names the record `GIT_REF_UNDO` deletes the ref at.
    [DriverRoute("GIT_REF_CREATE", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("GIT_REF_CREATE", Repository = "repository", Workspace = "workspace")]
    [GroundRepository("GIT_REF_CREATE", "repository", "workspace")]
    [Says("GIT_REF_CREATE", "repository:repository", "name:words")]
    [Publishes("GIT_REF_CREATE", StateScopes.Driver)]
    private Task<object?> GitRefCreateAsync(GitRefAct act, CancellationToken cancellationToken) => _refs.GitRefCreateAsync(act, cancellationToken);

    // *Delete* on a branch or a lightweight tag, at `at`, the commit the page showed: refused where the ref stands elsewhere, and for
    // the held branches, an annotated tag and an occupied branch.
    [DriverRoute("GIT_REF_DELETE", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("GIT_REF_DELETE", Repository = "repository", Workspace = "workspace")]
    [GroundRepository("GIT_REF_DELETE", "repository", "workspace")]
    [Says("GIT_REF_DELETE", "repository:repository", "name:words")]
    [Publishes("GIT_REF_DELETE", StateScopes.Driver)]
    private Task<object?> GitRefDeleteAsync(GitRefAct act, CancellationToken cancellationToken) => _refs.GitRefDeleteAsync(act, cancellationToken);

    // *Rename* on a branch or a lightweight tag, then its section under config.lock. An outcome `partial` stopped between the two:
    // the page offers GIT_REF_FINISH and GIT_REF_UNDO.
    [DriverRoute("GIT_REF_RENAME", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("GIT_REF_RENAME", Repository = "repository", Workspace = "workspace")]
    [GroundRepository("GIT_REF_RENAME", "repository", "workspace")]
    [Says("GIT_REF_RENAME", "repository:repository", "name:words", "to:words")]
    [Publishes("GIT_REF_RENAME", StateScopes.Driver)]
    private Task<object?> GitRefRenameAsync(GitRefRenameAct act, CancellationToken cancellationToken) => _refs.GitRefRenameAsync(act, cancellationToken);

    // GITAPP2e: the Finish beside a stopped rename's answer, the section moved alone where the ref and the section stand as the act journaled them.
    [DriverRoute("GIT_REF_FINISH", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("GIT_REF_FINISH", Repository = "repository", Workspace = "workspace")]
    [GroundRepository("GIT_REF_FINISH", "repository", "workspace")]
    [Says("GIT_REF_FINISH", "repository:repository")]
    [Publishes("GIT_REF_FINISH", StateScopes.Driver)]
    private Task<object?> GitRefFinishAsync(GitActTarget target, CancellationToken cancellationToken) => _refs.GitRefFinishAsync(target, cancellationToken);

    // GITAPP2c: the Undo beside a create's answer, the act's ref deleted only at the id the act made it at; refused as data where it moved.
    // GITAPP2d: and beside a delete's, the ref made again where the name is free and its section put back.
    [DriverRoute("GIT_REF_UNDO", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("GIT_REF_UNDO", Repository = "repository", Workspace = "workspace")]
    [GroundRepository("GIT_REF_UNDO", "repository", "workspace")]
    [Says("GIT_REF_UNDO", "repository:repository")]
    [Publishes("GIT_REF_UNDO", StateScopes.Driver)]
    private Task<object?> GitRefUndoAsync(GitActTarget target, CancellationToken cancellationToken) => _refs.GitRefUndoAsync(target, cancellationToken);

    // Whether *Fetch* is offered on a repository's Branches, and the command a person types. Told by event: it starts git, no network.
    [DriverRoute("GIT_FETCH_PLAN", PipeClass.Open)]
    [ByEvent("GIT_FETCH_PLAN", Untold = "the checkout's origin settings and the install's doors, which no scope tells")]
    private Task<object?> GitFetchPlanAsync(GitCheckoutRead read, CancellationToken cancellationToken) => _refs.GitFetchPlanAsync(read, cancellationToken);

    // *Fetch* of origin's branches, one `fetch` step that moves refs/remotes/origin/* alone; a terminal's verb asks it for its words.
    [DriverRoute("GIT_FETCH", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("GIT_FETCH", Repository = "repository", Workspace = "workspace")]
    [GroundRepository("GIT_FETCH", "repository", "workspace")]
    [Says("GIT_FETCH", "repository:repository")]
    [Publishes("GIT_FETCH", StateScopes.Driver)]
    private Task<object?> GitFetchAsync(GitFetchAct act, CancellationToken cancellationToken) => _refs.GitFetchAsync(act, cancellationToken);

    // A branch's history edited from objects, through the journal. `plan: true` answers what the edit would take and changes nothing;
    // the press carries the ids its plan pinned (`pins`) and is refused as data where any moved.
    [DriverRoute("GIT_HISTORY_EDIT", PipeClass.Person, Drain = PipeDrain.Waits)]
    [ClassedBy("GIT_HISTORY_EDIT", "plan", PipeClass.Open, "true", Unless = new[] { "said" })]
    [Owner("GIT_HISTORY_EDIT", Repository = "repository", Workspace = "workspace")]
    [GroundRepository("GIT_HISTORY_EDIT", "repository", "workspace")]
    [Says("GIT_HISTORY_EDIT", "repository:repository", "branch:words")]
    [Publishes("GIT_HISTORY_EDIT", StateScopes.Driver)]
    private Task<object?> GitHistoryEditAsync(GitHistoryEditAct act, CancellationToken cancellationToken) => _refs.GitHistoryEditAsync(act, cancellationToken);

    private Task<object?> GitAnsweredAsync<T>(
        string? named, string? workspace, CancellationToken cancellationToken, Func<string, CancellationToken, Task<GitAnswer<T>>> read) where T : class =>
        GitAnswers.GitAnsweredAsync(_loop, named, workspace, cancellationToken, read);

    /// <summary>A page the page asks for: absent is the first, and one past what a count holds is a page the driver refuses.</summary>
    private static int GitPage(long? asked) =>
        asked is { } page ? (int)Math.Clamp(page, -1, GitReads.LastPage + 1) : 0;

    // Try again on a held git remote: every hold on the repositories it names lifted, so the next fetch of each tries once.
    // Nothing connects here: the next act does.
    [DriverRoute("GIT_TRY_AGAIN", PipeClass.Open)]
    private Task<object?> GitTryAgainAsync(GitTryAgainPress press, CancellationToken cancellationToken)
    {
        var repositories = press.Repositories ?? [];
        if (repositories.Count == 0) throw new DriverException("Try again names the repositories it lifts.");
        var workspace = press.Workspace;
        var lifted = repositories.Sum(repository => GitHolds.LiftRepository(_loop.Home, repository, workspace));
        return Task.FromResult<object?>(new { Lifted = lifted });
    }
}
