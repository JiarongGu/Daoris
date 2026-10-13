using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An argument as a command in the driver's words prints it (ACCTQUOTE1b, D125's ACCTQUOTE1 note): bare where every shell
/// reads it as itself, in double quotes where they keep it whole in PowerShell, Command Prompt and a POSIX shell alike, and a
/// placeholder where no spelling does. The driver's half of a TWIN with the CLI's <c>shellword.ts</c> and the page's
/// <c>shellWord.ts</c>: all three read one table, the CLI's <c>test/fixtures/shell-words.json</c>, row for row, and the CLI's
/// suite holds that table against the shells themselves.
/// </summary>
public sealed class ShellWordTests
{
    /// <summary>The table, as the CLI's suite and the page's read it: each row why, the value, and how a hint prints it.</summary>
    private static readonly IReadOnlyList<(string Why, string Value, string Word)> Rows = ReadRows();

    [Fact]
    public void Each_value_is_spelled_as_the_table_spells_it()
    {
        foreach (var (why, value, word) in Rows) Assert.True(ShellWord.Of(value, "<name>") == word, $"{why}: {ShellWord.Of(value, "<name>")}");
        Assert.Contains(Rows, row => row.Word == "<name>");
        Assert.Contains(Rows, row => row.Word != "<name>");
    }

    [Fact]
    public void Several_values_are_spelled_one_by_one_a_space_between()
    {
        Assert.Equal("work \"my team\" <account>", ShellWord.Words(["work", "my team", "R&D"], ShellWord.Account));
        Assert.Equal("", ShellWord.Words([], ShellWord.Account));
    }

    [Fact]
    public void A_letter_beyond_the_basic_plane_is_a_letter_as_the_cli_reads_it()
    {
        // The CLI reads by code point; a letter written as two UTF-16 units is still one letter, so bare.
        Assert.Equal("𠀀work", ShellWord.Of("𠀀work", ShellWord.Name));
    }

    [Fact]
    public void The_placeholders_are_the_cli_s()
    {
        Assert.Equal("<account>", ShellWord.Account);
        Assert.Equal("<workspace>", ShellWord.Workspace);
        Assert.Equal("<name>", ShellWord.Name);
    }

    private static IReadOnlyList<(string, string, string)> ReadRows()
    {
        using var table = JsonDocument.Parse(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Cli", "test", "fixtures", "shell-words.json")));
        return [.. table.RootElement.EnumerateArray().Select(row => (row[0].GetString()!, row[1].GetString()!, row[2].GetString()!))];
    }
}
