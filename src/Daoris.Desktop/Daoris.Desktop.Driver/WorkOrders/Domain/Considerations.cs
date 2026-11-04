namespace Daoris.Driver;

public static class Considerations
{
    /// <summary>
    /// What a set of considerations SAYS, as one string — equal when the same quests carry the same
    /// verdicts and reasons, whatever the order. The shell forwards a tick to the page when this changes.
    /// </summary>
    public static string Signature(IEnumerable<Consideration> considered) =>
        string.Join("\n", considered
            .Select(c => $"{c.Quest.Id}\t{c.Verdict}\t{c.Reason}")
            .OrderBy(line => line, StringComparer.Ordinal));

    /// <summary>
    /// The plan as it turned out: every start that was held at spawn becomes <see cref="StartVerdict.Blocked"/> with the hold's own
    /// sentence, and everything else is the plan's word. The plan is not edited — what was decided and what happened are two records.
    /// </summary>
    /// <param name="signedOut">The accounts each held start passed not signed in, carried beside its hold.</param>
    public static IReadOnlyList<Consideration> Blocked(
        IReadOnlyList<Consideration> plan, IReadOnlyDictionary<string, string> heldAt,
        IReadOnlyDictionary<string, SignedOutAccounts>? signedOut = null) =>
        plan.Select(c =>
                c.Verdict == StartVerdict.Start && heldAt.TryGetValue(c.Quest.Id, out var why)
                    ? c with
                    {
                        Verdict = StartVerdict.Blocked, Reason = why, SignedOut = signedOut?.GetValueOrDefault(c.Quest.Id),
                        // The checkout hold by its code, for the page to word.
                        Said = why == CheckoutHeld ? new(SittingFact.Checkout) { Session = c.Resumes?.Session } : c.Said,
                    }
                    : c)
            .ToList();

    /// <summary>
    /// The hold of a start a steward's act caused that would work in the owner's checkout: held for the owner, never relocated,
    /// until their own Try again or Drive.
    /// </summary>
    public const string CheckoutHeld = "Its earlier work is in your checkout, so carrying it on is yours.";
}
