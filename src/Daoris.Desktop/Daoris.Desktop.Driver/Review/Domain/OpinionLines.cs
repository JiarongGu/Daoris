namespace Daoris.Driver;

/// <summary>
/// The machine log's second-opinion lines: <c>opinion.asked</c>,
/// <c>opinion.given</c>, <c>opinion.answered</c>, <c>opinion.held</c> and <c>opinion.unavailable</c>, codes, ids and counts only,
/// never a finding, a sentence, an account or anyone's words. They ride the landing line's channel
/// (<see cref="ServiceClient.LandingSaid"/>), which writes the event each names.
/// </summary>
public static class OpinionLines
{
    /// <summary>A pass asked: the opinion, the working session, its occasion, the reviewer's label and door, and the pass.</summary>
    public static LandingLine Asked(string opinion, string session, string occasion, string? label, string? door, string pass) =>
        new("opinion.asked",
        [
            ("opinion", opinion), ("session", session), ("occasion", occasion), ("label", label), ("door", door), ("pass", pass),
        ]);

    /// <summary>A pass given: its findings by weight, the seconds from its ask, and its tier.</summary>
    public static LandingLine Given(OpinionView opinion, long? seconds)
    {
        var findings = opinion.Findings ?? [];
        int Count(string weight) => findings.Count(finding => finding.Weight == weight);
        return new("opinion.given",
        [
            ("opinion", opinion.Id), ("must", Count(OpinionViews.Must)), ("should", Count("should")), ("note", Count("note")),
            ("seconds", seconds), ("tier", OpinionPass.TierAgent),
        ]);
    }

    /// <summary>The working session's answers as read when its turn ended: counted by answer, and those not answered.</summary>
    public static LandingLine Answered(string opinion, OpinionReading reading)
    {
        int Count(Func<OpinionAnswerRead, bool> which) => reading.Findings.Count(which);
        return new("opinion.answered",
        [
            ("opinion", opinion), ("fixed", Count(row => row.Counts == OpinionViews.Fixed)),
            ("rejected", Count(row => row.Counts == OpinionViews.Rejected)),
            ("unresolved", Count(row => row.Counts == OpinionViews.Unresolved && row.Why != OpinionAnswerWhy.NotAnswered)),
            ("notAnswered", Count(row => row.Why == OpinionAnswerWhy.NotAnswered)),
        ]);
    }

    /// <summary>A landing the second opinion held: whose, where, the gate's state, whether the rule requires one, and the door.</summary>
    public static LandingLine Held(string session, string? repository, string? workspace, OpinionGateState gate, string door) =>
        new("opinion.held",
        [
            ("session", session), ("repository", repository), ("workspace", workspace), ("state", gate.State),
            ("required", gate.Required), ("door", door),
        ]);

    /// <summary>No opinion could be had: the working session, the occasion and the code.</summary>
    public static LandingLine Unavailable(string session, string occasion, string code) =>
        new("opinion.unavailable", [("session", session), ("occasion", occasion), ("code", code)]);
}
