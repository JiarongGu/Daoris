namespace Daoris.Driver;

/// <summary>
/// A chain's review choice as a quest carries it: <c>off</c>,
/// <c>on</c> or an environment's name, with the person's words it is set on where it is set on theirs.
/// </summary>
public sealed record QuestReviewChoiceView(string Choice, string? Words = null);

/// <summary>
/// One set-up said on a set-up step, as the service answers it: the commit
/// Daoris reads from the step's tree, what it shows and how to show it again. Oldest first on the quest; the newest is the one
/// a verdict answers.
/// </summary>
/// <param name="Commit">The commit read, by its full id: what the gate asks git about.</param>
public sealed record QuestSetUpView(string Commit)
{
    /// <summary>Where to look; absent for a local set-up read on another machine, whose tab is on the machine that showed it.</summary>
    public string? Look { get; init; }

    /// <summary>What it showed and what to look at.</summary>
    public string? Shows { get; init; }

    /// <summary>How to show it again by hand.</summary>
    public string? Again { get; init; }

    /// <summary>The folder <c>review_serve</c> named, as a path in the step's tree; null where nothing is served.</summary>
    public string? Served { get; init; }

    /// <summary>A review run's command, quoted from the procedure; null where none runs.</summary>
    public string? Run { get; init; }

    /// <summary>The session that said it; null for the person's own set-up.</summary>
    public string? Session { get; init; }

    /// <summary>Whether it is shown on a machine (the local kind), rather than in a deployed environment.</summary>
    public bool Local { get; init; }

    public DateTimeOffset? At { get; init; }

    /// <summary>The machine that kept it, which with <see cref="Sequence"/> names it to a verdict.</summary>
    public string? Machine { get; init; }

    public long? Sequence { get; init; }
}

/// <summary>
/// The person's verdict on a review, as the service answers it: theirs alone, and
/// no connector tool or Ask Daoris card gives one. Oldest first on the quest.
/// </summary>
/// <param name="Said">
/// <c>reviewed</c>, <c>not-yet</c> or <c>skipped</c>, spelled as the ask's words of the same kinds are (<see cref="AskWordView"/>).
/// A verdict this build does not know is kept as spelled.
/// </param>
public sealed record QuestReviewVerdictView(string Said)
{
    /// <summary>The machine of the set-up it answers; null for a skip.</summary>
    public string? SetUpMachine { get; init; }

    /// <summary>The sequence of the set-up it answers; null for a skip.</summary>
    public long? SetUpSequence { get; init; }

    /// <summary>The commit of the set-up it answers, as that set-up holds it; null for a skip.</summary>
    public string? Commit { get; init; }

    /// <summary>The person's words: a <c>not-yet</c>'s always.</summary>
    public string? Words { get; init; }

    public DateTimeOffset? At { get; init; }

    /// <summary>The machine it is given on.</summary>
    public string? Machine { get; init; }
}

/// <summary>The person's review choice on an ask, with when and their words: the latest stands.</summary>
public sealed record AskReviewChoiceView(string Choice, DateTimeOffset At, string? Words = null);

/// <summary>
/// An intake's review proposal on an ask: a reading with its reason, which only the person's
/// press applies, the intake session that made it and the quest published with it.
/// </summary>
public sealed record AskReviewProposalView(string Choice, string Reason, DateTimeOffset At, string? Session = null, string? Quest = null);
