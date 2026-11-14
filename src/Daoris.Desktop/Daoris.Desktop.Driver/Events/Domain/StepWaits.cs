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

    /// <summary>What a record's note says it waits on (the run, and the step, attempt and tip where kept), or null where it waits on no run.</summary>
    public static StepWait? From(IReadOnlyList<NotePart>? parts)
    {
        if (parts?.LastOrDefault(part => part.Code == EndNoteCodes.WaitsStep.Code)?.Values is not { } values) return null;
        string? Value(string name) => values.FirstOrDefault(value => value.Key == name).Value?.ToString() is { Length: > 0 } text ? text : null;
        return Value("run") is { } run
            ? new StepWait(run, Value("node"), int.TryParse(Value("attempt"), out var attempt) ? attempt : null, Value("tip"))
            : null;
    }
}
