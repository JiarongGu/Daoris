namespace Daoris.Driver;

/// <summary>The codes of a session's end, its stop and its watch: how a run concluded. Part of the catalogue <see cref="NoteCodes"/> introduces.</summary>
public static class EndNoteCodes
{
    // ——— A session's end.
    public static readonly NoteCode EndedAwaits = new("ended.awaits", ["awaits"]);
    public static readonly NoteCode EndedExit = new("ended.exit", ["exit"]);
    public static readonly NoteCode EndedTurnFailedTaken = new("ended.turn-failed-taken", []);
    public static readonly NoteCode EndedTurnFailedOpen = new("ended.turn-failed-open", []);

    /// <summary>
    /// Its model's provider refused a call and the turn ended on it: a failed run and a strike, never a park, followed by the
    /// provider's words with their request id.
    /// </summary>
    public static readonly NoteCode EndedProviderRefused = new("ended.provider-refused", []);

    /// <summary>
    /// Its provider kept failing after every try a transient episode made, with the tries made and the minutes actually waited,
    /// followed by the provider's words: failed, no strike, no cool-off, its quest held for the person's word.
    /// </summary>
    public static readonly NoteCode EndedTransient = new("ended.transient", ["count", "minutes"]);

    /// <summary>
    /// Said once at a transient episode's first failure, with the schedule's tries and minutes as <i>up to</i> and when the
    /// first try goes, followed by the provider's words. No line per try.
    /// </summary>
    public static readonly NoteCode AgentTransientRetrying = new("agent.transient-retrying", ["agent", "count", "minutes", "at"]);

    /// <summary>
    /// Its agent did not offer, or refused, the model its actor asked at the open, before any prompt: failed, no strike, its quest
    /// planned again. Names no account, since the note travels.
    /// </summary>
    public static readonly NoteCode EndedChoiceNotOffered = new("ended.choice-not-offered", ["agent", "model"]);
    public static readonly NoteCode EndedParkedAsked = new("ended.parked-asked", []);
    public static readonly NoteCode EndedParkedTranscript = new("ended.parked-transcript", []);
    public static readonly NoteCode EndedParkedShort = new("ended.parked-short", []);

    /// <summary>
    /// Its agent ended its turn <c>cancelled</c> though nobody stopped it, after the permission it asked for (<c>call</c>, as
    /// the agent titles it) is refused: a park on the person that says it asked them nothing.
    /// </summary>
    public static readonly NoteCode EndedParkedCut = new("ended.parked-cut", ["call"]);

    /// <summary>As <see cref="EndedParkedCut"/>, with no refused call behind the agent's cancel.</summary>
    public static readonly NoteCode EndedParkedCancelled = new("ended.parked-cancelled", []);

    /// <summary>
    /// It ended its turn cleanly on its own open helper ask, so it waits on that helper and not on the person. A twin: the
    /// service's ledger reads this spelling to bring the session back to the person when the helper ends with no answer.
    /// </summary>
    public static readonly NoteCode EndedAwaitsHelper = new("ended.awaits-helper", []);

    /// <summary>
    /// It ended its turn cleanly on its own open <c>check_run</c> so the check waits on the machine, not on a running session: it goes on
    /// in the same conversation with the verdict. A twin of <see cref="EndedAwaitsHelper"/>: the service's ledger reads this spelling to keep its tree.
    /// </summary>
    public static readonly NoteCode EndedAwaitsCheck = new("ended.awaits-check", []);
    public static readonly NoteCode EndedAnsweredUnfinished = new("ended.answered-unfinished", ["awaits", "exit"]);
    public static readonly NoteCode EndedCarriedUnfinished = new("ended.carried-unfinished", ["exit"]);
    public static readonly NoteCode EndedDone = new("ended.done", []);
    public static readonly NoteCode EndedDoneLeft = new("ended.done-left", ["left"]);

    // A closure graph's quest is never done before the graph's end, so a session ends with the quest still taken, at its submission.
    public static readonly NoteCode EndedSubmitted = new("ended.submitted", [], Key: "statusHint.Submitted");
    public static readonly NoteCode EndedDeclined = new("ended.declined", []);
    public static readonly NoteCode EndedStoodDown = new("ended.stood-down", []);

    /// <summary>
    /// It stopped with its quest still taken, held because the person reopened the work it grew from (<c>quest</c>). A good
    /// ending: the quest goes on in the same tree. A twin: the service's ledger reads this spelling to open that carry-on.
    /// </summary>
    public static readonly NoteCode EndedSourceReopened = new("ended.source-reopened", ["quest"]);

    /// <summary>
    /// On the native door its turn ended while its own background work ran (<c>works</c>, as its agent named them), and its
    /// harness ends such work with the turn, so it is cut off: carried on in the same tree.
    /// </summary>
    public static readonly NoteCode EndedBackgroundCut = new("ended.background-cut", ["works"]);
    public static readonly NoteCode EndedTakenExit = new("ended.taken-exit", ["exit"]);
    public static readonly NoteCode EndedUntouched = new("ended.untouched", []);
    public static readonly NoteCode EndedUntouchedExit = new("ended.untouched-exit", ["exit"]);
    public static readonly NoteCode EndedWentOn = new("ended.went-on", []);
    public static readonly NoteCode EndedWentOnExit = new("ended.went-on-exit", ["exit"]);

    // A closed quest's session whose turn failed going on: the door's reason follows, in the agent's words.
    public static readonly NoteCode EndedWentOnFailed = new("ended.went-on-failed", []);

    // The person's words a failed turn carried wait to go again, or, failing twice, wait for the person.
    public static readonly NoteCode EndedWordsWait = new("ended.words-wait", []);
    public static readonly NoteCode EndedWordsWaitYou = new("ended.words-wait-you", []);

    // The person's words a turn an account's limit refused carried wait for the limit to lift, never failed twice.
    public static readonly NoteCode EndedLimitWordsWait = new("ended.limit-words-wait", []);

    // Daoris ended a run at its turn's end because its repository's permission prompts changed (`from`, `to`); the words it held go on
    // in the same conversation under the new setup.
    public static readonly NoteCode EndedSetupChanged = new("ended.setup-changed", ["from", "to"]);
    public static readonly NoteCode EndedWordsSetup = new("ended.words-setup", []);
    public static readonly NoteCode EndedTimeout = new("ended.timeout", ["minutes"]);

    // Its agent's process exited but something it started holds its output, so Daoris ends what is left and the run concludes by its exit.
    public static readonly NoteCode EndedGone = new("ended.gone", []);

    // The watcher's own end of a quiet run (`minutes` unheard).
    public static readonly NoteCode EndedQuiet = new("ended.quiet", ["minutes"]);

    // The person ended a quiet run to carry it on.
    public static readonly NoteCode EndedQuietPressed = new("ended.quiet-pressed", ["minutes"]);
    public static readonly NoteCode WatchQuiet = new("watch.quiet", ["minutes"]);

    // The same mark naming what it does: the call its agent is inside, or the background work (`works`, a list) it waits on that shows nothing.
    public static readonly NoteCode WatchQuietCall = new("watch.quiet-call", ["minutes", "call"]);
    public static readonly NoteCode WatchQuietWork = new("watch.quiet-work", ["minutes", "works"]);
    public static readonly NoteCode WatchLeft = new("watch.left", []);
    public static readonly NoteCode EndedStopped = new("ended.stopped", []);

    // The steward's own stop, a behavioural sibling of the person's.
    public static readonly NoteCode EndedStoppedSteward = new("ended.stopped-steward", []);
    public static readonly NoteCode EndedLostClaim = new("ended.lost-claim", []);

    // A take lost to the person's reopen of the work its step grew from, not to another machine's take.
    public static readonly NoteCode EndedLostClaimReopened = new("ended.lost-claim-reopened", []);
    public static readonly NoteCode EndedResolveAsk = new("ended.resolve-ask", []);

    // A quest's session stopped to go on with another agent: the agent it went to, never an account or a path.
    public static readonly NoteCode EndedHandedOver = new("ended.handed-over", ["agent"]);
    public static readonly NoteCode EndedDriverClosed = new("ended.driver-closed", []);

    // ——— A stop.
    public static readonly NoteCode StoppedPausedAsk = new("stopped.paused-ask", ["ask"]);
    public static readonly NoteCode StoppedPausedQuest = new("stopped.paused-quest", ["quest"]);

    // The steward's pause of a quest's work, a sibling of the person's above: its own code, so no record says the person paused it.
    public static readonly NoteCode StoppedPausedQuestSteward = new("stopped.paused-quest-steward", ["quest"]);
    public static readonly NoteCode StoppedAbandonedAsk =new("stopped.abandoned-ask", ["ask"]);
    public static readonly NoteCode StoppedAbandonedQuest = new("stopped.abandoned-quest", ["quest"]);
    public static readonly NoteCode StoppedCheckpointFinished = new("stopped.checkpoint-finished", []);
    public static readonly NoteCode StoppedCheckpointStopped = new("stopped.checkpoint-stopped", []);

    // The steward's finish and stop of a parked session, siblings of the person's two above.
    public static readonly NoteCode StoppedCheckpointFinishedSteward = new("stopped.checkpoint-finished-steward", []);
    public static readonly NoteCode StoppedCheckpointStoppedSteward = new("stopped.checkpoint-stopped-steward", []);
    public static readonly NoteCode StoppedCheckpointMoved = new("stopped.checkpoint-moved", []);
    public static readonly NoteCode StoppedOrphan = new("stopped.orphan", []);

    public static readonly NoteCode UpdateClosed = new("update.closed", ["build"]);

    // ——— A permission the rules refused: the lead and the closing sentence are Daoris's facts; the call's title between them is the
    // agent's words, which may hold a path.
    public static readonly NoteCode RefusedLead = new("refused.lead", []);
    public static readonly NoteCode RefusedGoverns = new("refused.governs", []);

    // ——— A step's session's end, by the step's answer.
    public static readonly NoteCode StepReached = new("step.reached", ["step", "message"]);
    public static readonly NoteCode StepFailed = new("step.failed", ["step", "code", "message"]);
}
