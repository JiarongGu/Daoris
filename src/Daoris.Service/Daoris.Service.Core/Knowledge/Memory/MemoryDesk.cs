namespace Daoris.Knowledge;

/// <summary>Why a decision through a memory row is refused.</summary>
public enum MemoryDecisionRefusal
{
    None,
    NotFound,
    Own,
    NotThePerson,
    Running,
}

/// <summary>A decision's outcome: the row as decided, or why none is made; for a Keep or Drop, how many rows moved.</summary>
public sealed record MemoryDecisionOutcome(MemoryDecisionRefusal Refusal, string Message, MemoryOp? Op = null, int Moved = 0);

/// <summary>
/// What the person decides through a memory row: an awaiting end, approved or
/// declined, kept on its row as a go-ahead is; an ended session's staged rows, kept or dropped. Until a second opinion may approve,
/// the person is every end's approver, and a session of the proposer's lineage is refused by name.
/// </summary>
public sealed class MemoryDesk(MemoryOpStore ops)
{
    /// <summary>The approver a row names when the person decided it.</summary>
    public const string Person = "person";

    /// <summary>
    /// The approver a row names: the person, or an appointed role and the agent that held it (<c>role:&lt;role&gt; · &lt;agent&gt;</c>), whose
    /// answer the driver posts with the key through the role's form of the door.
    /// </summary>
    public static string ApproverOf(string? role, string? agent) =>
        role is not { Length: > 0 } ? Person : agent is { Length: > 0 } ? $"role:{role} · {agent}" : $"role:{role}";

    /// <summary>
    /// An awaiting end decided: by <paramref name="approver"/> (the person, or an appointed role) where <paramref name="session"/> is null,
    /// else refused, its proposer's lineage as its own.
    /// </summary>
    public async Task<MemoryDecisionOutcome> DecideAsync(
        string id, string? session, bool approve, DateTimeOffset now, CancellationToken ct = default, string approver = Person)
    {
        if (await ops.FindAsync(id, ct).ConfigureAwait(false) is not { State: MemoryOpState.Awaiting } awaiting || !MemoryOpKind.Ends(awaiting.Change.Kind))
        {
            return new(MemoryDecisionRefusal.NotFound, $"No end awaits its approver as operation `{id}`. Nothing was decided.");
        }

        if (session is not null)
        {
            return awaiting.By?.Session is { } proposer && await ops.InLineageAsync(session, proposer, ct).ConfigureAwait(false)
                ? new(MemoryDecisionRefusal.Own,
                    $"Refused: operation `{id}` is its own end, proposed by session `{proposer}`, and an end's approver is neither its proposer "
                    + "nor of its lineage. The person decides it. Nothing was decided.", awaiting)
                : new(MemoryDecisionRefusal.NotThePerson,
                    $"Refused: until a second opinion may approve an end, the person decides operation `{id}`. Nothing was decided.", awaiting);
        }

        var what = awaiting.Change.Kind == MemoryOpKind.Supersede ? "replacement" : "end";
        var by = approver == Person ? "" : $" by {approver}";
        return await ops.DecideAsync(id, approver, approve, now, ct).ConfigureAwait(false) is { } decided
            ? new(MemoryDecisionRefusal.None, approve ? $"The {what} `{id}` is approved{by} and applies next." : $"The {what} `{id}` is declined{by}.", decided)
            : new(MemoryDecisionRefusal.NotFound, $"No end awaits its approver as operation `{id}`. Nothing was decided.");
    }

    /// <summary>
    /// An ended session's staged rows, and those of the sessions it continues, kept to apply or dropped: by the person, or by an appointed
    /// role, which its answer's words name.
    /// </summary>
    public async Task<MemoryDecisionOutcome> KeepAsync(string session, bool keep, DateTimeOffset now, CancellationToken ct = default, string by = Person) =>
        await ops.KeepStagedAsync(session, keep, now, ct).ConfigureAwait(false) is { } moved
            ? new(MemoryDecisionRefusal.None, $"{moved} staged change(s) of session `{session}` {(keep ? "kept" : "dropped")}{(by == Person ? "" : $" by {by}")}.", Moved: moved)
            : new(MemoryDecisionRefusal.Running,
                $"Refused: session `{session}` is running or unknown here, and its changes are its own until it ends. Nothing was moved.");

    /// <summary>A session's rows as its record's page shows them: an end's reason as its proposer wrote it.</summary>
    public async Task<IReadOnlyList<MemoryOp>> PageAsync(string session, CancellationToken ct = default) =>
        [.. (await ops.OfSessionAsync(session, ct).ConfigureAwait(false))
            .Select(op => op.Change.Text is { } text ? op with { Change = op.Change with { Text = MemoryEndToken.Strip(text) } } : op)];
}
