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
    public static SessionConclusion Conclude(
        int exitCode, string questStatus, string? awaitsBefore = null, string? awaitsAfter = null) => questStatus switch
    {
        // Ask and wait (D79): the session published a question to another repository and waits on it.
        // Its quest stays taken for the same tree to resume in, and that is a good ending.
        "Taken" when awaitsAfter is { Length: > 0 } && !string.Equals(awaitsAfter, awaitsBefore, StringComparison.Ordinal)
            => new("completed",
                $"asked `#{awaitsAfter}` of another repository and waits for its answer — the quest resumes, "
                + "in the same tree, once that is answered" + (exitCode == 0 ? "." : $" (exit {exitCode}).")),

        // A RESUMED session that ends with the old wait still standing did not carry on: it was handed
        // the answer and stopped short. Failed, so the strikes bound it — as a stand-down it would be
        // resumed again every tick, since nothing about the quest would have changed.
        "Taken" when awaitsBefore is { Length: > 0 }
            => new("failed",
                $"resumed with `#{awaitsBefore}` answered, and ended with the quest still taken"
                + (exitCode == 0 ? "." : $" (exit {exitCode}).")),

        // The quest reaching its close outranks a messy exit: the work is what matters, and the exit
        // is noted for the reader rather than allowed to overrule the record.
        "Done" => new("completed",
            exitCode == 0 ? "the quest reached done." : $"the quest reached done (exit {exitCode})."),
        "Declined" => new("declined",
            exitCode == 0 ? "the session declined, with its reason on the quest."
                          : $"the session declined (exit {exitCode}); the reason is on the quest."),

        // A clean exit with the quest taken is the stand-down shape: the session found someone else's
        // claim and finished without touching anything. A session that took the quest itself, finished
        // its work, and forgot to close it lands here too — the evidence carries the commits, so the
        // person can see which it was.
        "Taken" when exitCode == 0 => new("stood-down",
            "exited cleanly with the quest taken — someone else has it."),
        "Taken" => new("failed", $"exit {exitCode} with the quest still taken."),

        _ => new("failed",
            exitCode == 0 ? "exited without touching its quest."
                          : $"exit {exitCode} before taking its quest."),
    };

    /// <summary>
    /// Did the tool say its provider refused the account's credential (AGT3b)? Read from its own last
    /// words, by the pattern its toolchain declares; null declares none, and nothing is observed.
    /// </summary>
    public static bool Refused(IEnumerable<string> lastLines, string? pattern) =>
        pattern is { Length: > 0 }
        && lastLines.Any(line => line.Contains(pattern, StringComparison.OrdinalIgnoreCase));
}
