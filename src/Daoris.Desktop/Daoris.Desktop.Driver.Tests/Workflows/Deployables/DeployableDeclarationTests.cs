using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A repository's deployables as its own <c>daoris.json</c> declares them: an id, the paths that build it, what it provides and what it
/// consumes by resource and mode. The case's three repositories: the schema owner holds the migration and the API on one build, the
/// backend mirrors the schema, the frontend calls the API. An entry that does not read is left out and said; the others stand.
/// </summary>
public sealed class DeployableDeclarationTests
{
    /// <summary>The schema owner: two deployables, the migration and the API, built by one pipeline.</summary>
    public const string SchemaOwner = """
        {"source":"x","domain":{"summary":"The configuration schema and its API."},
         "deployables":[
           {"id":"config-migration","paths":["migrations/**"],"provides":[{"resource":"config-schema","mode":"migrates"}]},
           {"id":"config-api","paths":["src/**"],"provides":[{"resource":"config-api","mode":"owns"}],
            "consumes":[{"resource":"config-schema","mode":"reads"}]}]}
        """;

    public const string Backend = """
        {"source":"x","deployables":[{"id":"report-service","paths":["service/**"],
          "provides":[{"resource":"report-api","mode":"owns"}],"consumes":[{"resource":"config-schema","mode":"mirror"}]}]}
        """;

    public const string Frontend = """
        {"source":"x","deployables":[{"id":"report-web","paths":["web/**"],
          "consumes":[{"resource":"config-api","mode":"calls"},{"resource":"report-api","mode":"calls"}]}]}
        """;

    [Fact]
    public void A_repository_declaring_two_deployables_on_one_build_reads_both_with_what_each_provides_and_consumes()
    {
        var read = DeployableDeclaration.Read(SchemaOwner, "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2");

        Assert.Empty(read.Problems);
        Assert.Equal("a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2", read.Blob);
        Assert.Equal(["config-migration", "config-api"], read.Items.Select(each => each.Id));
        Assert.Equal([new DeployableUse("config-schema", "migrates")], read.Items[0].Provides);
        Assert.Equal(["src/**"], read.Items[1].Paths);
        Assert.Equal([new DeployableUse("config-schema", "reads")], read.Items[1].Consumes);
        Assert.Equal([new DeployableUse("config-schema", "mirror")], DeployableDeclaration.Read(Backend).Items.Single().Consumes);
        Assert.Equal(2, DeployableDeclaration.Read(Frontend).Items.Single().Consumes.Count);
    }

    [Fact]
    public void A_manifest_that_declares_none_has_none_and_says_nothing_wrong()
    {
        var none = DeployableDeclaration.Read("""{"source":"x","domain":{"summary":"s"}}""");

        Assert.Empty(none.Items);
        Assert.Empty(none.Problems);
        Assert.Empty(DeployableDeclaration.Read(null).Items);
    }

    [Fact]
    public void An_entry_that_does_not_read_is_left_out_naming_why_and_the_others_stand()
    {
        const string Manifest = """
            {"source":"x","deployables":[
              {"id":"config-api","paths":["src/**"]},
              {"id":"Config-API","paths":["api/**"]},
              {"id":"nopaths"},
              {"id":"schema","paths":["db/**"],"provides":[{"resource":"config-schema","mode":"mirror"}]},
              {"id":"web","paths":["/abs/**"]},
              {"id":"up","paths":["../elsewhere/**"]},
              {"id":"reader","paths":["r/**"],"consumes":[{"resource":"","mode":"reads"}]},
              "not an object"]}
            """;

        var read = DeployableDeclaration.Read(Manifest);

        Assert.Equal(["config-api"], read.Items.Select(each => each.Id));
        Assert.Equal(7, read.Problems.Count);
        Assert.Contains(read.Problems, said => said.Contains("`Config-API`", StringComparison.Ordinal) && said.Contains("lower-case", StringComparison.Ordinal));
        Assert.Contains(read.Problems, said => said.Contains("deployables[2]", StringComparison.Ordinal) && said.Contains("paths", StringComparison.Ordinal));
        Assert.Contains(read.Problems, said => said.Contains("`schema`", StringComparison.Ordinal) && said.Contains("owns or migrates", StringComparison.Ordinal));
        Assert.Contains(read.Problems, said => said.Contains("`web`", StringComparison.Ordinal) && said.Contains("repository-relative", StringComparison.Ordinal));
        Assert.Contains(read.Problems, said => said.Contains("`up`", StringComparison.Ordinal) && said.Contains("repository-relative", StringComparison.Ordinal));
        Assert.Contains(read.Problems, said => said.Contains("`reader`", StringComparison.Ordinal) && said.Contains("resource", StringComparison.Ordinal));
        Assert.Contains(read.Problems, said => said.Contains("deployables[7]", StringComparison.Ordinal) && said.Contains("not an object", StringComparison.Ordinal));
    }

    [Fact]
    public void A_repeated_id_in_any_case_keeps_the_first_and_a_list_that_is_no_list_is_said()
    {
        var repeated = DeployableDeclaration.Read("""{"deployables":[{"id":"api","paths":["a/**"]},{"id":"api","paths":["b/**"]}]}""");
        var notAList = DeployableDeclaration.Read("""{"deployables":{"id":"api"}}""");
        var notJson = DeployableDeclaration.Read("{ not json");

        Assert.Equal(["a/**"], repeated.Items.Single().Paths);
        Assert.Contains("declared twice", Assert.Single(repeated.Problems), StringComparison.Ordinal);
        Assert.Contains("not a list", Assert.Single(notAList.Problems), StringComparison.Ordinal);
        Assert.Contains("not valid JSON", Assert.Single(notJson.Problems), StringComparison.Ordinal);
    }
}
