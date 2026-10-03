namespace Daoris.Driver;

/// <param name="State">The terminal state, in the wire spelling the ledger accepts.</param>
/// <param name="Note">What was observed — the sentence the record keeps.</param>
public sealed record SessionConclusion(string State, string Note);

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
        "Taken" when awaitsAfter is { Length: > 0 } && !string.Equals(awaitsAfter, awaitsBefore, StringComparison.Ordinal)
            => new("completed",
                $"asked `#{awaitsAfter}` of another repository and waits for its answer — the quest resumes, "
                + "in the same tree, once that is answered" + (exitCode == 0 ? "." : $" (exit {exitCode}).")),

        // 🔴 A refused turn (ACPEND1), before the stand-down reading: measured on the first real run, an
        // account's spend limit ended the turn mid-edit, the adapter exited 0, and a taken quest read as
        // "someone else has it". Where the work reached its close or its wait first, that ending stands.
        "Taken" when turnFailed is { Length: > 0 }
            => new("failed", $"the agent's turn failed with the quest still taken: {turnFailed}"),
        "Open" when turnFailed is { Length: > 0 }
            => new("failed", $"the agent's turn failed before it took its quest: {turnFailed}"),

        // 🔴 A session that holds its own quest and ended its turn cleanly is WAITING ON THE PERSON
        // (STANDDOWN2): it took the quest itself, or resumed or carried on one this machine already
        // held. FG5's verify session did exactly this with three questions, and read "someone else has
        // it". Parked, it quotes what it said; the person's answer carries the quest on in the same tree.
        // A park is never resumed by itself, so nothing here loops.
        "Taken" when exitCode == 0 && (took || resumed || awaitsBefore is { Length: > 0 })
            // The words lead: wherever this note is shown, it already says the session is waiting.
            => new("awaiting-person",
                lastWords is { Length: > 0 } said
                    ? $"It stopped with its quest still taken, to ask you:\n\n{said}"
                    : "It stopped with its quest still taken, to ask you — what it needs is in the last words of its transcript."),

        // Holding its quest with a messy exit is a failure, and the strikes bound carrying it on (D80).
        "Taken" when awaitsBefore is { Length: > 0 }
            => new("failed", $"resumed with `#{awaitsBefore}` answered, and ended with the quest still taken (exit {exitCode})."),
        "Taken" when resumed
            => new("failed", $"carried the quest on, and ended with it still taken (exit {exitCode})."),

        // The quest reaching its close outranks a messy exit: the work is what matters, and the exit
        // is noted for the reader rather than allowed to overrule the record.
        "Done" => new("completed",
            exitCode == 0 ? "the quest reached done." : $"the quest reached done (exit {exitCode})."),
        "Declined" => new("declined",
            exitCode == 0 ? "the session declined, with its reason on the quest."
                          : $"the session declined (exit {exitCode}); the reason is on the quest."),

        // A clean exit with the quest taken, by a session that did not take it, is the stand-down shape:
        // it found someone else's claim and finished without touching anything.
        "Taken" when exitCode == 0 => new("stood-down",
            "exited cleanly with the quest taken — someone else has it."),
        "Taken" => new("failed", $"exit {exitCode} with the quest still taken."),

        _ => new("failed",
            exitCode == 0 ? "exited without touching its quest."
                          : $"exit {exitCode} before taking its quest."),
    };

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
        ? new(
            before is "completed" or "declined" or "failed" or "stopped" ? before : "completed",
            "it went on with your words and ended; its quest stays as it closed.")
        : new("failed", $"it went on with your words and exited {exitCode}; its quest stays as it closed.");

    /// <summary>
    /// Did the tool say its provider refused the account's credential (AGT3b)? Read from its own last
    /// words, by the pattern its toolchain declares; null declares none, and nothing is observed.
    /// </summary>
    public static bool Refused(IEnumerable<string> lastLines, string? pattern) =>
        pattern is { Length: > 0 }
        && lastLines.Any(line => line.Contains(pattern, StringComparison.OrdinalIgnoreCase));
}
