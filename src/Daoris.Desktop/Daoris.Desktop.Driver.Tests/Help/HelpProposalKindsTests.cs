using System.Text.RegularExpressions;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>Every kind of Ask Daoris's proposal is whole: a class here, the service's writer, the tool and its card, read as text.</summary>
[Trait(Category.Name, Category.Census)]
public sealed partial class HelpProposalKindsTests
{
    /// <summary>The kinds, and the connector's tool that proposes each, in the order the room allows them.</summary>
    private static readonly (string Kind, string Tool)[] Table =
    [
        ("go", "go_propose"),
        ("workflow", "workflow_propose"),
    ];

    /// <summary>
    /// The workflow kind's file names no door. Its judge reads <c>new</c> from a <c>base</c> written null, <c>edit</c>
    /// from one written as a number, and <c>use</c> from <c>choose</c>, so its twin is held to writing those.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> WorkflowDoorsWritten = new Dictionary<string, string>
    {
        ["new"] = """WriteNull("base")""",
        ["edit"] = """WriteNumber("base",""",
        ["use"] = """WriteBoolean("choose",""",
        ["draft"] = """WriteString("draft",""",
    };

    [Fact]
    public void The_workflow_writer_writes_its_name()
    {
        var writer = File.ReadAllText(CoreFile("HelpWorkflowProposalWriter.cs"));

        Assert.Contains("""WriteString("name",""", writer);
    }

    internal static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    internal static string CoreFolder() => Path.Combine(RepositoryRoot(), "src", "Daoris.Service", "Daoris.Service.Core");

    internal static string CoreFile(string name) =>
        Directory.EnumerateFiles(CoreFolder(), name, SearchOption.AllDirectories).Single();

    private static string Pascal(string kind) => char.ToUpperInvariant(kind[0]) + kind[1..];

    [Fact]
    public void The_kinds_are_the_twin_table()
    {
        Assert.Equal(Table, HelpProposalKinds.All.Select(kind => (kind.Kind, kind.Tool)));
    }

    [Fact]
    public void Every_kind_is_a_class_in_a_file_of_its_own()
    {
        var folder = Path.GetDirectoryName(SourceFiles.Driver($"{HelpProposalKinds.All[0].GetType().Name}.cs"))!;

        Assert.Equal(
            HelpProposalKinds.All.Select(kind => $"{kind.GetType().Name}.cs").Order(StringComparer.Ordinal),
            Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories)
                .Where(file => File.ReadAllText(file).Contains(": IHelpProposalKind", StringComparison.Ordinal))
                .Select(Path.GetFileName).Order(StringComparer.Ordinal));
        foreach (var kind in HelpProposalKinds.All)
        {
            var name = kind.GetType().Name;
            Assert.Equal($"Help{Pascal(kind.Kind)}Proposals", name);
            Assert.Matches($@"class {name}\b[^\n]*: IHelpProposalKind", File.ReadAllText(Path.Combine(folder, $"{name}.cs")));
        }
    }

    /// <summary>
    /// The service writes each kind in a file of its own (<c>Help&lt;Kind&gt;ProposalWriter.cs</c>), no kind this
    /// side does not judge, and the connector carries each kind's tool once, in a class and file of its own (<c>Help&lt;Kind&gt;Tool.cs</c>).
    /// </summary>
    [Fact]
    public void Every_kind_has_its_twins_writer_and_its_connectors_tool()
    {
        var service = Path.Combine(RepositoryRoot(), "src", "Daoris.Service");
        var written = Directory.GetFiles(CoreFolder(), "Help*ProposalWriter.cs", SearchOption.AllDirectories)
            .SelectMany(path => WritesKind().Matches(File.ReadAllText(path)).Select(match => (Kind: match.Groups[1].Value, File: Path.GetFileName(path))))
            .ToList();

        Assert.Equal(Table.Select(row => row.Kind).Order(StringComparer.Ordinal), written.Select(each => each.Kind).Order(StringComparer.Ordinal));
        foreach (var (kind, tool) in Table)
        {
            Assert.Equal($"Help{Pascal(kind)}ProposalWriter.cs", Assert.Single(written, each => each.Kind == kind).File);
            var tools = Directory.GetFiles(Path.Combine(service, "Daoris.Service.Mcp"), "*.cs")
                .Where(path => File.ReadAllText(path).Contains($"[McpServerTool(Name = \"{tool}\")]", StringComparison.Ordinal))
                .Select(Path.GetFileName)
                .ToList();
            Assert.Equal($"Help{Pascal(kind)}Tool.cs", Assert.Single(tools));
        }
    }

    /// <summary>
    /// Each kind's doors, which Ask Daoris's coverage is held against, are doors its twin writes: each
    /// spelled in the service's writer of that kind.
    /// </summary>
    [Fact]
    public void Every_kinds_doors_are_the_ones_its_twin_writes()
    {
        foreach (var kind in HelpProposalKinds.All)
        {
            Assert.NotEmpty(kind.Doors);
            var writer = File.ReadAllText(CoreFile($"Help{Pascal(kind.Kind)}ProposalWriter.cs"));
            foreach (var door in kind.Doors)
            {
                Assert.Contains(kind.Kind == "workflow" ? WorkflowDoorsWritten[door] : $"\"{door}\"", writer);
            }
        }
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
}
