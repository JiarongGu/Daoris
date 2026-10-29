using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1d (D215 point 2, the integration design §4.1): a repository's <c>daoris.gates.json</c> key <c>integration</c>, read whole
/// in one place, a twin of the merge tool's <c>integrationOf</c>. Held cell for cell to ONE table,
/// <c>fixtures/integration-declarations.json</c>, which <c>merge-branch.test.ts</c> holds the merge tool to, row for row; and where an
/// integration's acceptance stands against the line's blob. Reads text only.
/// </summary>
public sealed class IntegrationDeclarationTests
{
    private static JsonDocument Table() => JsonDocument.Parse(File.ReadAllText(Path.Combine(
        WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "integration-declarations.json")));

    /// <summary>Each row's declaration, its problem, and its facts as compact JSON (null where none): the theory data keeps scalars.</summary>
    public static TheoryData<string, string?, string?, string?> Rows()
    {
        var data = new TheoryData<string, string?, string?, string?>();
        using var table = Table();
        foreach (var row in table.RootElement.GetProperty("rows").EnumerateArray())
        {
            var read = row.GetProperty("read");
            data.Add(
                row.GetProperty("name").GetString()!,
                row.GetProperty("declaration").GetString(),
                row.GetProperty("problem").GetString(),
                read.ValueKind == JsonValueKind.Null ? null : JsonNode.Parse(read.GetRawText())!.ToJsonString());
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void The_integration_declaration_reads_as_the_shared_table_says(string name, string? declaration, string? problem, string? read)
    {
        var said = IntegrationDeclaration.Read(declaration);

        Assert.Equal((name, problem), (name, said.Problem));
        Assert.Equal((name, read), (name, said.Facts is { } facts ? Facts(facts).ToJsonString() : null));
        // Never a partial read: a problem leaves no facts behind it.
        if (said.Problem is not null) Assert.Null(said.Facts);
    }

    [Fact]
    public void The_table_holds_its_rows()
    {
        using var table = Table();
        Assert.True(table.RootElement.GetProperty("rows").GetArrayLength() >= 40);
    }

    [Fact]
    public void The_row_tables_are_the_rules_by_rows_once_each_in_the_order_first_declared_and_never_a_line_rule()
    {
        var facts = IntegrationDeclaration.Read(
            """{ "integration": { "merge": [ { "paths": "b.md", "by": "rows" }, { "paths": "docs/STATUS.md", "line": "^Status: ", "run": "node s.mjs" }, { "paths": "a.md", "by": "rows" }, { "paths": "b.md", "by": "rows" } ] } }""")
            .Facts!;

        Assert.Equal(["b.md", "a.md"], facts.RowTables);
        Assert.Equal(["b.md", "a.md"], BacklogMerge.RowTablesOf(
            """{ "integration": { "merge": [ { "paths": "b.md", "by": "rows" }, { "paths": "a.md", "by": "rows" }, { "paths": "b.md", "by": "rows" } ] } }"""));
        // A declaration with a problem declares no row table, as one that does not read declares none.
        Assert.Empty(BacklogMerge.RowTablesOf("""{ "integration": { "by": "agent", "merge": [ { "paths": "b.md", "by": "rows" } ] } }"""));
    }

    /// <summary>
    /// The acceptance against the line (the design §4.1, §4.3), in the shape of the landed route's (<c>LandedGates.StateOf</c>): the tip
    /// unread is unread whatever was accepted; nothing accepted is unaccepted; the accepted blob the tip's is accepted, any other changed.
    /// </summary>
    [Theory]
    [InlineData(null, null, IntegrationAcceptance.Unread)]
    [InlineData(Blob, null, IntegrationAcceptance.Unread)]
    [InlineData(null, Blob, IntegrationAcceptance.Unaccepted)]
    [InlineData(Blob, Blob, IntegrationAcceptance.Accepted)]
    [InlineData(Other, Blob, IntegrationAcceptance.Changed)]
    [InlineData("none", "none", IntegrationAcceptance.Accepted)]
    [InlineData("none", Blob, IntegrationAcceptance.Changed)]
    [InlineData(Blob, "none", IntegrationAcceptance.Changed)]
    public void An_acceptance_stands_against_the_tip_s_blob(string? accepted, string? tip, string state) =>
        Assert.Equal(state, IntegrationAcceptance.StateOf(accepted, tip));

    [Fact]
    public void An_acceptance_is_read_from_the_machine_s_settings_by_the_repository_s_name_in_any_case()
    {
        var config = DriverConfig.Empty.WithIntegrationGates("Engine", Blob);

        Assert.Equal(Blob, IntegrationAcceptance.Of(config, " engine "));
        Assert.Null(IntegrationAcceptance.Of(config, "shop-api"));
        Assert.Equal(IntegrationAcceptance.Accepted, IntegrationAcceptance.StateOf(IntegrationAcceptance.Of(config, "engine"), Blob));
    }

    private const string Blob = "0123456789abcdef0123456789abcdef01234567";
    private const string Other = "89abcdef0123456789abcdef0123456789abcdef";

    /// <summary>The facts in the table's shape: an absent key null, an absent list empty, each merge rule by rows or by its line.</summary>
    private static JsonObject Facts(IntegrationFacts facts) => new()
    {
        ["by"] = facts.By,
        ["gates"] = facts.Reached ? JsonValue.Create("reached") : facts.Gates is { } gates ? Strings(gates) : null,
        ["commits"] = facts.Commits,
        ["scratch"] = Strings(facts.Scratch),
        ["merge"] = new JsonArray([
            .. facts.Merge.Select(rule => (JsonNode)(rule.ByRows
                ? new JsonObject { ["paths"] = rule.Paths, ["by"] = "rows" }
                : new JsonObject { ["paths"] = rule.Paths, ["line"] = rule.Line, ["run"] = rule.Run })),
        ]),
        ["message"] = facts.Message,
        ["pass"] = Strings(facts.Pass),
    };

    private static JsonArray Strings(IEnumerable<string> values) => new([.. values.Select(value => (JsonNode?)JsonValue.Create(value))]);
}
