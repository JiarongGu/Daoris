using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An output's <c>builds</c> and <c>refuses</c> (the tree-pool design §35.1, §36.3, 4, §37.3, 2): <c>builds</c> is the one command the
/// door runs, naming the configuration it builds and refused where it skips the build; <c>refuses</c> names the runners the repository
/// integrated with the guard, for the start's words; the .NET signature fills <c>builds</c> for a <c>bin/</c> beside its project, and
/// this repository's file names its integrated runners.
/// </summary>
public sealed class BaseRuleFieldsTests
{
    [Fact]
    public void An_outputs_builds_and_refuses_read_and_a_builds_that_skips_the_build_or_names_no_configuration_is_refused()
    {
        var read = RepositoryFilesReader.Read("""
            { "form": 2, "rules": [ { "kind": "output", "paths": ["bin/"], "take": "base",
              "builds": "dotnet build -c {configuration}", "refuses": ["node tools/run.mjs"] } ] }
            """);

        Assert.Empty(read.Problems);
        Assert.Equal("dotnet build -c {configuration}", read.Rules.Single().Builds);
        Assert.Equal(["node tools/run.mjs"], read.Rules.Single().Refuses);
        Assert.Contains("--no-build", Problem("""{ "kind": "output", "paths": ["bin/"], "builds": "dotnet test -c {configuration} --no-build" }"""));
        Assert.Contains("{configuration}", Problem("""{ "kind": "output", "paths": ["bin/"], "builds": "dotnet build" }"""));
        Assert.Contains("`builds` is not a field", Problem("""{ "kind": "scratch", "paths": ["/tmp/"], "builds": "dotnet build -c {configuration}" }"""));
        Assert.Contains("`refuses` is not a list", Problem("""{ "kind": "output", "paths": ["bin/"], "refuses": "node x" }"""));
    }

    [Fact]
    public void The_dotnet_signature_fills_builds_for_a_bin_beside_its_project_and_this_repositorys_bin_names_its_integrated_runners()
    {
        var signature = FileSignatures.Shipped.Over(["src/App/App.csproj"]).Single(hit => hit.Rule.Kind == FileKind.Output).Rule;
        Assert.Equal("dotnet build -c {configuration}", signature.Builds);
        Assert.Null(signature.Take);

        var read = RepositoryFilesReader.Read(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, DeclarationNames.Rules)));
        var rules = FileRules.Of([], read.Rules, PersonFilesRead.Empty, "Daoris", (path, kind) => kind == signature.Kind && signature.Matches(path) ? signature : null);
        var bin = rules.For("src/App/bin/", new FileFloorFacts([], [])).Rule!;

        Assert.Equal(("base", "dotnet build -c {configuration}"), (bin.Take, bin.Builds));
        Assert.Contains("node tools/desktop.mjs run", bin.Refuses!);
    }

    private static string Problem(string entry) => Assert.Single(RepositoryFilesReader.Read($$"""{ "form": 2, "rules": [ {{entry}} ] }""").Problems);
}
