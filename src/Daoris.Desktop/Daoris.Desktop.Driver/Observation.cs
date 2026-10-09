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
    public static SessionConclusion Conclude(
        int exitCode, string questStatus, string? awaitsBefore = null, string? awaitsAfter = null,
        string? turnFailed = null, bool resumed = false, bool took = false, string? lastWords = null) => questStatus switch
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

        // 🔴 A session that holds its own quest and ended its turn cleanly is WAITING ON THE PERSON
        // (STANDDOWN2): it took the quest itself, or resumed or carried on one this machine already
        // held. FG5's verify session did exactly this with three questions, and read "someone else has
        // it". Parked, it quotes what it said; the person's answer carries the quest on in the same tree.
        // A park is never resumed by itself, so nothing here loops. The rule is one (Parks): the protocol door reads it too, to
        // keep a turn that ended on its own background work working instead (BGWAIT1).
        "Taken" when exitCode == 0 && Parks(questStatus, awaitsBefore, awaitsAfter, resumed, took)
            // The words lead: wherever this note is shown, it already says the session is waiting.
            => SessionConclusion.Of("awaiting-person",
                lastWords is { Length: > 0 } said
                    ? Noted.Of(NoteCodes.EndedParkedAsked, "It stopped with its quest still taken, to ask you:")
                        .Then("\n\n", Noted.Said(said, NoteBy.Agent))
                    : Noted.Of(
                        NoteCodes.EndedParkedTranscript,
                        "It stopped with its quest still taken, to ask you — what it needs is in the last words of its transcript.")),

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
    public static SessionConclusion Resumed(
        int exitCode, string before, string startedOn, string questStatus, string? awaitsBefore = null, string? awaitsAfter = null,
        string? turnFailed = null, bool took = false, string? lastWords = null) =>
        startedOn is "Open" or "Taken"
            ? Conclude(exitCode, questStatus, awaitsBefore, awaitsAfter, turnFailed, resumed: true, took, lastWords)
            : WentOn(exitCode, before);

    /// <summary>
    /// How a session whose quest had closed ends after going on with the person's words (MSG1b, D137 §2.3): in the state it
    /// had before it went on when it exits cleanly, <c>failed</c> otherwise. Its quest does not move, so its state says
    /// nothing of it; and it cannot park, since it holds no quest (D83).
    /// </summary>
    /// <remarks>
    /// A record live when it went on (a park whose quest closed under it) ends <c>completed</c> on a clean exit: a live
    /// record would hold its tree and a slot for ever.
    /// </remarks>
    /// <param name="before">The state it went on from, in the record's spelling.</param>
    public static SessionConclusion WentOn(int exitCode, string before) => exitCode == 0
        ? SessionConclusion.Of(
            before is "completed" or "declined" or "failed" or "stopped" ? before : "completed",
            Noted.Of(NoteCodes.EndedWentOn, "it went on with your words and ended; its quest stays as it closed."))
        : SessionConclusion.Of("failed", Noted.Of(
            NoteCodes.EndedWentOnExit, $"it went on with your words and exited {exitCode}; its quest stays as it closed.", ("exit", exitCode)));

    /// <summary>The person's plain stop, where the door that stopped it said nothing more (the language design §4 row 19).</summary>
    public static Noted Stopped => Noted.Of(NoteCodes.EndedStopped, "the person stopped it.");

    /// <summary>A session the timeout killed (row 18).</summary>
    public static Noted TimedOut(int minutes) =>
        Noted.Of(NoteCodes.EndedTimeout, $"timed out after {minutes} minutes and was killed.", ("minutes", minutes));

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

/// <summary>
/// What a harness itself said about how its run ended, apart from its agent's words (AGT3c): the process's exit, its door's
/// failure, and the lines it wrote that its agent could not. What a refused credential is read from (AGT3b), as a limit and a
/// refused sign-in are read from the door's failure (D125 point 1, ROSTER1b).
/// </summary>
/// <param name="Exit">The process's exit code; null where the timeout killed it.</param>
/// <param name="Failure">
/// The door's failure in the harness's words: the protocol door's refused call (ACPEND1), or the native door's failed
/// <c>result</c>. Null where the door said the turn ended.
/// </param>
/// <param name="Lines">
/// The harness's own last lines: on the native door its stderr and what it printed outside its protocol. A door that carries
/// only text gives nothing but its transcript, so its transcript's last lines are these, the agent's words among them: there,
/// only the exit, which the harness sets, tells a failure (<see cref="Transcript"/>). None on the protocol door, whose failure
/// is its door's.
/// </param>
public sealed record HarnessEnding(int? Exit, string? Failure, IReadOnlyList<string> Lines)
{
    /// <summary>How many of the harness's last lines are kept: where a tool says why it gave up.</summary>
    public const int LinesKept = 40;

    /// <summary>Whether the harness itself said the run failed: its door's failure, or an exit that was not 0.</summary>
    public bool Failed => Failure is { Length: > 0 } || Exit is { } exit && exit != 0;

    /// <summary>
    /// A door that carries only text (AGT3c): its transcript's last lines, both its streams, beside the process's exit. Its
    /// agent's words are among them and cannot be told apart, so only an exit that was not 0 lets them be read. Unreadable
    /// is none.
    /// </summary>
    public static HarnessEnding Transcript(int? exit, string transcript)
    {
        try
        {
            return new(exit, null, [.. File.ReadLines(transcript).TakeLast(LinesKept)]);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new(exit, null, []);
        }
    }
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

    /// <summary>A run begins: what the run before it said is not this one's ending.</summary>
    public void Begin()
    {
        lock (_gate)
        {
            _lines.Clear();
            _failure = null;
        }
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
        lock (_gate) return new(exit, _failure, [.. _lines]);
    }
}
