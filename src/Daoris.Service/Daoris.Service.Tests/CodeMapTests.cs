using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// <b>A repository's code map</b> (MAP3a, <c>docs/2026-09-23-map-design.md</c> §3): one committed
/// file, found by convention, judged whole — and read from the checkout, never written to it (D32).
/// </summary>
public sealed class CodeMapTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-codemap-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private const string Valid = """
        {
          "version": 1,
          "modules": [
            { "id": "web", "path": "src/Web", "summary": "the platform" },
            { "id": "service", "path": "src/Service", "summary": "indexes the family's knowledge" }
          ],
          "dependencies": [{ "from": "web", "to": "service", "kind": "http" }]
        }
        """;

    private string Checkout(string? atDocs = null, string? atRoot = null)
    {
        // A fresh checkout per call: one test reads several, and a shared one kept the last one's files.
        var dir = Path.Combine(_root, "repo-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(Path.Combine(dir, "docs"));
        if (atDocs is not null) File.WriteAllText(Path.Combine(dir, "docs", "code-map.json"), atDocs);
        if (atRoot is not null) File.WriteAllText(Path.Combine(dir, "code-map.json"), atRoot);
        return dir;
    }

    [Fact]
    public void A_valid_file_is_read_whole()
    {
        var read = CodeMapReader.Read(Checkout(atDocs: Valid));

        Assert.Null(read.Problem);
        Assert.Equal("docs/code-map.json", read.File);
        Assert.Equal(["web", "service"], read.Map!.Modules.Select(m => m.Id));
        Assert.Equal("indexes the family's knowledge", read.Map.Modules[1].Summary);
        Assert.Equal(new CodeDependency("web", "service", "http"), read.Map.Dependencies.Single());
    }

    /// <summary>Candidates, like the decisions log: the first that exists wins, and none is an answer.</summary>
    [Fact]
    public void The_file_is_found_by_convention_and_its_absence_is_an_answer()
    {
        Assert.Equal("code-map.json", CodeMapReader.Read(Checkout(atRoot: Valid)).File);
        Assert.Equal("docs/code-map.json", CodeMapReader.Read(Checkout(atDocs: Valid, atRoot: "{}")).File);

        var none = CodeMapReader.Read(Checkout());
        Assert.Null(none.File);
        Assert.Null(none.Map);
        Assert.Null(none.Problem);
    }

    /// <summary>
    /// 🔴 Judged whole: a file that breaks any rule is refused, naming the first break, and NOTHING of
    /// it is shown — a half-drawn map reads as a whole one.
    /// </summary>
    [Theory]
    [InlineData("not json", "not JSON")]
    [InlineData("""{ "version": 2, "modules": [], "dependencies": [] }""", "version 2")]
    [InlineData("""{ "version": 1, "modules": [{ "id": "a", "path": "a", "summary": "" }, { "id": "a", "path": "b", "summary": "" }], "dependencies": [] }""", "`a` twice")]
    [InlineData("""{ "version": 1, "modules": [{ "id": "a", "path": "a", "summary": "" }], "dependencies": [{ "from": "a", "to": "ghost", "kind": "imports" }] }""", "`ghost`")]
    [InlineData("""{ "version": 1, "modules": [{ "id": "a", "path": "C:/work/a", "summary": "" }], "dependencies": [] }""", "repository-relative")]
    [InlineData("""{ "version": 1, "modules": [{ "id": "a", "path": "/src/a", "summary": "" }], "dependencies": [] }""", "repository-relative")]
    [InlineData("""{ "version": 1, "modules": [{ "id": "a", "path": "../elsewhere", "summary": "" }], "dependencies": [] }""", "repository-relative")]
    [InlineData("""{ "version": 1, "modules": [{ "id": "", "path": "a", "summary": "" }], "dependencies": [] }""", "an id")]
    [InlineData("""{ "version": 1, "modules": [{ "id": "a", "path": "a", "summary": "one\ntwo" }], "dependencies": [] }""", "one line")]
    public void A_file_that_breaks_a_rule_is_refused_whole_naming_the_break(string json, string named)
    {
        var read = CodeMapReader.Read(Checkout(atDocs: json));

        Assert.Null(read.Map);
        Assert.Equal("docs/code-map.json", read.File);
        Assert.Contains(named, read.Problem);
    }

    [Fact]
    public void A_map_past_its_bound_is_refused()
    {
        var modules = string.Join(",", Enumerable.Range(0, CodeMapReader.MaxModules + 1)
            .Select(i => $$"""{ "id": "m{{i}}", "path": "src/m{{i}}", "summary": "" }"""));

        var read = CodeMapReader.Read(Checkout(atDocs: $$"""{ "version": 1, "modules": [{{modules}}], "dependencies": [] }"""));

        Assert.Null(read.Map);
        Assert.Contains($"{CodeMapReader.MaxModules}", read.Problem);
    }

    /// <summary>
    /// 🔴 MAP3c's twin, from the judge's side. This repository's own map is written by the devkit
    /// (`daoris-devkit map`), a separate artefact that shares no code with this reader — so the file it
    /// committed is judged here, whole, by the reader every deployment uses. The devkit's tests hold its
    /// restated rules to this file's source; this holds its output to this reader.
    /// </summary>
    [Fact]
    public void This_repositorys_own_map_as_the_devkit_wrote_it_is_judged_whole()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "daoris.json"))) root = root.Parent;
        Assert.NotNull(root);

        var read = CodeMapReader.Read(root.FullName);

        Assert.Equal("docs/code-map.json", read.File);
        Assert.Null(read.Problem);
        Assert.Contains(read.Map!.Modules, m => m.Id == "Daoris.Service.Core" && m.Path == "src/Daoris.Service/Daoris.Service.Core");
        Assert.Contains(read.Map.Dependencies, d => d is { From: "Daoris.Service.Http", To: "Daoris.Service.Core", Kind: "project" });
    }

    /// <summary>The service answers for a registered repository by reading its checkout — nothing is kept.</summary>
    [Fact]
    public async Task The_service_reads_a_registered_checkout_on_each_ask()
    {
        var family = Path.Combine(_root, "family");
        var engine = Path.Combine(family, "engine");
        Directory.CreateDirectory(Path.Combine(engine, "docs"));
        File.WriteAllText(Path.Combine(engine, "daoris.json"), """{ "source": "s", "packs": [] }""");
        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.ImportAsync(family, DateTimeOffset.UnixEpoch);

        Assert.Null((await service.CodeMapAsync("engine"))!.File);

        File.WriteAllText(Path.Combine(engine, "docs", "code-map.json"), Valid);
        var read = (await service.CodeMapAsync("engine"))!;
        Assert.Equal(2, read.Map!.Modules.Count);

        // A repository nobody registered is not an empty map; it is no answer at all.
        Assert.Null(await service.CodeMapAsync("nobody"));
    }
}
