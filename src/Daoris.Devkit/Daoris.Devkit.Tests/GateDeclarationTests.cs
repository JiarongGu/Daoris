using Daoris.Devkit;

namespace Daoris.Devkit.Tests;

public sealed class GateDeclarationTests : IDisposable
{
    private readonly Fixture _fx = new("declaration");

    public void Dispose() => _fx.Dispose();

    private void WriteDeclaration(string json) =>
        _fx.Write(GateDeclaration.FileName, json);

    [Fact]
    public void A_missing_declaration_names_the_command_that_writes_one()
    {
        var error = Assert.Throws<DevkitException>(() => GateDeclaration.Read(_fx.Path));

        Assert.Contains("daoris-devkit init", error.Message);
    }

    /// <summary>The file is hand-edited by definition, so the error has to name it.</summary>
    [Fact]
    public void Invalid_json_names_the_file_rather_than_a_framework_type()
    {
        WriteDeclaration("{ not json");

        var error = Assert.Throws<DevkitException>(() => GateDeclaration.Read(_fx.Path));

        Assert.Contains(GateDeclaration.FileName, error.Message);
    }

    [Fact]
    public void An_empty_declaration_is_valid_and_every_section_defaults()
    {
        WriteDeclaration("{}");

        var declaration = GateDeclaration.Read(_fx.Path);

        Assert.Empty(declaration.Gates);
        Assert.Empty(declaration.Disabled);
        Assert.Equal("local/sensitive-patterns.txt", declaration.Sensitive.PatternsFile);
        Assert.Null(declaration.Version.Source);
    }

    [Fact]
    public void Every_section_round_trips()
    {
        WriteDeclaration("""
            {
              "devkit": "0.0.1",
              "gates": [
                { "name": "build", "run": "dotnet build", "cwd": "src" },
                { "name": "test",  "run": "dotnet test" }
              ],
              "sensitive": { "patternsFile": "private/patterns.txt" },
              "version": { "source": "Directory.Build.props", "mirrors": ["web/package.json"] },
              "docs": {
                "changelog": "CHANGELOG.md",
                "tracked": [{ "document": "docs/api.md", "describes": ["src/Api.cs"] }]
              },
              "disabled": ["docs"]
            }
            """);

        var declaration = GateDeclaration.Read(_fx.Path);

        Assert.Equal("0.0.1", declaration.Devkit);
        Assert.Equal(2, declaration.Gates.Count);
        Assert.Equal("src", declaration.Gates[0].WorkingDirectory);
        Assert.Null(declaration.Gates[1].WorkingDirectory);
        Assert.Equal("private/patterns.txt", declaration.Sensitive.PatternsFile);
        Assert.Equal(["web/package.json"], declaration.Version.Mirrors);
        Assert.Equal("CHANGELOG.md", declaration.Docs.Changelog);
        Assert.Equal("src/Api.cs", declaration.Docs.Tracked!.Single().Describes.Single());
        Assert.Contains("docs", declaration.Disabled);
    }

    /// <summary>Case-insensitive, because a name typed into a list by hand is typed how it is typed.</summary>
    [Fact]
    public void Disabled_names_match_regardless_of_case()
    {
        WriteDeclaration("""{ "disabled": ["Sensitive"] }""");

        Assert.Contains("sensitive", GateDeclaration.Read(_fx.Path).Disabled);
    }

    [Fact]
    public void A_gate_missing_its_command_is_rejected_at_the_edge()
    {
        WriteDeclaration("""{ "gates": [{ "name": "build" }] }""");

        var error = Assert.Throws<DevkitException>(() => GateDeclaration.Read(_fx.Path));

        Assert.Contains("'name' and 'run'", error.Message);
    }
}
