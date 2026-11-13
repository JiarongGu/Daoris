using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Each declared deployable's release target, keyed by deployable: the release node whose values (a repository's <c>workflowValues</c>)
/// name it, and the earliest act that deploys into it, <c>push</c> where undeclared. One repository with two deployables on one build has
/// two release nodes; a deployable no node names has its target undeclared; a node naming a deployable the manifest does not declare is
/// said. Daoris reads only the deployable and <c>deploysOn</c>: the target's own ids are the plugin's to resolve.
/// </summary>
public sealed class DeployableTargetsTests
{
    private const string Config = """
        {"workflowValues":{"schema-owner":{"deliver":{
          "release-migration":{"deployable":"config-migration","pipeline":7,"releaseDefinition":50,"stage":501,"action":"start-stage","deploysOn":"start"},
          "release-api":{"deployable":"config-api","pipeline":7,"releaseDefinition":50,"stage":502,"action":"observe"},
          "pull-request":{"autoComplete":true}}}}}
        """;

    private static WorkflowValuesKey.Values? ValuesOf(string json, string repository) =>
        DriverConfigReader.Parse(json).WorkflowValues.TryGetValue(repository, out var values) ? values : null;

    [Fact]
    public void Two_deployables_on_one_build_each_have_their_own_release_node_and_an_undeclared_deploysOn_reads_as_push()
    {
        var declared = DeployableDeclaration.Read(DeployableDeclarationTests.SchemaOwner);

        var read = DeployableTargets.Judge(declared, ValuesOf(Config, "schema-owner"));

        Assert.Empty(read.Problems);
        var migration = read.Targets.Single(each => each.Deployable.Id == "config-migration");
        var api = read.Targets.Single(each => each.Deployable.Id == "config-api");
        Assert.Equal(DeployableStanding.Named, migration.Standing);
        Assert.Equal([new DeployableNode("deliver", "release-migration", "start", true)], migration.Nodes);
        Assert.Equal([new DeployableNode("deliver", "release-api", "push", false)], api.Nodes);
    }

    [Fact]
    public void A_deployable_no_release_node_names_has_its_target_undeclared()
    {
        var declared = DeployableDeclaration.Read(DeployableDeclarationTests.SchemaOwner);
        const string OneNode = """{"workflowValues":{"schema-owner":{"deliver":{"release":{"deployable":"config-api","pipeline":7}}}}}""";

        var read = DeployableTargets.Judge(declared, ValuesOf(OneNode, "schema-owner"));
        var none = DeployableTargets.Judge(declared, null);

        Assert.Equal(DeployableStanding.Undeclared, read.Targets.Single(each => each.Deployable.Id == "config-migration").Standing);
        Assert.Equal(DeployableStanding.Named, read.Targets.Single(each => each.Deployable.Id == "config-api").Standing);
        Assert.All(none.Targets, each => Assert.Equal(DeployableStanding.Undeclared, each.Standing));
    }

    [Fact]
    public void A_node_naming_an_undeclared_deployable_two_nodes_of_one_workflow_naming_one_and_an_unknown_act_are_said()
    {
        var declared = DeployableDeclaration.Read(DeployableDeclarationTests.SchemaOwner);
        const string Odd = """
            {"workflowValues":{"schema-owner":{"deliver":{
              "release-a":{"deployable":"config-api"},"release-b":{"deployable":"config-api","deploysOn":"later"},
              "release-c":{"deployable":"config-cache"}}}}}
            """;

        var read = DeployableTargets.Judge(declared, ValuesOf(Odd, "schema-owner"));

        var api = read.Targets.Single(each => each.Deployable.Id == "config-api");
        Assert.Equal(DeployableStanding.Several, api.Standing);
        Assert.Equal(["release-a", "release-b"], api.Nodes.Select(each => each.Node));
        Assert.Equal("push", api.Nodes[1].DeploysOn);
        Assert.Equal(2, read.Problems.Count);
        Assert.Contains(read.Problems, said => said.Contains("`config-cache`", StringComparison.Ordinal) && said.Contains("does not declare", StringComparison.Ordinal));
        Assert.Contains(read.Problems, said => said.Contains("`later`", StringComparison.Ordinal) && said.Contains("push, pull-request, merge or start", StringComparison.Ordinal));
    }

    [Fact]
    public void The_read_is_said_deployable_by_deployable_with_its_release_node_and_what_deploys_into_it()
    {
        var declared = DeployableDeclaration.Read(DeployableDeclarationTests.SchemaOwner, "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2");
        var read = DeployableTargets.Judge(declared, ValuesOf(Config, "schema-owner"));

        var lines = DeployablesWords.Lines("schema-owner", "main", "0123456789abcdef0123456789abcdef01234567", declared, read);

        // The whole blob, which a plan's `declared` evidence names so its staleness can be told.
        Assert.Equal("deployables  schema-owner: 2 on `main` at `0123456`, daoris.json blob `a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2`", lines[0]);
        Assert.Contains("  config-migration  paths migrations/**  provides config-schema (migrates)", lines);
        Assert.Contains("    released by deliver/release-migration, deploying on start", lines);
        Assert.Contains("  config-api  paths src/**  provides config-api (owns)  consumes config-schema (reads)", lines);
        Assert.Contains("    released by deliver/release-api, deploying on push (undeclared, so every exposing act waits)", lines);
        Assert.Equal(
            "deployables  schema-owner: none on `main` at `0123456`. Name each in daoris.json's `deployables`, and its release node in workflowValues.",
            DeployablesWords.Lines("schema-owner", "main", "0123456789abcdef0123456789abcdef01234567", DeployableDeclaration.Read("{}"), DeployableTargets.Judge(DeployableDeclaration.Read("{}"), null)).Single());
    }
}
