using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// A search hit says its standing (KNOWFRESH1a-ii; the knowledge-freshness design's §3.1 and §5): the commit its reading was
/// taken at, <c>+ uncommitted</c> where the root's record folders differ from it, <c>fed at</c> where a feed gave the
/// reading, and for a decision the notes that follow it and the mark a later note of its own record puts on it, read from the
/// note entries so a fed reading derives the same. The reading is taken once, so the cost is one <c>git status</c> a root and
/// never one a hit.
/// </summary>
public sealed class KnowledgeStandingTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-standing-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        SqlitePools.Release(Path.Combine(_root, "knowledge.db"));
        // Git writes its objects read-only, which a recursive delete on Windows refuses.
        if (Directory.Exists(_root))
        {
            foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>D10: three notes, the last marking it superseded by D50; D60: one note and no mark.</summary>
    private const string Superseded =
        "# D10: The probe lock\n\nAardvark opening: one probe at a time.\n\n"
        + "**Built 2026-10-01 (LOCK1a).** Quokka note: the lock is held.\n\n"
        + "**Amended 2026-10-02 (LOCK1b).** Narwhal note: the lock is kept per root.\n\n"
        + "**Superseded by D50, 2026-10-20.** Wombat note: the lock moved.\n";

    private const string Unmarked = "# D60: A choice nobody replaced\n\nPlatypus opening.\n\n**Built 2026-10-03 (PICK1a).** Echidna note.\n";

    private void Write(string relative, string content)
    {
        var file = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    private static void Git(string folder, params string[] arguments)
    {
        var start = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = folder, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "-c", "user.name=t", "-c", "user.email=t@example.test", "-c", "commit.gpgsign=false" }.Concat(arguments)) start.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(start)!;
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }

    private static string Head(string folder) =>
        File.ReadAllText(Path.Combine(folder, ".git", "refs", "heads", "main")).Trim();

    /// <summary>A committed checkout of two decisions, registered under a folder of its own.</summary>
    private string Committed()
    {
        Write("family/served/daoris.json", """{ "source": "s", "documents": { "decisions": "docs/decisions" } }""");
        Write("family/served/docs/decisions/D10.md", Superseded);
        Write("family/served/docs/decisions/D60.md", Unmarked);
        var checkout = Path.Combine(_root, "family", "served");
        Git(checkout, "init", "-q", "-b", "main");
        Git(checkout, "add", "-A");
        Git(checkout, "commit", "-q", "-m", "first");
        return checkout;
    }

    private async Task<(ComposedService Composed, KnowledgeSearchTools Tools)> Serve()
    {
        var composed = await ServiceFactory.CreateAsync(new ServiceOptions(Path.Combine(_root, "family"), Path.Combine(_root, "knowledge.db")));
        return (composed, new KnowledgeSearchTools(composed.Service, new WorkspaceScope(composed.Service, new AmbientWorkspace(Path.GetTempPath()))));
    }

    /// <summary>The standing line of the one hit whose id holds <paramref name="idPart"/>.</summary>
    private static string StandingOf(string answer, string idPart)
    {
        var hit = answer.Split("### ").Single(section => section.Contains($"id: `") && section.Contains(idPart));
        return Lines(hit).Single(line => line.StartsWith("standing: ", StringComparison.Ordinal));
    }

    private static string[] Lines(string text) => [.. text.Split('\n').Select(line => line.TrimEnd('\r'))];

    /// <summary>The row's proof: an opening says it was superseded, its note says what follows it, an unmarked decision says so.</summary>
    [Fact]
    public async Task A_decision_says_the_commit_it_was_read_at_what_follows_it_and_the_mark_it_carries()
    {
        var checkout = Committed();
        var (composed, tools) = await Serve();
        await using var _ = composed;
        var read = Head(checkout)[..8];

        var opening = StandingOf(await tools.SearchAsync("Aardvark", "decision", workspace: "all"), "D10.md`");
        var note = StandingOf(await tools.SearchAsync("Quokka", "decision", workspace: "all"), "D10.md#");
        var marking = StandingOf(await tools.SearchAsync("Wombat", "decision", workspace: "all"), "D10.md#");
        var unmarked = StandingOf(await tools.SearchAsync("Platypus", "decision", workspace: "all"), "D60.md`");

        Assert.Equal($"standing: read at {read} · later: 3 notes · superseded by D50", opening);
        Assert.Equal($"standing: read at {read} · later: 2 notes · no mark", note);
        Assert.Equal($"standing: read at {read} · later: 0 notes · no mark", marking);
        Assert.Equal($"standing: read at {read} · later: 1 note · no mark", unmarked);
    }

    /// <summary>A mark that names an earlier note by <c>replacing</c> lands on that note, and the opening stays unmarked.</summary>
    [Fact]
    public async Task A_mark_that_replaces_a_note_stands_on_that_note_and_not_on_the_opening()
    {
        Write("family/served/daoris.json", """{ "source": "s", "documents": { "decisions": "docs/decisions" } }""");
        Write(
            "family/served/docs/decisions/D20.md",
            "# D20: Retry\n\nHeron opening.\n\n**Built 2026-10-01 (RETRY1a).** Ibis note: retry thrice.\n\n"
            + "**Amended by D21, replacing \"Built 2026-10-01 (RETRY1a).\"** Kiwi note: retry once.\n");
        var checkout = Path.Combine(_root, "family", "served");
        Git(checkout, "init", "-q", "-b", "main");
        Git(checkout, "add", "-A");
        Git(checkout, "commit", "-q", "-m", "first");
        var (composed, tools) = await Serve();
        await using var _ = composed;

        Assert.EndsWith("no mark", StandingOf(await tools.SearchAsync("Heron", "decision", workspace: "all"), "D20.md`"));
        Assert.EndsWith("later: 1 note · amended by D21", StandingOf(await tools.SearchAsync("Ibis", "decision", workspace: "all"), "D20.md#"));
    }

    [Fact]
    public async Task A_root_with_uncommitted_records_says_so_and_a_clean_one_does_not()
    {
        var checkout = Committed();
        var (clean, cleanTools) = await Serve();
        await using (clean)
        {
            Assert.DoesNotContain("uncommitted", await cleanTools.SearchAsync("Platypus", "decision", workspace: "all"));
        }

        Write("family/served/docs/decisions/D60.md", Unmarked + "\n**Built 2026-10-04 (PICK1b).** Quail note, not yet committed.\n");
        var (dirty, dirtyTools) = await Serve();
        await using var _ = dirty;

        var read = Head(checkout)[..8];
        Assert.Equal(
            $"standing: read at {read} + uncommitted · later: 2 notes · no mark",
            StandingOf(await dirtyTools.SearchAsync("Platypus", "decision", workspace: "all"), "D60.md`"));
        Assert.Contains($"read at {read} + uncommitted", StandingOf(await dirtyTools.SearchAsync("Quail", "decision", workspace: "all"), "D60.md#"));
    }

    [Fact]
    public async Task Knowledge_get_says_the_same_as_the_hit()
    {
        Committed();
        var (composed, tools) = await Serve();
        await using var _ = composed;
        var hit = await tools.SearchAsync("Aardvark", "decision", workspace: "all");

        var read = await tools.GetAsync("served:docs/decisions/D10.md");

        Assert.Contains(StandingOf(hit, "D10.md`"), Lines(read));
        Assert.Contains(StandingOf(hit, "D10.md`"), Lines(await tools.GetAsync("served:docs/decisions/D10.md", "1-3")));
    }

    /// <summary>A kind that is no decision says only the reading, and a reading that is nowhere says it is unknown.</summary>
    [Fact]
    public async Task A_hit_of_another_kind_says_only_its_reading_and_one_with_no_reading_says_unknown()
    {
        Write("family/served/daoris.json", """{ "source": "s", "documents": { "decisions": "docs/decisions" } }""");
        Write("family/served/docs/decisions/D10.md", Superseded);
        Write("family/served/.claude/knowledge/pelican.md", "# Pelican\n\nAbout pelicans.\n");
        var checkout = Path.Combine(_root, "family", "served");
        Git(checkout, "init", "-q", "-b", "main");
        Git(checkout, "add", "-A");
        Git(checkout, "commit", "-q", "-m", "first");
        var (composed, tools) = await Serve();
        await using var _ = composed;
        Assert.Equal($"standing: read at {Head(checkout)[..8]}", StandingOf(await tools.SearchAsync("Pelican", workspace: "all"), "pelican.md"));

        // A service composed over a folder it was never told is a registered root names no reading at all.
        var store = new InMemoryKnowledgeStore();
        var bare = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new FileSystemKnowledgeSource([checkout], new RepositoryScanner(documents: "docs")));
        var unread = new KnowledgeSearchTools(bare, new WorkspaceScope(bare, new AmbientWorkspace(Path.GetTempPath())));
        Assert.Equal("standing: unknown", StandingOf(await unread.SearchAsync("Pelican", workspace: "all"), "pelican.md"));
    }

    /// <summary>
    /// A fed reading round-trips its commit and its marks: the entries arrive as the scanner makes them and carry no field of
    /// the mark's own, and the fed host says the commit the feed named and the mark its entries hold.
    /// </summary>
    [Fact]
    public async Task A_fed_reading_says_the_commit_it_was_fed_at_and_derives_its_marks_from_the_entries()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var registrations = await RegistrationStore.OpenAsync(connection);
        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly,
            registry: new Registry(), registrations: registrations);
        var at = DateTimeOffset.Parse("2026-10-30T09:00:00Z");
        await service.RegisterAsync(
            new Registration("fedrepo", Adopted: true, "A repo.", ["x"], [], [], Entries: 0, Joined: true, SharesKnowledge: true), at);

        var fed = await service.FeedAsync(
            "fedrepo", Scanned("fedrepo", "docs/decisions/D10.md", Superseded), new FeedProvenance("aaaa1111bbbb2222cccc", at, "main"));
        var tools = new KnowledgeSearchTools(service, new WorkspaceScope(service, new AmbientWorkspace(Path.GetTempPath())));

        Assert.True(fed.Accepted, fed.Message);
        Assert.Equal(
            "standing: fed at aaaa1111 · later: 3 notes · superseded by D50",
            StandingOf(await tools.SearchAsync("Aardvark", "decision", workspace: "all"), "D10.md`"));
        Assert.Equal(
            "standing: fed at aaaa1111 · later: 2 notes · no mark",
            StandingOf(await tools.SearchAsync("Quokka", "decision", workspace: "all"), "D10.md#"));
    }

    /// <summary>The entries a scan of a decision's file makes, as <c>RepositoryScanner</c> makes them.</summary>
    private static List<KnowledgeEntry> Scanned(string repository, string relative, string text)
    {
        var (entry, notes) = DecisionNotes.Split(text);
        var entries = new List<KnowledgeEntry>
        {
            new(repository, EntryKind.Decision, Provenance.Local, "D10: The probe lock", entry, relative,
                Lines: new LineSpan(1, entry.Split('\n').Length)),
        };
        entries.AddRange(notes.Select(note => new KnowledgeEntry(
            repository, EntryKind.Decision, Provenance.Local, $"D10 › {note.Label}", note.Body, relative, note.Label,
            Lines: new LineSpan(note.First, note.Last))));
        return entries;
    }

    /// <summary>
    /// The cost: the reading asks git once for each root it reads, and a search however many hits it answers, and however
    /// often, asks it no more (the design's §3.1: no git a hit).
    /// </summary>
    [Fact]
    public async Task The_reading_asks_git_once_a_root_and_a_hit_asks_it_nothing()
    {
        var checkout = Committed();
        var asked = new List<string>();
        var store = new InMemoryKnowledgeStore();
        var registry = new Registry();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store),
            new FileSystemKnowledgeSource(() => [checkout], new RepositoryScanner(documents: "docs")), DisclosurePolicy.LocalOnly,
            registry: registry, readsRegisteredRoots: true, uncommitted: root =>
            {
                asked.Add(root);
                return false;
            });
        await service.RegisterAsync(RegistryImport.ProposeAt(checkout), DateTimeOffset.UtcNow);
        var tools = new KnowledgeSearchTools(service, new WorkspaceScope(service, new AmbientWorkspace(Path.GetTempPath())));

        for (var round = 0; round < 3; round++)
        {
            var said = await tools.SearchAsync("note", "decision", limit: 10, workspace: "all");
            Assert.Contains("standing: read at", said);
        }

        Assert.Equal([checkout], asked);
    }

    /// <summary>A status that fails says the root's uncommitted state is unknown, never that it is clean (KNOWCENTRAL2a).</summary>
    [Fact]
    public async Task A_failed_status_says_uncommitted_unknown()
    {
        var checkout = Committed();
        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store),
            new FileSystemKnowledgeSource(() => [checkout], new RepositoryScanner(documents: "docs")), DisclosurePolicy.LocalOnly,
            registry: new Registry(), readsRegisteredRoots: true, uncommitted: _ => throw new IOException("git did not start"));
        await service.RegisterAsync(RegistryImport.ProposeAt(checkout), DateTimeOffset.UtcNow);
        var tools = new KnowledgeSearchTools(service, new WorkspaceScope(service, new AmbientWorkspace(Path.GetTempPath())));

        Assert.Equal(
            $"standing: read at {Head(checkout)[..8]} + uncommitted unknown · later: 1 note · no mark",
            StandingOf(await tools.SearchAsync("Platypus", "decision", workspace: "all"), "D60.md`"));
    }

    /// <summary>
    /// KNOWCENTRAL2a: a hit's standing reads the entry, its file's marks and the reading together, from the index every connector
    /// writes. Another connector writing, restamping or retiring the repository between the hit and its standing never leaves
    /// one reading's commit beside another's marks.
    /// </summary>
    [Fact]
    public async Task A_second_connector_writing_restamping_or_retiring_between_hit_and_standing_never_mixes_readings()
    {
        var checkout = Committed();
        var (found, _) = await Serve();
        await using var _ = found;
        var (other, _) = await Serve();
        await using var __ = other;
        var first = Head(checkout)[..8];
        async Task<KnowledgeEntry> HitAsync() =>
            Assert.Single(await found.Service.SearchAsync(new KnowledgeQuery("Platypus")), hit => hit.Entry.Anchor is null).Entry;

        var hit = await HitAsync();
        Write("family/served/docs/decisions/D60.md", Unmarked + "\n**Superseded by D70, 2026-10-31.** Gannet note.\n");
        Git(checkout, "commit", "-q", "-am", "superseded");
        var second = Head(checkout)[..8];
        await other.Service.RefreshAsync();
        var written = (await found.Service.StandingsAsync([hit]))[hit.Id].Says;
        Assert.StartsWith($"standing: read at {second}", written);
        Assert.EndsWith("later: 2 notes · superseded by D70", written);

        hit = await HitAsync();
        await other.Service.RegisterAsync(RegistryImport.ProposeAt(checkout) with { Workspace = "elsewhere" }, DateTimeOffset.UtcNow);
        var restamped = (await found.Service.StandingsAsync([hit]))[hit.Id].Says;
        Assert.StartsWith($"standing: read at {second}", restamped);
        Assert.EndsWith("superseded by D70", restamped);

        hit = await HitAsync();
        Assert.True(await other.Service.RetireAsync("served"));
        Assert.Equal("standing: unknown", (await found.Service.StandingsAsync([hit]))[hit.Id].Says);
        Assert.DoesNotContain(first, written + restamped);
    }

    /// <summary>
    /// A git that is slow to start never holds an answer: the registry answers while the status is pending, a hit says it was not
    /// yet read, and once the status lands the hit says what it found.
    /// </summary>
    [Fact]
    public async Task A_slow_status_holds_neither_the_registry_nor_a_hit_and_a_hit_says_not_yet_read_until_it_lands()
    {
        var checkout = Committed();
        using var release = new ManualResetEventSlim(false);
        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store),
            new FileSystemKnowledgeSource(() => [checkout], new RepositoryScanner(documents: "docs")), DisclosurePolicy.LocalOnly,
            registry: new Registry(), readsRegisteredRoots: true, uncommitted: root =>
            {
                release.Wait(TimeSpan.FromSeconds(60));
                return true;
            });
        await service.RegisterAsync(RegistryImport.ProposeAt(checkout), DateTimeOffset.UtcNow);
        var tools = new KnowledgeSearchTools(service, new WorkspaceScope(service, new AmbientWorkspace(Path.GetTempPath())));

        try
        {
            var registry = service.RegistryAsync();
            Assert.Same(registry, await Task.WhenAny(registry, Task.Delay(TimeSpan.FromSeconds(2))));
            Assert.NotEmpty(await registry);

            var read = Head(checkout)[..8];
            Assert.Equal(
                $"standing: read at {read} + uncommitted not yet read · later: 1 note · no mark",
                StandingOf(await tools.SearchAsync("Platypus", "decision", workspace: "all"), "D60.md`"));
        }
        finally
        {
            release.Set();
        }

        var landed = Head(checkout)[..8];
        var said = "";
        for (var tries = 0; tries < 50 && !said.Contains(" + uncommitted ·", StringComparison.Ordinal); tries++)
        {
            said = StandingOf(await tools.SearchAsync("Platypus", "decision", workspace: "all"), "D60.md`");
            await Task.Delay(100);
        }

        Assert.Equal($"standing: read at {landed} + uncommitted · later: 1 note · no mark", said);
    }
}
