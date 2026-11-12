namespace Daoris.Knowledge;

/// <summary>Why an ask did not do what it is asked — or <see cref="None"/> when it did.</summary>
public enum AskRefusal
{
    None,

    /// <summary>No sentence, or a close with no reason.</summary>
    Empty,

    /// <summary>A link or a file a quest could not carry either.</summary>
    BadCarry,

    /// <summary>The circle is not one this machine holds.</summary>
    UnknownWorkspace,

    /// <summary>No ask under that id.</summary>
    NotFound,

    /// <summary>The ask is closed; it becomes nothing more.</summary>
    Closed,

    /// <summary>The quest it is to become is refused by the exchange — the ask itself is kept.</summary>
    QuestRefused,

    /// <summary>A session's publish beside the ask's intake, which alone publishes it while it answers or before any has served it.</summary>
    IntakeAnswering,

    /// <summary>
    /// A delete that would remove a record something stands on — a quest asked by it that somebody took
    /// or answered, or that a session started for — so the ask stays whole.
    /// </summary>
    Kept,

    /// <summary>
    /// A review choice or proposal that is not one: not <c>off</c>, <c>on</c> or an environment's name, words
    /// or a reason past its bound, a proposal with no reason or from no intake, or a choice and a proposal at once.
    /// </summary>
    BadReview,

    /// <summary>A kind or a workflow that is not one by its shape, or words past their bound.</summary>
    BadWorkflow,

    /// <summary>A short title past <see cref="QuestTitles.MaxShort"/> characters, or over a line break.</summary>
    BadShort,

    /// <summary>A declared path that is no repository-relative glob, or too many.</summary>
    BadPaths,

    /// <summary>A requirement on a new ask that names no receiver, or one not of its shape.</summary>
    BadRequirement,

    /// <summary>Through words past their bound.</summary>
    BadThrough,

    /// <summary>A chain's step asked with no receiver named: a chain follows a first quest, and an ask that is only proposed publishes none.</summary>
    BadChain,
}
