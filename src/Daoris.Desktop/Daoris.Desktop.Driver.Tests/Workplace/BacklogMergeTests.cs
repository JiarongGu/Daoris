using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The declared backlog and its merge by the merge base, the rule a landing's replay settles a
/// backlog-only conflict by, a twin of the merge tool's. Held cell for cell to ONE table, <c>fixtures/backlog-merges.json</c>,
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

    /// <summary>The declared row tables, each row's tables joined by a newline (the theory data keeps scalars).</summary>
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
    public void The_backlog_merges_by_its_rows_and_a_table_by_its_base_as_the_shared_table_says(string name, string row)
    {
        using var document = JsonDocument.Parse(row);
        var cells = document.RootElement;
        var (b, o, t) = (Lines(cells, "base")!, Lines(cells, "ours")!, Lines(cells, "theirs")!);

        var merged = cells.TryGetProperty("table", out var table) && table.GetBoolean() ? BacklogMerge.ByBase(b, o, t) : BacklogRows.Merge(b, o, t);

        Assert.True(Lines(cells, "text") == merged.Text, $"{name}: merged to {merged.Text ?? "null"}");
        Assert.Equal(cells.GetProperty("kept").GetInt32(), merged.Kept);
        Assert.Equal(cells.GetProperty("dropped").GetInt32(), merged.Dropped);
        Assert.Equal(cells.GetProperty("both").EnumerateArray().Select(line => line.GetInt32()), merged.Both);
        Assert.Equal(
            Cells(cells, "differ").Select(each => new BacklogRows.Differs(each.GetProperty("id").GetString()!, each.GetProperty("ours").GetString(), each.GetProperty("theirs").GetString())),
            merged.Differ);
        Assert.Equal(Cells(cells, "twice").Select(id => id.GetString()), merged.Twice);
        Assert.Equal(Cells(cells, "unread").Select(line => line.GetString()), merged.Unread);
    }

    public static TheoryData<string, string, string?, string?> IdRows()
    {
        var data = new TheoryData<string, string, string?, string?>();
        using var table = Table();
        foreach (var row in table.RootElement.GetProperty("ids").EnumerateArray())
        {
            var ids = row.GetProperty("ids");
            data.Add(row.GetProperty("name").GetString()!, row.GetProperty("line").GetString()!, row.GetProperty("key").GetString(),
                ids.ValueKind == JsonValueKind.Null ? null : string.Join('\n', ids.EnumerateArray().Select(id => id.GetString())));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(IdRows))]
    public void A_rows_ids_read_as_the_shared_table_says(string name, string line, string? key, string? ids)
    {
        var read = BacklogIds.RowOf(line);

        Assert.True(key == read?.Key, $"{name}: keyed {read?.Key ?? "null"}");
        Assert.True(ids == (read?.Ids is { } covered ? string.Join('\n', covered) : null), $"{name}: covers {(read?.Ids is { } all ? string.Join(", ", all) : "null")}");
    }

    public static TheoryData<string, string> HeldRows()
    {
        var data = new TheoryData<string, string>();
        using var table = Table();
        foreach (var row in table.RootElement.GetProperty("held").EnumerateArray()) data.Add(row.GetProperty("name").GetString()!, row.GetRawText());
        return data;
    }

    [Theory]
    [MemberData(nameof(HeldRows))]
    public void A_made_backlog_holds_what_its_rows_keep_as_the_shared_table_says(string name, string row)
    {
        using var document = JsonDocument.Parse(row);
        var cells = document.RootElement;

        var held = BacklogHeld.Of(Lines(cells, "base")!, Lines(cells, "ours")!, Lines(cells, "theirs")!, Lines(cells, "made")!);

        Assert.Equal(Cells(cells, "twice").Select(id => id.GetString()), held.Twice);
        Assert.Equal(Cells(cells, "kept").Select(id => id.GetString()), held.Kept);
        Assert.Equal(Cells(cells, "lost").Select(id => id.GetString()), held.Lost);
        Assert.True(cells.GetProperty("refused").GetBoolean() == held.Refused is not null, name);
    }

    /// <summary>The tables are read whole: a table that lost its rows would pass every theory above.</summary>
    [Fact]
    public void The_table_holds_its_rows()
    {
        Assert.True(PathRows().Count() >= 10);
        Assert.True(RowTableRows().Count() >= 10);
        Assert.True(MergeRows().Count() >= 25);
        Assert.True(IdRows().Count() >= 15);
        Assert.True(HeldRows().Count() >= 5);
    }

    [Fact]
    public void A_refused_backlog_says_each_row_left_with_both_texts_and_each_id_twice()
    {
        var merged = new BacklogMerge.Merged(null, 0, 0, [4])
        {
            Differ = [new BacklogRows.Differs("A2", "- [ ] **A2** two, then B.\r", null)],
            Twice = ["A1"],
        };

        Assert.Equal(
            " `TASKS.md` holds the row `A1` twice on a side, so none of it was settled by its rows."
            + " `TASKS.md`'s row `A2` changed on both sides and is left for a person: the line has “- [ ] **A2** two, then B.”, and the work removed it.",
            BacklogRows.Refusal("TASKS.md", merged));
        Assert.Equal(
            " The replay would keep `TASKS.md`'s row `A2`, which one side removed while the other only moved it.",
            BacklogHeld.Refusal("TASKS.md", new BacklogHeld.Held([], ["A2"], [], null)));
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

    /// <summary>A row's list cell, none where the row has no such cell.</summary>
    private static IEnumerable<JsonElement> Cells(JsonElement row, string name) =>
        row.TryGetProperty(name, out var cell) ? [.. cell.EnumerateArray()] : [];

    /// <summary>A row's lines joined by LF, as the merge tool's test joins them; null where the cell is null.</summary>
    private static string? Lines(JsonElement row, string name) =>
        row.GetProperty(name) is { ValueKind: JsonValueKind.Array } lines ? string.Join('\n', lines.EnumerateArray().Select(line => line.GetString())) : null;
}
