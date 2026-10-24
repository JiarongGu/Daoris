namespace Daoris.Driver;

/// <summary>
/// A session whose work a second opinion is owed on by itself (XAGENT1f, D155 point 2; the second-agent design §2.1): its quest
/// done in a tree of its own under a rule whose occasions read it. The look asks the pass, delivers it, and closes the entry once
/// the work landed, its tree went, or the rule no longer asks.
/// </summary>
/// <param name="Quest">The quest the work served; empty for a chat run's (<see cref="Run"/>), which serves none.</param>
/// <param name="Occasion"><c>landing</c>, or <c>steps</c> where only the chain's next step waits for it.</param>
/// <param name="Tree">The tree its record names, under this home's trees (D51): machine-local, like the file that keeps it.</param>
public sealed record OpinionDue(
    string Session, string Quest, string Repository, string Workspace, string Tree, string Occasion, DateTimeOffset At)
{
    /// <summary>When the look closed it, and why; null while it is owed.</summary>
    public DateTimeOffset? Closed { get; init; }

    public string? Why { get; init; }

    /// <summary>
    /// The chat run it is owed on (CHATFLOW1d, the chat-flow design §7), <c>chat-&lt;first&gt;</c>, in place of a quest: asked at the person's
    /// press, at <c>landing</c> only, and closed <c>landed</c>, <c>gone</c> or <c>off</c>, never <c>undone</c>. Null for a quest's.
    /// </summary>
    public string? Run { get; init; }

    /// <summary>
    /// The tip the press captured (CHATFLOW1d, §5.2 point 1, §7): the candidate the pass reads, never the tree's <c>HEAD</c> at the look.
    /// Null for a quest's, whose pass reads the tree as it stands.
    /// </summary>
    public string? Tip { get; init; }

    /// <summary>
    /// The person's turn count at the press (CHATFLOW1d, §5.2 point 3, §6.1 (b)): their own words in the line's conversation, so a
    /// send-back compares it and gives the findings to the person where it rose. Null for a quest's.
    /// </summary>
    public int? Turns { get; init; }

    /// <summary>Whether it is owed on a chat run rather than a quest's chain.</summary>
    public bool OnRun => ChatRuns.IsChatRun(Run);
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
