namespace Daoris.Driver;

/// <summary>
/// What the supervisor's quarantine reads of a look's snapshot and what it hands back (<see cref="SupervisorQuarantine"/>): this machine's
/// records of each quest's last run and of the session a reopen names, and the snapshot taken back with the sessions a held decision holds,
/// the choices a released one made, or its records cleared of the words it left out. The planner's snapshot implements it directly, so the
/// quarantine names none of the planner's types and hands the caller's own back.
/// </summary>
/// <typeparam name="TLook">The implementing snapshot, which the quarantine returns.</typeparam>
public interface ISupervisorLook<TLook> where TLook : ISupervisorLook<TLook>
{
    /// <summary>The session this machine last ran on each quest, by quest id.</summary>
    IReadOnlyDictionary<string, PriorSession> LastRun { get; }

    /// <summary>The record of this machine's that each taken quest's standing reopen names, by quest id.</summary>
    IReadOnlyDictionary<string, PriorSession> NamedByReopen { get; }

    /// <summary>The snapshot with the sessions a held decision holds and the choice each released decision made, by session.</summary>
    TLook WithSupervisor(IReadOnlySet<string> held, IReadOnlyDictionary<string, string> chose);

    /// <summary>The snapshot with these records in place of its last runs and its reopens' named records.</summary>
    TLook WithRecords(IReadOnlyDictionary<string, PriorSession> lastRun, IReadOnlyDictionary<string, PriorSession> namedByReopen);
}
