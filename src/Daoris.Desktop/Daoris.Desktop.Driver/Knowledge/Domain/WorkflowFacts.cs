namespace Daoris.Driver;

/// <summary>
/// The named workflow a landing went by (WORKFLOW1f, the workflow design §5.5): its id, the version its run kept, the level of §4.1
/// that chose it, and the task's kind. Kept on the landing record beside the review and the opinion; absent under Current.
/// </summary>
public sealed record LandingWorkflow(string Workflow, int Version, string Level)
{
    public string? Kind { get; init; }
}

/// <summary>Which level of §4.1's table chose a workflow, as a code a door words and a run keeps (D143).</summary>
public static class WorkflowLevels
{
    /// <summary>Row 1: the task's own choice, its ask's.</summary>
    public const string Task = "task";

    /// <summary>Row 2: the repository's choice for the task's kind.</summary>
    public const string RepositoryKind = "repository-kind";

    /// <summary>Row 3: the repository's default, Current where a repository that chooses names none.</summary>
    public const string Repository = "repository";

    /// <summary>Row 4: the workspace's choice for the task's kind.</summary>
    public const string WorkspaceKind = "workspace-kind";

    /// <summary>Row 5: the workspace's default.</summary>
    public const string Workspace = "workspace";

    /// <summary>Row 6: nothing names a workflow, so Current.</summary>
    public const string Current = "current";

    /// <summary>The repository's landed route (FABRIC-PIPELINE1a), its <c>current</c> keeping its workspace's away.</summary>
    public const string RepositoryLanded = "repository-landed";

    /// <summary>The workspace's landed route, where the repository names none.</summary>
    public const string WorkspaceLanded = "workspace-landed";
}
