using System.Reflection;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;
using ModelContextProtocol.Server;

namespace Daoris.Service.Tests;

/// <summary>
/// Every kind the box writes is whole (MOD6): its writer in a file of its own that writes exactly that kind, and
/// the connector's tool that proposes it. The driver's <c>HelpProposalKindsTests</c> holds the same table, and
/// also checks that each kind has its judge there and its card on the page.
/// </summary>
public sealed class HelpProposalBoxKindsTests
{
    /// <summary>The kinds, and the connector's tool that proposes each — the driver's table, line for line.</summary>
    private static readonly (string Kind, string Tool)[] Table =
    [
        ("setting", "setting_propose"),
        ("ask", "ask_propose"),
        ("agent", "agent_propose"),
        ("delete", "delete_propose"),
        ("account", "agent_settings_propose"),
        ("go", "go_propose"),
        ("plugin", "plugin_propose"),
        ("hand", "hand_propose"),
        ("browser", "browser_propose"),
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
    public void Every_kind_is_written_by_a_file_of_its_own()
    {
        var core = Path.Combine(RepositoryRoot(), "src", "Daoris.Service", "Daoris.Service.Core");

        Assert.Equal(
            Table.Select(row => $"HelpProposalBox.{Pascal(row.Kind)}.cs").Append("HelpProposalBox.cs").Order(StringComparer.Ordinal),
            Directory.GetFiles(core, "HelpProposalBox*.cs").Select(Path.GetFileName).Order(StringComparer.Ordinal));
        foreach (var (kind, _) in Table)
        {
            Assert.Contains($"writer.WriteString(\"kind\", \"{kind}\");", File.ReadAllText(Path.Combine(core, $"HelpProposalBox.{Pascal(kind)}.cs")));
        }
    }

    [Fact]
    public void Every_kind_has_its_connectors_tool()
    {
        var tools = typeof(KnowledgeTools).GetMethods()
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .OfType<string>()
            .ToList();

        foreach (var (_, tool) in Table) Assert.Single(tools, name => name == tool);
    }
}
