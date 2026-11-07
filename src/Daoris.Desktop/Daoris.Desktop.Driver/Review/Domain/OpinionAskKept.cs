namespace Daoris.Driver;

/// <summary>
/// One pass the driver asked for the chain's work in a repository, kept beside it: the opinion the host opened, or
/// why no reviewer could read it, with the cool-off's reset where that is why.
/// </summary>
/// <param name="Occasion">One of <c>landing</c>, <c>steps</c>, <c>failure</c> or <c>asked</c>.</param>
/// <param name="Working">The session whose work it reads.</param>
/// <param name="Tip">The candidate's tip as asked.</param>
public sealed record OpinionAskKept(DateTimeOffset At, string Occasion, string Working, string Tip)
{
    /// <summary>The second opinion the host opened; null where none opened.</summary>
    public string? Opinion { get; init; }

    /// <summary>Why no other maker's agent could read it, the choice's code, or why the pass did not open; null where one opened.</summary>
    public string? Code { get; init; }

    /// <summary>The first reset among the reviewers passed for a cool-off, where that is why.</summary>
    public DateTimeOffset? Until { get; init; }

    /// <summary>Who asked: <c>look</c> for the occasion's own ask, <c>person</c> for a press.</summary>
    public string By { get; init; } = OpinionAskers.Look;

    /// <summary>The adapter that read it, where one did.</summary>
    public string? Reviewer { get; init; }

    /// <summary>How the reviewer stands to the work's families (<see cref="ReviewerLabels"/>), where one read it.</summary>
    public string? Label { get; init; }
}

/// <summary>Who asked a pass: the occasion's look, or the person's press.</summary>
public static class OpinionAskers
{
    public const string Look = "look";
    public const string Person = "person";
}

/// <summary>
/// A pass the person asked for at the gate (<i>Ask now</i>, <i>Try again</i>, <i>Ask again</i> and <i>Ask the same
/// agent, fresh</i>; the failure and asked occasions), kept until the driver's next look starts it.
/// </summary>
/// <param name="Session">The working session whose work it reads.</param>
public sealed record OpinionRequest(DateTimeOffset At, string Session, string Occasion)
{
    /// <summary>The reviewer the person named among the rule's, or null for the rule's order.</summary>
    public string? Reviewer { get; init; }

    /// <summary>The working agent's own, in a fresh conversation: never independent, and said so on its face.</summary>
    public bool SameAgent { get; init; }

    /// <summary>The person's words with it.</summary>
    public string? Words { get; init; }
}
