using System.Text.Json;
using Daoris.Desktop.Driver.Tests;

namespace Daoris.Driver.Tests;

/// <summary>
/// The sentences Ask Daoris's landing, helpers and commits cards say as a rule is set are
/// <c>daoris place landing</c>'s, <c>helpers</c>' and <c>commits</c>' words, held to one table, the CLI's
/// <c>fixtures/landing-says.json</c>, which holds the one writer to them.
/// </summary>
public sealed class LandingSaysTwinTests : HelpProposalsFixture
{
    private static JsonDocument Table() => JsonDocument.Parse(System.IO.File.ReadAllText(
        Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "landing-says.json")));

    private string Said(string door, string value) =>
        HelpProposals.Plan(Setting(door, "engine", null, value), DriverConfig.Empty, Facts).Describe;

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
