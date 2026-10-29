using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1c: the joins a union merge leaves (a note label or an archive heading glued to the line above) and the rule that sets them
/// apart, the driver's twin of the merge tool's <c>setApart</c> and <c>setApartHeadings</c> (MERGEJOIN1, UNIONGLUE1). Held cell for cell to
/// ONE table, <c>fixtures/union-joins.json</c>, which <c>merge-branch.test.ts</c> holds the merge tool to, row for row. The replay that
/// sets them apart is <c>LandBacklogLandingTests</c>' (Process). Reads text only.
/// </summary>
public sealed class UnionJoinsTests
{
    private static JsonDocument Table() => JsonDocument.Parse(File.ReadAllText(Path.Combine(
        WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "union-joins.json")));

    public static TheoryData<string, string> JoinRows()
    {
        var data = new TheoryData<string, string>();
        using var table = Table();
        foreach (var row in table.RootElement.GetProperty("joins").EnumerateArray())
        {
            data.Add(row.GetProperty("name").GetString()!, row.GetRawText());
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(JoinRows))]
    public void The_joins_are_set_apart_as_the_shared_table_says(string name, string row)
    {
        using var document = JsonDocument.Parse(row);
        var cells = document.RootElement;
        var text = Lines(cells.GetProperty("text"));

        var (made, set) = cells.GetProperty("kind").GetString() == "labels"
            ? UnionJoins.SetApart(text)
            : UnionJoins.SetApartHeadings(text, cells.GetProperty("sides").EnumerateArray().Select(Lines).ToList());

        Assert.True(Lines(cells.GetProperty("result")) == made, $"{name}: made {made}");
        Assert.Equal(
            cells.GetProperty("set").EnumerateArray().Select(each => (each.GetProperty("line").GetInt32(), each.GetProperty("label").GetString()!)),
            set);
    }

    /// <summary>The table is read whole: one that lost its rows would pass every theory above.</summary>
    [Fact]
    public void The_table_holds_its_rows()
    {
        Assert.True(JoinRows().Count() >= 10);
        using var table = Table();
        var kinds = table.RootElement.GetProperty("joins").EnumerateArray().Select(row => row.GetProperty("kind").GetString()).ToHashSet();
        Assert.Contains("labels", kinds);
        Assert.Contains("headings", kinds);
    }

    [Fact]
    public void The_files_a_replay_reads_are_the_attributes_union_patterns_and_the_declared_decisions_folder()
    {
        var patterns = UnionJoins.UnionPatternsOf(
            "# comment\n* text=auto eol=lf\nCHANGELOG.md merge=union\ndocs/decisions/*.md merge=union\ndocs/README.md merge=rows\nindex/** -merge\n#x merge=union\n");
        Assert.Equal(["CHANGELOG.md", "docs/decisions/*.md", "docs/README.md"], patterns);
        var matches = patterns.Select(UnionJoins.PatternOf).ToList();
        Assert.Contains(matches, pattern => pattern.IsMatch("docs/decisions/D1.md"));
        Assert.DoesNotContain(matches, pattern => pattern.IsMatch("docs/decisions/sub/D1.md"));
        Assert.Contains(matches, pattern => pattern.IsMatch("CHANGELOG.md"));
        Assert.Contains(matches, pattern => pattern.IsMatch("src/pack/CHANGELOG.md"));
        Assert.Equal("docs/decisions", UnionJoins.DecisionsOf("{ \"documents\": { \"decisions\": \"docs\\\\decisions/\" } }"));
        Assert.Equal("docs/decisions", UnionJoins.DecisionsOf("{ \"documents\": { \"decisions\": { \"path\": \"docs/decisions\" } } }"));
        Assert.Null(UnionJoins.DecisionsOf("{ \"documents\": { } }"));
        Assert.Null(UnionJoins.DecisionsOf("not json"));
        Assert.Null(UnionJoins.DecisionsOf(null));
    }

    /// <summary>A row's lines joined by LF, as the merge tool's test joins them; a line keeps its CR.</summary>
    private static string Lines(JsonElement lines) => string.Join('\n', lines.EnumerateArray().Select(line => line.GetString()));
}
