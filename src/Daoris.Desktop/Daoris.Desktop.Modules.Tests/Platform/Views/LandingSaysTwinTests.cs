using System.Text.Json;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The sentences the helpers and commits cards say of a mode are the words <c>daoris place helpers</c> and <c>commits</c> say as a rule is
/// set, held to one table (<c>fixtures/landing-says.json</c>): the card's are its views' catalogue words, the terminal's the driver's.
/// </summary>
public sealed class LandingSaysTwinTests
{
    private static JsonElement Read(params string[] path) => JsonDocument.Parse(File.ReadAllText(
        Path.Combine([WorkspaceRoot.Folder, .. path]))).RootElement;

    private static readonly JsonElement Table = Read("src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "landing-says.json");

    private static readonly JsonElement English = Read("src", "Daoris.Web", "src", "locales", "en", "person.json");

    private static IEnumerable<(string Mode, string Says)> Rows(string rule) =>
        Table.GetProperty(rule).EnumerateArray().Select(row => (row.GetProperty("mode").GetString()!, row.GetProperty("says").GetString()!));

    [Fact]
    public void The_helpers_and_commits_mode_sentences_are_the_tables_row_for_row_in_the_views_words_and_the_drivers()
    {
        Assert.Equal(["ask", "on", "off"], Rows("helpers").Select(row => row.Mode));
        Assert.Equal(["as-you-go", "ask", "at-end"], Rows("commits").Select(row => row.Mode));
        foreach (var (mode, says) in Rows("helpers"))
        {
            Assert.Equal(says, English.GetProperty($"person.view.helpers-mode-{mode}").GetString());
            Assert.Equal(says, HelperRules.Says(mode));
        }

        foreach (var (mode, says) in Rows("commits"))
        {
            Assert.Equal(says, English.GetProperty($"person.view.commits-mode-{mode}").GetString());
            Assert.Equal(says, CommitRules.Says(mode));
        }
    }
}
