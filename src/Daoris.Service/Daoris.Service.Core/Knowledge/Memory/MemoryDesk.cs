namespace Daoris.Knowledge;

/// <summary>Why a decision through a memory row was refused.</summary>
public enum MemoryDecisionRefusal
{
    None,
    NotFound,
    Own,
    NotThePerson,
    Running,
}

/// <summary>A decision's outcome: the row as decided, or why none was made; for a Keep or Drop, how many rows moved.</summary>
public sealed record MemoryDecisionOutcome(MemoryDecisionRefusal Refusal, string Message, MemoryOp? Op = null, int Moved = 0);

/// <summary>
/// What the person decides through a memory row (KNOWCENTRAL2d; the memory coordinator design §1, §3, §4): an awaiting end, approved or
/// declined, kept on its row as a go-ahead is; an ended session's staged rows, kept or dropped. Until a second opinion may approve (MC8),
/// the person is every end's approver, and a session of the proposer's lineage is refused by name.
/// </summary>
public sealed class MemoryDesk(MemoryOpStore ops)
{
    /// <summary>The approver a row names when the person decided it.</summary>
    public const string Person = "person";

    /// <summary>
    /// An awaiting end decided: by the person where <paramref name="session"/> is null, else refused, its proposer's lineage as its own.
    /// </summary>
    public async Task<MemoryDecisionOutcome> DecideAsync(string id, string? session, bool approve, DateTimeOffset now, CancellationToken ct = default)
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

        return await ops.DecideAsync(id, Person, approve, now, ct).ConfigureAwait(false) is { } decided
            ? new(MemoryDecisionRefusal.None, approve ? $"The end `{id}` is approved and applies next." : $"The end `{id}` is declined.", decided)
            : new(MemoryDecisionRefusal.NotFound, $"No end awaits its approver as operation `{id}`. Nothing was decided.");
    }

    /// <summary>An ended session's staged rows, and those of the sessions it continues, kept to apply or dropped.</summary>
    public async Task<MemoryDecisionOutcome> KeepAsync(string session, bool keep, DateTimeOffset now, CancellationToken ct = default) =>
        await ops.KeepStagedAsync(session, keep, now, ct).ConfigureAwait(false) is { } moved
            ? new(MemoryDecisionRefusal.None, keep ? $"{moved} staged change(s) of session `{session}` kept." : $"{moved} staged change(s) of session `{session}` dropped.", Moved: moved)
            : new(MemoryDecisionRefusal.Running,
                $"Refused: session `{session}` is running or unknown here, and its changes are its own until it ends. Nothing was moved.");

    /// <summary>A session's rows as its record's page shows them: an end's reason as its proposer wrote it.</summary>
    public async Task<IReadOnlyList<MemoryOp>> PageAsync(string session, CancellationToken ct = default) =>
        [.. (await ops.OfSessionAsync(session, ct).ConfigureAwait(false))
            .Select(op => op.Change.Text is { } text ? op with { Change = op.Change with { Text = MemoryEndToken.Strip(text) } } : op)];
}
