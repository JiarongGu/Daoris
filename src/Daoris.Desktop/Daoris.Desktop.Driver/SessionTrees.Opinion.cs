using System.Globalization;
using System.Text.Json;

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

public sealed partial class SessionTrees
{
    /// <summary>
    /// The whole landing gate for this tree (XAGENT1f, D155 point 9; the second-agent design §7): D154's look as
    /// <see cref="ReviewAsync"/> reads it, and the second opinion before it. Every door that lands reads it here, once, and hands it
    /// to <see cref="LandAsync"/>, which refuses the first part that holds.
    /// </summary>
    /// <param name="session">The working session the door lands, which the terminal's doors the gate names say.</param>
    public async Task<LandingGate> GateAsync(string path, string? quest, IOpinionWorld world, string? session = null, CancellationToken ct = default)
    {
        var full = Path.GetFullPath(path);
        var (workspace, repository) = OwnerOf(full);
        var review = await ReviewGate.ReadAsync(world, Config(), full, repository, workspace, quest, ct).ConfigureAwait(false);
        var opinion = await OpinionAsync(full, repository, workspace, quest, session, world, review, OpinionRules.Landing, ct).ConfigureAwait(false);
        return new LandingGate(opinion, review);
    }

    /// <summary>
    /// The second opinion's part of the gate for a tree (design §8): the rule standing for its repository, the chain its quest is a
    /// step of, the tree's <c>HEAD</c>, what this machine kept at the gate, the host's opinions and git's ancestry. Git and the
    /// service are asked only where a rule reads at the occasion. A service or git that does not answer holds the work, and says so.
    /// </summary>
    /// <param name="occasion"><c>landing</c> for a landing door; <c>steps</c> where the chain's next step waits on this work (§8.1).</param>
    public async Task<OpinionGateState> OpinionAsync(
        string tree, string repository, string workspace, string? quest, string? session, IOpinionWorld world, ReviewGateState? review,
        string occasion, CancellationToken ct = default)
    {
        var rule = OpinionRules.Resolve(Config(), repository, workspace);
        var facts = new OpinionGateFacts(repository, rule) { Session = session, Occasion = occasion, Review = review };
        if (quest is null || rule is not { Rule: { IsNone: false } standing } || !standing.On.Contains(occasion))
        {
            return new OpinionGateState(OpinionGateStates.None, repository) { Rule = rule, Session = session };
        }

        try
        {
            var chain = ReviewGate.ChainOf(await world.QuestsAsync(ct).ConfigureAwait(false), quest);
            var id = quest.TrimStart('#');
            var served = chain.FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.OrdinalIgnoreCase))
                         ?? new QuestView(id, "", repository, "", "", "Done");
            var kept = new OpinionGates(home).Read(OpinionChain(chain, id), repository);
            var tip = await ReviewGate.HeadAsync(tree, ct).ConfigureAwait(false);
            facts = facts with
            {
                Quest = served,
                Later = occasion == OpinionRules.Landing ? OpinionGate.LaterStep(chain, id, repository) : null,
                Tip = tip,
                Asks = [.. kept.Asks.Where(ask => occasion == OpinionRules.Landing ? ask.Occasion != OpinionRules.Steps : ask.Working == session)],
                Person = kept.Person,
            };

            var deliveries = new OpinionDeliveries(home);
            return await OpinionGate.JudgeAsync(facts, new OpinionReads(
                each => world.OpinionAsync(each, ct),
                deliveries.Read,
                commit => tip is null ? Task.FromResult<bool?>(null) : ReviewGate.HoldsAsync(tree, tip, commit, ct),
                from => SinceAsync(tree, from, ct))).ConfigureAwait(false);
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

    /// <summary>The name a chain's gate is kept under: its first quest's id, or the quest's own where no chain was read.</summary>
    public static string OpinionChain(IReadOnlyList<QuestView> chain, string quest) => chain.Count > 0 ? chain[0].Id : quest.TrimStart('#');

    /// <summary>How many commits <c>HEAD</c> holds that <paramref name="from"/> does not; null where git cannot say.</summary>
    internal static async Task<int?> SinceAsync(string tree, string from, CancellationToken ct)
    {
        if (!EvidenceCodes.IsObjectId(from) || !Directory.Exists(tree)) return null;
        var (code, count, _) = await WorkingTree.GitAsync(tree, ["rev-list", "--count", $"{from}..HEAD"], ct).ConfigureAwait(false);
        return code == 0 && int.TryParse(count.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var since) ? since : null;
    }

    /// <summary>
    /// Where a tree's work leaves its repository's line (XAGENT1f, design §4): the commit a landing's candidate starts from, so one
    /// opinion reads the chain's whole work there. Null where git cannot name the line or where they meet.
    /// </summary>
    public async Task<string?> ForkPointAsync(string tree, CancellationToken ct = default)
    {
        var full = Path.GetFullPath(tree);
        if (!Directory.Exists(full)) return null;
        var (code, commonDir, _) = await WorkingTree.GitAsync(full, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct).ConfigureAwait(false);
        if (code != 0) return null;
        var root = Path.GetDirectoryName(commonDir.Trim())!;
        var (workspace, repository) = OwnerOf(full);
        var line = (await LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
        var against = line is null ? null : await ComparableAsync(root, line, ct).ConfigureAwait(false);
        if (against is null) return null;
        var (forkCode, fork, _) = await WorkingTree.GitAsync(full, ["merge-base", against, "HEAD"], ct).ConfigureAwait(false);
        return forkCode == 0 && fork.Trim() is { Length: > 0 } found && EvidenceCodes.IsObjectId(found) ? found : null;
    }

    /// <summary>The repository's own checkout a tree belongs to (its git common folder's parent); null where git cannot say.</summary>
    public static async Task<string?> CheckoutOfTreeAsync(string tree, CancellationToken ct = default)
    {
        if (!Directory.Exists(tree)) return null;
        var (code, commonDir, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct).ConfigureAwait(false);
        return code == 0 && commonDir.Trim() is { Length: > 0 } common ? Path.GetDirectoryName(common) : null;
    }
}
