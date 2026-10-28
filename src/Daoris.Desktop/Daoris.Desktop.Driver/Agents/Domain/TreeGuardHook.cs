namespace Daoris.Driver;

/// <summary>What one session's installed tree guard is: the script the hook runs, and the tree it guards.</summary>
public sealed record TreeGuardHook(string Script, string Tree)
{
    /// <summary>The tools that name a file, which it judged alone before TREESHELL1: a session started then carries this matcher.</summary>
    public const string EditMatcher = "Edit|Write|MultiEdit|NotebookEdit";

    /// <summary>The tools it judges — exact alternatives, the harness's own matcher shape.</summary>
    public const string Matcher = EditMatcher + "|Bash|PowerShell";

    /// <summary>The declared write targets, each one more argument after the tree. Empty is the tree alone.</summary>
    public IReadOnlyList<string> Also { get; init; } = [];

    /// <summary>The node Tools resolves, by its whole path; the bare name only where the system's node is on no PATH.</summary>
    public string Node { get; init; } = "node";
}
