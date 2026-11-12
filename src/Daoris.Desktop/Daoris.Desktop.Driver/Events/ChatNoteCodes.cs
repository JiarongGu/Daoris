namespace Daoris.Driver;

/// <summary>The codes of a conversation's lines: landings, words, doors, reviews, background work and the browser. Part of the catalogue <see cref="NoteCodes"/> introduces.</summary>
public static class ChatNoteCodes
{
    /// <summary>
    /// A conversation's turn ended <c>error</c> because its provider kept failing after every try, followed by the provider's
    /// words. The conversation stays, and the person's next word is a turn of it.
    /// </summary>
    public static readonly NoteCode ChatTransientStopped = new("chat.transient-stopped", ["agent"]);

    // ——— A conversation handed to another agent: the handing one's end says which agent and conversation it went to, the receiving one's first
    // line where it came from and why. Facts only: never an account's name or a path.
    public static readonly NoteCode ChatHandedOver = new("chat.handed-over", ["agent", "session"]);
    public static readonly NoteCode ChatTakenOver = new("chat.taken-over", ["agent", "session", "why"]) { Why = NoteCodes.Handed };
    public static readonly NoteCode ChatBriefingUnsaid = new("chat.briefing-unsaid", []);

    // ——— What the outgoing part left running as a hand-over or a move ended it: ended and waited for before the next part
    // started in its tree, or still running at the bound, which the next part then started beside.
    public static readonly NoteCode ChatLeftEnded = new("chat.left-ended", ["count"]);
    public static readonly NoteCode ChatLeftRunning = new("chat.left-running", ["count", "seconds"]);

    // ——— A conversation moved to another account of its agent by the usage curve: the leaving one's last line says where it went and how
    // far over its curve its account stood, the arriving one's first where it came from. They name the accounts, since a conversation's
    // record stays on this machine.
    public static readonly NoteCode ChatMoved = new("chat.moved", ["to", "from", "window", "used", "curve"]);
    public static readonly NoteCode ChatMovedFrom = new("chat.moved-from", ["from", "window", "used", "curve"]);

    // ——— A conversation whose turn its account's limit refused, whose move to the next account did not open: it ended, and the words
    // its refused turns held stay in it. The start's own words follow in the English alone.
    public static readonly NoteCode ChatLimitNotMoved = new("chat.limit-not-moved", []);

    // ——— A conversation its account's limit cut off with no other account to go on on: it waits, its words with it, and goes on by
    // itself when an account is ready; the person's *Stop waiting* ends the wait. Accounts by their ids, since the record stays on
    // this machine.
    public static readonly NoteCode ChatLimitWaits = new("chat.limit-waits", ["owner", "until", "why"]) { Why = NoteCodes.Cooling };
    public static readonly NoteCode ChatLimitStoppedWaiting = new("chat.limit-stopped-waiting", []);

    // ——— Why a conversation whose agent has no account left does not go on with its actor's next choice on another agent, said before
    // its wait: it runs on the agent the person picked, or no choice on another agent can run now. The walk's own words follow in
    // the English alone.
    public static readonly NoteCode ChatLimitPicked = new("chat.limit-picked", ["owner"]);
    public static readonly NoteCode ChatLimitNoOther = new("chat.limit-no-other", ["owner"]);
    public static readonly NoteCode ChatClosed = new("chat.closed", []);
    public static readonly NoteCode ChatEndedByPerson = new("chat.ended-by-person", []);
    public static readonly NoteCode ChatEnded = new("chat.ended", []);
    public static readonly NoteCode ChatCancelled = new("chat.cancelled", []);
    public static readonly NoteCode ChatNotKept = new("chat.not-kept", []);
    public static readonly NoteCode ChatRestarted = new("chat.restarted", []);
    public static readonly NoteCode ChatCut = new("chat.cut", []);

    // ——— An opening past its bound (ChatOpening): Ask Daoris's and any other conversation's, and the plugins' servers left out.
    public static readonly NoteCode HelpNotUp = new("chat.help-not-up", ["what", "seconds"]);
    public static readonly NoteCode ChatNotUp = new("chat.not-up", ["what", "seconds"]);
    public static readonly NoteCode ServersLeftOut = new("chat.servers-left-out", ["seconds"]);

    // ——— A landing at the quest's done, under a rule that accepts automatically: said in the conversation's record.
    public static readonly NoteCode LandingAccepted = new("landing.accepted", ["branch"]);

    // Notes of the retired plugin push: written once, so the page still words them; nothing writes them.
    public static readonly NoteCode LandingUnready = new("landing.plugin-unready", ["branch", "plugin"]);
    public static readonly NoteCode LandingPluginFailed = new("landing.plugin-failed", ["branch", "plugin"]);
    public static readonly NoteCode LandingPushHeld = new("landing.push-held", ["branch", "plugin"]);
    public static readonly NoteCode LandingPushGated = new("landing.push-gated", ["branch", "plugin"]);
    public static readonly NoteCode LandingNothing = new("landing.nothing", []);
    public static readonly NoteCode LandingHeld = new("landing.held", []);
    public static readonly NoteCode LandingUncommitted = new("landing.uncommitted", ["paths"]);
    public static readonly NoteCode LandingExists = new("landing.exists", ["branch"]);
    public static readonly NoteCode LandingRefused = new("landing.refused", []);

    // Replaying its work onto the line conflicts, and its session is told to rebase in its own tree; a refusal it cannot tell keeps
    // `landing.refused`.
    public static readonly NoteCode LandingRebase = new("landing.rebase", []);

    public static readonly NoteCode LandingRootDirty = new("landing.root-dirty", []);

    // An automatic landing that needs nothing of the person: the line holds everything already, or the merge waits in its line's integration.
    public static readonly NoteCode LandingLineHolds = new("landing.line-holds", []);
    public static readonly NoteCode LandingQueued = new("landing.queued", []);

    // The person's Accept on a session whose tree holds uncommitted work or whose replay conflicts: the session is carried on in
    // Daoris's words, and the yes stays on it until its tree is clean. Its own codes, since the work is accepted, not refused.
    public static readonly NoteCode LandingCarriedDirty = new("landing.carried-dirty", ["paths"]);
    public static readonly NoteCode LandingCarriedConflict = new("landing.carried-conflict", []);

    // Carried on twice for one Accept and still not clean: it waits in Review's ready group, the yes taken back.
    public static readonly NoteCode LandingCarryBound = new("landing.carry-bound", []);

    // The work an Accept carried on landed once its tree is clean: the person's press, not the rule's switch, accepted it.
    public static readonly NoteCode LandingAcceptedCarried = new("landing.accepted-carried", ["branch"]);

    // Held by the review's gate, so a look's wait is never said as a refused landing; a note written before it keeps `landing.refused`.
    public static readonly NoteCode LandingUnreviewed = new("landing.unreviewed", []);

    // Held by the second opinion's gate; a note written before it keeps `landing.refused`.
    public static readonly NoteCode LandingOpinion = new("landing.opinion", []);
    public static readonly NoteCode LandingNoTree = new("landing.no-tree", []);
    public static readonly NoteCode LandingNotDone = new("landing.not-done", []);

    // The access setup says `landing: rule` and the rule merges (`access` is where the setup is set: repository, workspace or
    // machine); a merge still waits for Accept.
    public static readonly NoteCode LandingMerges = new("landing.merges", ["access"]);

    // ——— A chat run's binding, said once in the conversation that grew its tree: the named workflow and the repository or workspace
    // that chose it, the one bound with why it cannot land the work, or a bind the home could not keep. Under Current nothing is said.
    public static readonly NoteCode ChatWorkflow = new("chat.workflow", ["workflow", "version", "owner"]);
    public static readonly NoteCode ChatWorkflowUnread = new("chat.workflow-unread", ["workflow", "owner", "problem"]);
    public static readonly NoteCode ChatWorkflowFailed = new("chat.workflow-failed", ["problem"]);

    // ——— A press's note and a chat's settings: the lead is Daoris's, what follows a landing's or a preface's lead is a program's words
    // (git's, a plugin's, Daoris's one-copy English to the agent). The option and its value are the agent's own names, never a path.
    public static readonly NoteCode LandingPersonAccepted = new("landing.person-accepted", []);
    public static readonly NoteCode LandingPersonHanded = new("landing.person-handed", []);

    // A part the person left out of its entry's Accept: the branch its line of work landed on, or the line it merged into. Its review
    // reads it back (<c>LandingNotes.LeftOutOf</c>) and leads with Discard.
    public static readonly NoteCode LandingLeftOut = new("landing.left-out", ["branch"]);

    // A member whose work a combined Accept brought into its head's landing: the branch it went onto, or the line it merged into, and
    // the operation. The conversation's own landing then reads it as brought in, never landed.
    public static readonly NoteCode LandingBroughtInto = new("landing.brought-into", ["branch", "operation"]);
    public static readonly NoteCode LandingBroughtIntoLine = new("landing.brought-into-line", ["branch", "operation"]);
    public static readonly NoteCode ChatOptionSet = new("chat.option-set", ["option", "chosen"]);
    public static readonly NoteCode ChatToldWhere = new("chat.told-where", []);

    // ——— Words that did not reach an agent, or were not taken (<see cref="WordsNotes"/>): the lead is Daoris's; what follows a refusal
    // is the agent's words, and what follows a failed take is a program's or the service's (it may hold a path). The count and why a
    // send never reached are facts, `why` one of `Unreached`'s.
    public static readonly NoteCode WordsRefused = new("words.refused", []);
    public static readonly NoteCode WordsUnreached = new("words.unreached", ["count", "why"]) { Why = NoteCodes.Unreached };
    public static readonly NoteCode WordsNotTaken = new("words.not-taken", []);

    // The chat runner's and the modules' sites: words a conversation never got, a send that found the agent gone, a start's words not
    // taken off their session (the session id a fact), and held words the record would not keep.
    public static readonly NoteCode WordsNotReached = new("words.not-reached", []);
    public static readonly NoteCode WordsNotSent = new("words.not-sent", []);
    public static readonly NoteCode WordsStartNotTaken = new("words.start-not-taken", ["session"]);
    public static readonly NoteCode WordsNotKept = new("words.not-kept", []);

    public static readonly NoteCode WordsSentNowInTurn = new("words.sent-now-in-turn", []);
    public static readonly NoteCode WordsSentNowNextTurn = new("words.sent-now-next-turn", []);

    // ——— A server that drives Daoris's own browser, withheld: the servers are plugin-declared names; why it is not handed is a fact
    // of the host's, or an exception's words riding after the lead.
    public static readonly NoteCode BrowserWithheld = new("browser.withheld", ["servers"]);
    public static readonly NoteCode BrowserNoHost = new("browser.no-host", []);
    public static readonly NoteCode BrowserNoneUp = new("browser.none-up", []);

    // What a chat's agent is told of the browser it is handed and of the place it works in: the lead by its code, then Daoris's English.
    public static readonly NoteCode ChatToldBrowser = new("chat.told-browser", []);
    public static readonly NoteCode ChatToldPlace = new("chat.told-place", []);

    // The repository's process a chat in a tree of its own is told at its start: its commit rule's mode and where it is set are
    // facts, the words after the lead Daoris's one-copy English.
    public static readonly NoteCode ChatToldProcess = new("chat.told-process", ["mode", "source"]);

    // The standing block a conversation's agent is told with its first message: the lead by its code, the block after it Daoris's
    // one-copy English.
    public static readonly NoteCode ChatToldBlock = new("chat.told-block", []);

    // The workflow draft open in the editor a chat's agent is told beside the person's words: the lead by its code, then what the
    // agent is handed, the draft's JSON last and whole, which the draft door finds it by again.
    public static readonly NoteCode ChatToldDraft = new("chat.told-draft", []);

    // ——— A session's own background work (<see cref="BackgroundNotes"/>): the work's names are the agent's own descriptions of it,
    // said as code; a state is the harness's own word for how it ended, said as it came. Why a wait could not be read is a program's
    // words, which may hold a path.
    public static readonly NoteCode BackgroundUnread = new("background.unread", []);
    public static readonly NoteCode BackgroundWaits = new("background.waits", ["works"]);
    public static readonly NoteCode BackgroundWorkEnded = new("background.work-ended", ["work", "state"]);
    public static readonly NoteCode BackgroundGoesOn = new("background.goes-on", []);

    // The quest, read again while the session waits, stops waiting on the work, so the session ends and the work still running ends
    // with it.
    public static readonly NoteCode BackgroundWaitOver = new("background.wait-over", ["works"]);

    // The quest stops waiting, and the session ends rather than wait for what its agent scheduled for itself: a turn its harness
    // started that said nothing, or the turn its harness is about to wake.
    public static readonly NoteCode BackgroundTurnLeft = new("background.turn-left", []);
    public static readonly NoteCode BackgroundQuestClosed = new("background.quest-closed", []);

    // A turn that ended with its quest done and its tree uncommitted is carried on in Daoris's words, and the bound says when the
    // session ends; the person's Accept is no part of either (`landing.carried-dirty` is that one's).
    public static readonly NoteCode BackgroundCarriedOn = new("background.carried-on", ["paths"]);
    public static readonly NoteCode BackgroundCarryBound = new("background.carry-bound", []);

    // ——— A conversation's door that failed (<see cref="DoorNotes"/>): the protocol session that could not open or failed, and a turn
    // that could not be taken, each a lead with a program's message after it (it may hold a path). A resume the agent refused is
    // Daoris's own sentence, so `why` is the `Continue` family's, then the agent's words where it said any; the door adds that the
    // answer is carried on in a new session.
    public static readonly NoteCode DoorNotOpen = new("door.not-open", []);
    public static readonly NoteCode DoorTurnNotTaken = new("door.turn-not-taken", []);
    public static readonly NoteCode DoorFailed = new("door.failed", []);
    public static readonly NoteCode DoorResumeRefused = new("door.resume-refused", ["why"]) { Why = NoteCodes.Continue };
    public static readonly NoteCode DoorAnswerCarriedOn = new("door.answer-carried-on", []);

    // ——— A review set-up's record (<see cref="ReviewNotes"/>): the environment's name and the repository a fact; the commit is the
    // 8-character short id. The service's message and the served sentence are a program's words, which may hold a path.
    public static readonly NoteCode ReviewNoRule = new("review.no-rule", ["named", "to"]);
    public static readonly NoteCode ReviewNoCommit = new("review.no-commit", []);
    public static readonly NoteCode ReviewPosted = new("review.posted", ["commit", "named"]);
    public static readonly NoteCode ReviewNotPosted = new("review.not-posted", []);
    public static readonly NoteCode ReviewPostsNext = new("review.posts-next", []);
}
