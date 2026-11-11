using System.Reflection;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;
using ModelContextProtocol.Server;

namespace Daoris.Service.Tests;

/// <summary>Every kind the box writes is whole: its writer in a file of its own, and the connector's tool that proposes it.</summary>
public sealed class HelpProposalBoxKindsTests
{
    private static readonly (string Kind, string Tool)[] Table =
    [
        ("ask", "ask_propose"),
        ("go", "go_propose"),
        ("workflow", "workflow_propose"),
        ("setup", "setup_propose"),
    ];

    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    private static string Pascal(string kind) => char.ToUpperInvariant(kind[0]) + kind[1..];

    [Fact]
    public void The_kinds_are_the_twin_table()
    {
        Assert.Equal(Table, HelpProposalBox.Kinds);
    }

    [Fact]
    public void Every_kind_is_written_by_a_class_in_a_file_of_its_own()
    {
        var core = Path.Combine(RepositoryRoot(), "src", "Daoris.Service", "Daoris.Service.Core");

        Assert.Equal(
            Table.Select(row => $"Help{Pascal(row.Kind)}ProposalWriter.cs").Order(StringComparer.Ordinal),
            Directory.GetFiles(core, "Help*ProposalWriter.cs", SearchOption.AllDirectories).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Empty(Directory.GetFiles(core, "HelpProposalBox.*.cs", SearchOption.AllDirectories));
        foreach (var (kind, _) in Table)
        {
            var writer = File.ReadAllText(Directory.EnumerateFiles(core, $"Help{Pascal(kind)}ProposalWriter.cs", SearchOption.AllDirectories).Single());
            Assert.Matches($@"\bclass Help{Pascal(kind)}ProposalWriter\b", writer);
            Assert.DoesNotContain("class HelpProposalBox", writer);
            Assert.Contains($"writer.WriteString(\"kind\", \"{kind}\");", writer);
        }
    }

    [Fact]
    public void Every_kind_has_its_connectors_tool()
    {
        var tools = KnowledgeServer.ToolTypes.SelectMany(type => type.GetMethods())
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .OfType<string>()
            .ToList();

        foreach (var (_, tool) in Table) Assert.Single(tools, name => name == tool);
    }
}
