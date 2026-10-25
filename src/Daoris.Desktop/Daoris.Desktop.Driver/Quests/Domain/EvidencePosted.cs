namespace Daoris.Driver;

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

/// <summary>The catalogue of <see cref="EvidenceLine"/> lines, which the owner of what they say keeps: each builder writes the fields its line gives it and nothing else.</summary>
public static class EvidenceLines
{
    extension(EvidenceLine)
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
}
