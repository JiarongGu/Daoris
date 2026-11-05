namespace Daoris.Driver;

/// <summary>The codes of words held, waiting or limited. Part of the catalogue <see cref="NoteCodes"/> introduces.</summary>
public static class HeldNoteCodes
{
    // An attempt refused by the same kind of limit (`hit`) just after its account's cool-off ended.
    public static readonly NoteCode LimitNotLifted = new("limit.not-lifted", ["hit"]);

    // ——— The answers-alone bound: a third answer in a row with nobody between held for the person, by the agent that answered; their
    // *Go on with this answer*, which counts as their word; their *Not now*, which keeps the answer for them.
    public static readonly NoteCode AnswersHeld = new("answers.held", ["agent"]);
    public static readonly NoteCode AnswersPassed = new("answers.passed", []);
    public static readonly NoteCode AnswersKept = new("answers.kept", []);

    // ——— The limit's wake: its own account ready again at the margin-added `until`, it went on in its own conversation; or its own
    // conversation could not be resumed, by the refusal's code, and it went on briefed.
    public static readonly NoteCode LimitResumed = new("limit.resumed", ["until"]);
    public static readonly NoteCode LimitResumeRefused = new("limit.resume-refused", ["why"]) { Why = NoteCodes.Continue };

    // ——— A permission held for the person under a repository's asking permission prompts: the lead is Daoris's and the call's title
    // after it the agent's words; a refusal leads with `refused.lead`, and its close says who or what refused it: the person, the
    // turn's cancel, the session's end, or the bound, whose minutes are a fact.
    public static readonly NoteCode AskingHeld = new("asking.held", []);
    public static readonly NoteCode AskingAllowed = new("asking.allowed", []);
    public static readonly NoteCode AskingDenied = new("asking.denied", []);
    public static readonly NoteCode AskingCancelled = new("asking.cancelled", []);
    public static readonly NoteCode AskingEnded = new("asking.ended", []);
    public static readonly NoteCode AskingBound = new("asking.bound", ["minutes"]);

    // ——— A conversation's words waiting for its own account's reset (<see cref="ResumeWords"/>): the account by its owner and its id,
    // never by the name the person gave it, since a conversation's events never leave the machine; until when a moment, why a
    // cool-off's reason. The tool's own sign-in has a line of its own and its refresh another. Each door out of the wait is a line, the
    // session its command names a value; the command itself is the catalogue's.
    public static readonly NoteCode WaitsAccount = new("waits.account", ["owner", "account", "until", "why"]) { Why = NoteCodes.Cooling };
    public static readonly NoteCode WaitsOwn = new("waits.own", ["owner", "until", "why"]) { Why = NoteCodes.Cooling };
    public static readonly NoteCode WaitsReadAgain = new("waits.read-again", []);
    public static readonly NoteCode WaitsDoorNewSession = new("waits.door-new-session", ["session"]);
    public static readonly NoteCode WaitsDoorChat = new("waits.door-chat", ["session"]);
    public static readonly NoteCode WaitsDoorHelp = new("waits.door-help", []);

    // ——— A conversation's words held for another reason (<see cref="HeldWords"/>): a reason of Daoris's own sentence is one line, its
    // repository or conversation a value; a program's message (driver.json unread, the roster's refusal, the ledger's) rides as its
    // words between `lead` (or the reason `config-unread`) and `tail`, never a value, since it may hold a path.
    public static readonly NoteCode HeldNoCheckout = new("held.no-checkout", ["repository"]);
    public static readonly NoteCode HeldReplaying = new("held.replaying", ["repository"]);

    // What else holds the repository alone while a start waits for it; its branches brought up to date stay `replaying`.
    public static readonly NoteCode HeldTidying = new("held.tidying", ["repository"]);
    public static readonly NoteCode HeldPromoting = new("held.promoting", ["repository"]);
    public static readonly NoteCode HeldWriting = new("held.writing", ["repository"]);
    public static readonly NoteCode HeldAnswering = new("held.answering", ["session"]);
    public static readonly NoteCode HeldConfigUnread = new("held.config-unread", []);
    public static readonly NoteCode HeldLead = new("held.lead", []);
    public static readonly NoteCode HeldTail = new("held.tail", []);

    // The agent its words would go on with is no longer one this machine has; the hand-over is the way on.
    public static readonly NoteCode HeldAgentGone = new("held.agent-gone", ["agent", "session"]);

    // ——— The roster's refusal as a held note's reason (<see cref="HeldWords.Refused"/>): an account by its owner and its id, never the
    // name the person gave it (the page names it), until when a moment, why a cool-off's reason; the tool's own sign-in is a line of
    // its own, its refresh `waits.read-again`. `profile` is the id as a shell spells it in the sign-in command.
    public static readonly NoteCode HeldCoolingAccount = new("held.cooling-account", ["owner", "account", "until", "why"]) { Why = NoteCodes.Cooling };
    public static readonly NoteCode HeldCoolingOwn = new("held.cooling-own", ["owner", "until", "why"]) { Why = NoteCodes.Cooling };

    // A line a limit cut off, every account it may run on at its limit, goes on by itself once the earliest is ready.
    public static readonly NoteCode HeldLimitWaits = new("held.limit-waits", ["owner", "account", "until"]) { Why = NoteCodes.Cooling };
    public static readonly NoteCode HeldLimitWaitsOwn = new("held.limit-waits-own", ["owner", "until"]) { Why = NoteCodes.Cooling };

    // The same wait where some accounts need the person's sign-in, named apart from those cooling; `accounts` a list.
    public static readonly NoteCode HeldLimitWaitsSignIn = new("held.limit-waits-sign-in", ["owner", "account", "until", "accounts"]) { Why = NoteCodes.Cooling };
    public static readonly NoteCode HeldSignedOut = new("held.signed-out", ["owner", "account", "profile"]);

    // Every account the start could run on was locked past its patience; the accounts by id, a list.
    public static readonly NoteCode HeldLocked = new("held.locked", ["owner", "accounts"]);

    // No Daoris account of the agent's is signed in, and its own home is never a start's.
    public static readonly NoteCode HeldNoAccount = new("held.no-account", ["owner"]);
    public static readonly NoteCode HeldNotInstalled = new("held.not-installed", ["agent"]);
    public static readonly NoteCode HeldPinNotInstalled = new("held.pin-not-installed", ["agent", "version"]);

    // ——— The roster's other refusals. A wait over a list: its lead, a clause for each account (by its id, the page names it), the
    // first reset, then the sign-in that would start it sooner, `logins` each command into an account, its id spelled for any shell. A
    // pick refused adds who is ready, `accounts` a list. An agent with its own tooling names that command as a program's words between
    // `not-installed-own` and `never-installs`, never a value, since it may hold a path. An account its provider refused, and the
    // clauses a wait adds (kept for conversations, and the accounts outside the list with the command that adds the first).
    public static readonly NoteCode HeldWaitEvery = new("held.wait-every", ["agent"]);
    public static readonly NoteCode HeldWaitSome = new("held.wait-some", ["agent"]);
    public static readonly NoteCode HeldClauseCooling = new("held.clause-cooling", ["account", "until"]);
    public static readonly NoteCode HeldClauseRefused = new("held.clause-refused", ["account"]);
    public static readonly NoteCode HeldClauseSignedOut = new("held.clause-signed-out", ["account"]);
    public static readonly NoteCode HeldClauseReady = new("held.clause-ready", ["account"]);
    public static readonly NoteCode HeldWaitFirst = new("held.wait-first", ["account", "until", "why"]) { Why = NoteCodes.Cooling };
    public static readonly NoteCode HeldSignInSooner = new("held.sign-in-sooner", ["logins"]);
    public static readonly NoteCode HeldSignIn = new("held.sign-in", ["logins"]);
    public static readonly NoteCode HeldPickedReady = new("held.picked-ready", ["accounts"]);
    public static readonly NoteCode HeldPickedNone = new("held.picked-none", ["agent"]);
    public static readonly NoteCode HeldRefusedAccount = new("held.refused-account", ["owner", "account"]);
    public static readonly NoteCode HeldRefusedOwn = new("held.refused-own", ["owner"]);
    public static readonly NoteCode HeldSignInRefusedOwn = new("held.sign-in-refused-own", ["owner"]);
    public static readonly NoteCode HeldNotInstalledOwn = new("held.not-installed-own", ["agent"]);
    public static readonly NoteCode HeldNeverInstalls = new("held.never-installs", []);
    public static readonly NoteCode HeldKeptAside = new("held.kept-aside", ["account"]);
    public static readonly NoteCode HeldOutsideMachine = new("held.outside-machine", ["accounts", "first", "door"]);
    public static readonly NoteCode HeldOutsideWorkspace = new("held.outside-workspace", ["workspace", "accounts", "first", "door"]);
}
