namespace Daoris.Driver;

/// <summary>
/// A run bound to the workflow that chose it, at its first start in its repository on this machine: the workflow and its version, the level that chose it, the kind with its label and paths, and
/// when. Kept in <c>&lt;home&gt;/workflows/runs/&lt;run&gt;.json</c>, written once (<see cref="WorkflowRunBindings"/>, the runs' use
/// cases).
/// </summary>
/// <param name="Run">The run's id: its chain's first quest in the repository, which names it on this machine.</param>
/// <param name="Workflow">The workflow's id, or <c>current</c> where none is chosen.</param>
/// <param name="Level">One of <see cref="WorkflowLevels"/>: which level chose it.</param>
public sealed record WorkflowRunBinding(string Run, string Repository, string Workspace, string Workflow, string Level, DateTimeOffset At)
{
    /// <summary>The ask that asked the chain, whose choice is the task's; null for none.</summary>
    public string? Ask { get; init; }

    /// <summary>The workflow's newest version when the run started, which the run keeps; null where none is chosen.</summary>
    public int? Version { get; init; }

    /// <summary>That version's digest.</summary>
    public string? Digest { get; init; }

    /// <summary>The task's kind, where its workspace declares it.</summary>
    public string? Kind { get; init; }

    /// <summary>The kind's label as declared when the run started.</summary>
    public string? Label { get; init; }

    /// <summary>The kind's paths as declared when the run started: what the gate checks the changed paths against.</summary>
    public IReadOnlyList<string> Paths { get; init; } = [];

    /// <summary>A kind the task named that its workspace did not declare, read as none.</summary>
    public string? Undeclared { get; init; }

    /// <summary>Why the workflow chosen could not be read here when the run started, or that none is chosen: the run is held.</summary>
    public string? Problem { get; init; }

    /// <summary>The same problem as a code and its values, for the page to word where the bind could say it that way; null where it did not, or on a binding read from its file.</summary>
    public BindingProblem? Coded { get; init; }

    /// <summary>This binding held on <paramref name="problem"/>: its words and its code.</summary>
    public WorkflowRunBinding Held(BindingProblem problem) => this with { Problem = problem.Said, Coded = problem };

    /// <summary>
    /// The open options of the version's <c>land</c> node (<c>pattern</c>, <c>plugin</c> and the ticket <c>key</c>) as the bind resolved them
    /// Empty for a version with no <c>land</c> or none left open.
    /// </summary>
    public IReadOnlyDictionary<string, string> Land { get; init; } = LandNodes.NoResolved;

    /// <summary>
    /// The person's <i>Keep</i>: they kept the kind's workflow for this work though it changed paths outside
    /// the kind's, with when, their words and the door. Null where none is given.
    /// </summary>
    public WorkflowKept? Kept { get; init; }

    /// <summary>
    /// The role each agent step of the version names, in the graph's order, as the catalogue read it at the bind. Empty for a
    /// version whose steps name none, whose steps take their work's own role.
    /// </summary>
    public IReadOnlyList<WorkflowStepRole> Roles { get; init; } = [];

    /// <summary>The role the version's first step of <paramref name="kind"/> names; null where it names none.</summary>
    public WorkflowStepRole? RoleOf(string kind) => Roles.FirstOrDefault(each => each.Kind == kind);

    /// <summary>Whether the run is bound with no workflow chosen (<c>current</c>): nothing runs or lands it.</summary>
    public bool IsCurrent => Workflow == WorkflowSelection.Current;

    /// <summary>
    /// Which level chose a run's workflow, in words: <i>chosen by this repository for Documentation</i>. Static, so
    /// the record's shape on the wire is unchanged.
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
            WorkflowLevels.Home => "this machine's default",
            _ => "since nothing names a workflow",
        };
    }
}

/// <summary>
/// The role an agent step names, kept on its run's binding: the step's node and kind (<c>claimed</c> or <c>opinion</c>), the
/// role's id as the version names it, and the revision of its record the catalogue held at the bind.
/// </summary>
public sealed record WorkflowStepRole(string Step, string Kind, string Role, long Revision);

/// <summary>The person's say-so that a run keeps its workflow: when, at which door, and their words.</summary>
/// <param name="Door"><c>terminal</c> or <c>screen</c> (<see cref="ReviewDoors"/>).</param>
public sealed record WorkflowKept(DateTimeOffset At, string Door)
{
    public string? Words { get; init; }
}
