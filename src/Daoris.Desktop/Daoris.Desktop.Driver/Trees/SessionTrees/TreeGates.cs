using System.Globalization;
using System.Text.Json;
using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>
/// What the landing gate reads of the service (XAGENT1f): the review's quests and asks (<see cref="IReviewWorld"/>), and the local
/// host's opinions by id. A seam, so its tests stand in.
/// </summary>
public interface IOpinionWorld : IReviewWorld
{
    /// <summary>One second opinion as the local host answers it, or null where it holds none.</summary>
    Task<OpinionView?> OpinionAsync(string id, CancellationToken ct);
}

/// <summary>
/// The landing gate for a tree (XAGENT1f; PART10k, out of <c>SessionTrees.Opinion.cs</c>): the run's process, its workflow's part,
/// the second opinion's part and the fork point they read from, over the context alone. <see cref="SessionTrees"/> keeps the
/// public calls, one line each.
/// </summary>
internal sealed class TreeGates(TreeContext context)
{
    /// <summary>
    /// The whole landing gate for this tree (XAGENT1f, D155 point 9; the second-agent design §7): D154's look as
    /// <see cref="SessionTrees.ReviewAsync"/> reads it, and the second opinion before it. Every door that lands reads it here, once, and hands it
    /// to <see cref="SessionTrees.LandAsync"/>, which refuses the first part that holds.
    /// </summary>
    /// <param name="session">The working session the door lands, which the terminal's doors the gate names say.</param>
    /// <param name="process">
    /// The run's process, where the door read it already (WORKFLOW1f: the look's automatic landing reads it for the switch first);
    /// otherwise it is read here, once, for every part.
    /// </param>
    /// <param name="tip">
    /// The commit to judge the work at (FABRIC-CLOSE1h16, D173's CLOSELAND1 note): where a landing's push is judged, the one its branch
    /// was judged and made at (<see cref="LandingGate.Tip"/>), so a commit made on the tree after it, which the push does not carry, holds
    /// nothing back. Null judges the tree's <c>HEAD</c>, as every door before the landing does.
    /// </param>
    /// <param name="run">A chat run's key where the work serves no quest (CHATFLOW1c), whose bound process is read in place of the rules live.</param>
    internal async Task<LandingGate> GateAsync(
        string path, string? quest, IOpinionWorld world, string? session = null, CancellationToken ct = default, WorkflowProcess? process = null,
        string? tip = null, string? run = null)
    {
        var full = Path.GetFullPath(path);
        var (workspace, repository) = context.OwnerOf(full);
        // WORKFLOW1f (the workflow design §2.3, §4.6): the run's bound version where it names one, the rules live where it does not,
        // read once and handed to every part, so the look, the opinion and the landing decide by one process.
        var config = context.Config();
        process ??= await ProcessAsync(config, repository, workspace, quest, world, ct, run).ConfigureAwait(false);
        var workflow = await WorkflowAsync(full, process, session, ct, tip).ConfigureAwait(false);
        var review = await ReviewGate.ReadAsync(world, config, full, repository, workspace, quest, ct, process, tip, run, context.Home, session)
            .ConfigureAwait(false);
        var opinion = await OpinionAsync(full, repository, workspace, quest, session, world, review, OpinionRules.Landing, ct, process, tip, run)
            .ConfigureAwait(false);
        var gate = new LandingGate(opinion, review) { Workflow = workflow };
        return await GraphAsync(gate, full, repository, process, tip, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The graph's part of the gate for a run bound to a graph version (FABRIC-CUTOVER1d; D190 point 4, the cutover design §4): the run's record
    /// read as the runtime keeps it, with no lease and never written, and judged against the door's tree, the commit it would land (the tip it
    /// pinned, else its <c>HEAD</c>) and the opinion and look parts it read a moment before (<see cref="GraphGate"/>). Where it lets the work go,
    /// the gate is pinned to the commit <c>handed</c> is bound to, so every landing path lands that commit or nothing. Any other run's gate is
    /// returned as it is.
    /// </summary>
    private async Task<LandingGate> GraphAsync(LandingGate gate, string full, string repository, WorkflowProcess process, string? tip, CancellationToken ct)
    {
        if (process is not { Graph: { } graph, Run: { } run, Binding.Digest: { } digest, Problem: null, Unread: null }) return gate;
        var commit = tip ?? await ReviewGit.HeadAsync(full, ct).ConfigureAwait(false);
        var branches = context.Grown.Entries();
        var unit = branches.LastOrDefault(entry => string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Path.GetFullPath(context.TreeAt(entry.Workspace, entry.Repository, Path.GetFileName(entry.Branch))),
                Path.TrimEndingDirectorySeparator(full), StringComparison.OrdinalIgnoreCase))?.Unit;
        var judged = GraphGate.Judge(new GraphGateFacts(run, graph, digest, new NodeRecordFiles(context.Home, () => false).Read(RunUnits.IdOf(run)))
        {
            Unit = unit, Commit = commit, Branches = branches, OpinionLetsGo = gate.Opinion.LetsGo, ReviewLetsGo = gate.Review.LetsGo,
        });
        return gate with { Graph = judged, Tip = judged.LetsGo ? judged.Commit : gate.Tip };
    }

    /// <summary>
    /// The process a quest's work here follows (WORKFLOW1f, <see cref="WorkflowProcesses.ReadAsync"/>): its run's bound version, or the
    /// rules live. A conversation's work, which serves no quest, reads its chat run's (CHATFLOW1c, the chat-flow design §4.2), so the view
    /// and the gate decide by one process; one with no chat run named reads the rules live.
    /// </summary>
    /// <param name="run">The chat run's key, <c>chat-&lt;first&gt;</c>, where the work serves no quest; null for none.</param>
    internal async Task<WorkflowProcess> ProcessAsync(
        DriverConfig config, string repository, string? workspace, string? quest, IReviewWorld world, CancellationToken ct = default,
        string? run = null) =>
        quest is not null
            ? await WorkflowProcesses.ReadAsync(context.Home, config, world, quest, repository, workspace, ct).ConfigureAwait(false)
            : ChatRuns.IsChatRun(run)
                ? WorkflowProcesses.Read(context.Home, config, repository, workspace, run)
                : WorkflowProcess.Current(config, repository, workspace);

    /// <summary>
    /// The run's own part of the gate for a tree (WORKFLOW1f, the workflow design §4.4): the process judged, and where its kind chose
    /// a workflow that asks less of the person, the paths the chain changed read from git, from where its work leaves the line to its
    /// tip. Git is asked only there.
    /// </summary>
    /// <param name="tip">The commit to judge at, the branch's where a push is judged (FABRIC-CLOSE1h16); null reads the tree's <c>HEAD</c>.</param>
    internal async Task<WorkflowGateState> WorkflowAsync(
        string tree, WorkflowProcess process, string? session, CancellationToken ct = default, string? tip = null)
    {
        KindPathsRead? paths = null;
        if (WorkflowGate.ChecksPaths(process))
        {
            var (otherwise, steps) = WorkflowProcesses.Otherwise(context.Home, context.Config(), process.Repository, process.Binding!.Workspace);
            var lowered = steps is null
                ? [$"what {otherwise} asks of you could not be read here, so it is taken as more"]
                : Involvement.Lowers(process.Steps, steps);
            paths = new KindPathsRead(lowered, otherwise, lowered.Count == 0 || process.Binding.Kept is not null ? [] : await ChangedAsync(tree, ct, tip).ConfigureAwait(false));
        }

        return WorkflowGate.Judge(process, paths) with { Session = session };
    }

    /// <summary>
    /// The paths the work changed, from where it leaves its line to its tree's tip (§4.4), or to <paramref name="tip"/> where a push is
    /// judged (FABRIC-CLOSE1h16); null where git cannot say.
    /// </summary>
    internal async Task<IReadOnlyList<string>?> ChangedAsync(string tree, CancellationToken ct, string? tip = null)
    {
        var at = tip ?? "HEAD";
        if (await ForkPointAsync(tree, ct, at).ConfigureAwait(false) is not { } fork) return null;
        var (code, names, _) = await WorkingTree.GitAsync(tree, ["--no-optional-locks", "diff", "--name-only", "--no-renames", "-z", fork, at], ct).ConfigureAwait(false);
        return code == 0 ? [.. names.Split('\0', StringSplitOptions.RemoveEmptyEntries)] : null;
    }

    /// <summary>
    /// The second opinion's part of the gate for a tree (design §8): the rule standing for its repository, the chain its quest is a
    /// step of, the tree's <c>HEAD</c>, what this machine kept at the gate, the host's opinions and git's ancestry. Git and the
    /// service are asked only where a rule reads at the occasion. A service or git that does not answer holds the work, and says so.
    /// </summary>
    /// <param name="occasion"><c>landing</c> for a landing door; <c>steps</c> where the chain's next step waits on this work (§8.1).</param>
    /// <param name="process">
    /// The run's process (WORKFLOW1f), read once by the door or the look: under a named workflow, its version's opinion step over the
    /// declared reviewers. Null reads the rule live, as before.
    /// </param>
    /// <param name="at">The commit to judge at, the branch's where a push is judged (FABRIC-CLOSE1h16); null reads the tree's <c>HEAD</c>.</param>
    /// <param name="run">
    /// The chat run whose work this is where it serves no quest (CHATFLOW1d, the chat-flow design §7): its gate is kept under the run's key
    /// in place of a chain's, and a pass a press asked and the look has not started reads as asked. Null for a quest's work, or none.
    /// </param>
    internal async Task<OpinionGateState> OpinionAsync(
        string tree, string repository, string workspace, string? quest, string? session, IOpinionWorld world, ReviewGateState? review,
        string occasion, CancellationToken ct = default, WorkflowProcess? process = null, string? at = null, string? run = null)
    {
        var rule = process is null ? OpinionRules.Resolve(context.Config(), repository, workspace) : process.Opinion;
        var cannot = process?.OpinionCannot;
        var facts = new OpinionGateFacts(repository, rule) { Session = session, Occasion = occasion, Review = review, Cannot = cannot };
        var chatRun = quest is null && ChatRuns.IsChatRun(run) ? run : null;
        if ((quest is null && chatRun is null) || !OpinionGate.Reads(rule, cannot, occasion))
        {
            return new OpinionGateState(OpinionGateStates.None, repository) { Rule = rule, Session = session, Run = chatRun };
        }

        if (chatRun is not null) return await ChatOpinionAsync(tree, repository, chatRun, facts, world, ct, at).ConfigureAwait(false);

        try
        {
            var chain = ReviewGate.ChainOf(await world.QuestsAsync(ct).ConfigureAwait(false), quest);
            var id = quest.TrimStart('#');
            var served = chain.FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.OrdinalIgnoreCase))
                         ?? new QuestView(id, "", repository, "", "", "Done");
            var kept = new OpinionGates(context.Home).Read(OpinionChain(chain, id), repository);
            var tip = at ?? await ReviewGit.HeadAsync(tree, ct).ConfigureAwait(false);
            facts = facts with
            {
                Quest = served,
                Later = occasion == OpinionRules.Landing ? OpinionGate.LaterStep(chain, id, repository) : null,
                Tip = tip,
                Asks = [.. kept.Asks.Where(ask => occasion == OpinionRules.Landing ? ask.Occasion != OpinionRules.Steps : ask.Working == session)],
                Person = kept.Person,
            };

            var deliveries = new OpinionDeliveries(context.Home);
            return await OpinionGate.JudgeAsync(facts, new OpinionReads(
                each => world.OpinionAsync(each, ct),
                deliveries.Read,
                commit => tip is null ? Task.FromResult<bool?>(null) : ReviewGit.HoldsAsync(tree, tip, commit, ct),
                from => SinceAsync(tree, from, ct, at))
            {
                // LIMITRESUME1h: a limited pass's reset is its cool-off's, read by its session; none left names a ready account.
                Ready = reviewer => AccountCooling.OfSession(context.Home, reviewer)?.Until ?? DateTimeOffset.UtcNow,
            }).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or JsonException or InvalidOperationException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return new OpinionGateState(OpinionGateStates.Unread, repository)
            {
                Rule = rule, Session = session, Problem = error.Message.TrimEnd().TrimEnd('.'),
            };
        }
    }

    /// <summary>
    /// A chat run's opinion gate (CHATFLOW1d, the chat-flow design §5.2, §7): what is kept under the run's key, <c>chat-&lt;first&gt;</c>, at the
    /// commit that would land, with no chain and no later step; and whether a press asked a pass the look has not started yet, a due open
    /// on the run with no ask kept since it was owed, nor a request the look has still to take.
    /// </summary>
    private async Task<OpinionGateState> ChatOpinionAsync(
        string tree, string repository, string run, OpinionGateFacts facts, IOpinionWorld world, CancellationToken ct, string? at)
    {
        try
        {
            var kept = new OpinionGates(context.Home).Read(run, repository);
            var due = new OpinionDues(context.Home).Open().LastOrDefault(each => string.Equals(each.Run, run, StringComparison.OrdinalIgnoreCase));
            var tip = at ?? await ReviewGit.HeadAsync(tree, ct).ConfigureAwait(false);
            facts = facts with
            {
                Run = run,
                Tip = tip,
                Asks = [.. kept.Asks.Where(ask => ask.Occasion != OpinionRules.Steps)],
                Person = kept.Person,
                Asked = kept.Requests.Count > 0 || (due is not null && !kept.Asks.Any(ask => ask.At >= due.At)),
            };

            return await OpinionGate.JudgeAsync(facts, new OpinionReads(
                each => world.OpinionAsync(each, ct),
                new OpinionDeliveries(context.Home).Read,
                commit => tip is null ? Task.FromResult<bool?>(null) : ReviewGit.HoldsAsync(tree, tip, commit, ct),
                from => SinceAsync(tree, from, ct, at))
            {
                Ready = reviewer => AccountCooling.OfSession(context.Home, reviewer)?.Until ?? DateTimeOffset.UtcNow,
            }).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or JsonException or InvalidOperationException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return new OpinionGateState(OpinionGateStates.Unread, repository)
            {
                Rule = facts.Rule, Session = facts.Session, Run = run, Problem = error.Message.TrimEnd().TrimEnd('.'),
            };
        }
    }

    /// <summary>The name a chain's gate is kept under: its first quest's id, or the quest's own where no chain was read.</summary>
    internal static string OpinionChain(IReadOnlyList<QuestView> chain, string quest) => chain.Count > 0 ? chain[0].Id : quest.TrimStart('#');

    /// <summary>
    /// How many commits <c>HEAD</c>, or <paramref name="tip"/> where a push is judged (FABRIC-CLOSE1h16), holds that <paramref name="from"/>
    /// does not; null where git cannot say.
    /// </summary>
    internal static async Task<int?> SinceAsync(string tree, string from, CancellationToken ct, string? tip = null)
    {
        if (!EvidenceCodes.IsObjectId(from) || !Directory.Exists(tree)) return null;
        var (code, count, _) = await WorkingTree.GitAsync(tree, ["rev-list", "--count", $"{from}..{tip ?? "HEAD"}"], ct).ConfigureAwait(false);
        return code == 0 && int.TryParse(count.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var since) ? since : null;
    }

    /// <summary>
    /// Where a tree's work leaves its repository's line (XAGENT1f, design §4): the commit a landing's candidate starts from, so one
    /// opinion reads the chain's whole work there. Null where git cannot name the line or where they meet.
    /// </summary>
    /// <param name="at">The commit whose work is read, the tree's <c>HEAD</c> unless a push is judged at its branch's (FABRIC-CLOSE1h16).</param>
    internal async Task<string?> ForkPointAsync(string tree, CancellationToken ct = default, string at = "HEAD")
    {
        var full = Path.GetFullPath(tree);
        if (!Directory.Exists(full)) return null;
        var (code, commonDir, _) = await WorkingTree.GitAsync(full, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct).ConfigureAwait(false);
        if (code != 0) return null;
        var root = Path.GetDirectoryName(commonDir.Trim())!;
        var (workspace, repository) = context.OwnerOf(full);
        var line = (await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
        var against = line is null ? null : await ComparableAsync(root, line, ct).ConfigureAwait(false);
        if (against is null) return null;
        var (forkCode, fork, _) = await WorkingTree.GitAsync(full, ["merge-base", against, at], ct).ConfigureAwait(false);
        return forkCode == 0 && fork.Trim() is { Length: > 0 } found && EvidenceCodes.IsObjectId(found) ? found : null;
    }

    /// <summary>The repository's own checkout a tree belongs to (its git common folder's parent); null where git cannot say.</summary>
    internal static async Task<string?> CheckoutOfTreeAsync(string tree, CancellationToken ct = default)
    {
        if (!Directory.Exists(tree)) return null;
        var (code, commonDir, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct).ConfigureAwait(false);
        return code == 0 && commonDir.Trim() is { Length: > 0 } common ? Path.GetDirectoryName(common) : null;
    }
}
