namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The categories a run filters on (MOD8). A class that starts a real process (git, node, a harness
/// stub, a hook, the host, a pseudo console), runs a real tick, or holds a wall-clock bound on one of
/// those carries <c>[Trait(Category.Name, Category.Process)]</c>.
/// </summary>
/// <remarks>
/// 🔴 These are the tests another worktree's load fails (FLAKE1): each such class passed alone and
/// failed once while other builds shared the machine. So a subagent's worktree runs the rest, with
/// <c>--filter Category!=Process</c>, and the parent runs these at its merge one class at a time, with
/// <c>--settings src/Daoris.Desktop/process.runsettings</c>. Those are the two gates
/// <c>daoris.gates.json</c> declares for this project.
///
/// The trait goes on the class, never on one test: the class is the unit xUnit runs in parallel and
/// the one whose folder the process holds, and the cleanup that failed under load was a class's. A
/// class that only reads and writes files, or talks to an in-process stand-in, is not one. That half
/// has to stay fast and whole, because it is all a subagent runs.
///
/// A look's scheduling can stay in that half (DEV3): the real client over <c>StandInLedger</c>, an
/// in-process handler for the service's doors, and a driver or watch handed <c>StandInRuns</c> as its
/// <c>Runner</c>, which opens and moves records the way a start's run does and spawns nothing.
/// <c>SessionsOutliveTheirLookTests</c> is the exemplar; its real-stub twin stays in this category.
/// </remarks>
internal static class Category
{
    public const string Name = "Category";

    public const string Process = "Process";
}
