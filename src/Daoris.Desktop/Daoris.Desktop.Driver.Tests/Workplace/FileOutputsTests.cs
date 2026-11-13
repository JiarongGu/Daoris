using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The paths the repository's own rule file says its tools write again (output, intermediate, cache, scratch), as the output an unforced
/// removal takes with a tree. The pure halves; the real-git rows are in <c>FileOutputsGitTests</c>.
/// </summary>
public sealed class FileOutputsTests
{
    private const string Rules = """
        { "form": 2, "rules": [
          { "kind": "local", "paths": ["/local/"] },
          { "kind": "scratch", "paths": ["/_fixtures/", "/**/_fixtures/"] },
          { "kind": "dependency", "paths": ["/src/Web/node_modules/"], "key": ["package.json"] },
          { "kind": "output", "paths": ["/src/Web/src/bridge/generated/", "/docs/index/"] },
          { "kind": "intermediate", "paths": ["/src/**/obj/"] }
        ] }
        """;

    [Theory]
    [InlineData("src/Web/src/bridge/generated/", true)]
    [InlineData("src/Web/src/bridge/generated/routes.ts", true)]
    [InlineData("_fixtures/run/out.json", true)]
    [InlineData("src/Daoris.Core/_fixtures/a.txt", true)]
    [InlineData("docs/index/README.md", true)]
    [InlineData("src/Daoris.Core/obj/", true)]
    [InlineData("local/notes.md", false)]
    [InlineData("src/Web/src/bridge/handwritten/routes.ts", false)]
    [InlineData("src/Web/node_modules/", false)]
    public void What_the_rule_file_calls_regenerated_output_is_declared_and_what_it_calls_the_persons_is_not(string path, bool declared) =>
        Assert.Equal(declared, TreeScratch.IsDeclared(FileOutputs.Declared(Rules), path));

    [Theory]
    [InlineData("""{ "form": 2, "rules": [ { "kind": "output", "paths": ["/out/", "!/out/keep/"] } ] }""")]
    [InlineData("not json")]
    [InlineData("")]
    public void An_entry_that_takes_a_path_back_and_a_file_that_does_not_read_declare_nothing(string text) =>
        Assert.Empty(FileOutputs.Declared(text));
}
