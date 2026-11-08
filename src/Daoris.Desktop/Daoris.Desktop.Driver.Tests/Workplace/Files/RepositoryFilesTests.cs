using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The repository's own layer read: a dependency whose path a later rule takes is refused alone, the rest read; no file is no rule.</summary>
public sealed class RepositoryFilesTests
{
    [Fact]
    public void A_rule_file_reads_its_rules_in_order()
    {
        var read = RepositoryFilesReader.Read("""
            {
              "_why": "a note",
              "rules": [
                { "kind": "local", "paths": ["local/secrets/"] },
                { "kind": "dependency", "paths": ["node_modules/"], "key": ["package-lock.json", ".npmrc"], "caches": [".cache/"] },
                { "kind": "intermediate", "paths": ["src/**/obj/"], "toolchain": ["dotnet --version"] },
                { "kind": "output", "paths": ["src/api/bin/", "src/cli/dist/"] }
              ]
            }
            """);

        Assert.Empty(read.Problems);
        Assert.Equal([FileKind.Local, FileKind.Dependency, FileKind.Intermediate, FileKind.Output], read.Rules.Select(rule => rule.Kind));
        var dependency = read.Rules[1];
        Assert.Equal(["package-lock.json", ".npmrc"], dependency.Key);
        Assert.Equal([".cache/"], dependency.Caches);
        Assert.Null(dependency.Install);
        Assert.Equal(["dotnet --version"], read.Rules[2].Toolchain);
    }

    [Fact]
    public void No_rule_file_is_no_rule()
    {
        foreach (var text in new string?[] { null, """{ "_why": "nothing yet" }""" })
        {
            var read = RepositoryFilesReader.Read(text);
            Assert.Empty(read.Problems);
            Assert.Empty(read.Rules);
        }
    }

    [Theory]
    [InlineData("""{ "rules": [ { "kind": "dependency", "paths": ["node_modules/"] }, { "kind": "output", "paths": ["node_modules/"] }, { "kind": "scratch", "paths": ["tmp/"] } ] }""", "rules[1]: '/node_modules/' is the dependency's path a later rule takes too, so it acts on nothing")]
    [InlineData("""{ "form": 2, "rules": [ { "kind": "dependency", "paths": ["/node_modules/"] }, { "kind": "output", "paths": ["node_modules/"] }, { "kind": "scratch", "paths": ["/tmp/"] } ] }""", "rules[1]: '/node_modules/' is the dependency's path a later rule takes too, so it acts on nothing")]
    [InlineData("""{ "form": 2, "rules": [ { "kind": "local", "paths": ["/x"], "copy": true }, { "kind": "scratch", "paths": ["/tmp/"] } ] }""", "rules[1]: `copy` is not a field of a `local` rule")]
    [InlineData("""{ "rules": [ { "kind": "output", "paths": ["src\\bin\\"] }, { "kind": "scratch", "paths": ["tmp/"] } ] }""", "rules[1]: 'src\\bin\\' is written with backslashes; paths use '/'")]
    [InlineData("""{ "rules": [ { "kind": "cache", "paths": ["out/"], "install": "npm ci" }, { "kind": "scratch", "paths": ["tmp/"] } ] }""", "rules[1]: `install` is not a field of a `cache` rule")]
    public void A_rule_that_does_not_hold_is_refused_alone_and_the_rest_still_read(string text, string problem)
    {
        var read = RepositoryFilesReader.Read(text);

        Assert.Contains(problem, read.Problems);
        Assert.Equal(["/tmp/"], read.Rules[^1].Paths);
        Assert.DoesNotContain(read.Rules, rule => rule.Paths.Contains("src/**/bin/") || rule.Kind == FileKind.Dependency);
    }

    [Theory]
    [InlineData("{ \"rules\": [ ", "daoris.files.json is not valid JSON")]
    [InlineData("[]", "daoris.files.json is not a JSON object")]
    [InlineData("""{ "rules": {} }""", "rules: is not a list")]
    public void A_file_that_does_not_read_has_no_rule(string text, string problem)
    {
        var read = RepositoryFilesReader.Read(text);

        Assert.Equal([problem], read.Problems);
        Assert.Empty(read.Rules);
    }
}
