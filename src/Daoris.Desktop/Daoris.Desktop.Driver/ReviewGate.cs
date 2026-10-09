using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Which row of the level's table decided whether a chain's work in a repository waits for the person's review (REVIEWENV1c,
/// D154 point 3; the review environment design §1.4): the first that says anything, read from the most specific down.
/// </summary>
public static class ReviewLevels
{
    /// <summary>Row 1: the person's <i>skip</i> for this work, on any of the chain's quests in the repository.</summary>
    public const string Skip = "skip";

    /// <summary>Row 2: a set-up step for the repository in the chain, published or still composed, in its environment.</summary>
    public const string SetUpStep = "set-up-step";

    /// <summary>Row 3: the chain's choice, <c>off</c>, <c>on</c> or an environment's name.</summary>
    public const string Chain = "chain";

    /// <summary>Row 4: the ask's choice, the latest standing.</summary>
    public const string Ask = "ask";

    /// <summary>Row 5: the repository's rule, or its <c>false</c>.</summary>
    public const string Repository = "repository";

    /// <summary>Row 6: the workspace's rule.</summary>
    public const string Workspace = "workspace";

    /// <summary>Row 7: nothing set anywhere, which is today's behaviour.</summary>
    public const string Nothing = "nothing";
}

/// <summary>
/// Where a gate stands (REVIEWENV1c, D154 point 7; design §3.2's table), as a code a log line and a page can carry: three let
/// the work go, the rest hold it.
/// </summary>
public static class ReviewStates
{
    /// <summary>Nothing waits for a review here: the level says none.</summary>
    public const string None = "none";

    /// <summary>The person skipped the review of this work.</summary>
    public const string Skipped = "skipped";

    /// <summary>The person said <i>reviewed</i> of a set-up whose commit holds the work that lands.</summary>
    public const string Reviewed = "reviewed";

    /// <summary>The level says review, and no set-up step shows it yet.</summary>
    public const string NotShown = "not-shown";

    /// <summary>The set-up step is open, taken or working, or its set-up is not posted yet.</summary>
    public const string BeingSetUp = "being-set-up";

    /// <summary>Shown, and waiting for the person's look.</summary>
    public const string Shown = "shown";

    /// <summary>The person said <i>not yet</i> of the newest set-up: it waits for the next one shown.</summary>
    public const string NotYet = "not-yet";

    /// <summary>Reviewed, but what was reviewed does not hold the commits that would land.</summary>
    public const string NotHeld = "not-held";

    /// <summary>Whether it waits could not be read: the service did not answer.</summary>
    public const string Unread = "unread";
}

/// <summary>
/// What the level's table decided for a chain's work in one repository (design §1.4): the row, and the environment it is
/// reviewed in, or none, where nothing waits.
/// </summary>
/// <param name="Level">One of <see cref="ReviewLevels"/>.</param>
/// <param name="Environment">The environment the work is reviewed in; null where it is not reviewed.</param>
public sealed record ReviewDecision(string Level, string? Environment)
{
    /// <summary>Whether the work waits for the person's review.</summary>
    public bool Reviews => Environment is not null;

    /// <summary>The repository whose part of the chain this is.</summary>
    public string Repository { get; init; } = "";

    /// <summary>
    /// The chain's last work quest in the repository: where a skip is given when no set-up step can take it (design §3.6). Null
    /// for a chain this machine could not read.
    /// </summary>
    public string? Work { get; init; }

    /// <summary>Rows 3 and 4: the choice as it was set, <c>off</c>, <c>on</c> or a name.</summary>
    public string? Choice { get; init; }

    /// <summary>The person's words the level was set on: a skip's, or a choice's. Null where none were given.</summary>
    public string? Words { get; init; }

    /// <summary>Row 2: the set-up step published for the repository; null where it is only composed, and under every other row.</summary>
    public QuestView? SetUpStep { get; init; }

    /// <summary>Row 2: the set-up step still composed in a quest's <c>then</c>, which that quest's done publishes.</summary>
    public QuestStepView? Composed { get; init; }

    /// <summary>The quest whose done publishes <see cref="Composed"/>.</summary>
    public string? ComposedBy { get; init; }

    /// <summary>Row 1: the verdict that skipped it, and the quest it was given on.</summary>
    public QuestReviewVerdictView? Skip { get; init; }

    public string? SkippedOn { get; init; }
}

/// <summary>
/// What the landing record keeps of the review that let the work go (REVIEWENV1c, design §3.5): <c>reviewed</c>, with the
/// environment, the set-up's commit and when; or <c>skipped</c>, with the person's words.
/// </summary>
/// <param name="Said"><c>reviewed</c> or <c>skipped</c>.</param>
/// <param name="Quest">The quest the verdict was given on: the set-up step, or the quest a skip was given on.</param>
public sealed record LandingReview(string Said, string? Environment, string? Quest)
{
    /// <summary>The commit of the set-up the person reviewed; null for a skip.</summary>
    public string? Commit { get; init; }

    /// <summary>When the verdict was given, as the service kept it; null where it kept none.</summary>
    public DateTimeOffset? At { get; init; }

    /// <summary>The person's words with it, where they gave any.</summary>
    public string? Words { get; init; }
}

/// <summary>
/// Where a chain's work in a repository stands at its gate (REVIEWENV1c, D154 point 7; design §3.1–§3.2): the level's decision,
/// the state, and the set-up and verdict it turns on. A landing goes only where it <see cref="LetsGo"/>.
/// </summary>
/// <param name="State">One of <see cref="ReviewStates"/>.</param>
public sealed record ReviewGateState(string State, ReviewDecision Decision)
{
    /// <summary>The set-up the state speaks of: the one reviewed, the newest shown, or the one said not yet to.</summary>
    public QuestSetUpView? SetUp { get; init; }

    /// <summary>The verdict the state turns on: the <c>reviewed</c> or the skip that let it go, or the <c>not-yet</c>.</summary>
    public QuestReviewVerdictView? Verdict { get; init; }

    /// <summary>The commit that would land: the tree's <c>HEAD</c>; null where it was not read.</summary>
    public string? Tip { get; init; }

    /// <summary>Why it could not be read, for <see cref="ReviewStates.Unread"/>.</summary>
    public string? Problem { get; init; }

    /// <summary>Whether the work may land: nothing waits, the person skipped it, or they reviewed what lands.</summary>
    public bool LetsGo => State is ReviewStates.None or ReviewStates.Skipped or ReviewStates.Reviewed;

    /// <summary>What the gate says, in the driver's words: the terminal's line and every door's refusal.</summary>
    public string Says => ReviewGate.Says(this);

    /// <summary>What the landing record keeps of it (design §3.5): a review or a skip that let it go; null where none was asked.</summary>
    public LandingReview? Landing => State switch
    {
        ReviewStates.Reviewed => new LandingReview(ReviewVerdicts.Reviewed, Decision.Environment, Decision.SetUpStep?.Id)
        {
            Commit = SetUp?.Commit,
            At = Verdict?.At,
            Words = Verdict?.Words,
        },
        ReviewStates.Skipped => new LandingReview(ReviewVerdicts.Skipped, null, Decision.SkippedOn)
        {
            At = Decision.Skip?.At,
            Words = Decision.Skip?.Words,
        },
        _ => null,
    };
}

/// <summary>The verdicts a person gives a review, as the service spells them (REVIEWENV1b).</summary>
public static class ReviewVerdicts
{
    public const string Reviewed = "reviewed";
    public const string NotYet = "not-yet";
    public const string Skipped = "skipped";
}

/// <summary>What the gate reads of the service: every quest, closed ones included, and an ask. A seam, so its tests stand in.</summary>
public interface IReviewWorld
{
    /// <summary>Every quest the service holds, closed ones included, from which a chain is read.</summary>
    Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct);

    /// <summary>One ask, or null where the service holds none.</summary>
    Task<AskView?> AskAsync(string id, CancellationToken ct);
}

/// <summary>
/// The gate's world over this machine's service: the quests read once per instance, as a door reads them once, and the local
/// host's opinions (XAGENT1f), which the whole landing gate reads beside them.
/// </summary>
public sealed class ServiceReviewWorld(ServiceClient service) : IOpinionWorld
{
    private IReadOnlyList<QuestView>? _quests;

    public async Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) =>
        _quests ??= await service.EveryQuestAsync(ct).ConfigureAwait(false);

    public Task<AskView?> AskAsync(string id, CancellationToken ct) => service.FindAskAsync(id, ct);

    public Task<OpinionView?> OpinionAsync(string id, CancellationToken ct) => service.ReadOpinionAsync(id, ct);
}

/// <summary>
/// The review's gate (REVIEWENV1c, D154 points 3 and 7; design §1.4, §3.1–§3.2): which level decides for a chain's work in a
/// repository, and whether the work that would land may go. A table, a commit, an ancestry and a press: no model reads any of
/// it (design §4).
/// </summary>
/// <remarks>
/// <para><b>Every landing door asks it</b>: the review's press (<c>LAND_SESSION_TREE</c>), <c>daoris-driver trees land</c>, and
/// the look under <i>Accept automatically</i>, an advance of the chain's branch among them, since each lands through
/// <see cref="SessionTrees.LandAsync"/>, which refuses a state that does not let go.</para>
///
/// <para><b>What lets it go</b> is the person's <i>reviewed</i> on a set-up whose commit is the landing's tip or holds it
/// (<c>git merge-base --is-ancestor</c>), or their skip. A <i>reviewed</i> of an older set-up does not let newer work through.</para>
/// </remarks>
public static class ReviewGate
{
    /// <summary>
    /// The quests of the <c>follows</c> chain <paramref name="id"/> is a step of (D149 point 1): up to its first quest, then every
    /// step published after, in the order the quests are listed. A quest the list does not hold has no chain here.
    /// </summary>
    public static IReadOnlyList<QuestView> ChainOf(IReadOnlyList<QuestView> quests, string id)
    {
        var byId = quests.GroupBy(quest => quest.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        if (!byId.TryGetValue(id.TrimStart('#'), out var first)) return [];

        // Up to the first quest; a parent seen twice, which no chain makes, ends the walk rather than looping.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { first.Id };
        while (first.Parent is { Length: > 0 } parent && seen.Add(parent) && byId.TryGetValue(parent, out var above)) first = above;

        var chain = new List<QuestView> { first };
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { first.Id };
        for (var next = 0; next < chain.Count; next++)
        {
            foreach (var step in quests.Where(quest => Same(quest.Parent, chain[next].Id)))
            {
                if (visited.Add(step.Id)) chain.Add(step);
            }
        }

        return chain;
    }

    /// <summary>
    /// The level's table for the chain's work in <paramref name="repository"/> (design §1.4), read from the most specific down;
    /// the first row that says anything decides. A chain that crosses repositories reads it once for each one's part.
    /// </summary>
    /// <param name="chain">The chain's quests (<see cref="ChainOf"/>); empty where none could be read.</param>
    /// <param name="ask">The ask the chain was asked by, or null.</param>
    /// <param name="rule">The review rule standing for the repository (<see cref="ReviewRules.Resolve"/>), or null for none set.</param>
    public static ReviewDecision Decide(IReadOnlyList<QuestView> chain, string repository, AskView? ask, ResolvedReview? rule)
    {
        var part = chain.Where(quest => Same(quest.To, repository)).ToList();
        var work = part.LastOrDefault(quest => quest.SetUpIn is null)?.Id;
        ReviewDecision Of(string level, string? environment) => new(level, environment) { Repository = repository, Work = work };

        // Row 1: the person's skip, on a set-up step or on the chain's work in this repository.
        foreach (var quest in Enumerable.Reverse(part))
        {
            if (quest.Verdicts.LastOrDefault(verdict => verdict.Said == ReviewVerdicts.Skipped) is { } skip)
            {
                return Of(ReviewLevels.Skip, null) with { Skip = skip, SkippedOn = quest.Id, Words = skip.Words };
            }
        }

        // Row 2: a set-up step for this repository, published and not declined, or still composed in a quest whose done has not
        // published its steps yet. Another repository's set-up step is that repository's.
        if (part.LastOrDefault(quest => quest.SetUpIn is not null && quest.Status != "Declined") is { } step)
        {
            return Of(ReviewLevels.SetUpStep, step.SetUpIn) with { SetUpStep = step };
        }

        foreach (var quest in chain.Where(quest => quest.Status is "Open" or "Taken" || quest is { Status: "Done", Held: true }))
        {
            if (quest.Then.FirstOrDefault(each => each.SetUpIn is not null && Same(each.To, repository)) is { } composed)
            {
                return Of(ReviewLevels.SetUpStep, composed.SetUpIn) with { Composed = composed, ComposedBy = quest.Id };
            }
        }

        // Row 3: the chain's choice, inherited by each step: this repository's newest step's, else the chain's.
        if ((part.LastOrDefault(quest => quest.Review is not null) ?? chain.FirstOrDefault(quest => quest.Review is not null))?.Review is { } chosen)
        {
            return Chosen(ReviewLevels.Chain, chosen.Choice, chosen.Words);
        }

        // Row 4: the ask's choice, the latest standing, for every chain it publishes that sets none.
        if (ask?.ReviewChoices.LastOrDefault() is { } asked) return Chosen(ReviewLevels.Ask, asked.Choice, asked.Words);

        // Rows 5 and 6: the rule, a repository's replacing its workspace's whole; only a required one says review.
        if (rule is null) return Of(ReviewLevels.Nothing, null);
        var level = rule.Source == ReviewSource.Repository ? ReviewLevels.Repository : ReviewLevels.Workspace;
        return Of(level, rule.Rule is { IsNone: false, Required: true } required ? required.Environments[0].Name : null);

        // `on` is the rule's default environment, where the repository declares one (design §1.4: `on` applies wherever an
        // environment is declared); a name is that environment, which the planner sits a set-up step for where no rule names it.
        ReviewDecision Chosen(string level, string choice, string? words) => Of(level, choice switch
        {
            "off" => null,
            "on" => rule?.Rule is { IsNone: false } declared ? declared.Environments[0].Name : null,
            _ => choice,
        }) with { Choice = choice, Words = words };
    }

    /// <summary>
    /// Where the gate stands for a decision and the commit that would land (design §3.2): nothing to wait for, the skip, or the
    /// set-up step's state, which lets the work go only where the person reviewed a set-up whose commit holds the tip.
    /// </summary>
    /// <param name="tip">The commit that would land; null where none was read, which nothing reviewed can hold.</param>
    /// <param name="holds">Whether the tip is a commit or an ancestor of it: true, false, or null where git could not say.</param>
    public static async Task<ReviewGateState> JudgeAsync(ReviewDecision decision, string? tip, Func<string, Task<bool?>> holds)
    {
        if (!decision.Reviews)
        {
            return new(decision.Level == ReviewLevels.Skip ? ReviewStates.Skipped : ReviewStates.None, decision)
            {
                Tip = tip,
                Verdict = decision.Skip,
            };
        }

        if (decision.SetUpStep is not { } step) return new(ReviewStates.NotShown, decision) { Tip = tip };
        if (step.Status is "Open" or "Taken" || step.SetUps.Count == 0) return new(ReviewStates.BeingSetUp, decision) { Tip = tip };

        // The newest set-up the person reviewed, and whether its commit holds what would land.
        var reviewed = step.SetUps.LastOrDefault(setUp => VerdictOn(step, setUp, ReviewVerdicts.Reviewed) is not null);
        if (reviewed is not null && tip is not null && await holds(reviewed.Commit).ConfigureAwait(false) == true)
        {
            return new(ReviewStates.Reviewed, decision) { SetUp = reviewed, Verdict = VerdictOn(step, reviewed, ReviewVerdicts.Reviewed), Tip = tip };
        }

        var newest = step.SetUps[^1];
        if (VerdictOn(step, newest, ReviewVerdicts.NotYet) is { } notYet && !ReferenceEquals(reviewed, newest))
        {
            return new(ReviewStates.NotYet, decision) { SetUp = newest, Verdict = notYet, Tip = tip };
        }

        return ReferenceEquals(reviewed, newest)
            ? new(ReviewStates.NotHeld, decision) { SetUp = reviewed, Verdict = VerdictOn(step, reviewed, ReviewVerdicts.Reviewed), Tip = tip }
            : new(ReviewStates.Shown, decision) { SetUp = newest, Tip = tip };
    }

    /// <summary>
    /// The gate for a session's tree (design §3.1): the chain its quest is a step of, read from the service, the rule standing for
    /// the tree's repository, the tree's <c>HEAD</c> and git's ancestry. A conversation, which serves no quest, has nothing to wait
    /// for. A service that does not answer holds the work only where a rule stands that could ask for a review, and says so.
    /// </summary>
    public static async Task<ReviewGateState> ReadAsync(
        IReviewWorld world, DriverConfig config, string tree, string repository, string workspace, string? quest, CancellationToken ct = default)
    {
        var rule = ReviewRules.Resolve(config, repository, workspace);
        if (quest is null) return new(ReviewStates.None, new ReviewDecision(ReviewLevels.Nothing, null) { Repository = repository });

        ReviewDecision decision;
        try
        {
            var chain = ChainOf(await world.QuestsAsync(ct).ConfigureAwait(false), quest);
            var asked = chain.Select(each => AskWords.AskOf(each.From)).FirstOrDefault(id => id is not null);
            var ask = asked is null ? null : await world.AskAsync(asked, ct).ConfigureAwait(false);
            decision = Decide(chain, repository, ask, rule);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or JsonException or InvalidOperationException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            var none = new ReviewDecision(ReviewLevels.Nothing, null) { Repository = repository };
            return rule is { Rule.IsNone: false }
                ? new(ReviewStates.Unread, none) { Problem = error.Message.TrimEnd().TrimEnd('.') }
                : new(ReviewStates.None, none);
        }

        // Git is asked only where a review is asked: a landing no review touches costs no process.
        var tip = decision.Reviews ? await HeadAsync(tree, ct).ConfigureAwait(false) : null;
        return await JudgeAsync(decision, tip, commit => HoldsAsync(tree, tip!, commit, ct)).ConfigureAwait(false);
    }

    /// <summary>
    /// Whether a set-up step's done still waits for the person's review (design §2.6), as the service's hold reads it: a set-up
    /// step neither skipped nor reviewed on its newest set-up.
    /// </summary>
    public static bool Waits(QuestView quest) =>
        quest.SetUpIn is not null
        && !quest.Verdicts.Any(verdict => verdict.Said == ReviewVerdicts.Skipped)
        && !(quest.SetUps.Count > 0 && VerdictOn(quest, quest.SetUps[^1], ReviewVerdicts.Reviewed) is not null);

    /// <summary>
    /// What the gate says (design §3.1–§3.2): <i>Waits for your review in <c>environment</c></i> and why, with the door that moves
    /// it, where it holds; what let it go, where it does.
    /// </summary>
    public static string Says(ReviewGateState gate)
    {
        var decision = gate.Decision;
        var lead = $"Waits for your review in `{decision.Environment}`: ";
        var step = decision.SetUpStep?.Id;
        var skipOn = step is not null && gate.State != ReviewStates.NotHeld ? step : decision.Work ?? step;
        var skip = skipOn is null ? "" : $" `daoris-driver quest review {skipOn} skip \"…\"` lets it land without one.";
        return gate.State switch
        {
            ReviewStates.None => decision.Level == ReviewLevels.Nothing
                ? "Nothing waits for a review here."
                : $"No review waits for this work: {NoReview(decision)}.",
            ReviewStates.Skipped =>
                $"You skipped the review of this work{(decision.SkippedOn is { } on ? $" on quest `#{on}`" : "")}: it lands without one.",
            ReviewStates.Reviewed =>
                $"You reviewed it in `{decision.Environment}` on set-up step `#{step}` at `{Short(gate.SetUp!.Commit)}`, which holds what lands.",
            ReviewStates.NotShown => lead + (decision.Composed is { } composed
                ? $"its set-up step, \"{composed.Title.Replace("{parent}", $"#{decision.ComposedBy}", StringComparison.Ordinal)}\", is "
                  + $"published to `{composed.To}` when `#{decision.ComposedBy}` closes done, and shows it there."
                : $"nothing shows it there yet: no set-up step for `{decision.Repository}` is in its chain." + skip),
            ReviewStates.BeingSetUp => lead + (decision.SetUpStep!.Status is "Open" or "Taken"
                ? $"set-up step `#{step}` is {decision.SetUpStep.Status.ToLowerInvariant()}: it shows the work there, then waits for your look."
                : $"set-up step `#{step}` is done, and what it showed is posted when its session ends."),
            ReviewStates.Shown => lead + $"set-up step `#{step}` showed it at `{Short(gate.SetUp!.Commit)}`"
                + (gate.SetUp.Look is { } look ? $", at <{look}>" : gate.SetUp.Local ? ", on the machine that showed it" : "")
                + (gate.SetUp.Shows is { Length: > 0 } shows ? $": {shows.TrimEnd().TrimEnd('.')}" : "")
                + $". Look at it, then `daoris-driver quest review {step} reviewed`, or `daoris-driver quest review {step} not-yet \"…\"` "
                + "with what is not right yet.",
            ReviewStates.NotYet => lead
                + $"you said not yet to what set-up step `#{step}` showed at `{Short(gate.SetUp!.Commit)}`, and it waits for the set-up it "
                + "shows next.",
            ReviewStates.NotHeld => lead
                + $"what you reviewed does not hold these commits: set-up step `#{step}` was reviewed at `{Short(gate.SetUp!.Commit)}`, and "
                + $"{(gate.Tip is { } tip ? $"`{Short(tip)}`" : "the work's tip")} is not that commit or one before it. It waits for the "
                + "set-up step to show it again." + skip,
            _ => $"Whether this work waits for your review could not be read: {gate.Problem}. Nothing lands until it can be.",
        };
    }

    /// <summary>Why a level that spoke asks for no review, in the words of its row.</summary>
    private static string NoReview(ReviewDecision decision) => decision.Level switch
    {
        ReviewLevels.Chain or ReviewLevels.Ask when decision.Choice == "off" =>
            $"{(decision.Level == ReviewLevels.Chain ? "its chain" : "its ask")} says `off`",
        ReviewLevels.Chain or ReviewLevels.Ask =>
            $"{(decision.Level == ReviewLevels.Chain ? "its chain" : "its ask")} says `{decision.Choice}`, and `{decision.Repository}` declares no review environment",
        ReviewLevels.Repository => $"`{decision.Repository}`'s rule does not require one",
        _ => $"the workspace's rule does not require one for `{decision.Repository}`",
    };

    /// <summary>The person's verdict of one kind on a set-up, by the machine and sequence that name it, else by its commit.</summary>
    private static QuestReviewVerdictView? VerdictOn(QuestView step, QuestSetUpView setUp, string said) =>
        step.Verdicts.LastOrDefault(verdict => verdict.Said == said
            && (setUp is { Machine: { } machine, Sequence: { } sequence }
                ? string.Equals(verdict.SetUpMachine, machine, StringComparison.Ordinal) && verdict.SetUpSequence == sequence
                : string.Equals(verdict.Commit, setUp.Commit, StringComparison.OrdinalIgnoreCase)));

    /// <summary>The tree's <c>HEAD</c>, by its full id; null where git cannot say.</summary>
    internal static async Task<string?> HeadAsync(string tree, CancellationToken ct)
    {
        if (!Directory.Exists(tree)) return null;
        var (code, head, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "--verify", "--quiet", "HEAD"], ct).ConfigureAwait(false);
        return code == 0 && head.Trim() is { Length: > 0 } sha ? sha : null;
    }

    /// <summary>
    /// Whether <paramref name="tip"/> is <paramref name="commit"/> or an ancestor of it (design §3.2): git's own answer,
    /// <c>merge-base --is-ancestor</c>, exit 0 yes and 1 no; anything else, a commit this repository does not hold among them, is
    /// null, which holds nothing.
    /// </summary>
    internal static async Task<bool?> HoldsAsync(string tree, string tip, string commit, CancellationToken ct)
    {
        if (!EvidenceCodes.IsObjectId(commit)) return null;
        var (code, _, _) = await WorkingTree.GitAsync(tree, ["merge-base", "--is-ancestor", tip, commit], ct).ConfigureAwait(false);
        return code switch { 0 => true, 1 => false, _ => null };
    }

    private static bool Same(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static string Short(string commit) => commit.Length > 8 ? commit[..8] : commit;
}
