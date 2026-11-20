using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Whether a declaration runs nothing the one accepted does not: a change of patterns, words, timeouts, <c>_why</c> or test sets keeps
/// the acceptance; a command, a folder, a check added or removed, or any part the rule does not name as inert does not.
/// </summary>
public sealed class DeclarationCommandsTests
{
    private const string Accepted = """
        {
          "_why": "the checks",
          "gates": [
            { "name": "unit", "run": "dotnet test", "paths": ["src/"], "timeoutMinutes": 10, "timeoutWhy": "p90 x 1.5", "memoryGb": 4 },
            { "name": "tools", "run": "npm test", "cwd": "tools" }
          ],
          "tests": { "_why": "sets", "sets": [ { "name": "a", "gate": "tools", "files": ["tools/*.test.mjs"] } ] },
          "integration": { "merge": [ { "paths": "docs/README.md", "by": "rows" } ] }
        }
        """;

    private static string Edited(string from, string to) => Accepted.Replace(from, to, StringComparison.Ordinal);

    [Theory]
    [InlineData("\"src/\"", "\"src/\", \"tests/\"")]
    [InlineData("\"timeoutMinutes\": 10", "\"timeoutMinutes\": 20")]
    [InlineData("\"p90 x 1.5\"", "\"p90 x 2\"")]
    [InlineData("\"memoryGb\": 4", "\"memoryGb\": 6")]
    [InlineData("\"the checks\"", "\"the checks, said again\"")]
    [InlineData("\"tools/*.test.mjs\"", "\"tools/**/*.test.mjs\"")]
    [InlineData("\"name\": \"a\", \"gate\": \"tools\"", "\"name\": \"b\", \"gate\": \"tools\"")]
    public void A_change_of_patterns_words_timeouts_or_test_sets_keeps_the_acceptance(string from, string to)
    {
        Assert.True(DeclarationCommands.Keeps(Accepted, Edited(from, to)));
    }

    [Theory]
    [InlineData("\"dotnet test\"", "\"dotnet test --no-build\"")]
    [InlineData("\"cwd\": \"tools\"", "\"cwd\": \"src\"")]
    [InlineData("{ \"name\": \"tools\", \"run\": \"npm test\", \"cwd\": \"tools\" }", "{ \"name\": \"tools\", \"run\": \"npm test\", \"cwd\": \"tools\" }, { \"name\": \"e2e\", \"run\": \"npm run e2e\" }")]
    [InlineData("\"by\": \"rows\"", "\"by\": \"daoris\"")]
    [InlineData("\"_why\": \"the checks\",", "\"_why\": \"the checks\", \"generated\": [ { \"paths\": \"a.json\", \"run\": \"node a.mjs\" } ],")]
    [InlineData("\"_why\": \"the checks\",", "\"_why\": \"the checks\", \"unasked\": { \"may\": [\"push\"] },")]
    [InlineData("\"_why\": \"the checks\",", "\"_why\": \"the checks\", \"anything\": 1,")]
    public void A_command_a_folder_a_check_added_or_removed_or_any_other_part_waits_for_the_person(string from, string to)
    {
        var now = Edited(from, to);
        Assert.NotEqual(Accepted, now);
        Assert.False(DeclarationCommands.Keeps(Accepted, now));
    }

    [Fact]
    public void A_check_removed_or_reordered_waits_for_the_person()
    {
        const string Two = """{ "gates": [ { "name": "a", "run": "one" }, { "name": "b", "run": "two" } ] }""";
        Assert.False(DeclarationCommands.Keeps(Two, """{ "gates": [ { "name": "a", "run": "one" } ] }"""));
        Assert.False(DeclarationCommands.Keeps(Two, """{ "gates": [ { "name": "b", "run": "two" }, { "name": "a", "run": "one" } ] }"""));
    }

    [Fact]
    public void Nothing_accepted_or_a_text_that_does_not_read_keeps_nothing_and_the_same_text_keeps_itself()
    {
        Assert.False(DeclarationCommands.Keeps(null, Accepted));
        Assert.False(DeclarationCommands.Keeps(Accepted, null));
        Assert.False(DeclarationCommands.Keeps(Accepted, "{ not json"));
        Assert.True(DeclarationCommands.Keeps(null, null));
        Assert.True(DeclarationCommands.Keeps("{ not json", "{ not json"));
        Assert.True(DeclarationCommands.Keeps(Accepted, Accepted));
    }
}
