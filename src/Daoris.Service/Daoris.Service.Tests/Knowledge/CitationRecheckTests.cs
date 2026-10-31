using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// KNOWCENTRAL2a (the memory coordinator design's §5): a record cited by its entry id, the stored reading's commit and its
/// body's digest, and rechecked against the index's stored reading before it is shown as current.
/// </summary>
public sealed class CitationRecheckTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-cite-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly List<IAsyncDisposable> _held = [];

    private string Database => Path.Combine(_root, "knowledge.db");

    private string Checkout => Path.Combine(_root, "family", "served");

    private const string Opening = "served:docs/decisions/D60.md";

    /// <summary>D10: superseded by D50 in its own record.</summary>
    private const string Superseded =
        "# D10: The probe lock\n\nAardvark opening: one probe at a time.\n\n"
        + "**Built 2026-10-01 (LOCK1a).** Quokka note: the lock is held.\n\n"
        + "**Superseded by D50, 2026-10-20.** Wombat note: the lock moved.\n";

    private const string Unmarked = "# D60: A choice nobody replaced\n\nPlatypus opening.\n\n**Built 2026-10-03 (PICK1a).** Echidna note.\n";

    private static readonly Func<string, string, bool?> GitProbe = UncommittedRecords.OfFile;

    public Task InitializeAsync()
    {
        Write("family/served/daoris.json", """{ "source": "s", "documents": { "decisions": "docs/decisions" } }""");
        Write("family/served/docs/decisions/D10.md", Superseded);
        Write("family/served/docs/decisions/D60.md", Unmarked);
        Git("init", "-q", "-b", "main");
        Commit("first");
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        foreach (var held in _held) await held.DisposeAsync();
        SqlitePools.Release(Database);
        SqlitePools.Release(Path.Combine(_root, "fed.db"));
        // Git writes its objects read-only, which a recursive delete on Windows refuses.
        if (Directory.Exists(_root))
        {
            foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(_root, recursive: true);
        }
    }

    private void Write(string relative, string content)
    {
        var file = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    private void Git(params string[] arguments)
    {
        var start = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = Checkout, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "-c", "user.name=t", "-c", "user.email=t@example.test", "-c", "commit.gpgsign=false" }.Concat(arguments)) start.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(start)!;
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }

    private void Commit(string message)
    {
        Git("add", "-A");
        Git("commit", "-q", "-m", message);
    }

    private string Head() => File.ReadAllText(Path.Combine(Checkout, ".git", "refs", "heads", "main")).Trim();

    /// <summary>A connector of its own over the one file, reading the checkout as a registered root.</summary>
    private async Task<KnowledgeService> ServeAsync(Func<string, string, bool?>? probe, Func<string, RootSignature>? signature = null)
    {
        var store = await SqliteKnowledgeStore.OpenAsync(Database);
        _held.Add(store);
        var service = new KnowledgeService(
            store, new SqliteKnowledgeSearch(store),
            new FileSystemKnowledgeSource(() => [Checkout], new RepositoryScanner(documents: "docs")), DisclosurePolicy.LocalOnly,
            registry: new Registry(), registrations: await RegistrationStore.OpenAsync(store.Connection), readsRegisteredRoots: true,
            rootSignature: signature ?? (root => RootSignature.Of(root, "docs")), fileUncommitted: probe);
        await service.RegisterAsync(RegistryImport.ProposeAt(Checkout), DateTimeOffset.UtcNow);
        await service.RefreshAsync();
        return service;
    }

    private static Citation Cited(CitationAnswer answer) => answer.Cited ?? throw new Xunit.Sdk.XunitException(answer.Refused);

    [Fact]
    public async Task A_record_superseded_since_it_was_cited_fails_naming_the_record_that_superseded_it()
    {
        var service = await ServeAsync(GitProbe);
        var cited = Cited(await service.CiteAsync(Opening));

        Write("family/served/docs/decisions/D60.md", Unmarked + "\n**Superseded by D70, 2026-10-31.** Gannet note.\n");
        Commit("superseded");
        var check = await service.RecheckAsync(cited);

        Assert.Equal((CitationStanding.Failed, "D70"), (check.Standing, check.By));
    }

    [Fact]
    public async Task A_record_amended_since_it_was_cited_rechecks_as_amended()
    {
        var service = await ServeAsync(GitProbe);
        var cited = Cited(await service.CiteAsync(Opening));

        Write("family/served/docs/decisions/D60.md", Unmarked + "\n**Amended by D71, 2026-10-31.** Gannet note.\n");
        Commit("amended");
        var check = await service.RecheckAsync(cited);

        Assert.Equal((CitationStanding.Amended, "D71", "cited record amended by D71"), (check.Standing, check.By, check.Says));
    }

    [Fact]
    public async Task A_cited_file_modified_or_whose_probe_failed_or_that_no_probe_reads_is_inconclusive()
    {
        var failed = await ServeAsync((_, _) => null);
        var unprobed = await ServeAsync(null);
        var probed = await ServeAsync(GitProbe);
        var cited = Cited(await probed.CiteAsync(Opening));
        Assert.Equal(CitationStanding.Holds, (await probed.RecheckAsync(cited)).Standing);

        Assert.Equal(CitationStanding.Inconclusive, (await failed.RecheckAsync(cited)).Standing);
        Assert.Equal(CitationStanding.Inconclusive, (await unprobed.RecheckAsync(cited)).Standing);

        Write("family/served/docs/decisions/D60.md", Unmarked + "\nA line not yet committed.\n");
        var modified = await probed.RecheckAsync(cited);
        Assert.Equal((CitationStanding.Inconclusive, false), (modified.Standing, modified.CiteAgain));
    }

    [Fact]
    public async Task An_edited_body_is_inconclusive_and_asks_to_be_cited_again()
    {
        var service = await ServeAsync(GitProbe);
        var cited = Cited(await service.CiteAsync(Opening));

        Write("family/served/docs/decisions/D60.md", Unmarked.Replace("Platypus opening.", "Platypus opening, edited.", StringComparison.Ordinal));
        Commit("edited");
        var check = await service.RecheckAsync(cited);

        Assert.Equal((CitationStanding.Inconclusive, true), (check.Standing, check.CiteAgain));
    }

    [Fact]
    public async Task The_same_body_at_a_later_commit_or_beside_another_files_change_holds()
    {
        var service = await ServeAsync(GitProbe);
        var cited = Cited(await service.CiteAsync(Opening));

        Write("family/served/docs/decisions/D10.md", Superseded + "\nAnother file's change, not yet committed.\n");
        Assert.Equal(CitationStanding.Holds, (await service.RecheckAsync(cited)).Standing);

        Commit("another file");
        var later = await service.RecheckAsync(cited);
        Assert.Equal((CitationStanding.Holds, Head()), (later.Standing, later.Commit));
        Assert.NotEqual(cited.Commit, later.Commit);
    }

    [Fact]
    public async Task A_removed_id_asks_to_be_cited_again_on_the_third_try()
    {
        var service = await ServeAsync(GitProbe);
        var cited = Cited(await service.CiteAsync(Opening));
        File.Delete(Path.Combine(Checkout, "docs", "decisions", "D60.md"));
        Commit("removed");

        var tries = new List<CitationCheck>();
        for (var attempt = 1; attempt <= 3; attempt++) tries.Add(await service.RecheckAsync(cited, attempt));

        Assert.All(tries, check => Assert.Equal(CitationStanding.Inconclusive, check.Standing));
        Assert.Equal([false, false, true], tries.Select(check => check.CiteAgain));
    }

    [Fact]
    public async Task A_fed_reading_is_cited_and_rechecked_at_the_commit_it_was_fed_at()
    {
        var composed = await ServiceFactory.CreateAsync(
            new ServiceOptions(Path.Combine(_root, "unread"), Path.Combine(_root, "fed.db")), source: new EmptyKnowledgeSource());
        _held.Add(composed);
        var at = DateTimeOffset.Parse("2026-10-31T09:00:00Z");
        await composed.Service.RegisterAsync(new Registration(
            "fedrepo", Adopted: true, "A repo.", ["x"], [], [], Entries: 0, Joined: true, SharesKnowledge: true, DefaultBranch: "main"), at);
        var fed = await composed.Service.FeedAsync(
            "fedrepo",
            [new KnowledgeEntry("fedrepo", EntryKind.Decision, Provenance.Local, "D60: A choice", "Platypus opening.", "docs/decisions/D60.md", Lines: new LineSpan(1, 1))],
            new FeedProvenance("aaaa1111bbbb2222cccc", at, "main"));
        Assert.True(fed.Accepted, fed.Message);

        var cited = Cited(await composed.Service.CiteAsync("fedrepo:docs/decisions/D60.md"));
        var check = await composed.Service.RecheckAsync(cited);

        Assert.Equal(("aaaa1111bbbb2222cccc", true), (cited.Commit, cited.Fed));
        Assert.Equal((CitationStanding.Holds, "aaaa1111bbbb2222cccc", true), (check.Standing, check.Commit, check.Fed));
    }

    [Fact]
    public async Task No_reading_or_a_root_moved_during_the_check_is_inconclusive()
    {
        // A write that read no source names no commit, so nothing it left can be cited or held.
        var bare = new InMemoryKnowledgeStore();
        var entry = new KnowledgeEntry("bare", EntryKind.Knowledge, Provenance.Local, "Pelican", "About pelicans.", "docs/pelican.md");
        await bare.ReplaceRepositoryAsync("bare", [entry]);
        var unread = new KnowledgeService(bare, new LexicalKnowledgeSearch(bare), new EmptyKnowledgeSource());
        Assert.Null((await unread.CiteAsync(entry.Id)).Cited);
        Assert.Equal(
            CitationStanding.Inconclusive,
            (await unread.RecheckAsync(new Citation(entry.Id, null, "aaaa1111", false, FeedDigest.Of(entry.Body)))).Standing);

        // The root's signature moves while its file's state is asked.
        var moved = false;
        var service = await ServeAsync(
            (_, _) =>
            {
                moved = true;
                return false;
            },
            root => moved ? RootSignature.Of(root, "docs") with { Files = -1 } : RootSignature.Of(root, "docs"));
        var cited = Cited(await service.CiteAsync(Opening));

        Assert.Equal(CitationStanding.Inconclusive, (await service.RecheckAsync(cited)).Standing);
    }

    [Fact]
    public async Task A_superseded_entry_is_refused_at_citation_naming_its_successor()
    {
        var service = await ServeAsync(GitProbe);

        var answer = await service.CiteAsync("served:docs/decisions/D10.md");

        Assert.Null(answer.Cited);
        Assert.Equal("D50", answer.Successor);
    }
}
