using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// A deployment that serves one checkout (ORIENT1c): a workspace's own knowledge server, which agents
/// working in that checkout ask where something is and what was decided. It registers that checkout
/// alone, reads its documents and index where it names them, and re-reads the checkout as it moves.
/// </summary>
public sealed class OneCheckoutTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-checkout-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string Write(string relative, string content)
    {
        var file = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
        return file;
    }

    /// <summary>A family folder of two repositories, the one served and a sibling beside it.</summary>
    private (string Family, string Checkout) Family()
    {
        Write("family/served/daoris.json", """{ "source": "s" }""");
        Write("family/served/docs/design.md", "# The probe lock\n\n## Why a lock\n\nOne probe at a time.\n");
        Write("family/sibling/daoris.json", """{ "source": "s" }""");
        Write("family/sibling/.claude/knowledge/note.md", "# note\n\nThe sibling's probe lock.\n");
        return (Path.Combine(_root, "family"), Path.Combine(_root, "family", "served"));
    }

    private static Func<string, string?> Environment(params (string Name, string Value)[] set) =>
        name => set.FirstOrDefault(pair => pair.Name == name).Value;

    [Fact]
    public void The_environment_names_the_checkout_its_documents_and_its_index()
    {
        var (_, checkout) = Family();

        var (options, error) = ServiceOptions.FromEnvironment("root", "db", Environment(
            (ServiceOptions.RepositoryVariable, checkout),
            (ServiceOptions.DocumentsVariable, "docs"),
            (ServiceOptions.IndexVariable, "docs\\index\\")));

        Assert.Null(error);
        Assert.Equal(checkout, options.Repository);
        Assert.Equal("docs", options.Documents);
        // One spelling: forward slashes and no trailing one, as the scanner names every path.
        Assert.Equal("docs/index", options.Index);
    }

    [Fact]
    public void Silence_names_no_checkout_no_documents_and_no_index()
    {
        var (options, error) = ServiceOptions.FromEnvironment("root", "db", Environment());

        Assert.Null(error);
        Assert.Null(options.Repository);
        Assert.Null(options.Documents);
        Assert.Null(options.Index);
    }

    /// <summary>A setting that cannot mean what it says is refused, never quietly read as something else.</summary>
    [Theory]
    [InlineData(ServiceOptions.DocumentsVariable, "../elsewhere")]
    [InlineData(ServiceOptions.DocumentsVariable, "/etc")]
    [InlineData(ServiceOptions.IndexVariable, "C:/docs")]
    [InlineData(ServiceOptions.IndexVariable, "docs/../../up")]
    public void A_folder_outside_the_repository_is_refused(string variable, string value)
    {
        var (_, error) = ServiceOptions.FromEnvironment("root", "db", Environment((variable, value)));

        Assert.NotNull(error);
        Assert.Contains(variable, error);
        Assert.Contains(value, error);
    }

    [Fact]
    public void A_checkout_that_is_not_a_folder_is_refused()
    {
        var missing = Path.Combine(_root, "nowhere");

        var (_, error) = ServiceOptions.FromEnvironment("root", "db", Environment((ServiceOptions.RepositoryVariable, missing)));

        Assert.NotNull(error);
        Assert.Contains(ServiceOptions.RepositoryVariable, error);
        Assert.Contains(missing, error);
    }

    [Fact]
    public async Task A_store_over_one_checkout_registers_that_checkout_alone()
    {
        var (family, checkout) = Family();
        var options = new ServiceOptions(family, Path.Combine(_root, "knowledge.db"), Repository: checkout, Documents: "docs");

        await using var composed = await ServiceFactory.CreateAsync(options);

        var registered = Assert.Single(await composed.Service.RegistryAsync());
        Assert.Equal("served", registered.Repository);
        Assert.Equal(checkout, registered.Root);
        var hits = await composed.Service.SearchAsync(new KnowledgeQuery("probe lock"));
        Assert.NotEmpty(hits);
        Assert.All(hits, hit => Assert.Equal("served", hit.Entry.Repository));
        Assert.Contains(hits, hit => hit.Entry.Title == "Why a lock");
    }

    /// <summary>
    /// The store persists and the checkout moves under it: a document written after the index was built is
    /// found by the next process over the same store, because each re-reads the checkout at its first use.
    /// </summary>
    [Fact]
    public async Task Each_process_over_one_checkout_re_reads_it_at_its_first_use()
    {
        var (family, checkout) = Family();
        var options = new ServiceOptions(family, Path.Combine(_root, "knowledge.db"), Repository: checkout, Documents: "docs");
        await using (var first = await ServiceFactory.CreateAsync(options))
        {
            Assert.Empty(await first.Service.SearchAsync(new KnowledgeQuery("zeppelins")));
        }

        Write("family/served/docs/later.md", "# Zeppelins\n\nWritten after the index was built.\n");

        await using var second = await ServiceFactory.CreateAsync(options);
        Assert.Contains(await second.Service.SearchAsync(new KnowledgeQuery("zeppelins")), hit => hit.Entry.Title == "Zeppelins");
    }

    /// <summary>
    /// A long-lived process over one checkout re-reads it once its last reading is older than the window, and
    /// not before: a session's server lives as long as the session, and the checkout is merged into meanwhile.
    /// </summary>
    [Fact]
    public async Task A_long_lived_process_re_reads_the_checkout_once_its_reading_is_older_than_the_window()
    {
        var (_, checkout) = Family();
        var time = new SteppedTime(DateTimeOffset.Parse("2026-10-04T10:00:00Z"));
        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new FileSystemKnowledgeSource([checkout], new RepositoryScanner(documents: "docs")),
            rereadAfter: TimeSpan.FromMinutes(1), time: time);
        Assert.Empty(await service.SearchAsync(new KnowledgeQuery("zeppelins")));

        Write("family/served/docs/later.md", "# Zeppelins\n\nWritten while the session ran.\n");
        time.Advance(TimeSpan.FromSeconds(59));
        Assert.Empty(await service.SearchAsync(new KnowledgeQuery("zeppelins")));

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Contains(await service.SearchAsync(new KnowledgeQuery("zeppelins")), hit => hit.Entry.Title == "Zeppelins");
    }

    /// <summary>A deployment over a folder of repositories re-reads nothing on its own, as before.</summary>
    [Fact]
    public async Task A_process_with_no_window_reads_once_and_keeps_what_it_read()
    {
        var (_, checkout) = Family();
        var time = new SteppedTime(DateTimeOffset.Parse("2026-10-04T10:00:00Z"));
        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new FileSystemKnowledgeSource([checkout], new RepositoryScanner(documents: "docs")),
            time: time);
        Assert.Empty(await service.SearchAsync(new KnowledgeQuery("zeppelins")));

        Write("family/served/docs/later.md", "# Zeppelins\n\nWritten while the session ran.\n");
        time.Advance(TimeSpan.FromHours(1));

        Assert.Empty(await service.SearchAsync(new KnowledgeQuery("zeppelins")));
    }

    /// <summary>The clock a test moves by hand.</summary>
    private sealed class SteppedTime(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
