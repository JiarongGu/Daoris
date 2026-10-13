namespace Daoris.Driver;

/// <summary>
/// A session whose work a second opinion is owed on by itself (XAGENT1f, D155 point 2; the second-agent design §2.1): its quest
/// done in a tree of its own under a rule whose occasions read it. The look asks the pass, delivers it, and closes the entry once
/// the work landed, its tree went, or the rule no longer asks.
/// </summary>
/// <param name="Occasion"><c>landing</c>, or <c>steps</c> where only the chain's next step waits for it.</param>
/// <param name="Tree">The tree its record names, under this home's trees (D51): machine-local, like the file that keeps it.</param>
public sealed record OpinionDue(
    string Session, string Quest, string Repository, string Workspace, string Tree, string Occasion, DateTimeOffset At)
{
    /// <summary>When the look closed it, and why; null while it is owed.</summary>
    public DateTimeOffset? Closed { get; init; }

    public string? Why { get; init; }
}

/// <summary>Why the look closed an opinion owed (<see cref="OpinionDue"/>), each a code the file keeps.</summary>
public static class OpinionDueClosed
{
    /// <summary>The work landed, by any door.</summary>
    public const string Landed = "landed";

    /// <summary>Its tree is gone.</summary>
    public const string Gone = "gone";

    /// <summary>Its quest is no longer done.</summary>
    public const string Undone = "undone";

    /// <summary>The rule standing for its repository no longer reads at this occasion.</summary>
    public const string Off = "off";

    /// <summary>The chain's next step started: what <c>steps</c> held is past.</summary>
    public const string Past = "past";
}

/// <summary>What the driver keeps of a chain's work in one repository at the gate (XAGENT1f): each pass asked, each the person asked
/// for and not yet started, and each answer of theirs.</summary>
public sealed record OpinionGateKept
{
    public IReadOnlyList<OpinionAskKept> Asks { get; init; } = [];

    public IReadOnlyList<OpinionRequest> Requests { get; init; } = [];

    public IReadOnlyList<OpinionPersonWord> Person { get; init; } = [];

    public static OpinionGateKept None { get; } = new();
}
