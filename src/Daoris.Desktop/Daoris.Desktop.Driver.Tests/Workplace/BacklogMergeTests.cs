using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDBACKLOG1 (D87's LANDBACKLOG1 note): the declared backlog and its merge by the merge base, the rule a landing's replay settles a
/// backlog-only conflict by, a twin of the merge tool's (MERGETASKS1). Held cell for cell to ONE table, <c>fixtures/backlog-merges.json</c>,
/// which <c>merge-branch.test.ts</c> holds the merge tool's <c>backlogOf</c> and <c>mergeByBase</c> to, row for row; and what the
/// landing says of a backlog it settled. The replay itself is <c>LandBacklogLandingTests</c>' (Process). Reads text only.
/// </summary>
public sealed class BacklogMergeTests
{
    private static JsonDocument Table() => JsonDocument.Parse(File.ReadAllText(Path.Combine(
        WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "backlog-merges.json")));

    public static TheoryData<string, string?, string?> PathRows()
    {
        var data = new TheoryData<string, string?, string?>();
        using var table = Table();
        foreach (var row in table.RootElement.GetProperty("paths").EnumerateArray())
        {
            data.Add(row.GetProperty("name").GetString()!, row.GetProperty("manifest").GetString(), row.GetProperty("backlog").GetString());
        }

        return data;
    }

    /// <summary>INTEGRATE1c: the declared row tables, each row's tables joined by a newline (the theory data keeps scalars).</summary>
    public static TheoryData<string, string?, string> RowTableRows()
    {
        var data = new TheoryData<string, string?, string>();
        using var table = Table();
        foreach (var row in table.RootElement.GetProperty("rows").EnumerateArray())
        {
            data.Add(
                row.GetProperty("name").GetString()!,
                row.GetProperty("declaration").GetString(),
                string.Join('\n', row.GetProperty("tables").EnumerateArray().Select(path => path.GetString())));
        }

        return data;
    }

    public static TheoryData<string, string> MergeRows()
    {
        var data = new TheoryData<string, string>();
        using var table = Table();
        foreach (var row in table.RootElement.GetProperty("merges").EnumerateArray())
        {
            data.Add(row.GetProperty("name").GetString()!, row.GetRawText());
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(PathRows))]
    public void The_declared_backlog_reads_as_the_shared_table_says(string name, string? manifest, string? backlog) =>
        Assert.True(backlog == BacklogMerge.PathOf(manifest), $"{name}: read {BacklogMerge.PathOf(manifest) ?? "null"}");

    [Theory]
    [MemberData(nameof(RowTableRows))]
    public void The_declared_row_tables_read_as_the_shared_table_says(string name, string? declaration, string tables) =>
        Assert.True(tables == string.Join('\n', BacklogMerge.RowTablesOf(declaration)), $"{name}: read {string.Join(", ", BacklogMerge.RowTablesOf(declaration))}");

    [Fact]
    public void A_replay_settles_the_backlog_and_each_declared_table_and_never_one_it_was_not_told()
    {
        var declared = new BacklogMerge.Declared("TASKS.md", ["docs/development.md"]);
        Assert.True(declared.Settles("TASKS.md"));
        Assert.True(declared.Settles("docs/development.md"));
        Assert.False(declared.Settles("docs/README.md"));
        Assert.False(new BacklogMerge.Declared(null, []).Settles("TASKS.md"));
    }

    [Theory]
    [MemberData(nameof(MergeRows))]
    public void The_backlog_merges_by_its_base_as_the_shared_table_says(string name, string row)
    {
        using var document = JsonDocument.Parse(row);
        var cells = document.RootElement;

        var merged = BacklogMerge.ByBase(Lines(cells, "base")!, Lines(cells, "ours")!, Lines(cells, "theirs")!);

        Assert.True(Lines(cells, "text") == merged.Text, $"{name}: merged to {merged.Text ?? "null"}");
        Assert.Equal(cells.GetProperty("kept").GetInt32(), merged.Kept);
        Assert.Equal(cells.GetProperty("dropped").GetInt32(), merged.Dropped);
        Assert.Equal(cells.GetProperty("both").EnumerateArray().Select(line => line.GetInt32()), merged.Both);
    }

    /// <summary>Both tables are read whole: a table that lost its rows would pass every theory above.</summary>
    [Fact]
    public void The_table_holds_its_rows()
    {
        Assert.True(PathRows().Count() >= 10);
        Assert.True(RowTableRows().Count() >= 10);
        Assert.True(MergeRows().Count() >= 11);
    }

    [Fact]
    public void The_landing_says_a_declared_row_table_it_settled_by_the_merge_base()
    {
        Assert.Equal(
            " The declared row table `docs/development.md` conflicted with the line's and was settled by the merge base: 2 line(s) a side added kept, "
            + "0 line(s) a side removed dropped.",
            LandingSyncWords.Table("docs/development.md", 2, 0));
    }

    [Fact]
    public void The_landing_says_the_backlog_it_settled_by_the_merge_base()
    {
        Assert.Equal(
            " The backlog `TASKS.md` conflicted with the line's and was settled by the merge base: 1 line(s) a side added kept, 2 line(s) a side "
            + "removed dropped.",
            LandingSyncWords.Backlog("TASKS.md", 1, 2));
        Assert.Equal("", LandingSyncWords.Backlog(null, 0, 0));
    }

    /// <summary>A row's lines joined by LF, as the merge tool's test joins them; null where the cell is null.</summary>
    private static string? Lines(JsonElement row, string name) =>
        row.GetProperty(name) is { ValueKind: JsonValueKind.Array } lines ? string.Join('\n', lines.EnumerateArray().Select(line => line.GetString())) : null;
}
