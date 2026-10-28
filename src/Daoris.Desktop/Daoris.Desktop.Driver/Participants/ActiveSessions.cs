namespace Daoris.Driver;

public static class ActiveSessions
{
    /// <summary>A record's kind where a person's quest is planned into a session (D49 §3): what a host that answers none is read as.</summary>
    public const string DrivenKind = SessionKinds.Driven;

    /// <summary>A record's kind where a person opened a conversation, and the kind the driver's own intakes, helpers and reviewers keep (D65, D155, D163).</summary>
    public const string ChatKind = SessionKinds.Chat;

    /// <summary>
    /// Whether an active session takes a place under the machine's cap (CAPCOUNT1, D130's note): what the driver starts does, a
    /// conversation the person holds does not. The one place the cap's count is told, so every site that spends a slot (the planner's
    /// starts, a look's intakes, helpers, brainstorms and meetings) counts the same records.
    /// </summary>
    /// <remarks>
    /// <para>The driver's own are a driven quest's session and the three conversations it opens for itself: an intake
    /// (<see cref="SessionView.Ask"/>), a helper (<see cref="SessionView.Serves"/>) and a reviewer (<see cref="SessionView.Opinion"/>),
    /// each a chat at the service, serving no quest, and each told by the mark the driver's own start gave it. A conversation the person
    /// holds is a chat none of those marks names, or a record in Ask Daoris's place (<see cref="HelpRoom.Repository"/>) the same way,
    /// whatever the kind its host answers. Told by the record's kind, place and marks, never by its words.</para>
    /// <para>It spends nothing idle and is the person's to open, so a person's two conversations resting between turns held half a cap
    /// of four for hours (2026-10-20). The list keeps them: a busy tree and a repository's holder read every active session, and so do the
    /// reads that need the whole list (<see cref="Snapshot.Active"/>).</para>
    /// </remarks>
    public static bool HoldsSlot(SessionView session) =>
        ParticipantWork.HoldsSlot(ParticipantWork.Of(
            new WorkMarks(Mirror: false, session.Kind, session.Repository, session.Ask, session.Serves, session.Piece, session.Opinion)));

    /// <summary>
    /// The same rule over a record's fields as the service answers them (SESSBADGE1): what the Sessions badge and the status bar count
    /// the sessions the driver runs by, from the records every window holds, so the count a person reads is the one the cap spends.
    /// A null <paramref name="kind"/> is a host that answers none, read as driven work, as <see cref="SessionView.Kind"/> reads it.
    /// </summary>
    public static bool HoldsSlot(string? kind, string? repository, string? ask, string? serves, string? opinion) =>
        ParticipantWork.HoldsSlot(ParticipantWork.Of(new WorkMarks(Mirror: false, kind, repository, ask, serves, Piece: null, opinion)));

    /// <summary>How many of the sessions take a place under the cap (<see cref="HoldsSlot"/>).</summary>
    public static int Held(IEnumerable<SessionView> sessions) => sessions.Count(HoldsSlot);

    /// <summary>
    /// Which sessions are active and where each stands, as one string — equal when the same ones stand
    /// in the same states, whatever the order. The shell forwards a tick to the page when this changes
    /// (UX5 U13), as it does for <see cref="Asks.Signature"/>: a conversation started or ended from the
    /// main window moves nothing else a tick reports, so the other windows never heard of it.
    /// </summary>
    public static string Signature(IEnumerable<SessionView> sessions) =>
        string.Join("\n", sessions
            .Select(session => $"{session.Id}\t{session.State}")
            .OrderBy(line => line, StringComparer.Ordinal));
}
