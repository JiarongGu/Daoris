using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The state class's census: every field of the driver whose type holds a git's answer (the runners' answer, an object's, or a memo of
/// them) is a listed holder, said with what keeps its answer true: the stamp its key carries, or the one flow it lives for. Shown red on a
/// holder left out of the list and on a planted field of an answer's type.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class GitKeepersCensusTests
{
    private const string What = "holds a git answer";

    private static readonly Regex Answer = new(@"\bStdout\b|\bGitObjectAnswer\b|byte\[\]\s+Bytes\b|\bGitFacts\b|\bRefSnapshot\b|\bGitReadOnce\b|\bGitReadCache\b|\bGitStamp\b");

    private static readonly Dictionary<string, string> Holders = new(StringComparer.Ordinal)
    {
        ["BranchesByDate.cs _answer"] = "its stamp: the refs' version (CountedRefs) read before and after the listing, its listing kept by the "
            + "common directory; one listing in flight shared by every walk asking at that version",
        ["GitReads.cs _cache"] = "its stamp: each answer keyed by the asker's GitStamp and kept only where the stamp's fence held across the read, "
            + "and only an answer git read from objects alone (a graph page, a commit's fields, a compare's commits)",
        ["GitReadOnce.cs Current"] = "the read-only flow's listings, one pass's; dropped by any git that may move a ref",
        ["GitFacts.cs Current"] = "the flow's facts, one judgement's; dropped by any git that may move a ref (GitFacts.Ran)",
        ["GitFacts.cs _asked"] = "a join of a running read within one flow, asked under the flow's token and keyed by the asker's context",
        ["GitReadOnce.cs _asked"] = "a join of a running read within one read-only flow, keyed by the asker's context",
        ["AddressedReads.cs _text"] = "a join of a running read of a commit's files by id within one act, keyed by the asker's folder",
        ["AddressedReads.cs _bytes"] = "a join of a running read of objects by id within one act, keyed by the asker's folder and question",
        ["RefSnapshot.cs Held"] = "the flow's listings, one landing's; dropped by any git that may move a ref",
        ["RefSnapshot.cs _facts"] = "a join of a running read within one flow, keyed by the asker's context",
    };

    // The door class: where git is asked of its scheduler or the library's reads, and where an ask is built.
    private const string Reaches = "reaches git's scheduler or the library's reads";

    // The only way to make an ask the scheduler takes is `new GitRunAsk(`, which `For` and `Plain` call, so it covers them. A raw
    // helper process of a door program (GitDoors.cs) is a direct start the process-starts census names with its reason.
    private static readonly Regex Door = new(@"\bGitScheduler\.Shared\b|\bnew\s+GitScheduler\s*\(|\bnew\s+GitRunAsk\s*\(|\bGitLibraryReads\.AnswerAsync\s*\(|\bGitLibrary\.AskAsync\s*\(");

    private static readonly HashSet<string> Library = new(StringComparer.Ordinal)
    {
        "GitLibrary.cs", "GitLibraryCall.cs", "GitLibraryFaults.cs", "GitLibraryNames.cs", "GitLibraryReads.cs", "GitLibraryRefs.cs",
        "GitLibraryRevisions.cs", "GitLibraryWalks.cs",
    };

    private static readonly Dictionary<string, string> Doors = new(StringComparer.Ordinal)
    {
        ["GitService.cs"] = "the one service: every git ask is placed here",
        ["GitServiceReads.cs"] = "the service's in-process half (an extension of it): every read of the library is answered here",
        ["GitRunAsk.cs"] = "the ask's own record and its factory for a start no caller builds whole (a version's question)",
        ["GitCallers.cs"] = "which place a process's ancestor holds, told to a re-entry the service places: it asks no git",
        ["GitLibraryReads.cs"] = "the library's own door: it opens a call for a read the service asked",
        ["GitLibraryRefs.cs"] = "the library's own refs read, opening a call within a read the service asked",
        ["GitStartAsks.cs"] = "builds the ask a start places under (For, and Plain for a start not built whole): it asks no git",
    };

    private static SortedSet<string> Found(IEnumerable<(string File, string Source)> sources) => SourceCensus.Fields(sources, Answer);

    private static SortedSet<string> Reached(IEnumerable<(string File, string Source)> sources) => SourceCensus.Callers(sources, Door);

    // Spelt in two parts so this census's own text is not a site of the layers census.
    private const string Package = "LibGit2" + "Sharp";

    private static List<string> Packaged(IEnumerable<(string File, string Source)> sources) =>
        [.. SourceCensus.Callers(sources, new Regex($@"\b{Package}\b")).Where(file => !Library.Contains(file)).Select(file => $"{file} names {Package} outside the library's own files")];

    [Fact]
    public void No_caller_reaches_the_scheduler_or_the_librarys_reads_outside_the_service_and_the_files_listed()
    {
        var problems = SourceCensus.Problems(Reached(SourceCensus.Driver(typeof(GitKeepersCensusTests))), SourceCensus.Listed(typeof(GitKeepersCensusTests), Doors.Keys), Reaches);
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    [Fact]
    public void Each_door_left_out_of_the_list_turns_the_census_red_naming_it()
    {
        Assert.Empty(SourceCensus.Unproved(Reached(SourceCensus.Driver(typeof(GitKeepersCensusTests))), SourceCensus.Listed(typeof(GitKeepersCensusTests), Doors.Keys), Reaches));
    }

    [Fact]
    public void A_planted_run_on_the_scheduler_or_read_of_the_library_turns_the_census_red_naming_it()
    {
        var planted = SourceCensus.Driver(typeof(GitKeepersCensusTests)).Append(("Planted.cs",
            "internal static class Planted\n{\n    public static Task<int> Go(GitRunAsk ask) => GitScheduler.Shared.RunAsync(ask, _ => Task.FromResult(1), default);\n}\n"))
            .Append(("PlantedRead.cs", "internal static class PlantedRead\n{\n    public static Task Go() => GitLibraryReads.AnswerAsync(\"x\", [], default);\n}\n"));

        Assert.Equal([$"Planted.cs {Reaches} and is not listed", $"PlantedRead.cs {Reaches} and is not listed"],
            SourceCensus.Problems(Reached(planted), SourceCensus.Listed(typeof(GitKeepersCensusTests), Doors.Keys), Reaches));
    }

    [Fact]
    public void Only_the_librarys_own_files_name_its_package_and_a_planted_one_turns_it_red()
    {
        Assert.Empty(Packaged(SourceCensus.Driver(typeof(GitKeepersCensusTests))));
        Assert.Equal([$"Planted.cs names {Package} outside the library's own files"], Packaged([("Planted.cs", $"using {Package};\ninternal static class Planted {{ }}\n")]));
    }

    [Fact]
    public void Every_field_holding_a_git_answer_is_a_listed_holder_with_its_stamp_or_its_flow()
    {
        var problems = SourceCensus.Problems(Found(SourceCensus.Driver(typeof(GitKeepersCensusTests))), SourceCensus.Listed(typeof(GitKeepersCensusTests), Holders.Keys), What);
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    [Fact]
    public void Each_holder_left_out_of_the_list_turns_the_census_red_naming_it()
    {
        Assert.Empty(SourceCensus.Unproved(Found(SourceCensus.Driver(typeof(GitKeepersCensusTests))), SourceCensus.Listed(typeof(GitKeepersCensusTests), Holders.Keys), What));
    }

    [Fact]
    public void A_planted_field_of_a_git_answers_type_turns_the_census_red_naming_it()
    {
        var planted = SourceCensus.Driver(typeof(GitKeepersCensusTests)).Append(("Planted.cs",
            "internal static class Planted\n{\n    private static readonly Dictionary<string, (int Code, string Stdout, string Stderr)> s_kept = new();\n}\n"));

        Assert.Equal(["Planted.cs s_kept holds a git answer and is not listed"], SourceCensus.Problems(Found(planted), SourceCensus.Listed(typeof(GitKeepersCensusTests), Holders.Keys), What));
    }
}
