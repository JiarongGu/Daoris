namespace Daoris.Driver;

/// <summary>
/// What the gate reads for the chain's work in a repository (design §8.2): the rule standing for it, the quest that would land
/// and whether a later step here is still to run, the commit that would land, what the driver kept of the asks and the person's
/// answers, and D154's gate, whose <i>Reviewed</i> answers what it shows.
/// </summary>
public sealed record OpinionGateFacts(string Repository, ResolvedOpinion? Rule)
{
    /// <summary>The working session the door lands, which the terminal's doors name.</summary>
    public string? Session { get; init; }

    /// <summary>
    /// The quest the work served; null for a conversation's, which only a chat run's gate reads (<see cref="Run"/>), and otherwise lands
    /// nothing by the driver's gate (§12.2).
    /// </summary>
    public QuestView? Quest { get; init; }

    /// <summary>
    /// The chat run whose work this is (CHATFLOW1d, the chat-flow design §5.2, §7), <c>chat-&lt;first&gt;</c>, where no quest is: its gate is
    /// kept under the run's key, and its opinion is asked at the person's press, never at a look. Null for a quest's work.
    /// </summary>
    public string? Run { get; init; }

    /// <summary>Whether a press already asked the chat run's opinion and the look has not started it yet (CHATFLOW1d, §5.2 point 4).</summary>
    public bool Asked { get; init; }

    /// <summary>A later step of the chain in this repository still to run, by its quest's id; null where none is.</summary>
    public string? Later { get; init; }

    /// <summary>The commit that would land: the tree's <c>HEAD</c>; null where it was not read.</summary>
    public string? Tip { get; init; }

    /// <summary>The occasion the gate is read for: <c>landing</c>, or <c>steps</c> for the chain's next step (§8.1).</summary>
    public string Occasion { get; init; } = OpinionRules.Landing;

    public IReadOnlyList<OpinionAskKept> Asks { get; init; } = [];

    public IReadOnlyList<OpinionPersonWord> Person { get; init; } = [];

    /// <summary>D154's gate for the same work, whose <i>Reviewed</i> after the opinion stood answers its disputes (§7).</summary>
    public ReviewGateState? Review { get; init; }

    /// <summary>Why the run's named workflow's opinion step cannot start (WORKFLOW1f, <see cref="IWorkplaceProcess.OpinionCannot"/>); else null.</summary>
    public string? Cannot { get; init; }
}

/// <summary>What the judgement reads beyond the facts: the host's opinions, what the driver did with each, and git's ancestry.</summary>
/// <param name="Opinion">An opinion as the host answers it, or null.</param>
/// <param name="Delivered">What the driver kept of an opinion's delivery (<see cref="OpinionDeliveries"/>).</param>
/// <param name="Covers">Whether the tip that would land is a commit or an ancestor of it: true, false, or null where git could not say.</param>
/// <param name="Since">How many commits that would land a commit does not hold; null where git could not say.</param>
public sealed record OpinionReads(
    Func<string, Task<OpinionView?>> Opinion, Func<string, OpinionDelivered> Delivered, Func<string, Task<bool?>> Covers,
    Func<string, Task<int?>> Since)
{
    /// <summary>
    /// When a reviewer's account is ready again after the limit that ended its session, by that session (LIMITRESUME1h, D206 design
    /// §3.8): its cool-off's <c>until</c>, or the moment it was read where no cool-off names it any more. Null reads none, as before.
    /// </summary>
    public Func<string, DateTimeOffset>? Ready { get; init; }
}
