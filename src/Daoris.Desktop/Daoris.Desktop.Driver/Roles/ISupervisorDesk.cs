namespace Daoris.Driver;

/// <summary>
/// A subject an agent-held supervisor's hold covers: its kind and typed reference, the repository it waits in, the role and its
/// generation, and the kind's acts the role's grants cover there.
/// </summary>
public sealed record SupervisorWaiting(string Kind, SupervisorSubject Subject, string Repository, string Generation, IReadOnlyList<string> Acts)
{
    /// <summary>The role the generation names.</summary>
    public string Role => AgentHold.RoleOf(Generation) ?? "";

    /// <summary>A referred point's count, value, rule and the rule's place, as the list names them; none for a question.</summary>
    public (string Count, string N, string Rule, string Place)? Point { get; init; }
}

/// <summary>What a kept block answered: kept, or why not.</summary>
public sealed record SupervisorBlockKept(bool Kept, string? Why);

/// <summary>
/// The decision door's half an agent's look reaches: every kind's subjects an agent-held supervisor's hold covers, and the keeping of an
/// agent's block through its kind's own row, the admission, the grants and the subject read again there as for a terminal's act. The
/// window's host answers it; with none, no agent supervisor decides.
/// </summary>
public interface ISupervisorDesk
{
    Task<IReadOnlyList<SupervisorWaiting>> WaitingAsync(CancellationToken ct);

    /// <summary>The facts of a landing, allow or files subject, fenced under <paramref name="fence"/> by its kind's row; null where it no longer waits.</summary>
    Task<string?> FactsAsync(SupervisorWaiting waiting, string fence, CancellationToken ct);

    Task<SupervisorBlockKept> KeepAsync(SupervisorWaiting waiting, SupervisorBlock block, SupervisorHolder holder, CancellationToken ct);
}
