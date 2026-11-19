namespace Daoris.Driver;

/// <summary>
/// One act journaled on a run's step as the run's view draws it: the fact its receipt names, its state, its words, its credit
/// and page, when, and where it waits on another role, that role and its holder: <i>waiting on &lt;role&gt;: &lt;holder&gt;</i>.
/// </summary>
public sealed record WorkflowRunAct(string Fact, string State, string? Message, string? Url, string? By, DateTimeOffset At)
{
    public string? WaitsOn { get; init; }

    /// <summary>The holder of <see cref="WaitsOn"/> as the place casts it, or null where it waits on none.</summary>
    public string? Holder { get; init; }
}

/// <summary>The rows the run's view draws under a step: each act journaled on its attempt, oldest first. Pure.</summary>
public static class StepActViews
{
    /// <param name="journal">The run's journal (<see cref="StepActFiles.Journal"/>).</param>
    /// <param name="cast">The run's place's cast, role to holder; null where unread.</param>
    public static IReadOnlyList<WorkflowRunAct>? Of(IReadOnlyList<StepActRow> journal, string node, int attempt, Func<string, string>? cast)
    {
        var rows = journal.Where(row => row.Node == node && row.Attempt == attempt)
            .Select(row => new WorkflowRunAct(row.Fact, row.State, row.Message, row.Url, row.By, row.At)
            {
                WaitsOn = row.WaitsOn, Holder = row.WaitsOn is { } role ? cast?.Invoke(role) : null,
            })
            .ToList();
        return rows.Count == 0 ? null : rows;
    }
}
