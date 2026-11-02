using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// REVIEW2 T12: the CLI's usage line for <c>driver role</c> is held to the driver's own verb, so the one golden of the usage carries the fact
/// and a word the driver adds or drops fails here rather than in a text no test compares.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class RoleVerbUsageTests
{
    [Fact]
    public void The_cli_usage_names_the_words_the_role_verb_changes_the_catalogue_by()
    {
        var usage = File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Cli", "src", "cli", "driver.ts"));

        Assert.Contains($"role [<id>] · role {string.Join('|', RoleVerb.Changes)} <id> …", usage, StringComparison.Ordinal);
    }

    [Fact]
    public void Each_word_after_role_is_taken_by_the_verb_and_any_other_is_its_grammar()
    {
        Assert.Equal(["set", "remove", "default", "policy"], RoleVerb.Changes);
        var verb = DriverChoiceVerbs.All.OfType<RoleVerb>().Single();
        var world = new DriverChoicesWorld
        {
            Verbs = DriverChoiceVerbs.All,
            Instance = new InstanceWorld { Home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "role-verb-usage-" + Guid.NewGuid().ToString("N")[..8]), Environment = _ => null, Out = new StringWriter(), Error = new StringWriter(), Verb = "daoris" },
            ConfigPath = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "role-verb-usage-config.json"),
        };

        var refused = Assert.Throws<ChoiceWords>(() => verb.Plan(["role", "frobnicate", "x"], DriverConfig.Empty, world));
        Assert.Contains("driver role set <id>", refused.Message, StringComparison.Ordinal);
    }
}
