using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The object reads' census: every file of the driver that asks git an object's bytes, type, size or presence, or parses such an answer,
/// is listed, and only the one question and its one parser are, so an object read is one <c>cat-file --batch</c> start read one way.
/// Shown red on a member left out and on each planted form (<c>cat-file blob</c>, <c>cat-file -e</c>, <c>show &lt;rev&gt;:&lt;path&gt;</c>,
/// <c>ls-tree -l</c>) and on a second parser of the answer.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class GitObjectFormsCensusTests
{
    private const string What = "reads an object or its answer";

    // `cat-file` asked a type, an option or a batch of its own, `show` of an interpolated `<rev>:<path>`, `ls-tree` asked sizes, and
    // `--batch` or an answer's `missing` anywhere: what git writes back for an object, read as text.
    private static readonly Regex Forms = new(
        """
        "cat-file"\s*,\s*"(?:blob|commit|tree|tag|-[a-z]|--[a-z-]+)"|"show"\s*,\s*\$"[^"]*:|"ls-tree"[^\]\n]*"(?:-l|--long)"|"--batch(?:-check|-command)?"|" missing"
        """.Trim());

    private static readonly Dictionary<string, string> Readers = new(StringComparer.Ordinal)
    {
        ["GitObjectReads.cs"] = "the one question: one start of `cat-file --batch` asked one line",
        ["GitObjectAnswer.cs"] = "the one parser of its answer: the header, the bytes, or `missing`",
        ["GitLazyFetch.cs"] = "the proof: `cat-file -t` of an id nothing holds, read for whether git fetched and never for an answer",
    };

    private static SortedSet<string> Found(IEnumerable<(string File, string Source)> sources) => SourceCensus.Callers(sources, Forms);

    [Fact]
    public void Every_object_read_is_the_one_question_or_listed()
    {
        var problems = SourceCensus.Problems(Found(SourceCensus.Driver()), Readers.Keys, What);
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    [Fact]
    public void Each_reader_left_out_of_the_list_turns_the_census_red_naming_it()
    {
        Assert.Empty(SourceCensus.Unproved(Found(SourceCensus.Driver()), Readers.Keys, What));
    }

    [Theory]
    [InlineData("""WorkingTree.GitAsync(root, ["cat-file", "blob", blob], ct)""")]
    [InlineData("""WorkingTree.GitAsync(root, ["cat-file", "-e", $"{tip}^{{commit}}"], ct)""")]
    [InlineData("""WorkingTree.GitAsync(tree, ["show", $"{commit}:{path}"], ct)""")]
    [InlineData("""WorkingTree.GitAsync(root, ["ls-tree", "-l", "-z", tip, "--", path], ct)""")]
    [InlineData("""WorkingTree.GitBytesAsync(root, ["cat-file", "--batch"], 4096, id + "\n", ct)""")]
    [InlineData("""header.EndsWith(" missing", StringComparison.Ordinal)""")]
    public void A_planted_form_turns_the_census_red_naming_it(string form)
    {
        var planted = SourceCensus.Driver().Append(("Planted.cs", $"internal static class Planted\n{{\n    public static object Read(string root) => {form};\n}}\n"));

        Assert.Equal(["Planted.cs reads an object or its answer and is not listed"], SourceCensus.Problems(Found(planted), Readers.Keys, What));
    }
}
