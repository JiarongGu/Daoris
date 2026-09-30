namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The categories a run filters on (MOD8). A class that starts a real process (node, a harness stub,
/// the host's stand-in, a shell), runs the real loop, or holds a wall-clock bound on one of those
/// carries <c>[Trait(Category.Name, Category.Process)]</c>.
/// </summary>
/// <remarks>
/// The driver tests' <c>Category</c> is the same rule, with the reason. A subagent's worktree runs
/// <c>--filter Category!=Process</c>, and the parent runs
/// <c>--settings src/Daoris.Desktop/process.runsettings</c> at its merge. Those are the two gates
/// <c>daoris.gates.json</c> declares for this project. This assembly already runs one class at a time
/// (<c>Parallelism.cs</c>), so here the settings' serial run only keeps the two projects' Process
/// halves alike, and keeps this one serial if that ever changes.
/// </remarks>
internal static class Category
{
    public const string Name = "Category";

    public const string Process = "Process";
}
