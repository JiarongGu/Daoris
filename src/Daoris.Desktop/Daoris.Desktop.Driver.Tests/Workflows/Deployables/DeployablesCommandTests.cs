using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris repository deployables &lt;repository&gt;</c>: the deployables the repository's line declares, at its manifest's blob, with each
/// one's release node from the repository's values, against a world held in memory. It writes nothing; a value that does not fit the
/// declaration exits 1, and a repository with no checkout here exits 2.
/// </summary>
public sealed class DeployablesCommandTests
{
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    private static RegistrationStandIn World(string manifest)
    {
        var world = new RegistrationStandIn();
        world.Rows.Add(new RegistrationRow("schema-owner", "default", "/checkouts/schema-owner", true, null, [], [], [], [], false, false, []));
        world.Lines["schema-owner"] = new LineFiles("main", Commit, null) { Manifest = manifest, ManifestBlob = "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2" };
        return world;
    }

    [Fact]
    public async Task The_lines_deployables_are_said_with_their_release_nodes_and_nothing_is_sent()
    {
        var world = World(DeployableDeclarationTests.SchemaOwner);
        var config = DriverConfigReader.Parse("""{"workflowValues":{"schema-owner":{"deliver":{"release-api":{"deployable":"config-api","deploysOn":"start"}}}}}""");
        var output = new StringWriter();

        var exit = await DeployablesCommand.RunAsync("Schema-Owner", output, world, config);

        Assert.Equal(0, exit);
        var said = output.ToString();
        Assert.StartsWith("deployables  schema-owner: 2 on `main` at `0123456`, daoris.json blob `a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2`", said, StringComparison.Ordinal);
        Assert.Contains("released by deliver/release-api, deploying on start", said, StringComparison.Ordinal);
        Assert.Contains("no release node names it: its target is undeclared", said, StringComparison.Ordinal);
        Assert.Empty(world.Sent);
    }

    [Fact]
    public async Task A_value_naming_an_undeclared_deployable_exits_1_and_a_repository_with_no_checkout_here_exits_2()
    {
        var world = World(DeployableDeclarationTests.SchemaOwner);
        var config = DriverConfigReader.Parse("""{"workflowValues":{"schema-owner":{"deliver":{"release":{"deployable":"config-cache"}}}}}""");

        var odd = new StringWriter();
        var oddExit = await DeployablesCommand.RunAsync("schema-owner", odd, world, config);
        var elsewhere = new StringWriter();
        var elsewhereExit = await DeployablesCommand.RunAsync("frontend", elsewhere, world, config);

        Assert.Equal(1, oddExit);
        Assert.Contains("! deliver/release names deployable `config-cache`", odd.ToString(), StringComparison.Ordinal);
        Assert.Equal(2, elsewhereExit);
        Assert.Contains("`frontend` has no checkout", elsewhere.ToString(), StringComparison.Ordinal);
    }
}
