namespace Daoris.Driver;

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

/// <summary>The verdicts a person gives a review, as the service spells them (REVIEWENV1b).</summary>
public static class ReviewVerdicts
{
    public const string Reviewed = "reviewed";
    public const string NotYet = "not-yet";
    public const string Skipped = "skipped";
}

/// <summary>
/// Where the second opinion's part of the landing gate stands (XAGENT1f, D155 points 9 and 10; the second-agent design §8.5), as
/// a code a log line and a page can carry. Five let the work go: nothing to wait for, settled, and the person's three answers.
/// An opinion that is unavailable lets it go only where the rule does not require one (§8.4).
/// </summary>
public static class OpinionGateStates
{
    /// <summary>The level says no second opinion here: today's behaviour.</summary>
    public const string None = "none";

    /// <summary>A later step of the chain in this repository is still to run: one opinion reads the chain's whole work there.</summary>
    public const string WaitsChain = "waits-chain";

    /// <summary>The work is done and nothing has been asked yet: the driver's next look asks it, after the quest's own hold.</summary>
    public const string NotAsked = "not-asked";

    /// <summary>Another agent is reading it.</summary>
    public const string Reading = "reading";

    /// <summary>Its findings are with the working session, which answers them as its next turn.</summary>
    public const string WithSession = "with-session";

    /// <summary>The one recheck reads the commits made in answer.</summary>
    public const string ReadAgain = "read-again";

    /// <summary>A <c>must</c> finding not fixed and not withdrawn: the person's to answer, in the press that comes next.</summary>
    public const string Disputed = "disputed";

    /// <summary>No opinion could be had: no reviewer, a failed pass, or one out of time. It holds only where the rule requires one.</summary>
    public const string Unavailable = "unavailable";

    /// <summary>Commits that would land that no other agent read: the cap of one pass and one recheck is spent.</summary>
    public const string CommitsSince = "commits-since";

    /// <summary>An opinion covers the tip, the working session answered it, and no dispute is open.</summary>
    public const string Settled = "settled";

    /// <summary>The person's <i>Go on anyway…</i>.</summary>
    public const string Anyway = "anyway";

    /// <summary>The person's <i>I looked myself…</i>: their own reading, an opinion of tier <c>person</c>.</summary>
    public const string Myself = "myself";

    /// <summary>The press that came next answered what was unsettled: <i>Accept…</i> with it shown, or D154's <i>Reviewed</i> after it.</summary>
    public const string Answered = "answered";

    /// <summary>Whether it waits could not be read: the service or git did not answer, where a rule says one is asked.</summary>
    public const string Unread = "unread";

    /// <summary>
    /// The run's named workflow asks for a second opinion by reviewers its repository does not declare (WORKFLOW1f, the workflow
    /// design §2.3, §3.7): it sits saying why, until they are declared or the person goes on, or looks, themselves.
    /// </summary>
    public const string CannotStart = "cannot-start";

    /// <summary>The states in which another agent, or the working session, is still at work on the opinion: what sits a set-up step (§7).</summary>
    public static bool AtWork(string state) => state is NotAsked or Reading or WithSession or ReadAgain;
}

/// <summary>
/// What the landing record keeps of the second opinion (XAGENT1f, design §8.6): what let the work go, the reviewer and its label,
/// the candidate, the passes, the findings counted by weight and by answer, the person's adjudication, and the commits unread;
/// or <c>none</c> with its code.
/// </summary>
/// <param name="Said">The gate's state that let it go: <c>settled</c>, <c>anyway</c>, <c>myself</c>, <c>answered</c> or <c>unavailable</c>.</param>
public sealed record LandingOpinion(string Said)
{
    public string? Opinion { get; init; }

    public string? Reviewer { get; init; }

    public string? Label { get; init; }

    public string? Base { get; init; }

    public string? Tip { get; init; }

    public int Passes { get; init; }

    /// <summary>The findings, counted by weight: <c>must</c>, <c>should</c>, <c>note</c>.</summary>
    public IReadOnlyDictionary<string, int> Weights { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>The answers, counted: <c>fixed</c>, <c>rejected</c>, <c>unresolved</c>.</summary>
    public IReadOnlyDictionary<string, int> Answers { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal);

    public int Disputes { get; init; }

    /// <summary>The commits that landed which no other agent read.</summary>
    public int Unread { get; init; }

    /// <summary>The person's adjudication: what they said, and when, and their words.</summary>
    public string? Person { get; init; }

    public DateTimeOffset? At { get; init; }

    public string? Words { get; init; }

    /// <summary>Why none could be had, where it landed without one.</summary>
    public string? Code { get; init; }
}

/// <summary>How a reviewer stands to the work's families (§3.4): what every opinion carries on its face.</summary>
public static class ReviewerLabels
{
    /// <summary>Its maker is declared, and is not the maker of any family that wrote the work: tried first.</summary>
    public const string AnotherMaker = "another-maker";

    /// <summary>Not of the work's families by owner, and its maker not declared: never taken as another maker's agent.</summary>
    public const string MakerNotDeclared = "maker-not-declared";

    /// <summary>One of the work's families: the same agent, fresh, taken only when listed and no other can run.</summary>
    public const string SameAgent = "same-agent";
}

/// <summary>A rule as it resolves for a repository, and where it was set (<see cref="ReviewSource"/>).</summary>
public sealed record ResolvedReview(ReviewRule Rule, string Source);

/// <summary>A rule as it resolves for a repository, and where it was set (<see cref="OpinionSource"/>).</summary>
public sealed record ResolvedOpinion(OpinionRule Rule, string Source);
