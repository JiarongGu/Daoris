using Daoris.Driver;
using Daoris.Testing;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The line's admission, read from the declaration's own <c>admission</c> key: the gates every change passes and the checks its paths reach.
/// The merge tool selects its blocking gates from the same key, so a run's check and a merge hold the change to one list.
/// </summary>
public sealed class AdmissionGatesTests
{
    private const string Declaration = """
        {
          "admission": {
            "always": ["lint"],
            "suites": ["cli"],
            "byPaths": [
              { "gate": "types", "paths": ["web/src/**", "web/tsconfig.json"], "why": "the typecheck" },
              { "gate": "census", "paths": ["app/{One,Two}/**/*.cs"], "why": "the census" }
            ]
          },
          "verifyBudget": { "fullRunReasons": { "widePaths": ["daoris.gates.json", "contracts/**"] } },
          "gates": [
            { "name": "lint", "run": "node lint.mjs" },
            { "name": "cli", "run": "npm run verify:checks", "handback": true },
            { "name": "types", "run": "npm run typecheck" },
            { "name": "census", "run": "dotnet test --filter Category=Census" },
            { "name": "slow", "run": "dotnet test", "heavy": true }
          ]
        }
        """;

    private static IReadOnlyList<string> Admitted(string? text, params string[] touched)
    {
        var admitted = AdmissionGates.Of(DeclaredGates.Read(text), text, touched);
        Assert.Empty(admitted.Missing);
        Assert.Null(admitted.Problem);
        return [.. admitted.Gates.Select(gate => gate.Name)];
    }

    [Fact]
    public void The_gates_every_change_passes_are_admitted_and_a_path_check_only_where_a_changed_path_reaches_it()
    {
        Assert.Equal(["lint", "cli"], Admitted(Declaration, "docs/a.md"));
        Assert.Equal(["lint", "cli", "types"], Admitted(Declaration, "docs/a.md", "web/src/deep/App.tsx"));
        Assert.Equal(["lint", "cli", "types"], Admitted(Declaration, "web/tsconfig.json"));
        Assert.Equal(["lint", "cli", "census"], Admitted(Declaration, "app/Two/x/Y.cs"));
        Assert.Equal(["lint", "cli"], Admitted(Declaration, "app/Two/x/Y.json", "web/readme.md"));
    }

    [Fact]
    public void A_declared_wide_path_reaches_every_path_check_as_the_merge_tools_lane_table_does()
    {
        Assert.Equal(["lint", "cli", "types", "census"], Admitted(Declaration, "daoris.gates.json"));
        Assert.Equal(["lint", "cli", "types", "census"], Admitted(Declaration, "contracts/a/b.json"));
    }

    [Fact]
    public void The_gates_come_in_the_order_the_declaration_writes_them_whatever_the_admission_lists_first()
    {
        const string text = """
            {"admission":{"always":["b"],"suites":[],"byPaths":[{"gate":"a","paths":["x/"],"why":"w"}]},
             "gates":[{"name":"a","run":"a"},{"name":"b","run":"b"}]}
            """;

        Assert.Equal(["a", "b"], Admitted(text, "x/"));
    }

    [Fact]
    public void A_gate_the_admission_names_and_the_declaration_does_not_is_said_so_the_list_is_never_silently_shorter()
    {
        const string text = """{"admission":{"always":["ghost","lint"],"suites":[],"byPaths":[]},"gates":[{"name":"lint","run":"x"}]}""";

        var admitted = AdmissionGates.Of(DeclaredGates.Read(text), text, ["a.txt"]);

        Assert.Equal(["ghost"], admitted.Missing);
        Assert.Equal(["lint"], admitted.Gates.Select(gate => gate.Name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""{"gates":[{"name":"a","run":"x"}]}""")]
    public void A_declaration_with_no_admission_admits_nothing_beyond_what_it_requires(string? text)
    {
        Assert.Empty(Admitted(text, "src/a.cs"));
    }

    [Theory]
    [InlineData("""{"admission":{"always":"a","suites":[],"byPaths":[]},"gates":[{"name":"a","run":"x"}]}""")]
    [InlineData("""{"admission":{"always":[],"suites":[],"byPaths":[{"gate":"a","why":"w"}]},"gates":[{"name":"a","run":"x"}]}""")]
    [InlineData("""{"admission":"a","gates":[{"name":"a","run":"x"}]}""")]
    public void An_admission_that_does_not_read_is_a_problem_said_and_never_a_shorter_list(string text)
    {
        var admitted = AdmissionGates.Of(DeclaredGates.Read(text), text, ["src/a.cs"]);

        Assert.NotNull(admitted.Problem);
        Assert.Empty(admitted.Gates);
    }

    [Fact]
    public void This_repositorys_admission_is_the_one_the_merge_tool_selects_by_and_names_only_declared_gates()
    {
        var text = File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, DeclarationNames.Gates));
        var gates = DeclaredGates.Read(text);

        var web = AdmissionGates.Of(gates, text, ["src/Daoris.Web/scripts/e2e-port.d.mts"]);
        var docs = AdmissionGates.Of(gates, text, ["docs/README.md"]);

        Assert.Empty(web.Missing);
        Assert.Equal(["universal", "cli", "code-map", "test-map", "web-types", "reached"], web.Gates.Select(gate => gate.Name));
        Assert.Equal(["universal", "cli", "code-map", "test-map"], docs.Gates.Select(gate => gate.Name));
    }
}
