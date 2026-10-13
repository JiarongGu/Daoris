using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What the gate reads of the service: every quest, closed ones included, and an ask. A seam, so its tests stand in.</summary>
public interface IReviewWorld
{
    /// <summary>Every quest the service holds, closed ones included, from which a chain is read.</summary>
    Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct);

    /// <summary>One ask, or null where the service holds none.</summary>
    Task<AskView?> AskAsync(string id, CancellationToken ct);
}

/// <summary>The gate's world over this machine's service (<see cref="ServiceReviewWorld"/>), for a caller that holds the service.</summary>
public static class ReviewWorlds
{
    /// <summary>
    /// The review's and the opinion's world over <paramref name="service"/>: the quests read once, an ask, an opinion by id. The one
    /// way another module composes it (STRUCT3c): the world over the service is Review's Infrastructure.
    /// </summary>
    public static IOpinionWorld Of(ServiceClient service) => new ServiceReviewWorld(service);
}

/// <summary>
/// The review's gate read for a tree (REVIEWENV1c, D154 point 7; design §3.1): the chain from the service through
/// <see cref="IReviewWorld"/>, the rule, and the tree's <c>HEAD</c> and git's ancestry.
/// </summary>
/// <remarks>
/// <para><b>Named where its callers always named it</b> (STRUCT3c, D166's note): <c>ReviewGate.ReadAsync</c>, <c>HeadAsync</c>
/// and <c>HoldsAsync</c> are static members of a C# 14 extension block on the Domain's <see cref="ReviewGate"/>, whose table and
/// judgement are pure; the read is this layer's use case, and git is Review's Infrastructure's (<see cref="ReviewGit"/>). Declared
/// here, another module may name them.</para>
/// </remarks>
public static class ReviewGateReads
{
    extension(ReviewGate)
    {
        /// <summary>
        /// The gate for a session's tree (design §3.1): the chain its quest is a step of, read from the service, the rule standing for
        /// the tree's repository, the tree's <c>HEAD</c> and git's ancestry. A conversation, which serves no quest, has nothing to wait
        /// for. A service that does not answer holds the work only where a rule stands that could ask for a review, and says so.
        /// </summary>
        /// <param name="process">
        /// The run's process (WORKFLOW1f, <see cref="WorkflowProcesses"/>), read once by the door for every part of the gate: under a
        /// named workflow, rows 5 and 6 are its version's look. Null reads the rule live, as every gate did before.
        /// </param>
        public static async Task<ReviewGateState> ReadAsync(
            IReviewWorld world, DriverConfig config, string tree, string repository, string workspace, string? quest, CancellationToken ct = default,
            WorkflowProcess? process = null)
        {
            var rule = process is null ? ReviewRules.Resolve(config, repository, workspace) : process.Review;
            var look = process?.Look;
            if (quest is null) return new(ReviewStates.None, new ReviewDecision(ReviewLevels.Nothing, null) { Repository = repository });

            ReviewDecision decision;
            try
            {
                var chain = ReviewGate.ChainOf(await world.QuestsAsync(ct).ConfigureAwait(false), quest);
                var asked = chain.Select(each => AskWords.AskOf(each.From)).FirstOrDefault(id => id is not null);
                var ask = asked is null ? null : await world.AskAsync(asked, ct).ConfigureAwait(false);
                decision = ReviewGate.Decide(chain, repository, ask, rule, look, process?.Name);
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or JsonException or InvalidOperationException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                // Unread where a review could be asked: by a rule standing, or by the named version's look.
                var none = new ReviewDecision(ReviewLevels.Nothing, null) { Repository = repository };
                return rule is { Rule.IsNone: false } || look is { Environment: not null } or { Cannot: not null }
                    ? new(ReviewStates.Unread, none) { Problem = error.Message.TrimEnd().TrimEnd('.') }
                    : new(ReviewStates.None, none);
            }

            // Git is asked only where a review is asked: a landing no review touches costs no process.
            var tip = decision.Reviews ? await ReviewGit.HeadAsync(tree, ct).ConfigureAwait(false) : null;
            return await ReviewGate.JudgeAsync(decision, tip, commit => ReviewGit.HoldsAsync(tree, tip!, commit, ct)).ConfigureAwait(false);
        }

        /// <summary>The tree's <c>HEAD</c>, by its full id; null where git cannot say.</summary>
        internal static Task<string?> HeadAsync(string tree, CancellationToken ct) => ReviewGit.HeadAsync(tree, ct);

        /// <summary>
        /// Whether <paramref name="tip"/> is <paramref name="commit"/> or an ancestor of it (design §3.2): git's own answer,
        /// <c>merge-base --is-ancestor</c>, exit 0 yes and 1 no; anything else, a commit this repository does not hold among them, is
        /// null, which holds nothing.
        /// </summary>
        internal static Task<bool?> HoldsAsync(string tree, string tip, string commit, CancellationToken ct) => ReviewGit.HoldsAsync(tree, tip, commit, ct);
    }
}
