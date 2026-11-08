using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// One kind of decision as the supervisor's door takes it: its waiting subjects by their typed references, its keeping of a decide or a
/// leave against the subject as it stands, and so its release through the subject's own door. Each kind is one row of
/// <see cref="SupervisorDecisionDoor"/>, registered once; a row that lists landing, allow or files subjects composes their facts for an agent supervisor.
/// </summary>
internal interface ISupervisorKind
{
    /// <summary>The kind, one of <see cref="Daoris.Driver.SupervisorKinds"/>.</summary>
    string Kind { get; }

    /// <summary>The subjects of this kind waiting for a supervisor among <paramref name="records"/>, each with the acts its grants cover.</summary>
    Task<IReadOnlyList<object>> WaitingAsync(DriverLoop loop, IReadOnlyList<PriorSession> records, IDecisionAdmission admission, CancellationToken ct);

    /// <summary>The holder's act on its subject, judged against the subject as it stands and kept, or refused keeping nothing.</summary>
    Task<SupervisorDecided> DecideAsync(DriverLoop loop, SupervisorDecideForm form, SupervisorHolder holder, CancellationToken ct);

    /// <summary>The holder's leaving its subject to the owner, kept as <c>left</c>, or refused keeping nothing.</summary>
    Task<SupervisorDecided> LeaveAsync(DriverLoop loop, SupervisorLeaveForm form, SupervisorHolder holder, CancellationToken ct);

    Task<string?> FactsAsync(DriverLoop loop, SupervisorSubject subject, string fence, CancellationToken ct) => Task.FromResult<string?>(null);

    Task<RolePlace?> PlaceOfAsync(DriverLoop loop, SupervisorSubject subject, CancellationToken ct) => Task.FromResult<RolePlace?>(null);
}

/// <summary>
/// The door's answer: kept with the decision and when it goes on, or refused with why, keeping nothing; with the roles the holder holds
/// now and the brief's revision.
/// </summary>
public sealed record SupervisorDecided(bool Kept, string? Decision, string? GoesOnAt, string? Why)
{
    public IReadOnlyList<string> Roles { get; init; } = PipeDoor.Role?.Roles ?? [];

    public string Brief { get; init; } = RolesBrief.Revision;

    public static SupervisorDecided Refused(string why) => new(false, null, null, why);

    /// <summary>What the keeping answered, said as the door says it.</summary>
    public static SupervisorDecided Of(SupervisorKept kept, string refused) =>
        kept.Kept ? new(true, kept.Decision, kept.GoesOnAt is { } at ? Moments.Write(at) : null, null) : Refused(kept.Why ?? refused);
}
