namespace Daoris.Driver;

/// <param name="Considerations">Every open quest, with its verdict and reason — the plan, printable.</param>
/// <param name="Events">
/// What actually happened: sessions that ended since the last look (DEV3), and this look's holds, refusals
/// and stops.
/// </param>
/// <param name="Progressed">
/// Whether any session was actually OPENED this tick. A planned start that held (dirty tree) or was
/// refused (raced) is not progress — and treating it as progress is an infinite loop: the same quest
/// would plan, hold, and plan again forever.
/// </param>
/// <param name="Active">
/// The sessions the snapshot found running. Carried so a watcher can see a state change the driver
/// did not cause (SURF5b) — a park is the session asking, through its own connector, and this tick's
/// view is the only place it shows up.
/// </param>
/// <param name="Concluded">
/// What ended since the last look, structurally: a session outlives the look that started it, so its
/// ending joins the next look's report (DEV3). <paramref name="Events"/> already says so in English and
/// nothing can parse that — deliberately, since those sentences are written for a person (D24).
/// </param>
public sealed record TickReport(
    IReadOnlyList<Consideration> Considerations,
    IReadOnlyList<string> Events,
    bool Progressed,
    IReadOnlyList<SessionView>? Active = null,
    IReadOnlyList<SessionEnded>? Concluded = null)
{
    public bool PlannedAnything => Considerations.Any(c => c.Verdict == StartVerdict.Start);

    /// <inheritdoc cref="Active"/>
    public IReadOnlyList<SessionView> Active { get; init; } = Active ?? [];

    /// <inheritdoc cref="Concluded"/>
    public IReadOnlyList<SessionEnded> Concluded { get; init; } = Concluded ?? [];

    /// <summary>
    /// The starts this tick held because the harness has not trusted where they would run (D73) — the
    /// folder, the file that said so, and what was held. <see cref="Events"/> says it for a person;
    /// this says it for a screen, which offers the person the grant and nothing wider.
    /// </summary>
    public IReadOnlyList<TrustHold> Untrusted { get; init; } = [];

    /// <summary>
    /// The starts this look held because the account each would run on is cooling (TOOL4d, D125 §4), one per account:
    /// the agent, the account, the first ready time and what was held. <see cref="Events"/> says each hold for a person;
    /// this says the wait for a screen, which shows those quests as waiting for an account rather than parked, and for
    /// the attention watch, which says it once. Then the starts held on accounts not signed in with none cooling, one per
    /// agent with no time (UX6d1): an ask's intake among them, which no consideration carries.
    /// </summary>
    public IReadOnlyList<AccountWait> Waits { get; init; } = [];

    /// <summary>
    /// Each owed landing opinion's gate as this look judged it (XAGENT1g3), oldest first and one per session, for the attention band's
    /// route to read instead of running a gate per poll. <c>steps</c> opinions are not among them.
    /// </summary>
    public IReadOnlyList<OpinionJudged> JudgedOpinions { get; init; } = [];
}
