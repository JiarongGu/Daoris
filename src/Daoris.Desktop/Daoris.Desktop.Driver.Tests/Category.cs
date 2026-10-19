namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The categories a run filters on (MOD8). A class that starts a real process (git, node, a harness
/// stub, a hook, the host, a pseudo console), runs a real tick, or holds a wall-clock bound on one of
/// those carries <c>[Trait(Category.Name, Category.Process)]</c>.
/// </summary>
/// <remarks>
/// 🔴 These are the tests another worktree's load fails (FLAKE1): each such class passed alone and
/// failed once while other builds shared the machine. So a subagent's worktree runs the rest, with
/// <c>--filter Category!=Process</c>, and the parent runs these in its full set, with
/// <c>--settings src/Daoris.Desktop/process.runsettings</c> and <c>git.process.runsettings</c> beside it. Those
/// are the three gates <c>daoris.gates.json</c> declares for this project.
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
///
/// The Process half runs as two (PROC1b): a Process class that starts git and nothing else that waits on a short
/// bound also carries <c>[Trait(Category.Parallel, Category.Git)]</c>, and those classes run four at once in one
/// host (<c>git.process.runsettings</c>, which says why four), the rest several at once too (<c>process.runsettings</c>,
/// PROCPAR1), but for the classes in <see cref="Alone"/>'s collection or <see cref="ProcessPath"/>'s, which run alone after
/// them: a class that sets the process's environment or measures the whole process joins one (<c>CategoryTests</c> holds
/// it), and so does one whose claim is a bound of a few seconds on a real process.
/// A class joins only when it holds no wall-clock bound a neighbour's load could break (a node hook under a
/// landing's default patience may; three seconds may not, so <c>LandingPluginPatienceTests</c> is a class of its own,
/// PROC1f), starts no stub, tick, host or
/// listener, and neither it nor its base sets or reads a variable of the process's environment, which the classes
/// beside it share: <c>CategoryTests</c> holds the trait and the environment. The longest class is the run's wall,
/// so a long one is split, as <c>TreeSyncTests</c> was.
/// </remarks>
internal static class Category
{
    public const string Name = "Category";

    public const string Process = "Process";

    /// <summary>The second trait's name: how a Process class may run beside others (PROC1b).</summary>
    public const string Parallel = "Parallel";

    /// <summary>Starts git and nothing a neighbour's load could fail: runs four at once with the others so marked.</summary>
    public const string Git = "Git";
}
