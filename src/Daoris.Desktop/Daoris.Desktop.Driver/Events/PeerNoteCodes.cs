namespace Daoris.Driver;

/// <summary>The codes of a session's dealings with another: a partner, a helper, a hand-over, a second opinion, an intake, a brainstorm voice. Part of the catalogue <see cref="NoteCodes"/> introduces.</summary>
public static class PeerNoteCodes
{
    // ——— An agent's ask to hand its session over: on its session, waiting for the person's word with the agent it would go on with,
    // going with that agent at its turn's end, or refused and why. Facts only: never an account's name or a path.
    public static readonly NoteCode HandOverAsked = new("handover.asked", ["agent"]);
    public static readonly NoteCode HandOverTaking = new("handover.taking", ["agent"]);
    public static readonly NoteCode HandOverRefused = new("handover.refused", ["why"]) { Why = NoteCodes.HandOver };

    // The person's own ask names the path that made it, and a hand-over let go that Daoris could not take is said as not taken, with
    // its reason and what the session does next, never as refused.
    public static readonly NoteCode HandOverAskedTerminal = new("handover.asked-terminal", ["agent"]);
    public static readonly NoteCode HandOverAskedWindow = new("handover.asked-window", ["agent"]);
    public static readonly NoteCode HandOverAskedSteward = new("handover.asked-steward", ["agent"]);
    public static readonly NoteCode HandOverNotTaken = new("handover.not-taken", ["agent", "why"]) { Why = NoteCodes.HandOver };
    public static readonly NoteCode HandOverNotTakenEnds = new("handover.not-taken-ends", ["agent", "why"]) { Why = NoteCodes.HandOver };

    // ——— A helper: on the asker, a helper asked, answered or refused, with the agent and why; on the helper, whose session it helps.
    // Facts only: never an account's name or a path.
    public static readonly NoteCode HelperAsked = new("helper.asked", ["agent", "session"]);
    public static readonly NoteCode HelperAnswered = new("helper.answered", ["agent", "session"]);
    public static readonly NoteCode HelperAnsweredLastWords = new("helper.answered-last-words", ["agent", "session"]);
    public static readonly NoteCode HelperRefused = new("helper.refused", ["why"]) { Why = NoteCodes.Helper };
    public static readonly NoteCode HelperRefusedBy = new("helper.refused-by", ["agent", "why"]) { Why = NoteCodes.Helper };
    public static readonly NoteCode HelperHelping = new("helper.helping", ["session"]);

    // On the asker, at the person's own ask of a helper whose answer comes to it: their word on the line, which the answers-alone count
    // reads as one, so the answer that follows is the first alone.
    public static readonly NoteCode HelperPersonAsked = new("helper.person-asked", []);

    // On the asker, a helper an account's limit cut off and that it no longer waits on, with when that account is ready again, or
    // with none where no cool-off this machine holds names it.
    public static readonly NoteCode HelperLimited = new("helper.limited", ["agent", "until"]);
    public static readonly NoteCode HelperLimitedNoReset = new("helper.limited-no-reset", ["agent"]);

    // ——— A partner's piece: on the lead, a piece asked of an agent, started, answered, refused, ended by a stop, its minutes or a limit
    // with its work kept, uncertain, its files beyond its paths and its work held; on the partner, whose session it works for. Notes,
    // never words: none reaches a prompt or resets the answers-alone count. Facts only: never an account's name or a path.
    public static readonly NoteCode PartnerAsked = new("partner.asked", ["agent"]);
    public static readonly NoteCode PartnerStarted = new("partner.started", ["agent", "session"]);
    public static readonly NoteCode PartnerAnswered = new("partner.answered", ["agent", "session"]);
    public static readonly NoteCode PartnerRefused = new("partner.refused", ["why"]) { Why = NoteCodes.Partner };
    public static readonly NoteCode PartnerStopped = new("partner.stopped", ["agent"]);
    public static readonly NoteCode PartnerOutOfTime = new("partner.out-of-time", ["agent"]);
    public static readonly NoteCode PartnerLimited = new("partner.limited", ["agent"]);
    public static readonly NoteCode PartnerUncertain = new("partner.uncertain", ["why"]) { Why = NoteCodes.Partner };
    public static readonly NoteCode PartnerBeyond = new("partner.beyond", ["count"]);
    public static readonly NoteCode PartnerKept = new("partner.kept", ["why"]) { Why = NoteCodes.Partner };
    public static readonly NoteCode PartnerWorking = new("partner.working", ["session"]);

    // ——— An intake.
    public static readonly NoteCode IntakeAskDeleted = new("intake.ask-deleted", []);
    public static readonly NoteCode IntakePublished = new("intake.published", ["quests", "ask"]);
    public static readonly NoteCode IntakeAskClosed = new("intake.ask-closed", ["ask"]);
    public static readonly NoteCode IntakeAskAnswered = new("intake.ask-answered", ["ask", "quests"]);
    public static readonly NoteCode IntakeTurnFailed = new("intake.turn-failed", ["ask"]);
    public static readonly NoteCode IntakeAsks = new("intake.asks", ["ask"]);
    public static readonly NoteCode IntakeExit = new("intake.exit", ["ask", "exit"]);
    public static readonly NoteCode IntakePersonAnswered = new("intake.person-answered", ["ask", "quests"]);
    public static readonly NoteCode IntakePersonClosed = new("intake.person-closed", ["ask"]);
    public static readonly NoteCode IntakePersonDeleted = new("intake.person-deleted", ["ask"]);

    // ——— Ask Daoris's start, where the machine's reading for its room did not answer within its bound: started on the room as last
    // written, said in its conversation; or, with no room written before, ended saying so.
    public static readonly NoteCode HelpRoomAsWritten = new("help.room-as-written", ["seconds"]);
    public static readonly NoteCode HelpRoomUnread = new("help.room-unread", ["seconds"]);

    // ——— Ask Daoris's start on the facts the judges keep: started at once, on a room that says what is not read yet, or on the room
    // as last written where no fact has been judged.
    public static readonly NoteCode HelpRoomNotReadYet = new("help.room-not-read-yet", []);

    // ——— A second opinion's answers as the working session's turn ended (<see cref="OpinionAnswers.Told"/>): whose findings and how
    // many, then how many each way, each only above nought; a count the page says "(N in all)".
    public static readonly NoteCode OpinionAnswersLead = new("opinion.answers", ["who", "count"]);
    public static readonly NoteCode OpinionFixed = new("opinion.fixed", ["count"]);
    public static readonly NoteCode OpinionUnproven = new("opinion.unproven", ["count"]);
    public static readonly NoteCode OpinionRejected = new("opinion.rejected", ["count"]);
    public static readonly NoteCode OpinionUnresolved = new("opinion.unresolved", ["count"]);
    public static readonly NoteCode OpinionUnanswered = new("opinion.unanswered", ["count"]);
    public static readonly NoteCode OpinionDisputed = new("opinion.disputed", ["count"]);

    // ——— How the reviewer's own session ended (<see cref="OpinionAnswers.Concluded"/>): its turn failed (the failure follows as the
    // program's words), it read the work and ended, or it exited otherwise. The opinion's id is a short id.
    public static readonly NoteCode OpinionReviewerFailed = new("opinion.reviewer-failed", ["opinion"]);
    public static readonly NoteCode OpinionReviewerRead = new("opinion.reviewer-read", ["opinion"]);
    public static readonly NoteCode OpinionReviewerExited = new("opinion.reviewer-exited", ["opinion", "exit"]);

    // ——— A pass that is no driven session or conversation, ended past its transient bound: its provider keeps failing through the tries it makes, or until its own minutes ran out, never as timed out. Each is
    // followed by the provider's words.
    /// <summary>A helper settled refused for its provider (<see cref="HelperWhy.Transient"/>), said on the asker.</summary>
    public static readonly NoteCode HelperTransient = new("helper.transient", ["agent", "count"]);

    /// <summary>A second opinion's pass failed for its provider: its gate is unavailable, and the person's <i>Try again</i> asks it again.</summary>
    public static readonly NoteCode OpinionTransient = new("opinion.transient", ["agent", "count"]);

    /// <summary>An intake ended for its provider: its ask is unbound once, so the next look picks it again with a fresh intake.</summary>
    public static readonly NoteCode IntakeTransient = new("intake.transient", ["agent", "count"]);

    /// <summary>A partner's piece ended for its provider, its work kept; its lead is told.</summary>
    public static readonly NoteCode PartnerTransient = new("partner.transient", ["agent"]);

    /// <summary>A brainstorm voice dropped for its provider within its minutes.</summary>
    public static readonly NoteCode VoiceTransient = new("voice.transient", ["agent", "count"]);
}
