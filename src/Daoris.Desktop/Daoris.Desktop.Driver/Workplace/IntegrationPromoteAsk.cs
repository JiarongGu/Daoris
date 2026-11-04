namespace Daoris.Driver;

/// <summary>
/// What one run of the verdict, the promotion and the settle asks: the repository, its workspace, its registered checkout
/// (where git is asked of the line, and the line's own checkout is found), the line by name, the person's choices the accepted declaration is
/// read through, and the Daoris home whose admission the gates take their places in.
/// </summary>
internal sealed record IntegrationPromoteAsk(string Repository, string Workspace, string Root, string Line, DriverConfig Config, string Home)
{
    /// <summary>Signalled when the candidate is dropped: it is handed to the verdict port, which ends its confirm run and answers <c>dropped</c>.</summary>
    public CancellationToken Drop { get; init; }

    /// <summary>
    /// Whether a steward act's appointment still stands, read again at promotion. The default reads none, so a steward's request
    /// is held there: nothing in the driver reads an appointment's liveness until the role is active.
    /// </summary>
    public IIntegrationAppointments Appointments { get; init; } = IntegrationAppointments.Unread;

    /// <summary>
    /// The landing's own clean-up, through which the tidy step removes a landed branch the line now holds; null where none was
    /// handed in, and the tidy then removes nothing. Never a delete of its own.
    /// </summary>
    public IntegrationTidy? Tidy { get; init; }

    /// <summary>
    /// The ledger's sessions and their trees (<see cref="BranchTidying.HoldersAsync"/>), asked inside the checkout's hold, so the move waits
    /// for a session working in the checkout that has the line out; null asks nothing and moves as before.
    /// </summary>
    public Func<CancellationToken, Task<IReadOnlyList<TreeHolder>>>? Beside { get; init; }
}
