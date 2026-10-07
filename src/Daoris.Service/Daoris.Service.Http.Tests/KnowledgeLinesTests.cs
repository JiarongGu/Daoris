using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Http.Tests;

/// <summary>A local host over one repository that declares its index of where things are (ORIENT2e).</summary>
public sealed class IndexedHost() : DaorisHost(ServiceMode.Local, seed: repositories =>
{
    var root = Path.Combine(repositories, "atlas");
    Directory.CreateDirectory(Path.Combine(root, "docs", "index"));
    File.WriteAllText(Path.Combine(root, "daoris.json"), """
        { "source": "s", "packs": [], "documents": { "index": "docs/index/README.md" } }
        """);
    File.WriteAllText(Path.Combine(root, "docs", "index", "README.md"), "# Where things are\n\nGenerated; open the row you need.\n");
    File.WriteAllText(Path.Combine(root, "docs", "index", "routes.md"),
        "# Bridge routes\n\n## DAORIS.DRIVER (2)\n\n| Route | Handler |\n|---|---|\n"
        + "| `SESSION_GO_ON_NEW` | `DriverModule.Sessions.cs:97` |\n| `STATE` | `DriverModule.cs:38` |\n");
});

/// <summary>
/// The HTTP door names a hit's lines too (ORIENT2e; the orientation design §3.2), beside the fields every client
/// already reads: the entry's first and last line, and the line its excerpt starts on. Absent lines are null,
/// never a guess.
/// </summary>
public sealed class KnowledgeLinesTests(IndexedHost host) : IClassFixture<IndexedHost>
{
    [Fact]
    public async Task A_search_hit_and_an_entry_name_their_lines()
    {
        var found = await host.GetAsync($"/api/search?q={Uri.EscapeDataString("SESSION_GO_ON_NEW")}&kinds=index");

        Assert.Equal(200, found.Status);
        var hit = Assert.Single(found.Json.EnumerateArray());
        Assert.Equal("Index", hit.GetProperty("kind").GetString());
        Assert.Equal("docs/index/routes.md", hit.GetProperty("path").GetString());
        Assert.Equal(5, hit.GetProperty("firstLine").GetInt32());
        Assert.Equal(8, hit.GetProperty("lastLine").GetInt32());
        Assert.InRange(hit.GetProperty("excerptLine").GetInt32(), 5, 8);

        var entry = await host.GetAsync($"/api/entry?id={Uri.EscapeDataString(hit.GetProperty("id").GetString()!)}");
        Assert.Equal(5, entry.Json.GetProperty("firstLine").GetInt32());
        Assert.Equal(8, entry.Json.GetProperty("lastLine").GetInt32());

        // What a sync feeds from carries them, so a feed can pass them on.
        var entries = await host.GetAsync("/api/entries?repository=atlas");
        Assert.Contains(entries.Json.EnumerateArray(), e => e.GetProperty("firstLine").ValueKind == JsonValueKind.Number);
    }

    [Fact]
    public async Task An_unknown_kind_is_refused_naming_the_index_among_the_kinds()
    {
        var refused = await host.GetAsync("/api/search?q=route&kinds=indexes");

        Assert.Equal(400, refused.Status);
        Assert.Contains("task, index", refused.Error);
    }
}
