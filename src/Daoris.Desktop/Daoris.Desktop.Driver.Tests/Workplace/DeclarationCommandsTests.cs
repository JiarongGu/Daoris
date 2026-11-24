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
    [InlineData("\"memoryGb\": 4", "\"memoryGb\": 4, \"_why\": \"the build's peak\"")]
    public void A_change_of_patterns_words_timeouts_or_test_sets_keeps_the_acceptance(string from, string to)
    {
        Assert.True(DeclarationCommands.Keeps(Accepted, Edited(from, to)));
    }

    /// <summary>The verification budget is a workspace's rule the driver never reads, unlike the runner `verification` names, which waits.</summary>
    [Fact]
    public void A_verification_budget_added_keeps_the_acceptance_and_a_verification_runner_waits()
    {
        Assert.True(DeclarationCommands.Keeps(Accepted, Edited("\"integration\":", "\"verifyBudget\": { \"budgetMinutes\": 10 },\n  \"integration\":")));
        Assert.False(DeclarationCommands.Keeps(Accepted, Edited("\"integration\":", "\"verification\": { \"run\": \"node verify.mjs\" },\n  \"integration\":")));
    }

    /// <summary>A text the strict reader refuses (a member written twice, a comment, a trailing comma) is no declaration: it waits for the person and never throws.</summary>
    [Theory]
    [InlineData("\"run\": \"npm test\"", "\"run\": \"npm test\", \"run\": \"npm test --all\"")]
    [InlineData("\"_why\": \"the checks\",", "\"_why\": \"the checks\", // said again\n")]
    [InlineData("\"cwd\": \"tools\" }", "\"cwd\": \"tools\", }")]
    public void A_text_the_strict_reader_refuses_waits_for_the_person_and_never_throws(string from, string to)
    {
        var now = Edited(from, to);
        Assert.NotEqual(Accepted, now);
        Assert.False(DeclarationCommands.Keeps(Accepted, now));
        Assert.True(DeclarationCommands.Keeps(now, now));
    }

    /// <summary>Words are set aside only at the top and directly inside a check: a member named with <c>_</c> deeper, inside a part that grants, waits.</summary>
    [Fact]
    public void A_change_inside_a_nested_part_waits_whatever_its_name()
    {
        Assert.False(DeclarationCommands.Keeps(Accepted, Edited("\"by\": \"rows\"", "\"by\": \"rows\", \"_by\": \"daoris\"")));
    }

    /// <summary>What a keep set aside, by check and part, so the keep can say it in plain words.</summary>
    [Fact]
    public void A_keep_names_the_parts_it_set_aside()
    {
        var now = Edited("\"src/\"", "\"src/\", \"tests/\"").Replace("\"memoryGb\": 4", "\"memoryGb\": 6", StringComparison.Ordinal)
            .Replace("\"the checks\"", "\"the checks, said again\"", StringComparison.Ordinal).Replace("\"tools/*.test.mjs\"", "\"tools/**\"", StringComparison.Ordinal);

        Assert.Equal(
            [new DeclarationSetAside(null, DeclarationSetAside.Words), new DeclarationSetAside(null, DeclarationSetAside.Tests),
             new DeclarationSetAside("unit", DeclarationSetAside.Paths), new DeclarationSetAside("unit", DeclarationSetAside.Memory)],
            DeclarationCommands.SetAside(Accepted, now));
        Assert.Equal("the words, the test sets, the paths of `unit` and the memory of `unit`", DeclarationSetAside.Said(DeclarationCommands.SetAside(Accepted, now)));
        Assert.Empty(DeclarationCommands.SetAside(Accepted, Accepted));
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

    [Fact]
    public void A_command_change_waits_for_the_owner_under_restricted_access_and_is_taken_under_full_access()
    {
        static AccessEdit Permissions(string value) => new() { Repository = "engine", Set = [new(AccessRules.Permissions, value)] };
        var machine = DriverConfig.Empty.WithAccess(new AccessEdit { Machine = true, Full = true });

        Assert.Equal(DeclarationMode.Restricted, DeclarationMode.Of(DriverConfig.Empty, "engine", null));
        Assert.Equal(DeclarationMode.Restricted, DeclarationMode.Of(DriverConfig.Empty.WithAccess(Permissions(AccessRules.Auto)), "engine", null));
        Assert.Equal(DeclarationMode.Restricted, DeclarationMode.Of(machine.WithAccess(Permissions(AccessRules.Asking)), "engine", null));
        Assert.Equal(DeclarationMode.Full, DeclarationMode.Of(DriverConfig.Empty.WithAccess(Permissions(AccessRules.Unrestricted)), "engine", null));
        Assert.Equal(DeclarationMode.Full, DeclarationMode.Of(machine, "engine", "aurora"));
        Assert.Equal(DeclarationMode.Full, DeclarationMode.Of(DriverConfig.Empty.WithAccess(new AccessEdit { Repository = "Engine", Full = true }), "engine", null));

        Assert.True(DeclarationMode.Waits(DriverConfig.Empty, "engine", null));
        Assert.False(DeclarationMode.Waits(machine, "engine", null));
        Assert.Equal("the risk policy passes it: taken without asking", DeclarationMode.Said(DeclarationMode.Full));
        Assert.Equal("the risk policy asks for it: waits for you", DeclarationMode.Said(DeclarationMode.Restricted));
    }

    /// <summary>The mode is the judge's verdict on <c>command.change</c> at the place, not the access setup.</summary>
    [Fact]
    public void The_mode_is_the_verdict_on_command_change_at_the_place_not_the_access_setup()
    {
        static DriverConfig With(DriverConfig config, string risk)
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(config.ToJson())!.AsObject();
            node["risk"] = System.Text.Json.Nodes.JsonNode.Parse(risk);
            return DriverConfig.Parse(node.ToJsonString());
        }

        var restricted = DriverConfig.Empty;
        var full = DriverConfig.Empty.WithAccess(new AccessEdit { Repository = "engine", Full = true });

        Assert.Equal(DeclarationMode.Full, DeclarationMode.Of(With(restricted, """{ "repository/engine": [ { "kind": "command.change", "class": "none" } ] }"""), "engine", null));
        Assert.Equal(DeclarationMode.Full, DeclarationMode.Of(With(restricted, """{ "machine": [ { "kind": "command.change", "class": "none", "mode": "restricted" } ] }"""), "engine", null));
        Assert.Equal(DeclarationMode.Restricted, DeclarationMode.Of(With(full, """{ "workspace/aurora": [ { "kind": "command.change", "class": "owner" } ] }"""), "engine", "aurora"));
        Assert.Equal(DeclarationMode.Restricted, DeclarationMode.Of(With(full, """{ "repository/engine": [ { "kind": "command.change", "class": "role" } ] }"""), "engine", null));
        Assert.Equal(DeclarationMode.Full, DeclarationMode.Of(With(full, """{ "repository/other": [ { "kind": "command.change", "class": "owner" } ] }"""), "engine", null));
        Assert.Equal(DeclarationMode.Restricted, DeclarationMode.Of(With(full, """{ "nowhere": [] }"""), "engine", null));
    }
}
