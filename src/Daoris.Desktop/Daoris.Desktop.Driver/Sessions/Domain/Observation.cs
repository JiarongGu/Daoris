using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A stop made of a session while its process ran (FLAKE3): the driver's own, for its <paramref name="Reason"/> (a take that
/// lost, D68 §5), or the person's, saying <paramref name="Note"/> where the door that stopped it said more (a pause, PAUSE1b).
/// </summary>
public sealed record SessionStop(Noted? Reason, Noted? Note)
{
    /// <summary>Whose decision the end was (SURF5b): the person's wherever the driver gave no reason of its own.</summary>
    public bool ByPerson => Reason is null;

    /// <summary>What the record says it ended in: stood down for the driver's reason, or stopped as the person's.</summary>
    public SessionConclusion Conclusion => Reason is { } reason
        ? SessionConclusion.Of("stood-down", reason)
        : SessionConclusion.Of("stopped", Note ?? Observation.Stopped);
}

/// <param name="State">The terminal state, in the wire spelling the ledger accepts.</param>
/// <param name="Note">What was observed — the sentence the record keeps.</param>
public sealed record SessionConclusion(string State, string Note)
{
    /// <summary>
    /// The note's parts, each line's code and values beside its English (LANG1a, D142 point 2); null where its writer composed
    /// none, which the record then carries as one <see cref="NoteBy.Before"/> part when a line is added to it.
    /// </summary>
    public IReadOnlyList<NotePart>? Parts { get; init; }

    /// <summary>A conclusion whose note is composed.</summary>
    public static SessionConclusion Of(string state, Noted noted) => new(state, noted.Note) { Parts = noted.Parts };

    /// <summary>The note as composed: its parts, or its English whole.</summary>
    public Noted AsNoted() => Noted.From(Note, Parts);

    /// <summary>The same state, its note followed by the glue and a line (an account's, D125).</summary>
    public SessionConclusion Then(string glue, Noted line) => Of(State, AsNoted().Then(glue, line));

    /// <summary>
    /// The same state, its note without the agent's words that a line will say as facts (AGT3c): a limit's sentence names
    /// the zone of the machine it was said on, and the note travels. Unchanged where the note holds no such words part.
    /// </summary>
    public SessionConclusion Unsaid(string words)
    {
        var noted = AsNoted();
        var unsaid = noted.Unsaid(words, NoteBy.Agent);
        return ReferenceEquals(unsaid, noted) ? this : Of(State, unsaid);
    }
}

/// <summary>
/// A session's end, concluded from the two signals the driver can actually see: the exit code, and
/// the quest's own state (D46 §4). Observed rather than self-reported, because these are the two
/// signals outside work also produces — an in-band status protocol would only describe driven work,
/// and driven work is not supposed to be special.
/// </summary>
public static class Observation
{
    /// <param name="awaitsBefore">What the quest waited on when the session started (D79), or null.</param>
    /// <param name="awaitsAfter">What it waits on now. A NEW question is this session asking and waiting.</param>
    /// <param name="turnFailed">
    /// What the protocol door said when the agent refused the turn itself (ACPEND1), or null. Not a
    /// self-report about the work: the call failed, in the agent's words, and on that door the exit
    /// after stdin closes is 0 whatever happened, so without this a refused turn reads as a clean one.
    /// </param>
    /// <param name="resumed">
    /// Whether this session carried the quest on after its own earlier session — a resume once a
    /// question closed (D79), or a carry-on after a cut-off (D80). Its quest was this machine's take
    /// before it began, so ending with it still taken is not somebody else having it.
    /// </param>
    /// <param name="took">
    /// Whether this session took its quest itself, through its own connector (STANDDOWN2) — what tells
    /// a session holding the quest it took from one that found it taken.
    /// </param>
    /// <param name="lastWords">The agent's own last words, which a park quotes for the person.</param>
    /// <param name="reopenedFrom">
    /// The step before, where the person's reopen of the work this quest grew from holds it as the session ends (FABRIC-CLOSE1c2,
    /// <see cref="QuestView.HeldByReopen"/>); null otherwise.
    /// </param>
    /// <param name="backgroundCut">
    /// The session's own background work its harness ended with its turn, by name (BGWAIT1c, <see cref="HarnessEnding.BackgroundCut"/>):
    /// the native door's; none on any other.
    /// </param>
    /// <param name="submitted">
    /// Whether its quest stands at a closure graph's current submission (FABRIC-CLOSE1h5, <see cref="QuestResults.Submitted"/>): taken,
    /// and its result in, so the session ends as one whose quest reached done does, never parked as though it waited on the person.
    /// </param>
    /// <param name="awaitsHelper">
    /// Whether its agent has a helper ask of its own still open, and its last turn asked the person nothing (HELPERWAIT1,
    /// <c>Driver.WaitsOnOwnHelperAsync</c>): where it would park, it waits on that helper instead.
    /// </param>
    /// <param name="providerRefused">
    /// Whether its last turn ended on its model's provider refusing a call (APIREFUSAL1, <see cref="HarnessEnding.ProviderRefused"/>):
    /// the door's word, so <paramref name="lastWords"/> are the provider's error, quoted as its words and never as a question.
    /// </param>
    public static SessionConclusion Conclude(
        int exitCode, string questStatus, string? awaitsBefore = null, string? awaitsAfter = null,
        string? turnFailed = null, bool resumed = false, bool took = false, string? lastWords = null,
        string? reopenedFrom = null, IReadOnlyList<string>? backgroundCut = null, bool submitted = false,
        bool awaitsHelper = false, bool providerRefused = false) => questStatus switch
    {
        // Ask and wait (D79): the session published a question to another repository and waits on it.
        // Its quest stays taken for the same tree to resume in, and that is a good ending.
        "Taken" when Asks(awaitsBefore, awaitsAfter)
            => SessionConclusion.Of("completed", Exit(
                Noted.Of(
                    NoteCodes.EndedAwaits,
                    $"asked `#{awaitsAfter}` of another repository and waits for its answer — the quest resumes, "
                    + "in the same tree, once that is answered" + (exitCode == 0 ? "." : $" (exit {exitCode})."),
                    ("awaits", awaitsAfter)),
                exitCode)),

        // FABRIC-CLOSE1h5 (§E5): in a closure graph the agent's `ready` is its done, and Daoris writes no Done before the graph's end, so
        // the quest stays taken at its submission. That is the work's result in, read as a done is, before a messy exit, a failed turn or
        // the park a session holding its own quest would read as: parked, it was never due, and its branch never landed by itself.
        "Taken" when submitted
            => SessionConclusion.Of("completed", Exit(
                Noted.Of(NoteCodes.EndedSubmitted, "its agent made its work ready: the quest is submitted, and closes at its workflow's end."),
                exitCode)),

        // 🔴 A refused turn (ACPEND1), before the stand-down reading: measured on the first real run, an
        // account's spend limit ended the turn mid-edit, the adapter exited 0, and a taken quest read as
        // "someone else has it". Where the work reached its close or its wait first, that ending stands.
        // The lead-in is Daoris's and the failure the agent's own words, each a part of its own (LANG1a).
        "Taken" when turnFailed is { Length: > 0 }
            => SessionConclusion.Of("failed", Noted.Of(NoteCodes.EndedTurnFailedTaken, "the agent's turn failed with the quest still taken:")
                .Then(" ", Noted.Said(turnFailed, NoteBy.Agent))),
        "Open" when turnFailed is { Length: > 0 }
            => SessionConclusion.Of("failed", Noted.Of(NoteCodes.EndedTurnFailedOpen, "the agent's turn failed before it took its quest:")
                .Then(" ", Noted.Said(turnFailed, NoteBy.Agent))),

        // 🔴 APIREFUSAL1 (the install, 2026-10-23): its model's provider refused a call and the turn ended on it. Its adapter said the
        // provider's error as the agent's last words and exited 0, so with its quest taken it parked, asking the person what nobody
        // could answer. It is a failed run, a strike, carried on as one; read before every park, as a failed turn is.
        "Taken" or "Open" when providerRefused
            => SessionConclusion.Of("failed", ProviderRefusedNote(lastWords)),

        // FABRIC-CLOSE1c2 (§E5): the person reopened the work its quest grew from, and the service told it so at its step and refused
        // its done and its ready. It stopped holding its quest for their choice: a good ending, as a wait is, read before the park, which
        // would ask them what only the step's go-on or abandon answers. Their go-on carries it on in the same tree.
        "Taken" when exitCode == 0 && reopenedFrom is { Length: > 0 } source
            => SessionConclusion.Of("completed", Noted.Of(
                NoteCodes.EndedSourceReopened,
                $"stopped with its quest still taken: the work of `#{source}` it grew from was reopened, and it goes on in the same tree "
                + "once the person goes on with it on the new work.",
                ("quest", source))),

        // BGWAIT1c (D83's BGWAIT1 note): on the native door the harness ends the session's own background work with its turn, so a
        // turn that ended on that work is no question, as BGWAIT1 has it, and there is nothing left to wait for. Where it would park,
        // quoting what it said of the work, it is cut off, failed: D80 carries it on in the same tree, handed this note, and the
        // strikes bound it, as work that never ends is bounded on the protocol door.
        "Taken" when exitCode == 0 && backgroundCut is { Count: > 0 } works && Parks(questStatus, awaitsBefore, awaitsAfter, resumed, took)
            => SessionConclusion.Of("failed", Noted.Of(
                NoteCodes.EndedBackgroundCut,
                $"its turn ended while its own background work ran: {string.Join(", ", works.Select(name => $"`{name}`"))}. Its agent "
                + "ends such work with the turn, so it was cut off before that work finished, and is carried on in the same tree.",
                ("works", works))),

        // 🔴 HELPERWAIT1 (the owner's install, 2026-10-23, twice): its agent asked a helper and ended its turn so the helper could read
        // its work, which a helper does only at a turn's end (COAGENT1a). Parked, it read as asking the person, and its helper's answer
        // waited for the person's: a deadlock. It waits on its helper, which is no question, as BGWAIT1's own background work is not:
        // completed, by a code of its own and quoting nothing, so nothing counts it as the person's; the answer reopens it once (D188
        // point 4), and a helper that ends with no answer brings it back to the person as this park would have (HelperGone).
        "Taken" when exitCode == 0 && awaitsHelper && Parks(questStatus, awaitsBefore, awaitsAfter, resumed, took)
            => SessionConclusion.Of("completed", AwaitsHelper),

        // 🔴 A session that holds its own quest and ended its turn cleanly is WAITING ON THE PERSON
        // (STANDDOWN2): it took the quest itself, or resumed or carried on one this machine already
        // held. FG5's verify session did exactly this with three questions, and read "someone else has
        // it". Parked, it quotes what it said; the person's answer carries the quest on in the same tree.
        // A park is never resumed by itself, so nothing here loops. The rule is one (Parks): the protocol door reads it too, to
        // keep a turn that ended on its own background work working instead (BGWAIT1).
        "Taken" when exitCode == 0 && Parks(questStatus, awaitsBefore, awaitsAfter, resumed, took)
            => SessionConclusion.Of("awaiting-person", Parked(lastWords)),

        // Holding its quest with a messy exit is a failure, and the strikes bound carrying it on (D80).
        "Taken" when awaitsBefore is { Length: > 0 }
            => SessionConclusion.Of("failed", Noted.Of(
                NoteCodes.EndedAnsweredUnfinished,
                $"resumed with `#{awaitsBefore}` answered, and ended with the quest still taken (exit {exitCode}).",
                ("awaits", awaitsBefore), ("exit", exitCode))),
        "Taken" when resumed
            => SessionConclusion.Of("failed", Noted.Of(
                NoteCodes.EndedCarriedUnfinished, $"carried the quest on, and ended with it still taken (exit {exitCode}).", ("exit", exitCode))),

        // The quest reaching its close outranks a messy exit: the work is what matters, and the exit
        // is noted for the reader rather than allowed to overrule the record.
        "Done" => SessionConclusion.Of("completed", Exit(
            Noted.Of(NoteCodes.EndedDone, exitCode == 0 ? "the quest reached done." : $"the quest reached done (exit {exitCode})."),
            exitCode)),
        "Declined" => SessionConclusion.Of("declined", Exit(
            Noted.Of(
                NoteCodes.EndedDeclined,
                exitCode == 0 ? "the session declined, with its reason on the quest."
                              : $"the session declined (exit {exitCode}); the reason is on the quest."),
            exitCode)),

        // A clean exit with the quest taken, by a session that did not take it, is the stand-down shape:
        // it found someone else's claim and finished without touching anything.
        "Taken" when exitCode == 0 => SessionConclusion.Of("stood-down",
            Noted.Of(NoteCodes.EndedStoodDown, "exited cleanly with the quest taken — someone else has it.")),
        "Taken" => SessionConclusion.Of("failed",
            Noted.Of(NoteCodes.EndedTakenExit, $"exit {exitCode} with the quest still taken.", ("exit", exitCode))),

        _ => SessionConclusion.Of("failed",
            exitCode == 0 ? Noted.Of(NoteCodes.EndedUntouched, "exited without touching its quest.")
                          : Noted.Of(NoteCodes.EndedUntouchedExit, $"exit {exitCode} before taking its quest.", ("exit", exitCode))),
    };

    /// <summary>
    /// Whether a clean end with its quest so parks on the person (STANDDOWN2, D83): still taken, with no new question to
    /// another repository (D79), by a session that took it itself, or resumed or carried on this machine's take. The
    /// conclusion's rule, read also by the protocol door when a turn ends while the session's own background work runs
    /// (BGWAIT1): a session that would park then waits for that work instead.
    /// </summary>
    public static bool Parks(string questStatus, string? awaitsBefore, string? awaitsAfter, bool resumed, bool took) =>
        questStatus == "Taken"
        && !Asks(awaitsBefore, awaitsAfter)
        && (took || resumed || awaitsBefore is { Length: > 0 });

    /// <summary>
    /// A park's note (STANDDOWN2): the words lead, since wherever this note is shown it already says the session is waiting; with no
    /// words to quote, where they would be.
    /// </summary>
    internal static Noted Parked(string? lastWords) =>
        lastWords is { Length: > 0 } said
            ? Noted.Of(NoteCodes.EndedParkedAsked, "It stopped with its quest still taken, to ask you:")
                .Then("\n\n", Noted.Said(said, NoteBy.Agent))
            : Noted.Of(
                NoteCodes.EndedParkedTranscript,
                "It stopped with its quest still taken, to ask you — what it needs is in the last words of its transcript.");

    /// <summary>
    /// A run whose turn its model's provider refused (APIREFUSAL1): Daoris's line, then the provider's words as the harness said them,
    /// whole within the last words' bound, so its request id stays on the record; with none, the line alone.
    /// </summary>
    internal static Noted ProviderRefusedNote(string? words)
    {
        var line = Noted.Of(
            NoteCodes.EndedProviderRefused,
            "its model's provider refused its turn, so the run failed and goes on as a failed run does:");
        return words is { Length: > 0 } said ? line.Then(" ", Noted.Said(said, NoteBy.Program)) : line;
    }

    /// <summary>
    /// A session that ended its turn on its own open helper ask (HELPERWAIT1): it waits on that helper, and comes to the person only if
    /// no answer comes. Its agent's words are not quoted: they are its conversation's, and a quote would read as a question.
    /// </summary>
    public static Noted AwaitsHelper => Noted.Of(
        NoteCodes.EndedAwaitsHelper,
        "It asked a helper and stopped so the helper could read: it goes on in the same conversation with the helper's answer, "
        + "and comes to you only if no answer comes.");

    /// <summary>
    /// The park a session that waited on its own helper is brought back to when that helper ends with no answer (HELPERWAIT1): today's
    /// park, as its turn's end would have made it, after the reason by its own code (<see cref="HelperNotes.Refused"/>).
    /// </summary>
    /// <param name="why">The refusal's line: why no helper answered, and which agent, where one was chosen.</param>
    /// <param name="lastWords">The agent's last words, quoted as a park quotes them; null points at the transcript.</param>
    public static SessionConclusion HelperGone(Noted why, string? lastWords) =>
        SessionConclusion.Of("awaiting-person", why.Then("\n\n", Parked(lastWords)));

    /// <summary>A NEW question to another repository (D79): what it waits on now, and not what it waited on before.</summary>
    private static bool Asks(string? awaitsBefore, string? awaitsAfter) =>
        awaitsAfter is { Length: > 0 } && !string.Equals(awaitsAfter, awaitsBefore, StringComparison.Ordinal);

    /// <summary>
    /// A line whose English says an exit that was not 0, with the exit as a part of its own beside it (LANG1a, the language
    /// design §4 row 2): the line's text stays its whole sentence, and the exit's is the two words inside it.
    /// </summary>
    internal static Noted Exit(Noted line, int exitCode) =>
        exitCode == 0 ? line : line.Also(NoteCodes.EndedExit.Part($"exit {exitCode}", ("exit", exitCode)));

    /// <summary>
    /// How a run that resumed its own conversation ends (ANSWER1a; MSG1b, D137 §2.3): as any start's, from its exit and its
    /// quest's state, where its quest was open or taken when the look planned it; as a closed quest's (<see cref="WentOn"/>)
    /// where it had closed before.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Closed is read from the quest as the look planned it, never as the run left it.</b> A resumed run closes its own
    /// quest: read after it, an answered park that finished its work took a closed quest's ending and stayed parked, holding
    /// its tree and a slot, which the merge of 2026-10-03 caught in ANSWER1's tick and the family rehearsal's 17a.
    /// </remarks>
    /// <param name="before">The record's state before it went on, in its spelling.</param>
    /// <param name="startedOn">Its quest's status as the look planned the run.</param>
    /// <param name="questStatus">Its quest's status as the run left it.</param>
    /// <param name="backgroundCut">The session's own background work its harness ended with its turn (BGWAIT1c), as a start's.</param>
    /// <param name="submitted">Whether its quest stands at a closure graph's current submission, as a start's (FABRIC-CLOSE1h5).</param>
    /// <param name="awaitsHelper">Whether it ended its turn on its own open helper ask, as a start's (HELPERWAIT1).</param>
    /// <param name="providerRefused">Whether its turn ended on its provider's refusal, as a start's (APIREFUSAL1).</param>
    public static SessionConclusion Resumed(
        int exitCode, string before, string startedOn, string questStatus, string? awaitsBefore = null, string? awaitsAfter = null,
        string? turnFailed = null, bool took = false, string? lastWords = null, IReadOnlyList<string>? backgroundCut = null,
        bool submitted = false, bool awaitsHelper = false, bool providerRefused = false) =>
        startedOn is "Open" or "Taken"
            ? Conclude(exitCode, questStatus, awaitsBefore, awaitsAfter, turnFailed, resumed: true, took, lastWords, backgroundCut: backgroundCut,
                submitted: submitted, awaitsHelper: awaitsHelper, providerRefused: providerRefused)
            : turnFailed is not { Length: > 0 } && providerRefused
                // APIREFUSAL1: its words went on and its provider refused the turn, which never ends as the state it went on from.
                ? SessionConclusion.Of("failed", ProviderRefusedNote(lastWords))
                : WentOn(exitCode, before, turnFailed);

    /// <summary>
    /// How a session whose quest had closed ends after going on with the person's words (MSG1b, D137 §2.3): in the state it
    /// had before it went on when it exits cleanly, <c>failed</c> otherwise. Its quest does not move, so its state says
    /// nothing of it; and it cannot park, since it holds no quest (D83).
    /// </summary>
    /// <remarks>
    /// <para>A record live when it went on (a park whose quest closed under it) ends <c>completed</c> on a clean exit: a live
    /// record would hold its tree and a slot for ever.</para>
    ///
    /// <para>🔴 <b>A turn the door said failed is a failed run</b> (TURNFAIL1, as ACPEND1 has it for a first run): on the
    /// protocol door the agent exits 0 once stdin closes whatever happened, so a connection that dropped 27 minutes into the
    /// turn ended the install's record <c>completed</c>, "it went on with your words and ended", its words unanswered.</para>
    /// </remarks>
    /// <param name="before">The state it went on from, in the record's spelling.</param>
    /// <param name="turnFailed">What the protocol door said refusing the turn (ACPEND1), or null.</param>
    public static SessionConclusion WentOn(int exitCode, string before, string? turnFailed = null) =>
        turnFailed is { Length: > 0 } failed
            ? SessionConclusion.Of("failed", Noted.Of(NoteCodes.EndedWentOnFailed, "it went on with your words, and its turn failed:")
                .Then(" ", Noted.Said(failed, NoteBy.Agent)))
            : exitCode == 0
        ? SessionConclusion.Of(
            before is "completed" or "declined" or "failed" or "stopped" ? before : "completed",
            Noted.Of(NoteCodes.EndedWentOn, "it went on with your words and ended; its quest stays as it closed."))
        : SessionConclusion.Of("failed", Noted.Of(
            NoteCodes.EndedWentOnExit, $"it went on with your words and exited {exitCode}; its quest stays as it closed.", ("exit", exitCode)));

    /// <summary>
    /// How a run Daoris ended at its turn's end because its repository's permission prompts changed concludes (ACCESSSWAP1b, D183's note):
    /// neither a park nor a failure. It stopped because Daoris ended it, so where its clean end would read as the session's own choice, a
    /// park asking the person or a quest it never touched, it is completed, saying the change; its held words wait on the record, and the
    /// next look goes on with them in the same conversation under the new setup. Where its work had reached an ending of its own (done,
    /// declined, a wait, a submission) or its door failed, that ending stands, and the change is said after it.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>Never parked.</b> A park asks the person what the session never asked, and a move into <c>awaiting-person</c> clears
    /// the words waiting, which are the ones it goes on with. <b>Never failed</b>: a failure is a strike, and D80 would carry the quest on
    /// in a new conversation, where this one is to go on.</para>
    /// </remarks>
    /// <param name="observed">How the run concluded by the exit and the quest's state, the person's stop first.</param>
    public static SessionConclusion SetupChanged(SessionConclusion observed, SetupSwap swap)
    {
        var line = Noted.Of(
            NoteCodes.EndedSetupChanged,
            $"its permission prompts changed while it worked, from `{swap.From}` to `{swap.To}`: Daoris ended it at its turn's end, never "
            + "mid-turn, and it goes on in the same conversation under the new setup.",
            ("from", swap.From), ("to", swap.To));
        var ownChoice = SessionStates.IsParked(observed.State)
                        || observed.Parts?.Any(part => part.Code == NoteCodes.EndedUntouched.Code) == true;
        return ownChoice ? SessionConclusion.Of("completed", line) : observed.Then("\n\n", line);
    }

    /// <summary>
    /// What the record and the conversation say of the words a run handed back for a changed setup (ACCESSSWAP1b): they never went, so
    /// they go on, not again (<see cref="FailedTurnWords"/>).
    /// </summary>
    public static Noted WordsUnderNewSetup =>
        Noted.Of(NoteCodes.EndedWordsSetup, "Your words go on at the next look, in the same conversation under the new setup.");

    /// <summary>The person's plain stop, where the door that stopped it said nothing more (the language design §4 row 19).</summary>
    public static Noted Stopped => Noted.Of(NoteCodes.EndedStopped, "the person stopped it.");

    /// <summary>
    /// State E's note line (SESSWATCH1a, D200): its agent's process exited, something it started held its output, and Daoris ended what
    /// was left. Never the conclusion: the run concludes by its exit.
    /// </summary>
    public static Noted Gone { get; } = Noted.Of(
        NoteCodes.EndedGone, "its agent's process had ended, but something it started still held its output; Daoris ended what was left.");

    /// <summary>A run the watcher ended after <paramref name="minutes"/> with nothing heard (SESSWATCH1a): a headless <c>--quiet-end</c>.</summary>
    public static Noted Quiet(int minutes) =>
        Noted.Of(NoteCodes.EndedQuiet, $"nothing came from its agent for {minutes} minutes. Daoris ended this run, and the work goes on in the same tree.", ("minutes", minutes));

    /// <summary>
    /// How a driven run the watcher ended ends (SESSWATCH1a, the session-watch design §2.2): failed, saying the quiet, a strike as a timeout
    /// was, unless this run brought its quest to done (<see cref="AfterTimeout"/>'s rule).
    /// </summary>
    public static SessionConclusion AfterQuiet(int minutes, string? startedOn, string questStatus) =>
        questStatus == "Done" && startedOn is ("Open" or "Taken")
            ? SessionConclusion.Of("completed", Noted.Of(NoteCodes.EndedDone, "the quest reached done.").Then(" ", Quiet(minutes)))
            : SessionConclusion.Of("failed", Quiet(minutes));

    /// <summary>
    /// The person's <i>End and carry on</i> of a quiet run (SESSWATCH1a, the session-watch design §2.3): their stop, released in the same
    /// press, its record <c>stopped</c> with this note; no strike, and the words that waited for it go to the next part.
    /// </summary>
    public static Noted QuietPressed(int minutes) =>
        Noted.Of(NoteCodes.EndedQuietPressed, $"You ended this run after {minutes} minutes with nothing from its agent, and the work goes on in the same tree.", ("minutes", minutes));

    /// <summary>Whether a record's note is the person's <i>End and carry on</i> (<see cref="QuietPressed"/>), by its code, never its English.</summary>
    public static bool IsQuietPressed(IReadOnlyList<NotePart>? parts) => parts?.Any(part => part.Code == NoteCodes.EndedQuietPressed.Code) == true;

    /// <summary>A session the timeout killed (row 18).</summary>
    public static Noted TimedOut(int minutes) =>
        Noted.Of(NoteCodes.EndedTimeout, $"timed out after {minutes} minutes and was killed.", ("minutes", minutes));

    /// <summary>
    /// How a driven run the timeout killed ends (BGWAIT2): failed, saying the timeout, unless this run brought its quest to done,
    /// which outranks the kill as it outranks a messy exit, so it is completed and says both.
    /// </summary>
    /// <remarks>
    /// <para>🔴 The install's session committed and closed its quest done, then waited 48 minutes on a prompt its adapter never
    /// answered, and the timeout recorded it failed (2026-10-19). Its work had ended; only its wait had not.</para>
    ///
    /// <para><b>Only a done this run reached.</b> A run that went on with the person's words after its quest closed is judged on
    /// those words alone (MSG1b, <see cref="Resumed"/>), and its quest's old done says nothing of them, so its timeout is failed
    /// as before and the words it carried wait to go again (<see cref="FailedTurnWords"/>). A quest still taken at the timeout is
    /// failed, and D80 carries it on, bounded by the strikes. The person's stop is read before this, and stands.</para>
    /// </remarks>
    /// <param name="startedOn">Its quest's status as the look planned the run.</param>
    /// <param name="questStatus">Its quest's status as the run left it.</param>
    public static SessionConclusion AfterTimeout(int minutes, string? startedOn, string questStatus) =>
        questStatus == "Done" && startedOn is ("Open" or "Taken")
            ? SessionConclusion.Of("completed", Noted.Of(NoteCodes.EndedDone, "the quest reached done.").Then(" ", TimedOut(minutes)))
            : SessionConclusion.Of("failed", TimedOut(minutes));

    /// <summary>A session the driver's own shutdown ended (row 21), for a driven session, a resumed one and an intake alike.</summary>
    public static Noted DriverClosed =>
        Noted.Of(NoteCodes.EndedDriverClosed, "the driver was stopped while this ran; the session's process was ended with it.");

    /// <summary>
    /// How a driven session the driver's close ended is recorded (D104): stopped and interrupted, to be carried on at the next
    /// start, unless a stop came first.
    /// </summary>
    /// <remarks>
    /// 🔴 FLAKE3: a stop made before the close decided the end, so the record says that stop, as its conclusion would have, and
    /// is never interrupted: the driver's, for a take that lost, is no take to carry on, and the person's never is (D104). Which
    /// of the two reached the record first was a race: a process killed for its lost take was still exiting, or its conclusion
    /// still asking the service, when a failure closed the watch, and the record read <c>stopped</c> by the close.
    /// </remarks>
    /// <param name="first">The stop made of the session before the close, or null for none.</param>
    public static (SessionConclusion Conclusion, bool Interrupted) Closed(SessionStop? first) =>
        first is not null ? (first.Conclusion, false) : (SessionConclusion.Of("stopped", DriverClosed), true);

    /// <summary>A failure in a program's own words, an exception's message, which no catalogue re-authors (the design §2).</summary>
    public static Noted Failure(string message) => Noted.Said(message, NoteBy.Program);

    /// <summary>
    /// Did the harness say its provider refused the account's credential (AGT3b)? Read by the pattern its toolchain declares,
    /// from what the harness itself reported as the run's failure, and only where it reported one (AGT3c): its door's
    /// failure, or its own lines beside a failure or an exit that was not 0. Never from words its agent could write, which on
    /// any door but one that carries only text are not among them. Null declares no pattern, and nothing is observed.
    /// </summary>
    public static bool Refused(HarnessEnding ended, string? pattern)
    {
        if (pattern is not { Length: > 0 } || !ended.Failed) return false;
        bool Says(string? text) => text?.Contains(pattern, StringComparison.OrdinalIgnoreCase) == true;
        return Says(ended.Failure) || ended.Lines.Any(Says);
    }
}

/// <summary>What becomes of the person's words a run carried, judged as it concludes (TURNFAIL1).</summary>
public enum WordsAfter
{
    /// <summary>The run took them: they leave the record by their ids (MSG1b).</summary>
    Taken,

    /// <summary>Its turn failed before answering them: they stay waiting, unmarked, and the next look takes them again.</summary>
    Wait,

    /// <summary>They failed a second time running: they stay waiting, marked, until the person says more (MSG1b's marks).</summary>
    Held,
}

/// <summary>
/// The person's words a run carried when its door failed (TURNFAIL1, D137's note): never answered, so they stay waiting on its
/// record as a cooling account's do (D137 §2.2), and its note says why.
/// </summary>
/// <remarks>
/// <b>Bounded, by the record's own note.</b> Words waiting on an ended record are taken up at the next look, every fifteen
/// seconds, and a door that fails at once (a conversation too long to send) would fail at every one. So the words go again by
/// themselves once: where the record they went on from had ended saying its words wait after such a failure, they are marked,
/// and go again with the next words the person says (<see cref="GoOnMarks"/>).
/// </remarks>
public static class FailedTurnWords
{
    /// <param name="conclusion">How the run concluded, every account's line added.</param>
    /// <param name="turnFailed">What the protocol door said refusing the turn (ACPEND1), or null.</param>
    /// <param name="before">The record as the look planned the run: its note says how it ended before it went on.</param>
    public static WordsAfter Judge(SessionConclusion conclusion, string? turnFailed, PriorSession before) =>
        turnFailed is not { Length: > 0 } || conclusion.State != "failed" ? WordsAfter.Taken
        : before.NoteParts?.Any(part => part.Code == NoteCodes.EndedWordsWait.Code || part.Code == NoteCodes.EndedWordsWaitYou.Code) == true
            ? WordsAfter.Held
            : WordsAfter.Wait;

    /// <summary>
    /// As above, a failed turn read from either door (TURNFAIL4): the protocol door's refused turn, or the native door's failed
    /// <c>result</c> its harness ended with (<see cref="HarnessEnding.Failure"/>, which TURNFAIL2's words left read), so the bound
    /// marks a native run's words as it marks a protocol run's.
    /// </summary>
    public static WordsAfter Judge(SessionConclusion conclusion, string? turnFailed, HarnessEnding ended, PriorSession before) =>
        Judge(conclusion, turnFailed is { Length: > 0 } ? turnFailed : ended.Failure, before);

    /// <summary>The line the record's note and its conversation say why the words wait by; null for words taken.</summary>
    public static Noted? Line(WordsAfter after) => after switch
    {
        WordsAfter.Wait => Noted.Of(NoteCodes.EndedWordsWait, "Your words wait to go again at the next look."),
        WordsAfter.Held => Noted.Of(
            NoteCodes.EndedWordsWaitYou, "Your words failed twice and wait for you: they go again with the next words you say."),
        _ => null,
    };
}

/// <summary>
/// What a native run's harness wrote that its agent could not (AGT3c), kept as its output passes: its stderr and the lines it
/// printed outside its protocol, the last <see cref="HarnessEnding.LinesKept"/> of them, and the words its failed turn gave.
/// One per session, begun again by each run, so the conclusion reads the run that ended last.
/// </summary>
public sealed class HarnessWords
{
    private readonly object _gate = new();
    private readonly Queue<string> _lines = new();
    private string? _failure;

    // BGWAIT1c: the commands it backgrounded still running, by task id; whether its turn's `result` came; what the harness ended after it.
    private readonly Dictionary<string, string> _running = new(StringComparer.Ordinal);
    private readonly List<string> _cut = [];
    private bool _turnEnded;

    /// <summary>A run begins: what the run before it said is not this one's ending.</summary>
    public void Begin()
    {
        lock (_gate)
        {
            _lines.Clear();
            _failure = null;
            _running.Clear();
            _cut.Clear();
            _turnEnded = false;
        }
    }

    /// <summary>
    /// A line of the native door's structured stdout, read for the session's own background work (BGWAIT1c;
    /// docs/2026-09-30-console3-native-streams-evidence.md): a command it backgrounded (<c>task_started</c>, never a subagent's),
    /// the turn's end (<c>result</c>), and that command ended after it (<c>task_updated</c> <c>killed</c>, or its
    /// <c>task_notification</c> <c>stopped</c>), which only the harness does there, since the door has no stop for one task.
    /// Work that ended before the turn did is no cut. Read before a console's streams take the line, so a run with no console
    /// reads the same.
    /// </summary>
    public void Heard(string line)
    {
        // Most lines are the agent's words: only a task's line or the turn's result is parsed.
        if (!line.Contains("\"task_", StringComparison.Ordinal) && !line.Contains("\"result\"", StringComparison.Ordinal)) return;
        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(line);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return;
        }

        if (root.ValueKind != JsonValueKind.Object) return;
        var type = Text(root, "type");
        lock (_gate)
        {
            if (type == "result")
            {
                _turnEnded = true;
                return;
            }

            if (type != "system" || Text(root, "task_id") is not { Length: > 0 } id) return;
            switch (Text(root, "subtype"))
            {
                case "task_started" when Text(root, "task_type") != "local_agent":
                    _running[id] = Text(root, "description") is { Length: > 0 } named ? named : id;
                    break;
                case "task_updated" when root.TryGetProperty("patch", out var patch) && patch.ValueKind == JsonValueKind.Object:
                    Ended(id, Text(patch, "status"));
                    break;
                case "task_notification":
                    Ended(id, Text(root, "status"));
                    break;
            }
        }

        // Called under the gate: an ending of a command still running, cut where the turn had ended before it.
        void Ended(string id, string? status)
        {
            if (status is null or "running" or "pending" || !_running.Remove(id, out var name)) return;
            if (_turnEnded && status is "killed" or "stopped") _cut.Add(name);
        }

        static string? Text(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    /// <summary>A line the harness wrote itself: on its stderr, or outside its protocol.</summary>
    public void Said(string line)
    {
        lock (_gate)
        {
            _lines.Enqueue(line);
            while (_lines.Count > HarnessEnding.LinesKept) _lines.Dequeue();
        }
    }

    /// <summary>A turn the harness said failed, in its words.</summary>
    public void Failed(string failure)
    {
        lock (_gate) _failure = failure;
    }

    /// <summary>The run's ending, with the process's exit.</summary>
    public HarnessEnding Ended(int? exit)
    {
        lock (_gate) return new(exit, _failure, [.. _lines]) { BackgroundCut = [.. _cut] };
    }
}
