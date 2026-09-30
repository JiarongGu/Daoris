using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// Every kind of Ask Daoris's proposal is whole (MOD6): a class in a file of its own here, the service's writer
/// of the same kind in a file of its own there, the connector's tool that proposes it, and its card on the
/// page — so a new kind cannot be half-added, and none is left behind when one is removed.
/// </summary>
/// <remarks>
/// The three share no code (<c>.claude/knowledge/twins.md</c>): this reads the service's and the page's source
/// as text, the way a reviewer would, and the service's own test holds the same table.
/// </remarks>
public sealed partial class HelpProposalKindsTests
{
    /// <summary>
    /// The kinds, and the connector's tool that proposes each, in the order the room allows them — the
    /// service's <c>HelpProposalBoxKindsTests</c> holds the same table, line for line.
    /// </summary>
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
    ];

    internal static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    private static string Pascal(string kind) => char.ToUpperInvariant(kind[0]) + kind[1..];

    [Fact]
    public void The_kinds_are_the_twin_table()
    {
        Assert.Equal(Table, HelpProposalKinds.All.Select(kind => (kind.Kind, kind.Tool)));
    }

    [Fact]
    public void Every_kind_is_a_class_in_a_file_of_its_own()
    {
        var folder = Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver", "Help", "Proposals");

        Assert.Equal(
            HelpProposalKinds.All.Select(kind => $"{kind.GetType().Name}.cs").Order(StringComparer.Ordinal),
            Directory.GetFiles(folder, "*.cs").Select(Path.GetFileName).Order(StringComparer.Ordinal));
        foreach (var kind in HelpProposalKinds.All)
        {
            var name = kind.GetType().Name;
            Assert.Equal($"Help{Pascal(kind.Kind)}Proposals", name);
            Assert.Matches($@"class {name}\b[^\n]*: IHelpProposalKind", File.ReadAllText(Path.Combine(folder, $"{name}.cs")));
        }
    }

    /// <summary>
    /// The service writes each kind in a file of its own (<c>HelpProposalBox.&lt;Kind&gt;.cs</c>), no kind this
    /// side does not judge, and the connector carries each kind's tool once, in a file of its own.
    /// </summary>
    [Fact]
    public void Every_kind_has_its_twins_writer_and_its_connectors_tool()
    {
        var service = Path.Combine(RepositoryRoot(), "src", "Daoris.Service");
        var written = Directory.GetFiles(Path.Combine(service, "Daoris.Service.Core"), "HelpProposalBox*.cs")
            .SelectMany(path => WritesKind().Matches(File.ReadAllText(path)).Select(match => (Kind: match.Groups[1].Value, File: Path.GetFileName(path))))
            .ToList();

        Assert.Equal(Table.Select(row => row.Kind).Order(StringComparer.Ordinal), written.Select(each => each.Kind).Order(StringComparer.Ordinal));
        foreach (var (kind, tool) in Table)
        {
            Assert.Equal($"HelpProposalBox.{Pascal(kind)}.cs", Assert.Single(written, each => each.Kind == kind).File);
            var tools = Directory.GetFiles(Path.Combine(service, "Daoris.Service.Mcp"), "*.cs")
                .Where(path => File.ReadAllText(path).Contains($"[McpServerTool(Name = \"{tool}\")]", StringComparison.Ordinal))
                .Select(Path.GetFileName)
                .ToList();
            Assert.Equal($"KnowledgeTools.Help.{Pascal(kind)}.cs", Assert.Single(tools));
        }
    }

    /// <summary>
    /// Each kind's doors (HELP9), which Ask Daoris's coverage is held against, are doors its twin writes: each
    /// spelled in the service's writer of that kind, and the setting's, a list on both sides, the same list.
    /// </summary>
    [Fact]
    public void Every_kinds_doors_are_the_ones_its_twin_writes()
    {
        var core = Path.Combine(RepositoryRoot(), "src", "Daoris.Service", "Daoris.Service.Core");
        foreach (var kind in HelpProposalKinds.All)
        {
            Assert.NotEmpty(kind.Doors);
            var writer = File.ReadAllText(Path.Combine(core, $"HelpProposalBox.{Pascal(kind.Kind)}.cs"));
            foreach (var door in kind.Doors) Assert.Contains($"\"{door}\"", writer);
        }

        var listed = ServiceSettingDoors().Match(File.ReadAllText(Path.Combine(core, "HelpProposalBox.Setting.cs")));
        Assert.True(listed.Success, "the service's setting doors are a list, `Doors`.");
        Assert.Equal(
            new HelpSettingProposals().Doors,
            Regex.Matches(listed.Groups[1].Value, "\"([a-z-]+)\"").Select(match => match.Groups[1].Value));
    }

    /// <summary>The page's card takes exactly the kinds this side judges: the <c>kind</c> of its proposal type.</summary>
    [Fact]
    public void Every_kind_has_its_card_on_the_page()
    {
        var page = Path.Combine(RepositoryRoot(), "src", "Daoris.Web", "src");
        var declared = Directory.EnumerateFiles(page, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".ts", StringComparison.Ordinal) || path.EndsWith(".tsx", StringComparison.Ordinal))
            .Select(path => CardKinds().Match(File.ReadAllText(path)))
            .Where(match => match.Success)
            .ToList();

        var union = Assert.Single(declared).Groups[1].Value;
        Assert.Equal(
            Table.Select(row => row.Kind).Order(StringComparer.Ordinal),
            Regex.Matches(union, "'([a-z]+)'").Select(match => match.Groups[1].Value).Order(StringComparer.Ordinal));
    }

    [GeneratedRegex("""WriteString\("kind", "([a-z]+)"\)""")]
    private static partial Regex WritesKind();

    [GeneratedRegex(@"export type HelpProposal = \{[^}]*?\bkind:\s*([^;]+);", RegexOptions.Singleline)]
    private static partial Regex CardKinds();

    [GeneratedRegex(@"IReadOnlyList<string> Doors\s*=\s*\[([^\]]*)\]")]
    private static partial Regex ServiceSettingDoors();
}
