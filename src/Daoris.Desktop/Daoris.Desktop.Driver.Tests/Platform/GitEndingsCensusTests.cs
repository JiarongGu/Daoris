using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The ends class's census: every file of the git service's code (the trees' and the platform's) that ends a process early, or cancels a
/// placed git's ending, is listed with what it may end, so no act is ended mid-write by an end nobody listed. Shown red on an end left out
/// of the list and on a planted <c>OwnedProcesses.End</c>.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class GitEndingsCensusTests
{
    private const string What = "ends a process early";

    private static readonly Regex Ends = new(@"\bOwnedProcesses\.End\s*\(|\.Kill\s*\(|\bChildProcess\.EndAsync\s*\(|\bEnding\.Cancel\s*\(|\bPoolGit\.EndAsync\s*\(");

    private static readonly Dictionary<string, string> Endings = new(StringComparer.Ordinal)
    {
        ["ActReconciler.cs"] = "an act's step found by its pid alone after its owner died, reconciled first: the Git job's end with Daoris",
        ["ChildProcess.cs"] = "a git past its kind's bound or its caller's token, its tree ended and its exit awaited",
        ["GitDoors.cs"] = "the door's own ssh, ended once its answer is read; no git of the scheduler's",
        ["GitScheduler.cs"] = "an endable read whose askers all left, or inside a tree being removed",
        ["OwnedProcesses.cs"] = "the one management every end above goes through",
        ["PoolTakeovers.cs"] = "a dead run's pool claim, its job emptied before the claim is taken over",
    };

    private static bool GitsCode(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}Workplace{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}Platform{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static SortedSet<string> Found(IEnumerable<(string File, string Source)> sources) => SourceCensus.Callers(sources, Ends);

    [Fact]
    public void Every_early_end_is_listed_with_what_it_may_end()
    {
        var problems = SourceCensus.Problems(Found(SourceCensus.Driver(GitsCode)), Endings.Keys, What);
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    [Fact]
    public void Each_end_left_out_of_the_list_turns_the_census_red_naming_it()
    {
        Assert.Empty(SourceCensus.Unproved(Found(SourceCensus.Driver(GitsCode)), Endings.Keys, What));
    }

    [Fact]
    public void A_planted_end_turns_the_census_red_naming_it()
    {
        var planted = SourceCensus.Driver(GitsCode).Append(("Planted.cs", "internal static class Planted\n{\n    public static void Stop(Process git) => OwnedProcesses.End(git);\n}\n"));

        Assert.Equal(["Planted.cs ends a process early and is not listed"], SourceCensus.Problems(Found(planted), Endings.Keys, What));
    }
}
