namespace Daoris.Driver;

/// <summary>
/// What the evidence door said of a verdict (EVID1a's <c>POST /api/quests/{id}/evidence</c>), or why nothing was posted: an
/// outcome's code and the service's words, or this side's where it said none.
/// </summary>
/// <param name="Outcome">One of the codes below.</param>
/// <param name="Message">The service's sentence, verbatim, or why nothing reached it.</param>
public sealed record EvidencePosted(string Outcome, string Message)
{
    /// <summary>The exchange kept the verdict: all found lifts the hold, anything else keeps it.</summary>
    public const string Kept = "kept";

    /// <summary>The done no longer waits on evidence (409): found already, or accepted as it stands.</summary>
    public const string NotWaiting = "not-waiting";

    /// <summary>The door refused the verdict (400), or holds no such quest (404).</summary>
    public const string Refused = "refused";

    /// <summary>A host older than the door.</summary>
    public const string NoDoor = "no-door";

    /// <summary>The host did not answer.</summary>
    public const string Unanswered = "unanswered";

    /// <summary>Nothing was read, so nothing was posted (<see cref="EvidenceReading.Unread"/> says why).</summary>
    public const string Unread = "unread";
}

/// <summary>A read's whole end (EVID1b): the verdict where one was read, what the door said, and the record's lines.</summary>
/// <param name="Verdict">What was read, or null where nothing was.</param>
/// <param name="Posted">What the door said, or why nothing was posted.</param>
/// <param name="Bundle">
/// The lines the session record's evidence bundle gains (D46 §4, D144 §5) beneath its commits: the commit read by its full id
/// and how, the count found and whether it was kept, then each item with its code. No machine path.
/// </param>
public sealed record EvidenceOutcome(EvidenceVerdict? Verdict, EvidencePosted Posted, string Bundle)
{
    /// <summary>Whether the done's evidence was found and kept: what lets what it held go on.</summary>
    public bool Released => Posted.Outcome == EvidencePosted.Kept && Verdict is { Found: true };
}

/// <summary>
/// The machine log's evidence line (EVID1b, D144 §5, D94): <c>evidence.checked</c>, one per read, with counts and codes and no
/// words: the quest, the session whose end was read (null for the terminal's), how the commit was chosen, how many items were
/// read and how many each code took, and what the door said.
/// </summary>
public sealed record EvidenceLine(string Event, IReadOnlyList<(string Key, object? Value)> Data)
{
    public static EvidenceLine Checked(string quest, EvidenceAt at, int items, EvidenceOutcome outcome)
    {
        int Count(string result) => outcome.Verdict?.Count(result) ?? 0;
        return new("evidence.checked",
        [
            ("quest", quest), ("session", at.Session), ("how", at.How), ("items", items),
            ("found", Count(EvidenceCodes.Found)), ("missing", Count(EvidenceCodes.Missing)),
            ("uncommitted", Count(EvidenceCodes.Uncommitted)), ("case", Count(EvidenceCodes.Case)),
            ("noQueue", Count(EvidenceCodes.NoQueue)), ("outcome", outcome.Posted.Outcome),
        ]);
    }
}
