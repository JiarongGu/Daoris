namespace Daoris.Driver;

/// <summary>What a driven start that continues another part says and links: the part it left, the line saying so, and the record's <c>from</c>.</summary>
internal static class MovedParts
{
    /// <summary>The part a carry-on goes on from, as its record says it ran: its harness, that harness's maker where this build has it, and the models its door reported.</summary>
    public static EarlierMaker? LeftBy(LoopContext context, PriorSession prior)
    {
        if (prior.Adapter is not { Length: > 0 } ran) return null;
        string? maker;
        try { maker = context.Adapters.Resolve(ran).Toolchain?.Maker; }
        catch (DriverException) { maker = null; }
        return new EarlierMaker(ran, maker, prior.Ran?.Models ?? []);
    }

    /// <summary>
    /// A driven move to a new part said as it happens (D189 point 8): a coded line on the part it left and on the new part, each on
    /// its console too, with the context the part it left stood at and the brief's characters; and <c>session.moved</c> in the
    /// machine log. Every driven start that continues another says it, whatever moved it.
    /// </summary>
    public static void Say(LoopContext context, PriorSession prior, string sessionId, string agent, string? account, string why, ComposedBrief brief)
    {
        var used = RotatedOpening.ContextOf(context.Usage, context.Events, prior.Session);
        var ranOn = prior.Adapter ?? agent;
        var on = HandOverNotes.MovedOn(sessionId, agent, account, why, used, brief.Text.Length);
        var from = HandOverNotes.MovedFrom(prior.Session, ranOn, prior.Profile, why, used, brief.Text.Length);
        foreach (var (session, line) in new[] { (prior.Session, on), (sessionId, from) })
        {
            context.Output?.Append(session, line.Note);
            context.Events.Keep(session, new SessionEvent { Kind = SessionEventKind.Note, Text = line.Note, Parts = line.Parts }, say: null);
        }

        context.Service.AccountSaid(Continuations.Moved(sessionId, prior.Session, ranOn, agent, prior.Profile, account, why, briefed: true));
    }

    /// <summary>
    /// The session a quest's start continues, for its record's <c>from</c> link: the session the quest was handed over from, with the
    /// hand-over's why and asker; else a session a limit cut off, carried on onto another account, Daoris's own move; else the
    /// curve's move. Every other carry-on names its link too, so the brief follows the line: <c>retry</c> where its resume fell back,
    /// else <c>carry-on</c>, the person's where their press released or brought it forward. A first start, a resume of its own record
    /// and a fresh start carry nothing on and say none. The link is the course's move, its why and who asked.
    /// </summary>
    /// <param name="retried">Its record's resume was tried and fell back: a driven record's fallback.</param>
    /// <param name="pressed">The person's press carried it on: <i>Try again</i> after their stop, or a bring-forward.</param>
    /// <param name="handed">The quest's hand-over as held, or null.</param>
    /// <param name="carried">The record it carries on, where it carries one on, or null.</param>
    /// <param name="profile">The account it opens on.</param>
    /// <param name="agent">The agent it opens on, where a limit's move may have gone to another one; null compares accounts alone.</param>
    /// <param name="walked">
    /// The reason of the walk's first step, where the walk chose the account (<see cref="StepSaid.Why"/>). An account over its
    /// curve passed, or the most room under it, is the usage curve's move; a cooling or refused account passed, a limit reached
    /// and the rest are not.
    /// </param>
    public static ContinuedFrom? ContinuesFrom(
        HandedQuest? handed, PriorSession? carried, string? profile, string? walked = null, bool retried = false, bool pressed = false,
        string? agent = null, Action<LineCourse>? decided = null, CoursePolicy? policy = null) =>
        carried is null ? null : DrivenCourse.From(handed, carried, profile, walked, retried, pressed, agent, decided, policy);
}
