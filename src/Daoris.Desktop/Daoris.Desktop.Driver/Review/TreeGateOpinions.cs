using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The second opinion's part of the landing gate (XAGENT1f; design §8), which the review owns because the opinion's gate, its rules and its
/// reads are the review's: the workplace's <see cref="TreeGates"/> keeps the git reads it uses (<c>ChangedAsync</c>, <c>SinceAsync</c>, the
/// fork point) and the workflows' assembly of the gate asks this over the one process it read. Each step is what <see cref="TreeGates"/> did
/// before it moved, in the same order.
/// </summary>
internal static class TreeGateOpinions
{
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
    internal static async Task<OpinionGateState> OpinionAsync(
        this TreeGates gates, string tree, string repository, string workspace, string? quest, string? session, IOpinionWorld world, ReviewGateState? review,
        string occasion, CancellationToken ct = default, IWorkplaceProcess? process = null, string? at = null, string? run = null)
    {
        var rule = process is null ? OpinionRules.Resolve(gates.Context.Config(), repository, workspace) : process.Opinion;
        var cannot = process?.OpinionCannot;
        var facts = new OpinionGateFacts(repository, rule) { Session = session, Occasion = occasion, Review = review, Cannot = cannot };
        var chatRun = quest is null && WorkplaceRunFiles.IsChatRun(run) ? run : null;
        if ((quest is null && chatRun is null) || !OpinionGate.Reads(rule, cannot, occasion))
        {
            return new OpinionGateState(OpinionGateStates.None, repository) { Rule = rule, Session = session, Run = chatRun };
        }

        if (chatRun is not null) return await ChatOpinionAsync(gates, tree, repository, chatRun, facts, world, ct, at).ConfigureAwait(false);

        try
        {
            var chain = ReviewGate.ChainOf(await world.QuestsAsync(ct).ConfigureAwait(false), quest);
            var id = quest.TrimStart('#');
            var served = chain.FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.OrdinalIgnoreCase))
                         ?? new QuestView(id, "", repository, "", "", "Done");
            var kept = new OpinionGates(gates.Context.Home).Read(TreeGates.OpinionChain(chain, id), repository);
            var tip = at ?? await ReviewGit.HeadAsync(tree, ct).ConfigureAwait(false);
            facts = facts with
            {
                Quest = served,
                Later = occasion == OpinionRules.Landing ? OpinionGate.LaterStep(chain, id, repository) : null,
                Tip = tip,
                Asks = [.. kept.Asks.Where(ask => occasion == OpinionRules.Landing ? ask.Occasion != OpinionRules.Steps : ask.Working == session)],
                Person = kept.Person,
            };

            var deliveries = new OpinionDeliveries(gates.Context.Home);
            return await OpinionGate.JudgeAsync(facts, new OpinionReads(
                each => world.OpinionAsync(each, ct),
                deliveries.Read,
                commit => tip is null ? Task.FromResult<bool?>(null) : ReviewGit.HoldsAsync(tree, tip, commit, ct),
                from => TreeGates.SinceAsync(tree, from, ct, at))
            {
                // LIMITRESUME1h: a limited pass's reset is its cool-off's, read by its session; none left names a ready account.
                Ready = reviewer => AccountCooling.OfSession(gates.Context.Home, reviewer)?.Until ?? DateTimeOffset.UtcNow,
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
    private static async Task<OpinionGateState> ChatOpinionAsync(
        TreeGates gates, string tree, string repository, string run, OpinionGateFacts facts, IOpinionWorld world, CancellationToken ct, string? at)
    {
        try
        {
            var kept = new OpinionGates(gates.Context.Home).Read(run, repository);
            var due = new OpinionDues(gates.Context.Home).Open().LastOrDefault(each => string.Equals(each.Run, run, StringComparison.OrdinalIgnoreCase));
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
                new OpinionDeliveries(gates.Context.Home).Read,
                commit => tip is null ? Task.FromResult<bool?>(null) : ReviewGit.HoldsAsync(tree, tip, commit, ct),
                from => TreeGates.SinceAsync(tree, from, ct, at))
            {
                Ready = reviewer => AccountCooling.OfSession(gates.Context.Home, reviewer)?.Until ?? DateTimeOffset.UtcNow,
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
}
