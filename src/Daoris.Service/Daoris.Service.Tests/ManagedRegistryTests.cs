using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The registry is the authority, and the folder scan is a bootstrap (D48 §3).
/// </summary>
/// <remarks>
/// The ghost-repository fix already showed scan-as-authority failing: what a scan does not say governs
/// as much as what it says, and nobody reviews a silence. Workspaces finished the argument — one root
/// folder cannot express "these repositories, in these workspaces, wherever they live" — so the list
/// became explicit and the scan became a verb a person runs.
/// </remarks>
public sealed class RegistryImportTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-import-" + Guid.NewGuid().ToString("N")[..8]);

    private void Repo(string name, string? manifest)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        if (manifest is not null) File.WriteAllText(Path.Combine(dir, "daoris.json"), manifest);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void An_import_reads_each_declaration_from_its_own_manifest()
    {
        Repo("Cognition", """
            {
              "source": "s", "packs": ["dotnet-library"],
              "domain": {
                "summary": "The LLM cognition layer.",
                "owns": ["provider adapters", "routing"],
                "accepts": ["a new provider"]
              }
            }
            """);

        var proposed = Assert.Single(RegistryImport.Propose(_root));

        Assert.True(proposed.Adopted);
        Assert.True(proposed.Registered);
        Assert.Equal("The LLM cognition layer.", proposed.Summary);
        Assert.Equal(["provider adapters", "routing"], proposed.Owns);
        Assert.Equal(["dotnet-library"], proposed.Packs);
        Assert.Equal(Path.Combine(_root, "Cognition"), proposed.Root);
    }

    /// <summary>
    /// The manifest's remote declaration is read here too (D47 §4) — and knowledge without join is
    /// narrowed, because an import reads manifests the CLI never validated.
    /// </summary>
    [Fact]
    public void An_import_honours_the_remote_declaration_and_narrows_knowledge_to_join()
    {
        Repo("Joined", """{ "source": "s", "packs": [], "remote": { "join": true, "knowledge": true } }""");
        Repo("Orphaned", """{ "source": "s", "packs": [], "remote": { "knowledge": true } }""");

        var proposed = RegistryImport.Propose(_root);

        Assert.True(proposed.Single(r => r.Repository == "Joined").SharesKnowledge);
        Assert.False(proposed.Single(r => r.Repository == "Orphaned").Joined);
        Assert.False(proposed.Single(r => r.Repository == "Orphaned").SharesKnowledge);
    }

    /// <summary>
    /// "Who cannot be asked yet" is the same question as "who can", so a folder with no manifest is
    /// proposed and marked rather than skipped — a silent omission reads as the repository not existing.
    /// </summary>
    [Fact]
    public void A_folder_that_has_not_adopted_is_proposed_and_marked()
    {
        Repo("Stranger", null);

        Assert.False(Assert.Single(RegistryImport.Propose(_root)).Adopted);
    }

    /// <summary>A broken manifest is that repository's problem; it is not a reason to drop it off the map.</summary>
    [Fact]
    public void An_unparseable_manifest_is_still_adopted()
    {
        Repo("Broken", "{ not json");

        var proposed = Assert.Single(RegistryImport.Propose(_root));

        Assert.True(proposed.Adopted);
        Assert.Null(proposed.Summary);
    }

    [Fact]
    public void A_folder_that_does_not_exist_proposes_nothing()
    {
        Assert.Empty(RegistryImport.Propose(Path.Combine(_root, "no-such-folder")));
    }

    /// <summary>
    /// The workspace is NOT proposed (D48 §2): it is wiring, and an import knows nothing about circles.
    /// Leaving it unstated is what makes importing over an existing registry safe — every row keeps the
    /// workspace it was wired to.
    /// </summary>
    [Fact]
    public void An_import_proposes_no_workspace_at_all()
    {
        Repo("Cognition", """{ "source": "s" }""");

        Assert.Null(Assert.Single(RegistryImport.Propose(_root)).Workspace);
    }
}

/// <summary>
/// The registry itself: an explicit list, not a view over a folder. Adding is registering, updating is
/// an upsert, and removing is retiring — which touches no file anywhere.
/// </summary>
public sealed class ManagedRegistryTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-managed-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private RegistrationStore _store = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-20T10:00:00Z");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_root);
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _store = await RegistrationStore.OpenAsync(_connection);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static Registration Row(string name, bool adopted = true) => new(
        name, adopted, adopted ? $"{name}." : null, adopted ? ["x"] : [], [], [], Entries: 0,
        Root: $"D:/repos/{name}");

    /// <summary>
    /// Adoption is now a stored fact, not one derived from a scan — because the list is the authority
    /// and a row for a folder with no manifest still belongs on the map.
    /// </summary>
    [Fact]
    public async Task Adoption_round_trips_so_a_non_adopter_survives_a_restart()
    {
        await _store.UpsertAsync(Row("Member"), Now);
        await _store.UpsertAsync(Row("Stranger", adopted: false), Now);

        var all = await _store.AllAsync();

        Assert.True(all.Single(r => r.Repository == "Member").Adopted);
        Assert.False(all.Single(r => r.Repository == "Stranger").Adopted);
    }

    /// <summary>
    /// Retiring removes the row and **nothing else** — the repository's files are its own, and the one
    /// thing a person must be able to trust about a remove button is what it does not do.
    /// </summary>
    [Fact]
    public async Task Retiring_removes_the_row_and_touches_no_file()
    {
        var directory = Path.Combine(_root, "Member");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "daoris.json"), """{ "source": "s" }""");
        await _store.UpsertAsync(Row("Member") with { Root = directory }, Now);

        Assert.True(await _store.DeleteAsync("Member"));

        Assert.Empty(await _store.AllAsync());
        Assert.True(File.Exists(Path.Combine(directory, "daoris.json")));
    }

    [Fact]
    public async Task Retiring_something_that_was_never_registered_is_an_answer_not_an_error()
    {
        Assert.False(await _store.DeleteAsync("Nobody"));
    }

    /// <summary>The registry serves what it was told, with no folder anywhere in the answer.</summary>
    [Fact]
    public void The_registry_is_the_list_it_was_given()
    {
        var registry = new Registry();
        registry.Register(Row("Beta"));
        registry.Register(Row("Alpha"));

        var read = registry.Read(new Dictionary<string, int> { ["Alpha"] = 7 });

        Assert.Equal(["Alpha", "Beta"], read.Select(r => r.Repository));
        Assert.Equal(7, read[0].Entries);
    }

    [Fact]
    public void Retiring_takes_it_out_of_the_registry()
    {
        var registry = new Registry();
        registry.Register(Row("Alpha"));

        registry.Retire("ALPHA"); // the name is a repository name, and those are compared loosely

        Assert.Empty(registry.Read(new Dictionary<string, int>()));
    }
}

/// <summary>
/// The bootstrap: a store that has never been managed imports the old root once, and says so.
/// </summary>
/// <remarks>
/// Back-compat with no silent magic. The scan is gone as an authority, so a machine that has been
/// running on `DAORIS_KNOWLEDGE_ROOT` would otherwise come up to an empty family — and an empty family
/// after an upgrade is indistinguishable from a broken one. It runs ONCE, marked in the store, because
/// the second run would resurrect every repository the person deliberately retired.
/// </remarks>
public sealed class FirstRunImportTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-firstrun-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string Family()
    {
        var family = Path.Combine(_root, "family");
        foreach (var name in new[] { "engine", "game" })
        {
            Directory.CreateDirectory(Path.Combine(family, name));
            File.WriteAllText(Path.Combine(family, name, "daoris.json"), """{ "source": "s" }""");
        }

        return family;
    }

    private ServiceOptions Options() => new(Family(), Path.Combine(_root, "knowledge.db"));

    /// <summary>One knowledge document in each named repository, so the index has something to hold.</summary>
    private static void Know(ServiceOptions options, params string[] repositories)
    {
        foreach (var name in repositories)
        {
            var folder = Path.Combine(options.RepositoryRoot, name, ".claude", "knowledge");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "note.md"), $"# {name}'s note\n\nWhat {name} learned.\n");
        }
    }

    [Fact]
    public async Task A_store_that_has_never_been_managed_imports_its_root_once()
    {
        var options = Options();

        await using (var first = await ServiceFactory.CreateAsync(options))
        {
            var imported = (await first.Service.RegistryAsync()).Select(r => r.Repository).Order().ToList();
            Assert.Equal(["engine", "game"], imported);
        }

        // Retired deliberately, then reopened: the second run must not resurrect it, or "remove" would
        // mean "until the next restart" — which is not a remove.
        await using (var second = await ServiceFactory.CreateAsync(options))
        {
            await second.Service.RetireAsync("game");
        }

        await using var third = await ServiceFactory.CreateAsync(options);
        Assert.Equal(["engine"], (await third.Service.RegistryAsync()).Select(r => r.Repository));
    }

    /// <summary>
    /// A deployment that is fed rather than scanned never imports anything — the same condition that
    /// keeps a shared host off its own disk (D47 §4). Otherwise the first run would register whatever
    /// happened to sit beside the server binary.
    /// </summary>
    [Fact]
    public async Task A_fed_deployment_imports_nothing()
    {
        await using var fed = await ServiceFactory.CreateAsync(Options(), source: new EmptyKnowledgeSource());

        Assert.Empty(await fed.Service.RegistryAsync());
    }

    /// <summary>
    /// Minting a key scans nothing.
    /// </summary>
    /// <remarks>
    /// The console verb used to compose the whole service, which bootstrapped a registry from the
    /// configured root — so `keys mint` on a server registered whatever sat beside the binary, machine
    /// paths included, into the deployment that must be FED and never scanned (D47 §4). The family
    /// rehearsal caught it: a workspace's deployment answered a registry full of the operator's own
    /// repositories, in a workspace it does not serve. Key administration opens the key store alone.
    /// </remarks>
    [Fact]
    public async Task Key_administration_never_bootstraps_a_registry()
    {
        var options = Options();

        await using (var administration = await ServiceFactory.OpenKeysAsync(options))
        {
            await administration.Keys.MintAsync("person@machine", TimeSpan.FromDays(1), DateTimeOffset.UtcNow);
        }

        // Read back as a FED deployment, which imports nothing itself — so anything registered here
        // was registered by the mint above.
        await using (var fed = await ServiceFactory.CreateAsync(options, source: new EmptyKnowledgeSource()))
        {
            Assert.Empty(await fed.Service.RegistryAsync());
        }

        // And it did not spend the once-only bootstrap either: a local deployment over the same store
        // still imports its root. Otherwise minting a key on a laptop would silently cost the person
        // their family, and the verb that consumed it would never be suspected.
        await using var local = await ServiceFactory.CreateAsync(options);
        Assert.Equal(["engine", "game"], (await local.Service.RegistryAsync()).Select(r => r.Repository).Order());
    }

    /// <summary>
    /// The index reads the REGISTERED paths (D48 §3) — so a repository that is merely present in the
    /// folder, and never registered, contributes nothing. The folder stopped being the authority.
    /// </summary>
    [Fact]
    public async Task A_folder_nobody_registered_is_not_indexed()
    {
        var options = Options();
        await using var service = await ServiceFactory.CreateAsync(options);
        await service.Service.RetireAsync("game");

        // Born after the bootstrap, so never registered: present on disk and absent from the family.
        var stranger = Path.Combine(options.RepositoryRoot, "stranger");
        Directory.CreateDirectory(Path.Combine(stranger, ".claude", "knowledge"));
        File.WriteAllText(
            Path.Combine(stranger, ".claude", "knowledge", "uninvited.md"), "# uninvited\n\nNobody asked.\n");

        await service.Service.RefreshAsync();

        var indexed = (await service.Service.SummarizeAsync()).Select(r => r.Repository).ToList();
        Assert.DoesNotContain("stranger", indexed);
        Assert.DoesNotContain("game", indexed);
    }

    /// <summary>
    /// 🔴 Seen on the owner's install (POLISH5): every registration retired, and 1,052 entries from 17
    /// repositories still charted, searched and compared. The ghost rule prunes only when the scan saw
    /// something — right for a folder, where nothing means a mis-set path — and with the last
    /// registration gone the scan sees nothing by construction. A local host is fed by nobody, so a
    /// repository it holds and no longer registers is a ghost whatever the scan saw.
    /// </summary>
    [Fact]
    public async Task Retiring_the_last_repository_leaves_nothing_indexed()
    {
        var options = Options();
        Know(options, "engine", "game");
        await using var service = await ServiceFactory.CreateAsync(options);
        await service.Service.RefreshAsync();
        Assert.Equal(2, (await service.Service.SummarizeAsync()).Count);

        await service.Service.RetireAsync("engine");
        await service.Service.RetireAsync("game");
        await service.Service.RefreshAsync();

        Assert.Empty(await service.Service.SummarizeAsync());
    }

    /// <summary>
    /// The half the guard is still for: when nothing can be read, a REGISTERED repository keeps what
    /// it had, because a wrong path must not wipe the index — and a retired one still leaves.
    /// </summary>
    [Fact]
    public async Task When_nothing_can_be_read_a_registered_repository_keeps_its_entries_and_a_retired_one_leaves()
    {
        var options = Options();
        Know(options, "engine", "game");
        await using var service = await ServiceFactory.CreateAsync(options);
        await service.Service.RefreshAsync();
        await service.Service.RetireAsync("game");

        Directory.Move(Path.Combine(options.RepositoryRoot, "engine"), Path.Combine(_root, "engine-moved"));
        await service.Service.RefreshAsync();

        Assert.Equal(["engine"], (await service.Service.SummarizeAsync()).Select(r => r.Repository));
    }

    /// <summary>
    /// A registered path that no longer exists is REPORTED, never silently skipped (D48 §3). The ghost
    /// fix taught the other half of this: a repository that quietly stops contributing looks exactly
    /// like one with nothing to say, and the index will happily report a healthy-looking count while a
    /// checkout has been moved or deleted underneath it.
    /// </summary>
    [Fact]
    public async Task A_registered_path_that_vanished_is_named_in_the_report()
    {
        var options = Options();
        await using var service = await ServiceFactory.CreateAsync(options);
        Directory.Delete(Path.Combine(options.RepositoryRoot, "game"), recursive: true);

        var report = await service.Service.RefreshAsync();

        Assert.Equal(["game"], report.Absent);
    }

    [Fact]
    public async Task A_family_that_is_all_present_names_nothing()
    {
        await using var service = await ServiceFactory.CreateAsync(Options());

        Assert.Empty((await service.Service.RefreshAsync()).Absent);
    }

    /// <summary>
    /// A foreign registration — a teammate's repository mirrored down from a remote (D47 §9) — has no
    /// checkout here by construction, and must not be reported as missing. Absence is about a path that
    /// was named and is gone, not about a row that never named one.
    /// </summary>
    [Fact]
    public async Task A_registration_with_no_path_is_not_an_absence()
    {
        await using var service = await ServiceFactory.CreateAsync(Options());
        await service.Service.RegisterAsync(
            new Registration("teammate", Adopted: true, "Elsewhere entirely.", ["x"], [], [], Entries: 0),
            DateTimeOffset.UtcNow);

        Assert.Empty((await service.Service.RefreshAsync()).Absent);
    }
}
