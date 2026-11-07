namespace Daoris.Driver;

/// <summary>The codes of a session's start, its opening, its tree and its going on. Part of the catalogue <see cref="NoteCodes"/> introduces.</summary>
public static class StartNoteCodes
{
    // A move to a new part that a live Daoris keeps and has not moved for `minutes`, said once: never taken over while that Daoris runs.
    public static readonly NoteCode MoveQuiet = new("move.quiet", ["minutes"]);

    // ——— While it starts and works, replaced by its end.
    public static readonly NoteCode StartedTree = new("started.tree", ["branch", "basedOn"]);
    public static readonly NoteCode StartedTreeUnrecorded = new("started.tree-unrecorded", []);

    // What the files stage did with each dependency and the tree's other files, a line each, after the record opens.
    public static readonly NoteCode StartedDepsKept = new("started.deps-kept", ["path"]);
    public static readonly NoteCode StartedDepsPlaced = new("started.deps-placed", ["path", "install"]);
    public static readonly NoteCode StartedDepsInstalled = new("started.deps-installed", ["path", "install"]);
    public static readonly NoteCode StartedDepsUnstored = new("started.deps-unstored", ["path", "why"]);
    public static readonly NoteCode StartedDepsUnplaced = new("started.deps-unplaced", ["path", "why"]);
    public static readonly NoteCode StartedDepsLeft = new("started.deps-left", ["path", "why"]);
    public static readonly NoteCode StartedFilesWaiting = new("started.files-waiting", ["count", "repository"]);
    public static readonly NoteCode StartedFilesUnread = new("started.files-unread", ["repository", "why"]);
    public static readonly NoteCode StartedFilesStopped = new("started.files-stopped", []);
    public static readonly NoteCode StartedFilesWrittenBack = new("started.files-written-back", ["paths"]);
    public static readonly NoteCode StartedFilesFailed = new("started.files-failed", ["why"]);

    // Written by openings that copied the checkout's dependencies before the store replaced the copy, and read while those
    // sessions' records stand; nothing writes them now.
    public static readonly NoteCode StartedTreeCopied = new("started.tree-copied", ["branch", "basedOn", "copied"]);
    public static readonly NoteCode StartedUncopiedManager = new("started.uncopied-manager", ["modules"]);
    public static readonly NoteCode StartedUncopiedMoved = new("started.uncopied-moved", ["modules"]);
    public static readonly NoteCode StartedUncopiedUninstalled = new("started.uncopied-uninstalled", ["modules"]);
    public static readonly NoteCode StartedUncopiedWorkspace = new("started.uncopied-workspace", ["modules", "workspace"]);
    public static readonly NoteCode StartedUncopiedLink = new("started.uncopied-link", ["modules"]);
    public static readonly NoteCode StartedUncopiedUnignored = new("started.uncopied-unignored", ["modules"]);
    public static readonly NoteCode StartedUncopiedFailed = new("started.uncopied-failed", ["modules"]);

    // The machine-local files a repository's gates read, copied into its tree read-only, after the opening; and why one is not.
    public static readonly NoteCode StartedLocalCopied = new("started.local-copied", ["inputs"]);
    public static readonly NoteCode StartedLocalMissing = new("started.local-missing", ["input"]);
    public static readonly NoteCode StartedLocalRefused = new("started.local-refused", ["input"]);
    public static readonly NoteCode StartedLocalFailed = new("started.local-failed", ["input"]);

    // A session's tree asked for before it goes on: made again on its branch, gone with its branch, or not made.
    public static readonly NoteCode TreeRemade = new("tree.remade", ["branch"]);
    public static readonly NoteCode TreeGone = new("tree.gone", ["branch"]);

    // Its tree and branch gone with nothing beyond the line, its part goes on in a fresh tree.
    public static readonly NoteCode TreeFresh = new("tree.fresh", ["branch"]);
    public static readonly NoteCode TreeUnmade = new("tree.unmade", []);
    public static readonly NoteCode StartedJoined = new("started.joined", ["session"]);
    public static readonly NoteCode StartedResumesAnswered = new("started.resumes-answered", ["quest", "answered"]);
    public static readonly NoteCode StartedInAskingTree = new("started.in-asking-tree", ["session"]);
    public static readonly NoteCode StartedWordsOpen = new("started.words-open", ["quest", "session"]);
    public static readonly NoteCode StartedWordsTaken = new("started.words-taken", ["quest", "session"]);
    public static readonly NoteCode StartedAnswer = new("started.answer", ["quest", "session"]);
    public static readonly NoteCode StartedReleased = new("started.released", ["quest", "session"]);

    // A quest carried on after the steward's stop of its session is released.
    public static readonly NoteCode StartedReleasedSteward = new("started.released-steward", ["quest", "session"]);
    public static readonly NoteCode StartedCutOff = new("started.cut-off", ["quest", "session"]);

    // An open quest carried on in the tree whose session committed work and failed before it took it: never rebuilt.
    public static readonly NoteCode StartedCarriedWork = new("started.carried-work", ["quest", "session"]);

    // A stand-down carried on once the person brought its lost take forward: the press, never a cut-off.
    public static readonly NoteCode StartedBroughtForward = new("started.brought-forward", ["quest", "session"]);

    // A quest handed to another agent: the carry-on's first line says which agent it came from, and where the account the hand-over
    // named could not run it, that another of the named agent's did. Facts only, no account's name.
    public static readonly NoteCode StartedHandedOver = new("started.handed-over", ["quest", "session", "agent"]);
    public static readonly NoteCode StartedHandedWalked = new("started.handed-walked", []);
    public static readonly NoteCode StartedOtherAccount = new("started.other-account", []);
    public static readonly NoteCode StartedSameTree = new("started.same-tree", []);
    public static readonly NoteCode StartedFellBack = new("started.fell-back", ["why"]) { Why = NoteCodes.Continue };
    public static readonly NoteCode WorkingResumesAnswer = new("working.resumes-answer", []);
    public static readonly NoteCode WorkingGoesOn = new("working.goes-on", []);

    // A park going on with none of the owner's words, the authors that are there in a fixed order (`supervisor`, `helper`, `opinion`,
    // `daoris`), never "your answer".
    public static readonly NoteCode WorkingResumesOthers = new("working.resumes-others", ["authors"]);

    // ——— A session's move to a new part: the part it left says where it went on, the new part where it came from, each the agent and
    // account, why, the context the part stood at (`tokens`) and the brief's characters (`count`). The account by its id, as
    // `chat.moved` names them: a session's events stay on this machine.
    public static readonly NoteCode MovedOn = new("moved.on", ["session", "agent", "account", "why", "tokens", "count"]) { Why = NoteCodes.Handed };
    public static readonly NoteCode MovedFrom = new("moved.from", ["session", "agent", "account", "why", "tokens", "count"]) { Why = NoteCodes.Handed };

    // A limit's move whose own conversation goes on on the new account: the part it left, the new part carrying it whole or compacted
    // past the bound its agent carries whole (`bound`), and a new part handed the brief instead, the carry's code its `reason`.
    public static readonly NoteCode MovedCarriedOn = new("moved.carried-on", ["session", "agent", "account", "why", "tokens"]) { Why = NoteCodes.Handed };
    public static readonly NoteCode MovedCarriedFrom = new("moved.carried-from", ["session", "agent", "account", "why", "tokens"]) { Why = NoteCodes.Handed };
    public static readonly NoteCode MovedCompactedFrom = new("moved.compacted-from", ["session", "agent", "account", "why", "tokens", "bound"]) { Why = NoteCodes.Handed };
    public static readonly NoteCode MovedNotCarried = new("moved.not-carried", ["reason"]);

    // ——— Where the person's words went, after the record's earlier parts.
    public static readonly NoteCode WentCannot = new("went.cannot", ["why"], Key: "work.say.cannot") { Why = NoteCodes.Continue };
    public static readonly NoteCode WentNewSession = new("went.new-session", ["why"]) { Why = NoteCodes.Continue };
    public static readonly NoteCode WentCarriedOn = new("went.carried-on", ["why"]) { Why = NoteCodes.Continue };

    // ——— A conversation's first line, where a start's account is chosen: said in the conversation's record, which never leaves the
    // machine, so its values name accounts and a workspace. The line, then the rest's step and a refused turn riding inside it, then
    // what each account said, a line per account and one per thing it said.
    public static readonly NoteCode OpeningOpened = new("opening.opened", ["account", "why"]) { Why = NoteCodes.Opening };
    public static readonly NoteCode OpeningCarriedOn = new("opening.carried-on", ["session", "account", "why"]) { Why = NoteCodes.Opening };
    public static readonly NoteCode OpeningRest = new("opening.rest", ["why"]) { Why = NoteCodes.Opening };
    public static readonly NoteCode OpeningTurnRefused = new("opening.turn-refused", ["turn"]);
    public static readonly NoteCode OpeningTurnContext = new("opening.turn-context", ["turn", "tokens"]);
    public static readonly NoteCode OpeningUnsaid = new("opening.unsaid", []);
    public static readonly NoteCode OpeningSaidAt = new("opening.said-at", ["account", "seen"]);
    public static readonly NoteCode OpeningSaidNothing = new("opening.said-nothing", ["account"]);
    public static readonly NoteCode OpeningSaidReached = new("opening.said-reached", ["window"]);

    // The agent's own pace warning, never near: `opening.said-warned`; a note written before keeps `opening.said-near`, which nothing writes now.
    public static readonly NoteCode OpeningSaidNear = new("opening.said-near", ["window"]);
    public static readonly NoteCode OpeningSaidWarned = new("opening.said-warned", ["window"]);
    public static readonly NoteCode OpeningSaidCredits = new("opening.said-credits", []);
    public static readonly NoteCode OpeningSaidUsed = new("opening.said-used", ["used", "window"]);

    // Over its curve on a window, and the curve's value there now: `opening.said-over`; a note written before keeps
    // `opening.said-near-at`, the scope's one number, which nothing writes now.
    public static readonly NoteCode OpeningSaidNearAt = new("opening.said-near-at", ["near"]);
    public static readonly NoteCode OpeningSaidOver = new("opening.said-over", ["window", "curve"]);

    // A window's use projected to now from its reading and Daoris's own turns since, by the account's measured ratio: what decided the
    // start asked nothing of it, never near or over, which a reading alone says.
    public static readonly NoteCode OpeningSaidProjected = new("opening.said-projected", ["projected", "window"]);
    public static readonly NoteCode OpeningSaidClear = new("opening.said-clear", []);

    // Beside the opening line, the actor line where a later choice ran, in the local conversation's record only, never the record's
    // note: the actor by its id, the choice's number, the entry that ran and each one passed.
    public static readonly NoteCode OpeningActor = new("opening.actor", ["actor", "n", "entry", "passed"]);

    /// <summary>A role's policy chose the choice that ran, by weight, share, budget or work; the sentence is the judge's, in English.</summary>
    public static readonly NoteCode OpeningChosen = new("opening.chosen", ["actor", "said"]);

    /// <summary>A part that began on another choice of its role than the one before, moved by the role's policy; <c>from</c> says the choice it left, <c>said</c> the condition and the count, in English.</summary>
    public static readonly NoteCode OpeningMovedChoice = new("opening.moved-choice", ["from", "said"]);

    // A quest marked design ran on the order of the role taking `design`, Build's while none does; the role by its name as written,
    // since an added role has no id the page knows.
    public static readonly NoteCode OpeningDesign = new("opening.design", ["role"]);

    // A start refused for its account's sign-in went on under another, which says the account it moved off.
    public static readonly NoteCode OpeningMovedOff = new("opening.moved-off", ["account"]);

    // A start whose model its agent refused (or answered with another) went on under the walk's next answer, which says the agent and
    // the model; the account is never named, since it is not at fault.
    public static readonly NoteCode OpeningMovedOffModel = new("opening.moved-off-model", ["agent", "model"]);

    // ——— A resumed conversation's first line (<see cref="Continuations.Opening"/>): the adapter it resumed on is a fact, and a version
    // that moved is a part riding inside it.
    public static readonly NoteCode ResumedOpening = new("resumed.opening", ["door", "why"]) { Why = NoteCodes.Resumed };
    public static readonly NoteCode ResumedMoved = new("resumed.moved", ["now", "then"]);
    public static readonly NoteCode ResumedNative = new("resumed.native", ["door"]);

    // The native door going on with Daoris's own words alone: never "what you said".
    public static readonly NoteCode ResumedNativeDaoris = new("resumed.native-daoris", ["door"]);
}
