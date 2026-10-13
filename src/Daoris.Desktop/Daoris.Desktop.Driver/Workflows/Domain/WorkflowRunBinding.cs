namespace Daoris.Driver;

/// <summary>
/// A run bound to the workflow that chose it, at its first start in its repository on this machine (WORKFLOW1e, D157 point 11,
/// the workflow design §4.5, §5.1): the workflow and its version, or Current and the graph it drew; the level that chose it,
/// the kind with its label and paths, and when. Kept in <c>&lt;home&gt;/workflows/runs/&lt;run&gt;.json</c>, written once
/// (<see cref="WorkflowRunBindings"/>, the runs' use cases; STRUCT3b).
/// </summary>
/// <param name="Run">The run's id: its chain's first quest in the repository, which names it on this machine.</param>
/// <param name="Workflow">The workflow's id, or <c>current</c>.</param>
/// <param name="Level">One of <see cref="WorkflowLevels"/>: which level of §4.1's table chose it.</param>
public sealed record WorkflowRunBinding(string Run, string Repository, string Workspace, string Workflow, string Level, DateTimeOffset At)
{
    /// <summary>The ask the chain was asked by, whose choice is the task's; null for none.</summary>
    public string? Ask { get; init; }

    /// <summary>The named workflow's newest version when the run started, which the run keeps; null under Current.</summary>
    public int? Version { get; init; }

    /// <summary>That version's digest; under Current, the version Current drew (its digest too).</summary>
    public string? Digest { get; init; }

    /// <summary>The task's kind, where its workspace declares it.</summary>
    public string? Kind { get; init; }

    /// <summary>The kind's label as it was declared when the run started.</summary>
    public string? Label { get; init; }

    /// <summary>The kind's paths as they were declared when the run started: what WORKFLOW1f checks the changed paths against.</summary>
    public IReadOnlyList<string> Paths { get; init; } = [];

    /// <summary>A kind the task named that its workspace did not declare, read as none.</summary>
    public string? Undeclared { get; init; }

    /// <summary>Why the named workflow chosen could not be read here when the run started: its file gone, or its newest version unreadable.</summary>
    public string? Problem { get; init; }

    /// <summary>Under Current, its steps as <see cref="WorkflowCurrent.ToJson"/> wrote them at the start, for the trace (design §2.6).</summary>
    public IReadOnlyList<string> CurrentSteps { get; init; } = [];

    /// <summary>
    /// The person's <i>Keep</i> (WORKFLOW1f, design §4.4): they kept the kind's workflow for this work though it changed paths outside
    /// the kind's, with when, their words and the door. Null where none was given, which every binding from before reads as.
    /// </summary>
    public WorkflowKept? Kept { get; init; }

    /// <summary>Whether the run follows Current, which every gate reads live, as today (design §2.6).</summary>
    public bool IsCurrent => Workflow == WorkflowSelection.Current;

    /// <summary>
    /// Which level chose a run's workflow, in words (design §4.1): <i>chosen by this repository for Documentation</i>. Static, so
    /// the record's shape on the wire is as it was (STRUCT3b).
    /// </summary>
    public static string Why(WorkflowRunBinding binding)
    {
        var kind = binding.Label ?? binding.Kind;
        return binding.Level switch
        {
            WorkflowLevels.Task => $"chosen by its ask{(binding.Ask is { } ask ? $" `#{ask}`" : "")}",
            WorkflowLevels.RepositoryKind => $"chosen by `{binding.Repository}` for {kind}",
            WorkflowLevels.Repository => $"`{binding.Repository}`'s default",
            WorkflowLevels.WorkspaceKind => $"chosen by the workspace `{binding.Workspace}` for {kind}",
            WorkflowLevels.Workspace => $"the workspace `{binding.Workspace}`'s default",
            _ => "since nothing names a workflow",
        };
    }
}

/// <summary>The person's say-so that a run keeps its workflow (WORKFLOW1f, design §4.4): when, at which door, and their words.</summary>
/// <param name="Door"><c>terminal</c> or <c>screen</c> (<see cref="ReviewDoors"/>).</param>
public sealed record WorkflowKept(DateTimeOffset At, string Door)
{
    public string? Words { get; init; }
}
