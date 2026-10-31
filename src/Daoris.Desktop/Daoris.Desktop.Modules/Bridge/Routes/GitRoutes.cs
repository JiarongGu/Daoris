using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// Git's reads, the page's `bridge/git.ts` (GIT1d-a, D147 §2, D150): the branch list and a graph, a commit, a file's history,
/// a blame and a compare, for Repositories' Branches and History tabs. Shell-only: each is the driver's own read
/// (<see cref="GitBranches"/>, <see cref="GitReads"/>) through the git the agents run, and `daoris-driver git` is the terminal's.
/// </summary>
public sealed class GitRoutes(DriverLoop loop)
{
    private readonly DriverLoop _loop = loop;

    // One reader for the module's life, so its answers, kept by whole commit ids (D147 §4.2), outlive a call: a page asking the
    // same commit twice starts no second git.
    private readonly GitReads _gitReads = new();

    // The acts this module runs, journaled in the home's store; one object, so a run's reconciler knows its own acts.
    private readonly ActRuns _acts = new(new ActJournal(loop.Home));

    // The list, read as the terminal's `git branches` reads it: every checkout here, the session records naming each session's
    // branch (read without them where the service will not give them), D112's scope by `all` or `repository`. 🔴 No machine
    // path leaves (D47 §4): a branch's tree by its folder's name, and a sentence that named a root names the repository.
    [DriverRoute("GIT_BRANCHES")]
    private async Task<object?> GitBranchesAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var named = Optional(request, "repository");
        var service = _loop.Service ?? throw NotReady();
        var rows = LinesRoutes.Checkouts(await service.RegistryAsync(cancellationToken).ConfigureAwait(false), named, Optional(request, "workspace"));

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

        var scope = Flag(request, "all") ? SyncScope.Everything : named is not null ? SyncScope.Named([named]) : SyncScope.Held;
        var list = await GitBranches.ListAsync(rows, DriverConfig.Load(_loop.ConfigPath), _loop.Home, sessions, scope, cancellationToken)
            .ConfigureAwait(false);

        string?[] RootsOf(RepositoryBranches each) => [.. rows
            .Where(row => string.Equals(row.Repository, each.Repository, StringComparison.Ordinal)
                && string.Equals(RemoteTarget.Workspace(row.Workspace), each.Workspace, StringComparison.Ordinal))
            .Select(row => row.Root)];
        return new
        {
            Repositories = list.Repositories.Select(each => new
            {
                each.Repository, each.Workspace, each.Line, each.Fetch, each.Holds, each.Missing, each.Commands,
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
    [DriverRoute("CHECKOUT_BRANCH")]
    private async Task<object?> CheckoutBranchAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var repository = Optional(request, "repository") ?? throw new DriverException("a Git read names its repository.");
        var service = _loop.Service ?? throw NotReady();
        var rows = LinesRoutes.Checkouts(await service.RegistryAsync(cancellationToken).ConfigureAwait(false), repository, Optional(request, "workspace"));
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
    [DriverRoute("GIT_GRAPH")]
    private async Task<object?> GitGraphAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var tips = Strings(request, "tips");
        return await GitAnsweredAsync(request, cancellationToken, (root, token) =>
            _gitReads.GraphAsync(root, tips, GitPage(request), Flag(request, "sinceParted"), token));
    }

    // A commit's page: its fields, whole message and changes against its first parent.
    [DriverRoute("GIT_COMMIT")]
    private async Task<object?> GitCommitAsync(IpcRequest request, CancellationToken cancellationToken) =>
        await GitAnsweredAsync(request, cancellationToken, (root, token) =>
            _gitReads.CommitAsync(root, Optional(request, "commit") ?? "", token));

    // A file's history at a commit, following renames, a page at a time.
    [DriverRoute("GIT_HISTORY")]
    private async Task<object?> GitHistoryAsync(IpcRequest request, CancellationToken cancellationToken) =>
        await GitAnsweredAsync(request, cancellationToken, (root, token) =>
            _gitReads.HistoryAsync(root, Optional(request, "commit") ?? "", Optional(request, "path") ?? "", GitPage(request), token));

    // A file as a commit holds it, each line with the commit that last changed it.
    [DriverRoute("GIT_BLAME")]
    private async Task<object?> GitBlameAsync(IpcRequest request, CancellationToken cancellationToken) =>
        await GitAnsweredAsync(request, cancellationToken, (root, token) =>
            _gitReads.BlameAsync(root, Optional(request, "commit") ?? "", Optional(request, "path") ?? "", token));

    // Two commits compared: the commits only one side holds, bounded (D147 §2.6: not paged), and the changes.
    [DriverRoute("GIT_COMPARE")]
    private async Task<object?> GitCompareAsync(IpcRequest request, CancellationToken cancellationToken) =>
        await GitAnsweredAsync(request, cancellationToken, (root, token) =>
            _gitReads.CompareAsync(root, Optional(request, "left") ?? "", Optional(request, "right") ?? "", Flag(request, "everything"), token));

    // GITAPP2b (D204, the git-writes design §1 and §4): the Changes tab's read, three fresh observations of the checkout through
    // the counting seam, asked again at every ask (kept nowhere across opens, `readAt` saying when). Told by event: it starts git.
    [DriverRoute("GIT_CHANGES")]
    [ByEvent("GIT_CHANGES", Untold = "the checkout's index, tree and untracked files, which no scope tells")]
    private async Task<object?> GitChangesAsync(IpcRequest request, CancellationToken cancellationToken) =>
        await GitAnsweredAsync(request, cancellationToken, (root, token) => GitChanges.ReadAsync(root, token));

    // GITAPP2h (D204, the git-writes design §4): one file's hunks on a side, with the ids a press must still find equal. A read.
    [DriverRoute("GIT_HUNKS")]
    [ByEvent("GIT_HUNKS", Untold = "the checkout's index and the file's bytes, which no scope tells")]
    private async Task<object?> GitHunksAsync(IpcRequest request, CancellationToken cancellationToken) =>
        await GitAnsweredAsync(request, cancellationToken, (root, token) =>
            GitHunks.ReadAsync(root, Optional(request, "path") ?? "", Optional(request, "side") ?? "", token));

    // GITAPP2h (D204, the git-writes design §8): stage and unstage a file, hunks or lines of it, and Undo of such an act. Each is one act
    // of the journal under the checkout's lock; a refusal is answered as data (`done: false`, the driver's sentence), never thrown.
    // The checkout is the registered one the repository's page reads, never a session's tree.
    [DriverRoute("GIT_STAGE")]
    [DriverRoute("GIT_UNSTAGE")]
    [DriverRoute("GIT_UNDO")]
    [Publishes("GIT_STAGE", StateScopes.Checkouts)]
    [Publishes("GIT_UNSTAGE", StateScopes.Checkouts)]
    [Publishes("GIT_UNDO", StateScopes.Checkouts)]
    private async Task<object?> GitStageAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var repository = Optional(request, "repository") ?? throw new DriverException("a stage names its repository.");
        var service = _loop.Service ?? throw NotReady();
        var rows = LinesRoutes.Checkouts(await service.RegistryAsync(cancellationToken).ConfigureAwait(false), repository, Optional(request, "workspace"));
        if (rows.FirstOrDefault(row => Directory.Exists(row.Root)).Root is not { } root)
        {
            return Staged(new ActRun(null, null, $"{repository} has no checkout on this machine: nothing was changed.", Refused: true), repository, null);
        }

        var context = new StageContext(
            repository, root, () => DriverConfig.Load(_loop.ConfigPath).MayChangeCheckout(repository), token => BranchTidying.BesideAsync(service, token));
        var journal = new ActJournal(_loop.Home);
        var acts = new StageActs(journal);
        // The act is not the window's to cancel once its first effect begins; the watch ends only a wait before it.
        if (request.Type == "GIT_UNDO")
        {
            var undone = Optional(request, "act") ?? "";
            // GITAPP2j: a resolve and a mark are undone by Resolve's own rules, which stand in a merge or a rebase.
            return Staged(
                journal.Act(undone)?.Kind is ResolveVerbs.Resolve or ResolveVerbs.Mark
                    ? await new ResolveActs(journal).UndoAsync(context, undone, cancellationToken).ConfigureAwait(false)
                    : await acts.UndoAsync(context, undone, cancellationToken).ConfigureAwait(false),
                repository, root);
        }

        var picks = new List<HunkPick>();
        if (request.Payload is { } payload && payload.TryGetProperty("picks", out var chosen) && chosen.ValueKind == JsonValueKind.Array)
        {
            foreach (var pick in chosen.EnumerateArray())
            {
                var lines = pick.TryGetProperty("lines", out var each) && each.ValueKind == JsonValueKind.Array
                    ? each.EnumerateArray().Where(line => line.ValueKind == JsonValueKind.Number).Select(line => line.GetInt32()).ToList()
                    : null;
                picks.Add(new HunkPick(pick.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "", lines));
            }
        }

        var press = new StagePress(
            request.Type == "GIT_STAGE" ? StageVerbs.Stage : StageVerbs.Unstage, Optional(request, "path") ?? "", Optional(request, "head"),
            Optional(request, "index") ?? "", Optional(request, "source") ?? "", picks);
        return Staged(await acts.RunAsync(context, press, cancellationToken).ConfigureAwait(false), repository, root);
    }

    // GITAPP2j (D204, the git-writes design §8 Resolve): a file in conflict read by its stages, one conflict at a time. A read, told by event: it starts git.
    [DriverRoute("GIT_CONFLICT")]
    [ByEvent("GIT_CONFLICT", Untold = "the checkout's index stages and the file's bytes, which no scope tells")]
    private async Task<object?> GitConflictAsync(IpcRequest request, CancellationToken cancellationToken) =>
        await GitAnsweredAsync(request, cancellationToken, (root, token) => GitConflicts.ReadAsync(root, Optional(request, "path") ?? "", token));

    // GITAPP2j: *Resolve* (ours, theirs or both for each conflict, replacing the file's bytes) and *Mark resolved* (the file added). Each is one act of
    // the journal under the checkout's lock, answered as the stage's are; Undo of either is `GIT_UNDO`.
    [DriverRoute("GIT_RESOLVE")]
    [DriverRoute("GIT_MARK_RESOLVED")]
    [Publishes("GIT_RESOLVE", StateScopes.Checkouts)]
    [Publishes("GIT_MARK_RESOLVED", StateScopes.Checkouts)]
    private async Task<object?> GitResolveAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var repository = Optional(request, "repository") ?? throw new DriverException("a resolve names its repository.");
        var service = _loop.Service ?? throw NotReady();
        var rows = LinesRoutes.Checkouts(await service.RegistryAsync(cancellationToken).ConfigureAwait(false), repository, Optional(request, "workspace"));
        if (rows.FirstOrDefault(row => Directory.Exists(row.Root)).Root is not { } root)
        {
            return Staged(new ActRun(null, null, $"{repository} has no checkout on this machine: nothing was changed.", Refused: true), repository, null);
        }

        var context = new StageContext(
            repository, root, () => DriverConfig.Load(_loop.ConfigPath).MayChangeCheckout(repository), token => BranchTidying.BesideAsync(service, token));
        var picks = new List<ResolvePick>();
        if (request.Payload is { } payload && payload.TryGetProperty("picks", out var chosen) && chosen.ValueKind == JsonValueKind.Array)
        {
            foreach (var pick in chosen.EnumerateArray())
            {
                picks.Add(new ResolvePick(
                    pick.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "", pick.TryGetProperty("take", out var take) ? take.GetString() ?? "" : ""));
            }
        }

        var press = new ResolvePress(
            request.Type == "GIT_RESOLVE" ? ResolveVerbs.Resolve : ResolveVerbs.Mark, Optional(request, "path") ?? "", Optional(request, "head"),
            Optional(request, "base") ?? "", Optional(request, "ours") ?? "", Optional(request, "theirs") ?? "", Optional(request, "worktree") ?? "", picks);
        return Staged(await new ResolveActs(new ActJournal(_loop.Home)).RunAsync(context, press, cancellationToken).ConfigureAwait(false), repository, root);
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

    // GITAPP2i (D204, the git-writes design §8): what a commit or an amend would rest on (the tip, the index as a tree, the hooks it runs, who
    // commits), read fresh. A read, told by event: it starts git.
    [DriverRoute("GIT_COMMIT_PLAN")]
    [ByEvent("GIT_COMMIT_PLAN", Untold = "the checkout's index, HEAD and hooks, which no scope tells")]
    private async Task<object?> GitCommitPlanAsync(IpcRequest request, CancellationToken cancellationToken) =>
        await GitAnsweredAsync(request, cancellationToken, (root, token) =>
            new CommitActs(new ActJournal(_loop.Home)).PlanAsync(root, Optional(request, "verb") == CommitVerbs.Amend ? CommitVerbs.Amend : CommitVerbs.Commit, token));

    // GITAPP2i: *Commit* and *Amend* of the staged changes, one commit step of the journal under the checkout's lock; the repository's hooks run
    // and are never skipped. Answered as the stage's acts are: a refusal, or a hook's refusal, is data with the driver's sentence.
    [DriverRoute("GIT_MAKE_COMMIT")]
    [DriverRoute("GIT_AMEND")]
    [Publishes("GIT_MAKE_COMMIT", StateScopes.Checkouts | StateScopes.Driver)]
    [Publishes("GIT_AMEND", StateScopes.Checkouts | StateScopes.Driver)]
    private async Task<object?> GitMakeCommitAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var repository = Optional(request, "repository") ?? throw new DriverException("a commit names its repository.");
        var service = _loop.Service ?? throw NotReady();
        var rows = LinesRoutes.Checkouts(await service.RegistryAsync(cancellationToken).ConfigureAwait(false), repository, Optional(request, "workspace"));
        if (rows.FirstOrDefault(row => Directory.Exists(row.Root)).Root is not { } root)
        {
            return Staged(new ActRun(null, null, $"{repository} has no checkout on this machine: nothing was changed.", Refused: true), repository, null);
        }

        var context = new StageContext(
            repository, root, () => DriverConfig.Load(_loop.ConfigPath).MayChangeCheckout(repository), token => BranchTidying.BesideAsync(service, token));
        var press = new CommitPress(
            request.Type == "GIT_AMEND" ? CommitVerbs.Amend : CommitVerbs.Commit, Optional(request, "message") ?? "", Optional(request, "head"), Optional(request, "tree") ?? "");
        var run = await new CommitActs(new ActJournal(_loop.Home)).RunAsync(context, press, cancellationToken).ConfigureAwait(false);
        return new
        {
            Done = !run.Refused && run.Outcome == ActOutcomes.Done,
            run.Refused,
            run.Act,
            run.Outcome,
            Said = WithoutRoots(run.Said, [root], repository),
            Notes = run.Notes is null ? null : WithoutRoots(run.Notes, [root], repository),
        };
    }

    // GITAPP2k (D204, the git-writes design §8): what a switch to a branch would rest on (where HEAD is, whether tracked files are changed, the tree that
    // has the branch out, the hooks it runs), read fresh. A read, told by event: it starts git.
    [DriverRoute("GIT_SWITCH_PLAN")]
    [ByEvent("GIT_SWITCH_PLAN", Untold = "the checkout's HEAD, branches, index and the other trees, which no scope tells")]
    private async Task<object?> GitSwitchPlanAsync(IpcRequest request, CancellationToken cancellationToken) =>
        await GitAnsweredAsync(request, cancellationToken, (root, token) =>
            new SwitchActs(new ActJournal(_loop.Home)).PlanAsync(root, Optional(request, "branch") ?? "", token));

    // GITAPP2k: *Switch* of the registered checkout to a local branch, never forced; refused as data where tracked files are changed, an
    // operation is in progress or the branch is out in another tree. It is undone by pressing Switch again on the branch it left.
    [DriverRoute("GIT_SWITCH")]
    [Publishes("GIT_SWITCH", StateScopes.Checkouts | StateScopes.Driver)]
    private async Task<object?> GitSwitchAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var (context, refused) = await WorkContextAsync(request, cancellationToken).ConfigureAwait(false);
        if (context is null) return refused;
        var press = new SwitchPress(Optional(request, "branch") ?? "", Optional(request, "from"), Optional(request, "head") ?? "");
        return Acted(await new SwitchActs(new ActJournal(_loop.Home)).RunAsync(context, press, cancellationToken).ConfigureAwait(false), context);
    }

    // GITAPP2k: what a stash Save would rest on: the changes, the shared stash's tip and the trees that share it. A read, told by event.
    [DriverRoute("GIT_STASH_PLAN")]
    [ByEvent("GIT_STASH_PLAN", Untold = "the checkout's index, tree, untracked files and the shared stash, which no scope tells")]
    private async Task<object?> GitStashPlanAsync(IpcRequest request, CancellationToken cancellationToken) =>
        await GitAnsweredAsync(request, cancellationToken, (root, token) =>
            new StashActs(new ActJournal(_loop.Home)).PlanAsync(root, token));

    // GITAPP2k: stash *Save*, tracked files only unless `untracked`; `refs/stash` is shared with every linked tree, so a stash that moved since the plan
    // refuses the press. Apply is a later act and Drop is deferred.
    [DriverRoute("GIT_STASH")]
    [Publishes("GIT_STASH", StateScopes.Checkouts | StateScopes.Driver)]
    private async Task<object?> GitStashAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var (context, refused) = await WorkContextAsync(request, cancellationToken).ConfigureAwait(false);
        if (context is null) return refused;
        var press = new StashPress(Flag(request, "untracked"), Optional(request, "message"), Optional(request, "head") ?? "", Optional(request, "stash"));
        return Acted(await new StashActs(new ActJournal(_loop.Home)).RunAsync(context, press, cancellationToken).ConfigureAwait(false), context);
    }

    /// <summary>The context of a working-tree act on the repository's registered checkout, or the answer that there is none.</summary>
    private async Task<(StageContext? Context, object? Refused)> WorkContextAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var repository = Optional(request, "repository") ?? throw new DriverException("an act names its repository.");
        var service = _loop.Service ?? throw NotReady();
        var rows = LinesRoutes.Checkouts(await service.RegistryAsync(cancellationToken).ConfigureAwait(false), repository, Optional(request, "workspace"));
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

    // GITAPP2c (D204, the git-writes design §6, §9): *New branch* and *New tag* at a commit, one `create` run as an act of the journal
    // (GitRefActs). A ref act writes no working tree, so it asks no switch. The page presses it after its own confirmation; a terminal's
    // `git branch|tag <repository> <name> --yes` is the same route asked for its words (`said`), run on the window's Confirm. A refusal
    // is data, `{ refused, said }`, never thrown; `act` names the journal's record, which `GIT_REF_UNDO` deletes the ref at.
    [DriverRoute("GIT_REF_CREATE")]
    [Publishes("GIT_REF_CREATE", StateScopes.Driver)]
    private async Task<object?> GitRefCreateAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var repository = Optional(request, "repository") ?? throw new DriverException("a ref act names its repository.");
        var kind = Optional(request, "kind");
        var name = Optional(request, "name");
        if (kind is not ("branch" or "tag") || string.IsNullOrWhiteSpace(name)) throw new DriverException("a create names its kind, `branch` or `tag`, and its name.");
        _ = _loop.Service ?? throw NotReady();

        var verb = new GitRefVerb(kind, repository, name, Optional(request, "at"), Yes: true);
        var world = new GitRefWorld(async (_, token) => await CheckoutOfAsync(repository, Optional(request, "workspace"), token).ConfigureAwait(false), () => _acts);
        if (Flag(request, "said"))
        {
            var writer = new StringWriter { NewLine = "\n" };
            var code = await GitRefCommand.ExecuteAsync(verb, world, "terminal", "window", writer, writer, cancellationToken).ConfigureAwait(false);
            var lines = writer.ToString().Split('\n');
            return new { Said = new TreesSaid(lines[^1].Length == 0 ? lines[..^1] : lines, [], code).ToWire() };
        }

        if (await world.CheckoutOf(repository, cancellationToken).ConfigureAwait(false) is not { } checkout) return NoCheckout(repository);
        var run = await GitRefActs.CreateAsync(_acts, new RefAsk(repository, checkout, kind, name, verb.At), "window", "window", cancellationToken).ConfigureAwait(false);
        return Answered(run, checkout, repository);
    }

    // GITAPP2d (D204, the git-writes design §6): *Delete* on a branch or a lightweight tag, one transaction of the delete, the recovery ref and each
    // holder's verify, then the branch's section. `at` is the commit the page showed; the act refuses where the ref stands elsewhere, and the held
    // branches (daoris/*, a landing's, the line, a together-bound one), an annotated tag and an occupied branch. Refusals are data, as the create's are.
    [DriverRoute("GIT_REF_DELETE")]
    [Publishes("GIT_REF_DELETE", StateScopes.Driver)]
    private async Task<object?> GitRefDeleteAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var repository = Optional(request, "repository") ?? throw new DriverException("a ref act names its repository.");
        var kind = Optional(request, "kind");
        var name = Optional(request, "name");
        if (kind is not ("branch" or "tag") || string.IsNullOrWhiteSpace(name)) throw new DriverException("a delete names its kind, `branch` or `tag`, and its name.");
        _ = _loop.Service ?? throw NotReady();

        var verb = new GitRefVerb(kind, repository, name, Optional(request, "at"), Yes: true, Delete: true);
        var world = new GitRefWorld(
            async (_, token) => await CheckoutOfAsync(repository, Optional(request, "workspace"), token).ConfigureAwait(false), () => _acts,
            async (_, checkout, token) => (await CanonicalLine.ResolveAsync(checkout, repository, Optional(request, "workspace"), DriverConfig.Load(_loop.ConfigPath), token).ConfigureAwait(false)).Branch);
        if (Flag(request, "said"))
        {
            var writer = new StringWriter { NewLine = "\n" };
            var code = await GitRefCommand.ExecuteAsync(verb, world, "terminal", "window", writer, writer, cancellationToken).ConfigureAwait(false);
            var lines = writer.ToString().Split('\n');
            return new { Said = new TreesSaid(lines[^1].Length == 0 ? lines[..^1] : lines, [], code).ToWire() };
        }

        if (await world.CheckoutOf(repository, cancellationToken).ConfigureAwait(false) is not { } checkout) return NoCheckout(repository);
        var line = await world.LineOf!(repository, checkout, cancellationToken).ConfigureAwait(false);
        var run = await GitRefActs.DeleteAsync(_acts, new RefDeleteAsk(repository, checkout, kind, name, verb.At, line), "window", "window", cancellationToken).ConfigureAwait(false);
        return Answered(run, checkout, repository);
    }

    // GITAPP2e (D204, the git-writes design §6): *Rename* on a branch or a lightweight tag, one transaction (the old name deleted, the new made, the old
    // tip kept on a recovery ref), then the branch's section renamed under config.lock. The refusals are the delete's, and a taken name. An answer of
    // outcome `partial` is a rename stopped between its ref and its section: the page offers GIT_REF_FINISH and GIT_REF_UNDO, each conditional.
    [DriverRoute("GIT_REF_RENAME")]
    [Publishes("GIT_REF_RENAME", StateScopes.Driver)]
    private async Task<object?> GitRefRenameAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var repository = Optional(request, "repository") ?? throw new DriverException("a ref act names its repository.");
        var kind = Optional(request, "kind");
        var name = Optional(request, "name");
        var to = Optional(request, "to");
        if (kind is not ("branch" or "tag") || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(to)) throw new DriverException("a rename names its kind, `branch` or `tag`, its name and the name it takes.");
        _ = _loop.Service ?? throw NotReady();

        var verb = new GitRefVerb(kind, repository, name, Optional(request, "at"), Yes: true, RenameTo: to);
        var world = new GitRefWorld(
            async (_, token) => await CheckoutOfAsync(repository, Optional(request, "workspace"), token).ConfigureAwait(false), () => _acts,
            async (_, checkout, token) => (await CanonicalLine.ResolveAsync(checkout, repository, Optional(request, "workspace"), DriverConfig.Load(_loop.ConfigPath), token).ConfigureAwait(false)).Branch);
        if (Flag(request, "said"))
        {
            var writer = new StringWriter { NewLine = "\n" };
            var code = await GitRefCommand.ExecuteAsync(verb, world, "terminal", "window", writer, writer, cancellationToken).ConfigureAwait(false);
            var lines = writer.ToString().Split('\n');
            return new { Said = new TreesSaid(lines[^1].Length == 0 ? lines[..^1] : lines, [], code).ToWire() };
        }

        if (await world.CheckoutOf(repository, cancellationToken).ConfigureAwait(false) is not { } checkout) return NoCheckout(repository);
        var line = await world.LineOf!(repository, checkout, cancellationToken).ConfigureAwait(false);
        var run = await GitRefActs.RenameAsync(_acts, new RefRenameAsk(repository, checkout, kind, name, to, verb.At, line), "window", "window", cancellationToken).ConfigureAwait(false);
        return Answered(run, checkout, repository);
    }

    // GITAPP2e: the Finish beside a stopped rename's answer, the section moved alone where the ref and the section stand as the act journaled them.
    [DriverRoute("GIT_REF_FINISH")]
    [Publishes("GIT_REF_FINISH", StateScopes.Driver)]
    private async Task<object?> GitRefFinishAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var repository = Optional(request, "repository") ?? throw new DriverException("a ref act names its repository.");
        var act = Optional(request, "act") ?? throw new DriverException("a finish names the act it finishes.");
        _ = _loop.Service ?? throw NotReady();
        if (await CheckoutOfAsync(repository, Optional(request, "workspace"), cancellationToken).ConfigureAwait(false) is not { } checkout) return NoCheckout(repository);
        var run = await GitRefActs.FinishAsync(_acts, repository, checkout, act, "window", "window", cancellationToken).ConfigureAwait(false);
        return Answered(run, checkout, repository);
    }

    // GITAPP2c: the Undo beside a create's answer, the act's ref deleted only at the id the act made it at; refused as data where it moved.
    // GITAPP2d: and beside a delete's, the ref made again where the name is free and its section put back.
    [DriverRoute("GIT_REF_UNDO")]
    [Publishes("GIT_REF_UNDO", StateScopes.Driver)]
    private async Task<object?> GitRefUndoAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var repository = Optional(request, "repository") ?? throw new DriverException("a ref act names its repository.");
        var act = Optional(request, "act") ?? throw new DriverException("an undo names the act it undoes.");
        _ = _loop.Service ?? throw NotReady();
        if (await CheckoutOfAsync(repository, Optional(request, "workspace"), cancellationToken).ConfigureAwait(false) is not { } checkout) return NoCheckout(repository);
        var run = await GitRefActs.UndoAsync(_acts, repository, checkout, act, "window", "window", cancellationToken).ConfigureAwait(false);
        return Answered(run, checkout, repository);
    }

    // GITAPP2m (D204, the git-writes design §7, §9): a branch's history edited from objects, through the journal. `plan: true` answers what
    // the edit would take and changes nothing (a look: a rule on `plan` in the pipe's census); the press carries the ids its plan pinned
    // (`pins`) and is refused as data where any moved. A terminal's `git pick|revert|rebase … --yes` is the press asked for its words (`said`).
    [DriverRoute("GIT_HISTORY_EDIT")]
    [Publishes("GIT_HISTORY_EDIT", StateScopes.Driver)]
    private async Task<object?> GitHistoryEditAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var repository = Optional(request, "repository") ?? throw new DriverException("a history edit names its repository.");
        var (edit, branch, target) = (Optional(request, "edit"), Optional(request, "branch"), Optional(request, "target"));
        if (edit is not (GitHistoryEdits.Pick or GitHistoryEdits.Revert or GitHistoryEdits.Rebase) || string.IsNullOrWhiteSpace(branch) || string.IsNullOrWhiteSpace(target))
        {
            throw new DriverException("a history edit names its edit (`pick`, `revert` or `rebase`), its branch and its target.");
        }

        _ = _loop.Service ?? throw NotReady();
        var workspace = Optional(request, "workspace");
        var world = new GitHistoryWorld(
            async (_, token) => await CheckoutOfAsync(repository, workspace, token).ConfigureAwait(false),
            async (_, root, token) => (await CanonicalLine.ResolveAsync(root, repository, workspace, DriverConfig.Load(_loop.ConfigPath), token).ConfigureAwait(false)).Branch,
            () => _acts, _loop.Home);
        var pins = Pins(request);
        if (Flag(request, "said"))
        {
            var writer = new StringWriter { NewLine = "\n" };
            var code = await GitHistoryCommand.ExecuteAsync(new GitHistoryVerb(edit, repository, branch, target, Yes: true), pins, world, "terminal", "window", writer, writer, cancellationToken)
                .ConfigureAwait(false);
            var lines = writer.ToString().Split('\n');
            return new { Said = new TreesSaid(lines[^1].Length == 0 ? lines[..^1] : lines, [], code).ToWire() };
        }

        if (await world.CheckoutOf(repository, cancellationToken).ConfigureAwait(false) is not { } checkout) return NoCheckout(repository);
        var ask = new HistoryEditAsk(_loop.Home, repository, checkout, edit, branch, target)
        {
            Line = await world.LineOf(repository, checkout, cancellationToken).ConfigureAwait(false), Pins = pins,
        };
        if (Flag(request, "plan"))
        {
            var (plan, refusal) = await GitHistoryEdits.PlanAsync(ask, cancellationToken).ConfigureAwait(false);
            return plan is null
                ? new { Refused = true, Said = WithoutRoots(refusal, [checkout], repository) ?? "" }
                : new
                {
                    Refused = false, Said = "",
                    Plan = new { plan.Edit, plan.Branch, plan.Tip, plan.Onto, Commits = plan.Commits.Select(each => new { each.Id, each.Subject }), plan.Commands },
                };
        }

        var run = await GitHistoryEdits.EditAsync(_acts, ask, "window", "window", cancellationToken).ConfigureAwait(false);
        return new { run.Run.Act, run.Run.Outcome, run.Run.Refused, Said = WithoutRoots(run.Run.Said, [checkout], repository) ?? "", run.Tip, run.Kept };
    }

    /// <summary>The ids a shown plan pinned, as the page or the verb sends them back; null where none was sent.</summary>
    private static HistoryEditPins? Pins(IpcRequest request) =>
        request.Payload is { } payload && payload.TryGetProperty("pins", out var pins) && pins.ValueKind == JsonValueKind.Object
        && pins.TryGetProperty("tip", out var tip) && tip.ValueKind == JsonValueKind.String
        && pins.TryGetProperty("onto", out var onto) && onto.ValueKind == JsonValueKind.String
        && pins.TryGetProperty("commits", out var commits) && commits.ValueKind == JsonValueKind.Array
            ? new HistoryEditPins(tip.GetString()!, onto.GetString()!, [.. commits.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!)])
            : null;

    private static object NoCheckout(string repository) => new
    {
        Refused = true, Code = Refusals.GitNoCheckout, Said = $"{repository} has no checkout on this machine, so nothing was changed.",
    };

    /// <summary>An act's run as the page reads it: the record, its outcome, and the words with the checkout's path said as the repository (D47 §4).</summary>
    private static object Answered(ActRun run, string checkout, string repository) => new
    {
        run.Act, run.Outcome, run.Refused, Said = WithoutRoots(run.Said, [checkout], repository) ?? "",
    };

    /// <summary>The first checkout of the repository that exists on this machine, or null.</summary>
    private async Task<string?> CheckoutOfAsync(string repository, string? workspace, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        var rows = LinesRoutes.Checkouts(await service.RegistryAsync(cancellationToken).ConfigureAwait(false), repository, workspace);
        return rows.FirstOrDefault(row => Directory.Exists(row.Root)).Root;
    }

    /// <summary>
    /// A read of the repository the payload names, answered as the driver answered it: its value, or a code the page words and
    /// the sentence beside it, and the git commands that give the same answer. A repository with no checkout here is an answer
    /// too, so the page words it as it words the rest. Writes nothing.
    /// </summary>
    private async Task<object?> GitAnsweredAsync<T>(
        IpcRequest request, CancellationToken cancellationToken, Func<string, CancellationToken, Task<GitAnswer<T>>> read) where T : class
    {
        var repository = Optional(request, "repository") ?? throw new DriverException("a Git read names its repository.");
        var service = _loop.Service ?? throw NotReady();
        var rows = LinesRoutes.Checkouts(await service.RegistryAsync(cancellationToken).ConfigureAwait(false), repository, Optional(request, "workspace"));
        if (rows.FirstOrDefault(row => Directory.Exists(row.Root)).Root is not { } root)
        {
            return new
            {
                Value = (T?)null, Refusal = Refusals.GitNoCheckout,
                Problem = $"{repository} has no checkout on this machine, so there is no Git history here to read.",
                Commands = Array.Empty<string>(), Remembered = false,
            };
        }

        var answer = await read(root, cancellationToken).ConfigureAwait(false);
        return new
        {
            answer.Value,
            Refusal = GitRefusalCode(answer.Refusal),
            Problem = WithoutRoots(answer.Problem, [root], repository),
            answer.Commands,
            answer.Remembered,
        };
    }

    /// <summary>The code the page words for a refusal the driver gave, or null where it answered (D147).</summary>
    private static string? GitRefusalCode(GitReadRefusal refusal) => refusal switch
    {
        GitReadRefusal.None => null,
        GitReadRefusal.NotTop => Refusals.GitNotTop,
        GitReadRefusal.NotACommit => Refusals.GitNotACommit,
        GitReadRefusal.NotAPath => Refusals.GitNotAPath,
        GitReadRefusal.NotAPage => Refusals.GitNotAPage,
        GitReadRefusal.TooManyTips => Refusals.GitTooManyTips,
        _ => Refusals.GitNotAnswered,
    };

    /// <summary>
    /// 🔴 A sentence with the checkout's path said as the repository (D47 §4): the driver's sentences name the root they were
    /// asked of, in either slash, and the machine's path is the desktop's alone.
    /// </summary>
    private static string? WithoutRoots(string? problem, IEnumerable<string?> roots, string repository)
    {
        if (problem is null) return null;
        foreach (var root in roots.OfType<string>().Where(root => root.Length > 0))
        {
            var full = Path.GetFullPath(root);
            foreach (var form in new[] { full, root, full.Replace('\\', '/'), root.Replace('\\', '/'), full.Replace('/', '\\'), root.Replace('/', '\\') }
                         .Distinct(StringComparer.Ordinal).OrderByDescending(form => form.Length))
            {
                problem = problem.Replace(form, $"{repository}'s checkout", StringComparison.OrdinalIgnoreCase);
            }
        }

        return problem;
    }

    /// <summary>A page the page asks for: absent is the first, and one past what a count holds is a page the driver refuses.</summary>
    private static int GitPage(IpcRequest request) =>
        Number(request, "page") is { } page ? (int)Math.Clamp(page, -1, GitReads.LastPage + 1) : 0;

    // GITAUTH1a: the person's Try again on a held git remote's row in What needs you (the git-auth design §5.2): every hold on the
    // repositories it names, in its workspace where it names one, lifted, so the next fetch of each tries once. The row goes as the
    // attention slice hears the lift (GitHolds.Changed); `{ lifted }`, how many holds were. Nothing connects here: the next act does.
    [DriverRoute("GIT_TRY_AGAIN")]
    private Task<object?> GitTryAgainAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var repositories = Strings(request, "repositories");
        if (repositories.Count == 0) throw new DriverException("Try again names the repositories it lifts.");
        var workspace = Optional(request, "workspace");
        var lifted = repositories.Sum(repository => GitHolds.LiftRepository(_loop.Home, repository, workspace));
        return Task.FromResult<object?>(new { Lifted = lifted });
    }

    /// <summary>The strings an array the page sent holds, in the order sent: a graph's two-tip walk keeps its order.</summary>
    private static List<string> Strings(IpcRequest request, string field) =>
        request.Payload is { } payload && payload.TryGetProperty(field, out var items) && items.ValueKind == JsonValueKind.Array
            ? [.. items.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!)]
            : [];
}
