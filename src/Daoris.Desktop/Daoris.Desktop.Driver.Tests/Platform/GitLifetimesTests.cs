using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The lifetimes class's census: every field of the git service's code (the trees' and the platform's) that keeps a process, a library
/// repository or a git's place is listed with what ends it, so no git or handle outlives the question it was started for unsaid. Shown red
/// on a keeper left out of the list and on a planted field holding a process.
/// </summary>
/// <remarks>A library call's <c>Repository</c> is matched though none is kept yet: the library comes with GITLIB1d, one call at a time.</remarks>
[Trait(Category.Name, Category.Census)]
public sealed class GitLifetimesTests
{
    private const string What = "keeps a process or a handle";

    private static readonly Regex Kept = new(@"\bProcess\b|\bRepository\b|\bGitPlace\b|\bHeldChild\b");

    /// <summary>Each keeper, with what ends what it keeps.</summary>
    private static readonly Dictionary<string, string> Keepers = new(StringComparer.Ordinal)
    {
        ["AgentJobs.cs _watch"] = "an uncontained agent start's process, disposed as it ends or as its start is disposed; never a git",
        ["GitPlace.cs s_current"] = "the flow's place, an async local: it ends with the git it was entered for",
        ["GitScheduler.cs s_beneath"] = "the place a re-entry is lent, an async local: disposed with the ask beneath it",
        ["OwnedProcesses.cs s_end"] = "the tests' stand-in kill, an async local null in production",
        ["ProcessStarts.cs _start"] = "the gate's start, which hands the process to its caller and keeps none",
        ["ProcessStarts.cs s_within"] = "the tests' stand-in start, an async local null in production",
    };

    private static bool GitsCode(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}Workplace{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}Platform{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static SortedSet<string> Found(IEnumerable<(string File, string Source)> sources) => SourceCensus.Fields(sources, Kept);

    [Fact]
    public void Every_kept_process_and_handle_is_listed_with_what_ends_it()
    {
        var problems = SourceCensus.Problems(Found(SourceCensus.Driver(GitsCode)), Keepers.Keys, What);
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    [Fact]
    public void Each_keeper_left_out_of_the_list_turns_the_census_red_naming_it()
    {
        Assert.Empty(SourceCensus.Unproved(Found(SourceCensus.Driver(GitsCode)), Keepers.Keys, What));
    }

    [Fact]
    public void A_planted_field_holding_a_process_turns_the_census_red_naming_it()
    {
        var planted = SourceCensus.Driver(GitsCode).Append(("Planted.cs", "internal sealed class Planted\n{\n    private Process? _held;\n}\n"));

        Assert.Equal(["Planted.cs _held keeps a process or a handle and is not listed"], SourceCensus.Problems(Found(planted), Keepers.Keys, What));
    }
}
