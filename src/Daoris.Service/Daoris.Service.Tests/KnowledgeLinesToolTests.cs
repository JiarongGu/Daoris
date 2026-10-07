using System.Text.RegularExpressions;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The agent's door over a repository's declared index (ORIENT2e; the orientation design §3.1–§3.2, §3.5): a hit
/// names its kind, its file and lines and the line its excerpt starts on, the tier line still closes every answer,
/// and <c>knowledge_get</c> reads the lines a hit names rather than the entry whole.
/// </summary>
public sealed partial class KnowledgeLinesToolTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-lines-tool-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private KnowledgeTools _tools = null!;

    private const string Routes =
        "# Bridge routes\n"                                     // 1
        + "\n"                                                  // 2
        + "## DAORIS.DRIVER (2)\n"                              // 3
        + "\n"                                                  // 4
        + "| Route | Handler |\n"                              // 5
        + "|---|---|\n"                                         // 6
        + "| `SESSION_GO_ON_NEW` | `DriverModule.Sessions.cs:97` |\n" // 7
        + "| `STATE` | `DriverModule.cs:38` |\n";              // 8

    /// <summary>
    /// An outline: its prose a section of several lines, what a range is cut from, and each of its items an entry
    /// of its own line (ORIENT2h2).
    /// </summary>
    private const string Outline =
        "# Outline of `src/Widget.cs`\n"                        // 1
        + "\n"                                                  // 2
        + "Generated; never edit by hand. Each item is a\n"     // 3
        + "declaration and its lines: read it with offset\n"    // 4
        + "and limit.\n"                                        // 5
        + "\n"                                                  // 6
        + "- 4-40 class Widget\n"                               // 7
        + "  - 9-12 Widget()\n"                                 // 8
        + "  - 412-417 test CountAsync\n";                      // 9

    private void Write(string relative, string content)
    {
        var file = Path.Combine(_root, "atlas", relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    public async Task InitializeAsync()
    {
        Write("daoris.json", """{"source":"s","packs":[],"documents":{"index":"docs/index/README.md","decisions":"docs/DECISIONS.md"}}""");
        Write("docs/index/README.md", "# Where things are\n\nGenerated; open the row you need.\n");
        Write("docs/index/routes.md", Routes);
        Write("docs/index/outlines/src/Widget.cs.md", Outline);
        Write("docs/DECISIONS.md", "# Decisions\n\n## D1 — Why the handler is one\n\nOne handler per route, so a route is found once.\n");
        for (var n = 1; n <= 4; n++) Write($"docs/index/outlines/route{n}.md", $"# Outline of route {n}\n\n- 1-9 the handler of route {n}\n");

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        var quests = await QuestStore.OpenAsync(_connection);
        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store),
            new FileSystemKnowledgeSource([Path.Combine(_root, "atlas")]), DisclosurePolicy.LocalOnly);
        await service.RefreshAsync();
        _tools = new KnowledgeTools(service, quests, new QuestExchange(service, quests), new AmbientWorkspace(Path.GetTempPath()));
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    /// <summary>
    /// The row's proof at the door: the hit is an index entry naming <c>path:first-last</c> and its excerpt's line,
    /// and with no model the answer still says it matched on words only (TIER1, the design's §3.5 tier line). A
    /// table's row is the hit, naming its own line (ORIENT2h).
    /// </summary>
    [Fact]
    public async Task A_hit_names_its_kind_its_lines_and_its_excerpt_s_line_and_the_tier_line_closes_the_answer()
    {
        var said = await _tools.SearchAsync("SESSION_GO_ON_NEW", kinds: "index", workspace: "all");

        Assert.Contains("### Bridge routes › DAORIS.DRIVER (2) › Route: SESSION_GO_ON_NEW", said);
        Assert.Contains("`atlas` · Index · `docs/index/routes.md:7`", said);
        var excerpt = ExcerptLine().Match(said);
        Assert.True(excerpt.Success, said);
        Assert.Equal(7, int.Parse(excerpt.Groups[1].Value));
        Assert.Contains("`lines`", said);
        Assert.Contains("_Matched on words only.", said);
    }

    [Fact]
    public async Task Knowledge_get_reads_the_lines_a_hit_names_rather_than_the_entry_whole()
    {
        const string id = "atlas:docs/index/outlines/src/Widget.cs.md#Outline of `src/Widget.cs`";

        var read = await _tools.GetAsync(id, lines: "4-4");

        Assert.Contains("`docs/index/outlines/src/Widget.cs.md:4`", read);
        Assert.Contains("declaration and its lines", read);
        Assert.DoesNotContain("Generated", read);
        Assert.DoesNotContain("and limit", read);

        // Past the entry's own lines, the part inside is read; wholly outside, the entry's lines are named.
        Assert.Contains("and limit.", await _tools.GetAsync(id, lines: "5-400"));
        var outside = await _tools.GetAsync(id, lines: "1-2");
        Assert.Contains("lines 3-5 of `docs/index/outlines/src/Widget.cs.md`", outside);
        Assert.DoesNotContain("declaration", outside);

        Assert.Contains("is not a range of lines", await _tools.GetAsync(id, lines: "eight"));
        Assert.Contains("Generated; never edit by hand.", await _tools.GetAsync(id));
        Assert.DoesNotContain("class Widget", await _tools.GetAsync(id));

        // An item is its own line, titled by its parent's label, and its range reads it (ORIENT2h2).
        var item = await _tools.GetAsync("atlas:docs/index/outlines/src/Widget.cs.md#Outline of `src/Widget.cs` › 4-40 class Widget › 9-12 Widget()", lines: "8");
        Assert.Contains("`docs/index/outlines/src/Widget.cs.md:8`", item);
        Assert.Contains("- 9-12 Widget()", item);
        Assert.DoesNotContain("CountAsync", item);

        // A row is one line, and its range reads it (ORIENT2h).
        var row = await _tools.GetAsync("atlas:docs/index/routes.md#Bridge routes › DAORIS.DRIVER (2) › Route: SESSION_GO_ON_NEW", lines: "7");
        Assert.Contains("`docs/index/routes.md:7`", row);
        Assert.Contains("| `SESSION_GO_ON_NEW` | `DriverModule.Sessions.cs:97` |", row);
        Assert.DoesNotContain("STATE", row);
    }

    /// <summary>
    /// A search that names no kinds answers with at most two index entries and says more matched, naming how to
    /// ask for them, so a where-question is one search more and never a silent miss.
    /// </summary>
    [Fact]
    public async Task A_search_naming_no_kinds_holds_the_index_to_two_and_says_how_to_ask_for_more()
    {
        var said = await _tools.SearchAsync("handler", workspace: "all");

        Assert.Equal(2, Regex.Count(said, "· Index ·"));
        Assert.Contains("`atlas` · Decision · `docs/DECISIONS.md:5`", said);
        Assert.Contains("kinds `index`", said);
    }

    [GeneratedRegex(@"> line (\d+): ")]
    private static partial Regex ExcerptLine();
}
