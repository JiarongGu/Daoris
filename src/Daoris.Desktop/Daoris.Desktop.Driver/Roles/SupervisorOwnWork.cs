namespace Daoris.Driver;

/// <summary>
/// No supervisor decides its own work's gate: an agent holder whose turn ran in one of the work's makers' sessions, or in a helper they started,
/// is refused <see cref="SupervisorWhy.OwnWork"/> on a landing or a departure. The session is the one the decision door's holder keeps; a
/// terminal's holder names none, and makers not read refuse nothing here, since the role's own turn is Daoris's start and never a working
/// session.
/// </summary>
public static class SupervisorOwnWork
{
    /// <summary>The run whose makers a subject's decision is read against: a landing's run, a departure's quest; null for every other kind.</summary>
    public static string? RunOf(SupervisorSubject subject) =>
        subject.LandingParts?.Run ?? (subject.Kind == SupervisorKinds.Departure ? subject.Id : null);

    /// <summary><see cref="SupervisorWhy.OwnWork"/> where <paramref name="holder"/>'s turn ran in one of <paramref name="makers"/>; else null.</summary>
    public static string? Refused(SupervisorHolder holder, IReadOnlyCollection<string>? makers) =>
        holder.Agent is { Session: { Length: > 0 } session } && makers?.Contains(session, StringComparer.OrdinalIgnoreCase) == true ? SupervisorWhy.OwnWork : null;
}
