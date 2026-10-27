namespace Daoris.Driver;

/// <summary>A quest of the work and what an abandon does with it.</summary>
public sealed record AbandonQuest(WorkQuest Quest, string Act)
{
    public string Key => AbandonKeys.QuestKey(Quest.Quest.Id);

    /// <summary>For a kept quest, why (<see cref="AbandonWhy"/>).</summary>
    public string? Why { get; init; }

    /// <summary>For a quest taken on another machine, that machine where a teammate's record names it.</summary>
    public string? Machine { get; init; }

    /// <summary>Whether its decline applies only while it is open (PAUSE1c): an open quest's.</summary>
    public bool WhileOpen { get; init; }
}

/// <summary>A session of the work and what an abandon does with it: stopped or ended, then archived where nothing keeps it needing you.</summary>
public sealed record AbandonSession(WorkSession Session, string Act)
{
    public string Key => AbandonKeys.SessionKey(Session.Record.Id);

    /// <summary>Whether it is archived last, once stopped or ended.</summary>
    public bool Archive { get; init; }

    /// <summary>For a session kept, or not archived, why (<see cref="AbandonWhy"/>).</summary>
    public string? Why { get; init; }

    /// <summary>For a teammate's session, the machine it runs on.</summary>
    public string? Machine { get; init; }
}

/// <summary>A tree of the work, or its branch alone, and what an abandon does with it, with the proof's facts.</summary>
public sealed record AbandonTree(WorkTree Tree, string Act, TreeOnlyHere Judged)
{
    public string Key => AbandonKeys.TreeKey(Tree.Repository, Tree.Branch);

    /// <summary>For a kept tree, why (<see cref="AbandonWhy"/>).</summary>
    public string? Why { get; init; }

    /// <summary>The base its commits are judged from: its first session's (SURF6), or null for the merge-base with the line.</summary>
    public string? BaseCommit { get; init; }

    /// <summary>The repository's registered checkout, which answers for a branch whose tree is gone: never handed to a page.</summary>
    public string? Root { get; init; }
}

/// <summary>
/// The work of an ask or a quest with what an abandon would do with each piece (PAUSE1d, design §3.1, §3.2): the first press,
/// which <c>WORK_PLAN</c> answers beside the pause's half and <c>ask --abandon</c> prints.
/// </summary>
public sealed record AbandonPlan(
    WorkPlan Work, IReadOnlyList<AbandonQuest> Quests, IReadOnlyList<AbandonSession> Sessions, IReadOnlyList<AbandonTree> Trees)
{
    /// <summary>The ask the abandon closes with the reason (ask scope, an ask not closed), or null.</summary>
    public string? Closes { get; init; }

    /// <summary>The newest abandon of this scope here, from <c>abandoned.json</c>, or null.</summary>
    public AbandonEntry? Abandoned { get; init; }

    /// <summary>
    /// Whether the abandon would take anything (design §3.1): a quest it would decline, a tree or branch it would discard, or
    /// a session it would stop, end or archive. Closing the ask alone is *Close ask*'s, never an abandon's.
    /// </summary>
    public bool Abandonable =>
        Quests.Any(quest => quest.Act == AbandonAct.Decline)
        || Trees.Any(tree => tree.Act is AbandonAct.Discard or AbandonAct.Delete)
        || Sessions.Any(session => session.Act is AbandonAct.Stop or AbandonAct.End or AbandonAct.Archive);

    /// <summary>The key of every piece the abandon would take: what the first press lists, and the second press sends back.</summary>
    public IReadOnlyList<string> Pieces => !Abandonable
        ? []
        : [
            .. Quests.Where(quest => AbandonAct.Takes(quest.Act)).Select(quest => quest.Key),
            .. Sessions.Where(session => AbandonAct.Takes(session.Act)).Select(session => session.Key),
            .. Trees.Where(tree => AbandonAct.Takes(tree.Act)).Select(tree => tree.Key),
            .. Closes is { } ask ? [AbandonKeys.AskKey(ask)] : Array.Empty<string>(),
        ];

    /// <summary>Every piece the abandon would keep, with why, in the words of design §3.2.</summary>
    public IReadOnlyList<AbandonKeep> Kept =>
    [
        .. Quests.Where(quest => quest.Act == AbandonAct.Keep).Select(quest => new AbandonKeep(quest.Key, quest.Why!) { Machine = quest.Machine }),
        .. Sessions.Where(session => session.Act == AbandonAct.Keep).Select(session => new AbandonKeep(session.Key, session.Why!) { Machine = session.Machine }),
        .. Trees.Where(tree => tree.Act == AbandonAct.Keep).Select(tree => new AbandonKeep(tree.Key, tree.Why!) { Where = tree.Judged.Where }),
    ];
}

/// <summary>What an abandon did (design §3.4, §4.2).</summary>
public sealed record AbandonOutcome(WorkScope Scope, string Id, AbandonVerdict Verdict)
{
    /// <summary>How many pieces the list held to go.</summary>
    public int Listed { get; init; }

    /// <summary>How many of them went.</summary>
    public int Went { get; init; }

    /// <summary>Pieces listed to go that changed since the list and were kept, each with why now.</summary>
    public IReadOnlyList<AbandonKeep> Changed { get; init; } = [];

    /// <summary>Pieces a step could not take, each with why and the service's or git's words.</summary>
    public IReadOnlyList<AbandonKeep> Failed { get; init; } = [];

    /// <summary>Pieces that joined the work since the list: not taken, and the scope stays paused.</summary>
    public IReadOnlyList<string> Joined { get; init; } = [];

    /// <summary>Each quest declined with the reason.</summary>
    public IReadOnlyList<string> Declined { get; init; } = [];

    /// <summary>Whether the ask was closed with the reason.</summary>
    public bool Closed { get; init; }

    /// <summary>Each session stopped, or ended unanswered.</summary>
    public IReadOnlyList<WorkStop> Stopped { get; init; } = [];

    /// <summary>Each tree discarded and each branch deleted alone, with its tip.</summary>
    public IReadOnlyList<AbandonedTree> Discarded { get; init; } = [];

    /// <summary>Each session archived.</summary>
    public IReadOnlyList<string> Archived { get; init; } = [];

    /// <summary>What the list kept by design, and each session stopped but left to review.</summary>
    public IReadOnlyList<AbandonKeep> Stayed { get; init; } = [];

    /// <summary>Each shared decline's answer from the pass.</summary>
    public IReadOnlyList<AbandonDecline> Declines { get; init; } = [];

    /// <summary>Whether the scope stays paused: a step failed, or something joined the work since the list.</summary>
    public bool StillPaused { get; init; }
}
