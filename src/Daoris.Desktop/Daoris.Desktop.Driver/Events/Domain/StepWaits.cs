namespace Daoris.Driver;

/// <summary>
/// Whether a turn's end waits on a workflow's step, read where the turn ran: a first start from the run its bind made, a later turn from
/// the wait its record already carries, so every turn of one session waits on the same run.
/// </summary>
public static class StepWaits
{
    /// <summary>The run a start bound to a graph version, or null where it follows none (no workflow governs it, so its done ends it).</summary>
    public static StepWait? Of(WorkflowBind? bound) =>
        bound is { Run: { Length: > 0 } run, Binding: { IsCurrent: false } } ? new StepWait(run) : null;

    /// <summary>The run a record's note says it waits on, or null where it carries no wait on a run.</summary>
    public static StepWait? From(IReadOnlyList<NotePart>? parts) =>
        parts?.LastOrDefault(part => part.Code == EndNoteCodes.WaitsStep.Code)?.Values
            .FirstOrDefault(value => value.Key == "run").Value?.ToString() is { Length: > 0 } run
            ? new StepWait(run)
            : null;
}
