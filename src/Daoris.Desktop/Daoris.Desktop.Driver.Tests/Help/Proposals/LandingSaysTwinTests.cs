using System.Text.Json;
using Daoris.Desktop.Driver.Tests;

namespace Daoris.Driver.Tests;

/// <summary>
/// The sentences Ask Daoris's landing, helpers and commits cards say as a rule is set (TWINWORDS1, D164's DOORS1a note) are
/// <c>daoris driver landing</c>'s, <c>helpers</c>' and <c>commits</c>' words, held to one table, the CLI's
/// <c>fixtures/landing-says.json</c>, which the CLI's <c>landing-says.test.ts</c> read against its own code until the driver family's
/// TypeScript left (PUSH1f2b); <c>daoris-driver driver</c> says these words now, so the table holds the one writer to them.
/// </summary>
public sealed class LandingSaysTwinTests : HelpProposalsFixture
{
    private static JsonDocument Table() => JsonDocument.Parse(System.IO.File.ReadAllText(
        Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "landing-says.json")));

    private string Said(string door, string value) =>
        HelpProposals.Plan(Setting(door, "engine", null, value), DriverConfig.Empty, Facts).Describe;

    [Fact]
    public void The_ticket_sentence_is_the_tables_row_for_row()
    {
        using var table = Table();
        var rows = table.RootElement.GetProperty("ticket").EnumerateArray().ToList();
        Assert.Equal(2, rows.Count);
        foreach (var row in rows)
        {
            var key = row.GetProperty("key").ValueKind == JsonValueKind.Null ? "" : $" --key \"{row.GetProperty("key").GetString()}\"";
            Assert.Contains(row.GetProperty("says").GetString()!, Said("landing", "branch feature/{ticket}" + key));
        }
    }

    [Fact]
    public void The_squash_sentence_is_the_tables()
    {
        using var table = Table();
        Assert.Contains(table.RootElement.GetProperty("squash").GetString()!, Said("landing", "branch feature/{quest} --squash"));
    }

    [Fact]
    public void The_helpers_and_commits_mode_sentences_are_the_tables_row_for_row()
    {
        using var table = Table();
        var helpers = table.RootElement.GetProperty("helpers").EnumerateArray().ToList();
        var commits = table.RootElement.GetProperty("commits").EnumerateArray().ToList();
        Assert.Equal(["ask", "on", "off"], helpers.Select(row => row.GetProperty("mode").GetString()));
        Assert.Equal(["as-you-go", "ask", "at-end"], commits.Select(row => row.GetProperty("mode").GetString()));
        foreach (var row in helpers)
        {
            Assert.Contains(row.GetProperty("says").GetString()!, Said("helpers", $"--mode {row.GetProperty("mode").GetString()}"));
        }

        foreach (var row in commits)
        {
            Assert.Contains(row.GetProperty("says").GetString()!, Said("commits", $"--mode {row.GetProperty("mode").GetString()}"));
        }
    }
}
