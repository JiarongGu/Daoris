using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// A teammate's code map, brought down to this machine (MAP3e). MAP3b held a fed map at the remote
/// only, so a machine without the checkout answered "no map" for a teammate's repository that keeps
/// one. The host's pass now holds what the remote holds, at the commit the remote holds it, for the
/// circle's repositories this machine has no checkout of — and says where each came from.
/// </summary>
public sealed class CodeMapSyncTests : IAsyncLifetime, IDisposable
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-24T10:00:00Z");

    private const string Map = """
        {"version":1,"modules":[{"id":"core","path":"src/Core","summary":"the heart"},{"id":"web","path":"src/Web","summary":"the face"}],
         "dependencies":[{"from":"web","to":"core","kind":"project"}]}
        """;

    private readonly List<SqliteConnection> _connections = [];
    private readonly string _checkout = Path.Combine(Path.GetTempPath(), "daoris-map3e-" + Guid.NewGuid().ToString("N")[..8]);
    private KnowledgeService _remote = null!;
    private KnowledgeService _here = null!;
    private CountingRemote _wire = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_checkout);
        _remote = await ServiceAsync();
        _here = await ServiceAsync();
        _wire = new CountingRemote(_remote);

        foreach (var name in new[] { "Teammate", "Mine", "Toolmate" })
        {
            await _remote.RegisterAsync(Declared(name), Now);
        }

        // Here: a teammate's copy in this circle (no root), a checkout of this machine's own, and a
        // teammate's copy in another circle.
        await _here.RegisterAsync(Declared("Teammate"), Now);
        await _here.RegisterAsync(Declared("Mine") with { Root = _checkout }, Now);
        await _here.RegisterAsync(Declared("Toolmate") with { Workspace = "tools" }, Now);
    }

    public async Task DisposeAsync()
    {
        foreach (var connection in _connections) await connection.DisposeAsync();
    }

    public void Dispose()
    {
        try { Directory.Delete(_checkout, recursive: true); } catch (IOException) { }
    }

    private async Task<KnowledgeService> ServiceAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        _connections.Add(connection);
        var store = new InMemoryKnowledgeStore();
        return new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly,
            registry: new Registry(), registrations: await RegistrationStore.OpenAsync(connection));
    }

    private static Registration Declared(string name) =>
        new(name, Adopted: true, $"{name}.", [], [], [], Entries: 0, Joined: true, SharesKnowledge: true);

    private static FeedProvenance At(string commit, string at, string? onBase = null) =>
        new(commit, DateTimeOffset.Parse(at), "main", "person@machine-b") { Base = onBase };

    private Task<CodeMapSyncReport> PassAsync(IRemote? remote = null) =>
        CodeMapSync.RunAsync(_here, remote ?? _wire, Workspaces.Default);

    [Fact]
    public async Task A_teammate_s_map_held_at_the_remote_comes_down_and_says_where_it_came_from()
    {
        Assert.Null((await _here.CodeMapAsync("Teammate"))!.File);
        await _remote.FeedCodeMapAsync("Teammate", "docs/code-map.json", Map, At("aaaa1111bbbb2222", "2026-09-24T09:00:00Z"));

        var pass = await PassAsync();
        var read = (await _here.CodeMapAsync("Teammate"))!;

        Assert.Null(pass.Problem);
        Assert.Equal(1, pass.Fetched);
        Assert.Equal("docs/code-map.json", read.File);
        Assert.Equal(["core", "web"], read.Map!.Modules.Select(m => m.Id));
        Assert.Equal(("aaaa1111bbbb2222", "person@machine-b"), (read.Fed!.Commit, read.Fed.Origin));
    }

    /// <summary>What the remote holds is asked by commit first, so a map that has not moved is not sent again.</summary>
    [Fact]
    public async Task A_map_that_has_not_moved_is_not_fetched_again()
    {
        await _remote.FeedCodeMapAsync("Teammate", "docs/code-map.json", Map, At("aaaa1111bbbb2222", "2026-09-24T09:00:00Z"));

        await PassAsync();
        var again = await PassAsync();

        Assert.Equal(0, again.Fetched);
        Assert.Equal(1, _wire.Fetches);
    }

    /// <summary>
    /// The remote orders what it holds, so this machine holds what the remote holds: a newer commit's map
    /// replaces the one here, and a commit that keeps none is held with none — the page says no map.
    /// </summary>
    [Fact]
    public async Task A_newer_commit_replaces_it_and_a_commit_with_no_map_is_held_as_none()
    {
        await _remote.FeedCodeMapAsync("Teammate", "docs/code-map.json", Map, At("parentparent", "2026-09-24T09:00:00Z"));
        await PassAsync();

        await _remote.FeedCodeMapAsync("Teammate", file: null, map: null, At("childchild", "2026-09-24T10:00:00Z", "parentparent"));
        var pass = await PassAsync();
        var read = (await _here.CodeMapAsync("Teammate"))!;

        Assert.Equal(1, pass.Fetched);
        Assert.Null(read.File);
        Assert.Null(read.Map);
        Assert.Equal("childchild", read.Fed!.Commit);
    }

    /// <summary>A map the remote no longer holds — its repository retired there — is not kept here either.</summary>
    [Fact]
    public async Task A_map_the_remote_no_longer_holds_is_forgotten_here()
    {
        await _remote.FeedCodeMapAsync("Teammate", "docs/code-map.json", Map, At("aaaa1111bbbb2222", "2026-09-24T09:00:00Z"));
        await PassAsync();

        await _remote.RetireAsync("Teammate");
        var pass = await PassAsync();

        Assert.Equal(1, pass.Forgotten);
        var read = (await _here.CodeMapAsync("Teammate"))!;
        Assert.Null(read.File);
        Assert.Null(read.Fed);
    }

    /// <summary>
    /// A checkout here is read from disk and is the authority on its own map; a teammate's copy in another
    /// circle is that circle's sync's. Neither is asked for.
    /// </summary>
    [Fact]
    public async Task A_checkout_here_and_another_circle_s_copy_are_never_asked_for()
    {
        foreach (var name in new[] { "Mine", "Toolmate" })
        {
            await _remote.FeedCodeMapAsync(name, "docs/code-map.json", Map, At("aaaa1111bbbb2222", "2026-09-24T09:00:00Z"));
        }

        var pass = await PassAsync();

        Assert.Equal(0, pass.Fetched);
        Assert.DoesNotContain("Mine", _wire.Asked);
        Assert.DoesNotContain("Toolmate", _wire.Asked);
        Assert.Null((await _here.CodeMapAsync("Mine"))!.Fed);
    }

    [Fact]
    public async Task An_unreachable_remote_is_named_and_changes_nothing()
    {
        await _remote.FeedCodeMapAsync("Teammate", "docs/code-map.json", Map, At("aaaa1111bbbb2222", "2026-09-24T09:00:00Z"));
        await PassAsync();

        var pass = await PassAsync(new UnreachableRemote());

        Assert.Contains("could not be reached", pass.Problem);
        Assert.Equal("docs/code-map.json", (await _here.CodeMapAsync("Teammate"))!.File);
    }

    // ——— The one wire (CodeMapWire): what the code-map door answers and what this pass reads.

    [Fact]
    public async Task A_map_crosses_the_wire_whole_with_where_it_came_from()
    {
        await _remote.FeedCodeMapAsync("Teammate", "docs/code-map.json", Map, At("aaaa1111bbbb2222", "2026-09-24T09:00:00Z"));

        var json = CodeMapWire.Answer("Teammate", (await _remote.CodeMapAsync("Teammate"))!);
        var back = CodeMapWire.Read(json)!;

        Assert.Equal("docs/code-map.json", back.File);
        Assert.Equal(("aaaa1111bbbb2222", "main"), (back.Fed!.Commit, back.Fed.Branch));
        Assert.Equal(2, CodeMapReader.Parse(back.Body!, back.File!).Map!.Modules.Count);
        Assert.DoesNotContain("\"problem\"", json);
    }

    /// <summary>
    /// The page reads the same answer: a repository with no map leaves `file` out rather than naming it
    /// null (the host's rule since the browser gate found it), and a refusal is its own sentence.
    /// </summary>
    [Fact]
    public void The_door_s_answer_leaves_out_what_it_does_not_have()
    {
        var none = CodeMapWire.Answer("Bare", new CodeMapRead(null, null, null));
        var refused = CodeMapWire.Answer("Broken", new CodeMapRead(null, "docs/code-map.json", "`x` is not repository-relative"));

        Assert.DoesNotContain("\"file\"", none);
        Assert.DoesNotContain("\"fed\"", none);
        Assert.Contains("\"modules\":[]", none);
        Assert.Contains("not repository-relative", refused);
        Assert.Null(CodeMapWire.Read("[]"));
        Assert.Null(CodeMapWire.Read("""{ "repository": "Half", "fed": { "commit": "abc" } }"""));
    }

    /// <summary>A remote that is a real service behind the real wire, counting what this machine asked of it.</summary>
    private sealed class CountingRemote(KnowledgeService remote) : IRemote
    {
        public int Fetches { get; private set; }

        public List<string> Asked { get; } = [];

        public async Task<string?> HeldCodeMapAsync(string repository, CancellationToken ct = default)
        {
            Asked.Add(repository);
            return (await remote.HeldAsync(repository, ct)).CodeMap;
        }

        public async Task<FedCodeMap?> FetchCodeMapAsync(string repository, CancellationToken ct = default)
        {
            Fetches++;
            return await remote.CodeMapAsync(repository, ct) is { } read
                ? CodeMapWire.Read(CodeMapWire.Answer(repository, read))
                : null;
        }

        public Task<QuestFetch> FetchQuestsAsync(long since, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<QuestPush> PushQuestsAsync(long @base, IReadOnlyList<QuestOperation> operations, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task PushSessionsAsync(IReadOnlyList<FedSessionRecord> records, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<SessionFetch> FetchSessionsAsync(long since, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
