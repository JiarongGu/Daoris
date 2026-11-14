namespace Daoris.Driver;

/// <summary>
/// A later turn's wait on its workflow's step, from its record's wait (a first start's is <c>WorkflowBind.Waits</c>, null where no graph
/// version governs it, so its done ends it), so every turn of one session waits on the same run.
/// </summary>
public static class StepWaits
{
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
