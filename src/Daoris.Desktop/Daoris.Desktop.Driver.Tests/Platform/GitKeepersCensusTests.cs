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

    private static readonly Regex Answer = new(@"\bStdout\b|\bGitObjectAnswer\b|byte\[\]\s+Bytes\b|\bGitFacts\b|\bRefSnapshot\b|\bGitReadOnce\b|\bGitReadCache\b|\bGitAskerContext\b");

    /// <summary>Each holder, with what keeps its answer true.</summary>
    private static readonly Dictionary<string, string> Holders = new(StringComparer.Ordinal)
    {
        ["BranchesByDate.cs Kept"] = "its stamp: the refs' version (CountedRefs) read before and after the listing, keyed by the common directory",
        ["GitReadOnce.cs Current"] = "the read-only flow's listings, one pass's; dropped by any git that may move a ref",
        ["GitRuns.cs Answered"] = "the tests' stand-in answer, an async local null in production",
        ["GitFacts.cs Current"] = "the flow's facts, one judgement's; dropped by any git that may move a ref (GitFacts.Ran)",
        ["GitFacts.cs _asked"] = "a join of a running read within one flow, asked under the flow's token and keyed by the asker's context",
        ["GitReadOnce.cs _asked"] = "a join of a running read within one read-only flow, keyed by the asker's context",
        ["RefSnapshot.cs Held"] = "the flow's listings, one landing's; dropped by any git that may move a ref",
        ["RefSnapshot.cs _facts"] = "a join of a running read within one flow, keyed by the asker's context",
    };

    private static SortedSet<string> Found(IEnumerable<(string File, string Source)> sources) => SourceCensus.Fields(sources, Answer);

    [Fact]
    public void Every_field_holding_a_git_answer_is_a_listed_holder_with_its_stamp_or_its_flow()
    {
        var problems = SourceCensus.Problems(Found(SourceCensus.Driver()), Holders.Keys, What);
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    [Fact]
    public void Each_holder_left_out_of_the_list_turns_the_census_red_naming_it()
    {
        Assert.Empty(SourceCensus.Unproved(Found(SourceCensus.Driver()), Holders.Keys, What));
    }

    [Fact]
    public void A_planted_field_of_a_git_answers_type_turns_the_census_red_naming_it()
    {
        var planted = SourceCensus.Driver().Append(("Planted.cs",
            "internal static class Planted\n{\n    private static readonly Dictionary<string, (int Code, string Stdout, string Stderr)> s_kept = new();\n}\n"));

        Assert.Equal(["Planted.cs s_kept holds a git answer and is not listed"], SourceCensus.Problems(Found(planted), Holders.Keys, What));
    }
}
