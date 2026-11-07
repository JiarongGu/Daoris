using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The classes of a repository's ignored content, read from its gates file's <c>trees</c>. A declared path git
/// tracks is refused, as is a <c>prepare</c> command its <c>safe</c> list lacks; no declaration carries nothing.
/// </summary>
public sealed class TreeClassesTests
{
    private static readonly string[] Tracked = ["package.json", "package-lock.json", "src/cli/bin/cli.mjs", "src/api/Api.csproj", "local/.keep"];

    [Fact]
    public void A_declaration_reads_its_classes()
    {
        var read = TreeClassesReader.Read("""
            {
              "safe": ["npm ci", "dotnet build src/api"],
              "trees": {
                "dependencies": [ { "path": "node_modules/", "key": ["package-lock.json"], "toolchain": "node --version" } ],
                "intermediates": ["src/**/obj/"],
                "outputs": ["src/api/bin/", "src/cli/dist/"],
                "prepare": ["npm ci"]
              }
            }
            """, Tracked);

        Assert.Empty(read.Problems);
        Assert.True(read.Classes.Declares);
        var dependency = Assert.Single(read.Classes.Dependencies);
        Assert.Equal(("node_modules/", "node --version"), (dependency.Path, dependency.Toolchain));
        Assert.Equal(["package-lock.json"], dependency.Key);
        Assert.Equal(["src/**/obj/"], read.Classes.Intermediates);
        Assert.Equal(["src/api/bin/", "src/cli/dist/"], read.Classes.Outputs);
        Assert.Equal(["npm ci"], read.Classes.Prepare);
    }

    [Fact]
    public void No_declaration_carries_nothing()
    {
        foreach (var text in new string?[] { null, """{ "gates": [] }""" })
        {
            var read = TreeClassesReader.Read(text, Tracked);
            Assert.Empty(read.Problems);
            Assert.False(read.Classes.Declares);
            Assert.Empty(read.Classes.Outputs);
        }
    }

    [Theory]
    [InlineData("""{ "trees": { "outputs": ["src/**/bin/"] } }""", "trees.outputs: 'src/**/bin/' holds src/cli/bin/cli.mjs, which git tracks")]
    [InlineData("""{ "safe": ["npm ci"], "trees": { "prepare": ["npm install"] } }""", "trees.prepare: 'npm install' is not in the safe list")]
    [InlineData("""{ "trees": { "prepare": ["npm ci"] } }""", "trees.prepare: 'npm ci' is not in the safe list")]
    [InlineData("""{ "trees": { "local": ["local/a.txt"] } }""", "trees.local: the local class is localInputs, declared once")]
    [InlineData("""{ "trees": { "caches": [] } }""", "trees.caches: is not dependencies, intermediates, outputs or prepare")]
    [InlineData("""{ "trees": { "intermediates": ["out/"], "outputs": ["out/"] } }""", "trees.outputs: 'out/' is declared in two classes")]
    [InlineData("""{ "trees": { "dependencies": [ { "path": "node_modules/", "key": ["yarn.lock"] } ] } }""", "trees.dependencies: the key 'yarn.lock' is not a tracked file")]
    [InlineData("""{ "trees": { "outputs": ["src\\bin\\"] } }""", "trees.outputs: 'src\\bin\\' is written with backslashes; paths use '/'")]
    [InlineData("""{ "localInputs": ["local/a.txt"], "trees": { "outputs": ["local/"] } }""", "trees.outputs: 'local/' holds the local input local/a.txt: the local class is localInputs, declared once")]
    [InlineData("""{ "trees": [] }""", "trees: is not an object")]
    public void A_malformed_declaration_is_refused_and_carries_nothing(string text, string problem)
    {
        var read = TreeClassesReader.Read(text, Tracked);

        Assert.Contains(problem, read.Problems);
        Assert.False(read.Classes.Declares);
    }
}
