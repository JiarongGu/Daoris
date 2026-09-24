using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The remote's knowledge ingest (D47 §4): fed entries are the only entries a remote has — it never
/// scans a filesystem — and the disclosure judgement runs AT the door, over what each repository's own
/// reviewed manifest declared. Join admits records; knowledge is a second, separate declaration.
/// </summary>
public sealed class FeedTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-20T10:00:00Z");

    private readonly InMemoryKnowledgeStore _store = new();
    private Microsoft.Data.Sqlite.SqliteConnection _connection = null!;
    private RegistrationStore _registrations = null!;
    private KnowledgeService _service = null!;

    public async Task InitializeAsync()
    {
        // A REAL registration store, because since D48 §6 the door's judgement has a memory: what
        // commit this deployment already holds is the thing a stale feed is compared against, and it
        // must survive a restart or the first feed after one would always win.
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _registrations = await RegistrationStore.OpenAsync(_connection);

        // Registrations are the only family here — the remote's own shape, and since D48 §3 every
        // deployment's shape: the registry is an explicit list, never a view over a folder.
        _service = new KnowledgeService(
            _store, new LexicalKnowledgeSearch(_store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry(), registrations: _registrations);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private Task Register(string name, bool joined, bool shares, string? defaultBranch = null) =>
        _service.RegisterAsync(
            new Registration(name, Adopted: true, "A repo.", ["x"], [], [], Entries: 0,
                Joined: joined, SharesKnowledge: shares, DefaultBranch: defaultBranch), Now);

    private static KnowledgeEntry Entry(string title = "A lesson") => new(
        "ignored-by-normalization", EntryKind.Decision, Provenance.Canonical, title,
        "What was learned.", "docs/DECISIONS.md", title);

    /// <summary>The ordinary feed: the canonical line, at a moment in its history.</summary>
    /// <param name="onBase">The commit the deployment held when this machine checked its ancestry (SYNC5a).</param>
    private static FeedProvenance From(
        string commit = "aaaa1111bbbb2222", string branch = "main", string at = "2026-09-20T09:00:00Z",
        string? origin = "person@machine-a", string? onBase = null) =>
        new(commit, DateTimeOffset.Parse(at), branch, origin) { Base = onBase };

    [Fact]
    public async Task Feeding_an_unjoined_repository_is_refused()
    {
        await Register("Silent", joined: false, shares: false);

        var outcome = await _service.FeedAsync("Silent", [Entry()], From());

        Assert.Equal(FeedRefusal.NotJoined, outcome.Refusal);
        Assert.Contains("join", outcome.Message);
        Assert.Empty(await _store.AllAsync());
    }

    /// <summary>Join admits records; knowledge is its own declaration. One switch must not imply the other.</summary>
    [Fact]
    public async Task Feeding_a_joined_repository_that_does_not_share_knowledge_is_refused()
    {
        await Register("Reserved", joined: true, shares: false);

        var outcome = await _service.FeedAsync("Reserved", [Entry()], From());

        Assert.Equal(FeedRefusal.NotSharing, outcome.Refusal);
        Assert.Contains("knowledge", outcome.Message);
        Assert.Empty(await _store.AllAsync());
    }

    /// <summary>
    /// Accepted entries are normalized at the door: the repository name comes from the registration and
    /// the provenance is forced Local — canonical doctrine is distributed by `sync`, never by the feed.
    /// </summary>
    [Fact]
    public async Task Feeding_a_sharing_repository_replaces_its_entries_normalized()
    {
        await Register("Open", joined: true, shares: true);

        var first = await _service.FeedAsync("Open", [Entry("First lesson"), Entry("Second lesson")], From());
        var second = await _service.FeedAsync(
            "Open", [Entry("Second lesson")], From("cccc3333dddd4444", at: "2026-09-20T09:30:00Z"));

        Assert.True(first.Accepted);
        Assert.Equal(2, first.Entries);
        Assert.True(second.Accepted);

        var stored = await _store.AllAsync();
        var entry = Assert.Single(stored);
        Assert.Equal("Open", entry.Repository);
        Assert.Equal(Provenance.Local, entry.Provenance);
        Assert.Equal("Second lesson", entry.Title);
    }

    [Fact]
    public async Task Feeding_an_unknown_repository_is_refused()
    {
        var outcome = await _service.FeedAsync("Stranger", [Entry()], From());

        Assert.Equal(FeedRefusal.NotJoined, outcome.Refusal);
        Assert.Empty(await _store.AllAsync());
    }

    // ——— Which point in the history speaks (D48 §6). Wholesale replacement between two machines is a
    // flapping generator without these three: each tick, whichever fed last would overwrite the other.

    /// <summary>
    /// A feed that names no commit cannot be compared with what is held, so it is refused rather than
    /// taken on trust — an unguarded replacement is exactly the failure these rules exist to end.
    /// </summary>
    [Fact]
    public async Task A_feed_that_names_no_commit_is_refused()
    {
        await Register("Open", joined: true, shares: true);

        var outcome = await _service.FeedAsync("Open", [Entry()], provenance: null);

        Assert.Equal(FeedRefusal.NoProvenance, outcome.Refusal);
        Assert.Empty(await _store.AllAsync());
    }

    /// <summary>
    /// Only the canonical line feeds knowledge. A feature-branch checkout is work in flight: the
    /// branch and its review display it better than an index would, and its records still travel.
    /// </summary>
    [Fact]
    public async Task A_feed_from_a_branch_that_is_not_the_canonical_line_is_refused_naming_both()
    {
        await Register("Open", joined: true, shares: true, defaultBranch: "main");

        var outcome = await _service.FeedAsync("Open", [Entry()], From(branch: "feature/streaming"));

        Assert.Equal(FeedRefusal.NotDefaultBranch, outcome.Refusal);
        Assert.Contains("feature/streaming", outcome.Message);
        Assert.Contains("main", outcome.Message);
        Assert.True(outcome.Information);
        Assert.Empty(await _store.AllAsync());
    }

    /// <summary>
    /// A repository that never declared a canonical line has not told the deployment which branch is
    /// the family's — so any branch feeds, and the provenance served names which one did. Refusing
    /// instead would silence every repository that simply never said.
    /// </summary>
    [Fact]
    public async Task A_repository_with_no_declared_default_branch_feeds_from_any_line()
    {
        await Register("Open", joined: true, shares: true, defaultBranch: null);

        var outcome = await _service.FeedAsync("Open", [Entry()], From(branch: "whatever"));

        Assert.True(outcome.Accepted);
        Assert.Equal("whatever", (await _service.SummarizeAsync()).Single().Fed!.Branch);
    }

    /// <summary>
    /// The flapping this exists to end: a stale checkout may not clobber a fresher one. The refusal is
    /// INFORMATION — that machine is simply behind — and the index keeps what it has.
    /// </summary>
    [Fact]
    public async Task A_feed_from_an_older_commit_keeps_what_is_held_and_says_why()
    {
        await Register("Open", joined: true, shares: true, defaultBranch: "main");
        await _service.FeedAsync(
            "Open", [Entry("The newer lesson")], From("newnewnew", at: "2026-09-20T12:00:00Z"));

        var stale = await _service.FeedAsync(
            "Open", [Entry("The older lesson")], From("oldoldold", at: "2026-09-19T12:00:00Z"));

        Assert.Equal(FeedRefusal.Stale, stale.Refusal);
        Assert.True(stale.Information);
        Assert.Contains("newer commit", stale.Message);
        Assert.Equal("The newer lesson", (await _store.AllAsync()).Single().Title);
    }

    /// <summary>
    /// The refusal states UTC instants, because it writes `Z` after them.
    /// </summary>
    /// <remarks>
    /// A commit time carries whatever offset the committer's machine had, and every other test here
    /// feeds `Z` times — so the offset was always zero and the sentence was never exercised. It
    /// printed the LOCAL wall clock and appended `Z`, which is a sentence claiming an instant it is
    /// not: the ordering underneath was right and its explanation was wrong, which is the worst
    /// shape for a message whose whole job is to convince a person that being refused is fine.
    /// </remarks>
    [Fact]
    public async Task The_stale_refusal_states_its_times_in_the_utc_it_claims()
    {
        await Register("Open", joined: true, shares: true, defaultBranch: "main");
        // 22:30 at +10:00 is 12:30Z — a held commit whose local clock reads ten hours ahead.
        await _service.FeedAsync(
            "Open", [Entry("The newer lesson")], From("newnewnew", at: "2026-09-20T22:30:00+10:00"));

        var stale = await _service.FeedAsync(
            "Open", [Entry("The older lesson")], From("oldoldold", at: "2026-09-20T11:00:00Z"));

        Assert.Equal(FeedRefusal.Stale, stale.Refusal);
        Assert.Contains("12:30Z", stale.Message);
        Assert.DoesNotContain("22:30Z", stale.Message);
    }

    /// <summary>The newest canonical view replaces wholesale — which is what makes DELETE work for free.</summary>
    [Fact]
    public async Task A_feed_from_a_newer_commit_replaces_and_a_deletion_travels_with_it()
    {
        await Register("Open", joined: true, shares: true, defaultBranch: "main");
        await _service.FeedAsync(
            "Open", [Entry("Kept"), Entry("Deleted upstream")], From("oldoldold", at: "2026-09-19T12:00:00Z"));

        var newer = await _service.FeedAsync(
            "Open", [Entry("Kept")], From("newnewnew", at: "2026-09-20T12:00:00Z"));

        Assert.True(newer.Accepted);
        Assert.Equal("Kept", (await _store.AllAsync()).Single().Title);
        var fed = (await _service.SummarizeAsync()).Single().Fed!;
        Assert.Equal("newnewnew", fed.Commit);
        Assert.Equal("person@machine-a", fed.Origin);
    }

    /// <summary>
    /// Re-feeding the same commit stays idempotent, as the feed always was — a tick that re-sent what
    /// it sent last time must not be answered as though the machine had gone backwards.
    /// </summary>
    [Fact]
    public async Task Re_feeding_the_same_commit_is_idempotent()
    {
        await Register("Open", joined: true, shares: true, defaultBranch: "main");
        await _service.FeedAsync("Open", [Entry()], From());

        var again = await _service.FeedAsync("Open", [Entry()], From());

        Assert.True(again.Accepted);
        Assert.Single(await _store.AllAsync());
    }

    /// <summary>
    /// A retired repository takes its position in the history with it. A leftover row would refuse the
    /// first feed after it re-joined, as though this deployment still held a commit it had dropped.
    /// </summary>
    [Fact]
    public async Task Retiring_forgets_the_commit_this_deployment_held()
    {
        await Register("Open", joined: true, shares: true, defaultBranch: "main");
        await _service.FeedAsync("Open", [Entry()], From("newnewnew", at: "2026-09-20T12:00:00Z"));

        await _service.RetireAsync("Open");
        await Register("Open", joined: true, shares: true, defaultBranch: "main");
        var afterRejoin = await _service.FeedAsync(
            "Open", [Entry("Fed again")], From("oldoldold", at: "2026-09-19T12:00:00Z"));

        Assert.True(afterRejoin.Accepted);
    }

    /// <summary>
    /// A fed host reads nothing from disk, so a refresh there must keep what it was fed — the case the
    /// ghost rule's guard exists for, and the one POLISH5's registry rule must never reach: only a host
    /// that reads its registered roots lets the registry decide what is a ghost.
    /// </summary>
    [Fact]
    public async Task A_refresh_on_a_fed_host_keeps_what_it_was_fed()
    {
        await Register("Open", joined: true, shares: true, defaultBranch: "main");
        await _service.FeedAsync("Open", [Entry()], From("newnewnew", at: "2026-09-20T12:00:00Z"));

        await _service.RefreshAsync();

        Assert.Contains("Open", (await _service.SummarizeAsync()).Select(r => r.Repository));
    }

    /// <summary>
    /// The declared line survives an ordinary re-registration. `daoris connect` says nothing about
    /// branches and runs on every tick; a null that overwrote would quietly reopen every branch.
    /// </summary>
    [Fact]
    public async Task An_ordinary_re_registration_preserves_the_declared_canonical_line()
    {
        await Register("Open", joined: true, shares: true, defaultBranch: "main");
        await Register("Open", joined: true, shares: true, defaultBranch: null);

        var outcome = await _service.FeedAsync("Open", [Entry()], From(branch: "feature/x"));

        Assert.Equal(FeedRefusal.NotDefaultBranch, outcome.Refusal);
    }

    // ——— Ancestry and the digest (SYNC5a, sync design §8). The machine with the checkout asks git
    // whether its commit descends from the one held; the deployment checks that it still holds it.

    /// <summary>
    /// The same commit read the same way changes nothing — not even who is credited with it. A tick
    /// that re-sends what is already held must not rewrite the record of who fed it first.
    /// </summary>
    [Fact]
    public async Task The_same_commit_with_the_same_content_is_already_held()
    {
        await Register("Open", joined: true, shares: true, defaultBranch: "main");
        await _service.FeedAsync("Open", [Entry()], From(origin: "person@machine-a"));

        var again = await _service.FeedAsync("Open", [Entry()], From(origin: "person@machine-b"));

        Assert.True(again.Accepted);
        Assert.Contains("already", again.Message);
        Assert.Equal("person@machine-a", (await _service.SummarizeAsync()).Single().Fed!.Origin);
    }

    /// <summary>
    /// SYNC0c: two machines on one commit whose readings differ used to replace each other on every
    /// tick. The first reading of a commit stands, and the second hears why — as information.
    /// </summary>
    [Fact]
    public async Task The_same_commit_read_differently_keeps_the_first_reading_and_says_so()
    {
        await Register("Open", joined: true, shares: true, defaultBranch: "main");
        await _service.FeedAsync("Open", [Entry("As machine A read it")], From(origin: "person@machine-a"));

        var other = await _service.FeedAsync(
            "Open", [Entry("As machine B read it")], From(origin: "person@machine-b"));

        Assert.Equal(FeedRefusal.ContentDiffers, other.Refusal);
        Assert.True(other.Information);
        Assert.Contains("aaaa1111", other.Message);
        Assert.Contains("person@machine-a", other.Message);
        Assert.Equal("As machine A read it", (await _store.AllAsync()).Single().Title);
    }

    /// <summary>
    /// Ancestry decides where it was asked, not the clock. A commit that descends from the one held is
    /// a fast-forward even when its committer's clock reads earlier — a rebase keeps author dates and a
    /// machine's clock can be wrong, and neither makes a descendant stale.
    /// </summary>
    [Fact]
    public async Task A_feed_on_the_commit_held_is_a_fast_forward_whatever_its_clock_says()
    {
        await Register("Open", joined: true, shares: true, defaultBranch: "main");
        await _service.FeedAsync("Open", [Entry("Before")], From("parentparent", at: "2026-09-20T12:00:00Z"));

        var child = await _service.FeedAsync(
            "Open", [Entry("After")], From("childchild", at: "2026-09-20T11:00:00Z", onBase: "parentparent"));

        Assert.True(child.Accepted);
        Assert.Equal("After", (await _store.AllAsync()).Single().Title);
        Assert.Equal("childchild", (await _service.SummarizeAsync()).Single().Fed!.Commit);
    }

    /// <summary>
    /// The compare-and-swap half: this machine checked against a commit another machine has since
    /// replaced, so its answer is out of date. Information, and the next pass asks git again.
    /// </summary>
    [Fact]
    public async Task A_feed_checked_against_a_commit_no_longer_held_is_refused_as_moved()
    {
        await Register("Open", joined: true, shares: true, defaultBranch: "main");
        await _service.FeedAsync(
            "Open", [Entry("Fed in between")], From("betweenbetween", at: "2026-09-20T12:00:00Z", origin: "person@machine-b"));

        var late = await _service.FeedAsync(
            "Open", [Entry("Checked earlier")], From("latelate", at: "2026-09-20T13:00:00Z", onBase: "parentparent"));

        Assert.Equal(FeedRefusal.Moved, late.Refusal);
        Assert.True(late.Information);
        Assert.Contains("betweenb", late.Message);
        Assert.Equal("Fed in between", (await _store.AllAsync()).Single().Title);
    }

    /// <summary>
    /// A row recorded before digests existed has none to compare, so the same commit is taken once and
    /// the digest recorded — refusing it would freeze every repository fed by an older deployment.
    /// </summary>
    [Fact]
    public async Task A_commit_held_without_a_digest_is_taken_once_and_then_compared()
    {
        await Register("Open", joined: true, shares: true, defaultBranch: "main");
        await _registrations.RecordProvenanceAsync("Open", From(origin: "person@machine-a"));

        var first = await _service.FeedAsync("Open", [Entry("Read now")], From(origin: "person@machine-b"));
        var second = await _service.FeedAsync("Open", [Entry("Read otherwise")], From(origin: "person@machine-c"));

        Assert.True(first.Accepted);
        Assert.Equal(FeedRefusal.ContentDiffers, second.Refusal);
        Assert.Equal("Read now", (await _store.AllAsync()).Single().Title);
    }

    /// <summary>The digest is over what the entries SAY, in no particular order of arrival.</summary>
    [Fact]
    public void The_digest_ignores_order_and_hears_every_field()
    {
        var one = Entry("One");
        var two = Entry("Two");

        Assert.Equal(FeedDigest.Of([one, two]), FeedDigest.Of([two, one]));
        Assert.NotEqual(FeedDigest.Of([one]), FeedDigest.Of([one with { Body = "Said otherwise." }]));
        Assert.NotEqual(FeedDigest.Of([one]), FeedDigest.Of([one with { Anchor = "elsewhere" }]));
        // Two fields that meet at a boundary must not collide with two that meet elsewhere.
        Assert.NotEqual(
            FeedDigest.Of([one with { Title = "ab", Body = "c" }]),
            FeedDigest.Of([one with { Title = "a", Body = "bc" }]));
    }

    // ——— The code map, fed (MAP3b): the same gates and the same judgement as knowledge, and the file
    // judged whole again at the door by the reader that judges it on disk.

    private const string MapJson = """
        {"version":1,"modules":[{"id":"core","path":"src/Core","summary":"the heart"},{"id":"web","path":"src/Web","summary":"the face"}],
         "dependencies":[{"from":"web","to":"core","kind":"project"}]}
        """;

    /// <summary>A shared deployment has no checkout; what was fed is the map it answers with.</summary>
    [Fact]
    public async Task A_fed_code_map_is_what_a_repository_with_no_checkout_answers()
    {
        await Register("Open", joined: true, shares: true, defaultBranch: "main");

        var outcome = await _service.FeedCodeMapAsync("Open", "docs/code-map.json", MapJson, From());
        var read = (await _service.CodeMapAsync("Open"))!;

        Assert.True(outcome.Accepted);
        Assert.Equal("docs/code-map.json", read.File);
        Assert.Null(read.Problem);
        Assert.Equal(["core", "web"], read.Map!.Modules.Select(m => m.Id));
        Assert.Equal("web", read.Map.Dependencies.Single().From);
    }

    /// <summary>Judged whole at the door: a map that breaks a rule is refused naming the break, and nothing of it is kept.</summary>
    [Fact]
    public async Task A_fed_code_map_that_breaks_a_rule_is_refused_whole()
    {
        await Register("Open", joined: true, shares: true, defaultBranch: "main");

        var outcome = await _service.FeedCodeMapAsync(
            "Open", "docs/code-map.json",
            """{"version":1,"modules":[{"id":"core","path":"/etc/core","summary":"x"}],"dependencies":[]}""",
            From());

        Assert.Equal(FeedRefusal.Malformed, outcome.Refusal);
        Assert.False(outcome.Information);
        Assert.Contains("repository-relative", outcome.Message);
        Assert.Null((await _service.CodeMapAsync("Open"))!.File);
    }

    /// <summary>The file it names is one the reader would have found — a feed cannot invent where a map lives.</summary>
    [Fact]
    public async Task A_fed_code_map_names_a_file_the_reader_would_have_found()
    {
        await Register("Open", joined: true, shares: true, defaultBranch: "main");

        var outcome = await _service.FeedCodeMapAsync("Open", "../secrets.json", MapJson, From());

        Assert.Equal(FeedRefusal.Malformed, outcome.Refusal);
        Assert.Contains("docs/code-map.json", outcome.Message);
    }

    /// <summary>
    /// A newer commit with no map deletes the held one — and the commit stays held, so a checkout from
    /// before the deletion cannot bring the map back.
    /// </summary>
    [Fact]
    public async Task A_newer_commit_with_no_map_deletes_the_held_one_for_good()
    {
        await Register("Open", joined: true, shares: true, defaultBranch: "main");
        await _service.FeedCodeMapAsync("Open", "docs/code-map.json", MapJson, From("withmap", at: "2026-09-20T09:00:00Z"));

        var removed = await _service.FeedCodeMapAsync(
            "Open", file: null, map: null, From("withoutmap", at: "2026-09-20T10:00:00Z", onBase: "withmap"));
        var older = await _service.FeedCodeMapAsync(
            "Open", "docs/code-map.json", MapJson, From("oldermap", at: "2026-09-20T08:00:00Z"));

        Assert.True(removed.Accepted);
        Assert.Equal(FeedRefusal.Stale, older.Refusal);
        Assert.Null((await _service.CodeMapAsync("Open"))!.File);
    }

    /// <summary>A code map is knowledge about the repository: it leaves only with the second declaration.</summary>
    [Fact]
    public async Task A_code_map_from_a_repository_that_does_not_share_knowledge_is_refused()
    {
        await Register("Reserved", joined: true, shares: false);

        var outcome = await _service.FeedCodeMapAsync("Reserved", "docs/code-map.json", MapJson, From());

        Assert.Equal(FeedRefusal.NotSharing, outcome.Refusal);
        Assert.Null((await _service.CodeMapAsync("Reserved"))!.File);
    }

    /// <summary>The map keeps its own place in the history: a knowledge feed does not move it, nor the reverse.</summary>
    [Fact]
    public async Task The_code_map_and_the_knowledge_are_held_at_their_own_commits()
    {
        await Register("Open", joined: true, shares: true, defaultBranch: "main");
        await _service.FeedCodeMapAsync("Open", "docs/code-map.json", MapJson, From("mapcommit", at: "2026-09-20T09:00:00Z"));
        await _service.FeedAsync("Open", [Entry()], From("knowcommit", at: "2026-09-20T10:00:00Z"));

        var held = await _service.HeldAsync("Open");

        Assert.Equal("knowcommit", held.Knowledge);
        Assert.Equal("mapcommit", held.CodeMap);
    }

    // ——— A registration by ancestry (SYNC5b, sync design §8): the declaration is the manifest at a
    // commit, ordered like a feed, so two checkouts of one repository stop overwriting each other.

    private static Registration Declaration(string summary = "A repo.", string? defaultBranch = "main") =>
        new("Open", Adopted: true, summary, ["x"], [], [], Entries: 0,
            Joined: true, SharesKnowledge: true, DefaultBranch: defaultBranch);

    private async Task<string?> SummaryOf(string repository) =>
        (await _service.RegistryAsync()).Single(r => r.Repository == repository).Summary;

    /// <summary>
    /// The first registration is taken from any line, and from a checkout that names no commit: nothing
    /// else of a repository can travel until it is registered, and nothing is held to order it against.
    /// </summary>
    [Fact]
    public async Task The_first_registration_is_taken_from_any_line_or_none()
    {
        var branch = await _service.RegisterFedAsync(Declaration(), From(branch: "feature/x"), Now);
        var unnamed = await _service.RegisterFedAsync(
            Declaration() with { Repository = "Unnamed" }, provenance: null, Now);

        Assert.True(branch.Outcome.Accepted);
        Assert.True(unnamed.Outcome.Accepted);
        Assert.Equal("aaaa1111bbbb2222", (await _service.HeldAsync("Open")).Registration);
        Assert.Null((await _service.HeldAsync("Unnamed")).Registration);
    }

    /// <summary>
    /// SYNC0b's last-writer-wins: a checkout behind the held commit re-sent its older declaration every
    /// tick and overwrote the newer one. A descendant fast-forwards; an older commit keeps what is held.
    /// </summary>
    [Fact]
    public async Task A_newer_declaration_fast_forwards_and_an_older_one_keeps_what_is_held()
    {
        await _service.RegisterFedAsync(Declaration("First."), From("parentparent", at: "2026-09-20T09:00:00Z"), Now);

        var child = await _service.RegisterFedAsync(
            Declaration("Newer."), From("childchild", at: "2026-09-20T10:00:00Z", onBase: "parentparent"), Now);
        var older = await _service.RegisterFedAsync(
            Declaration("Older."), From("olderolder", at: "2026-09-20T08:00:00Z"), Now);

        Assert.True(child.Outcome.Accepted);
        Assert.Equal(FeedRefusal.Stale, older.Outcome.Refusal);
        Assert.True(older.Outcome.Information);
        Assert.Contains("registration", older.Outcome.Message);
        Assert.Equal("Newer.", await SummaryOf("Open"));
        Assert.Equal("childchild", (await _service.HeldAsync("Open")).Registration);
    }

    /// <summary>The same commit declared the same way is held already; declared otherwise, the first reading stands.</summary>
    [Fact]
    public async Task The_same_commit_is_held_once_and_a_second_reading_of_it_is_information()
    {
        await _service.RegisterFedAsync(Declaration("As read first."), From(origin: "person@machine-a"), Now);

        var same = await _service.RegisterFedAsync(Declaration("As read first."), From(origin: "person@machine-b"), Now);
        var other = await _service.RegisterFedAsync(Declaration("Read otherwise."), From(origin: "person@machine-b"), Now);

        Assert.True(same.Outcome.Accepted);
        Assert.Contains("already", same.Outcome.Message);
        Assert.Equal(FeedRefusal.ContentDiffers, other.Outcome.Refusal);
        Assert.True(other.Outcome.Information);
        Assert.Equal("As read first.", await SummaryOf("Open"));
    }

    /// <summary>
    /// Once a repository is registered, a declaration from a line it does not call canonical is not yet
    /// the family's. The line is the one the arriving declaration names, so a repository that renamed
    /// its default branch is not locked out by the name it used before.
    /// </summary>
    [Fact]
    public async Task Once_registered_a_declaration_is_taken_only_from_the_line_it_calls_canonical()
    {
        await _service.RegisterFedAsync(Declaration("Main."), From("mainmain", at: "2026-09-20T09:00:00Z"), Now);

        var feature = await _service.RegisterFedAsync(
            Declaration("Feature."), From("featfeat", branch: "feature/x", at: "2026-09-20T10:00:00Z", onBase: "mainmain"), Now);
        var renamed = await _service.RegisterFedAsync(
            Declaration("Trunk.", defaultBranch: "trunk"),
            From("trunktrunk", branch: "trunk", at: "2026-09-20T11:00:00Z", onBase: "mainmain"), Now);

        Assert.Equal(FeedRefusal.NotDefaultBranch, feature.Outcome.Refusal);
        Assert.True(feature.Outcome.Information);
        Assert.Contains("feature/x", feature.Outcome.Message);
        Assert.True(renamed.Outcome.Accepted);
        Assert.Equal("Trunk.", await SummaryOf("Open"));
    }

    /// <summary>
    /// A declaration naming no commit cannot be ordered against one that names one, so it does not
    /// replace it: information, and the repository stays registered as held.
    /// </summary>
    [Fact]
    public async Task A_declaration_naming_no_commit_does_not_replace_one_that_names_one()
    {
        await _service.RegisterFedAsync(Declaration("Committed."), From(), Now);

        var unnamed = await _service.RegisterFedAsync(Declaration("Uncommitted."), provenance: null, Now);

        Assert.Equal(FeedRefusal.Unordered, unnamed.Outcome.Refusal);
        Assert.True(unnamed.Outcome.Information);
        Assert.Contains("aaaa1111", unnamed.Outcome.Message);
        Assert.Equal("Committed.", await SummaryOf("Open"));
    }

    /// <summary>The digest is over the whole declaration: the flags and the canonical line are part of what it says.</summary>
    [Fact]
    public void A_declaration_s_digest_hears_every_field_it_stores()
    {
        var one = Declaration();

        Assert.Equal(FeedDigest.Of(one), FeedDigest.Of(one with { Entries = 7, Root = "ignored" }));
        Assert.NotEqual(FeedDigest.Of(one), FeedDigest.Of(one with { SharesKnowledge = false }));
        Assert.NotEqual(FeedDigest.Of(one), FeedDigest.Of(one with { DefaultBranch = "trunk" }));
        Assert.NotEqual(FeedDigest.Of(one), FeedDigest.Of(one with { Owns = ["x", "y"] }));
        Assert.NotEqual(
            FeedDigest.Of(one with { Owns = ["ab"], Accepts = ["c"] }),
            FeedDigest.Of(one with { Owns = ["a"], Accepts = ["bc"] }));
    }

    /// <summary>The retire the deployment is told of forgets the declaration's commit with the row: it keeps no tombstone.</summary>
    [Fact]
    public async Task A_retired_registration_is_registered_afresh_by_whoever_holds_the_checkout()
    {
        await _service.RegisterFedAsync(Declaration("Newer."), From("newernewer", at: "2026-09-20T10:00:00Z"), Now);
        await _service.RetireAsync("Open");

        var again = await _service.RegisterFedAsync(
            Declaration("From an older checkout."), From("olderolder", at: "2026-09-20T08:00:00Z"), Now);

        Assert.True(again.Outcome.Accepted);
        Assert.Equal("From an older checkout.", await SummaryOf("Open"));
    }
}

/// <summary>
/// The session feed's judgement, in Core beside the ledger's (D47 §6): records upsert whole under
/// their origin, only joined repositories' records are taken, and a refused feed changes nothing.
/// </summary>
public sealed class SessionFeedTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-20T10:00:00Z");

    private Microsoft.Data.Sqlite.SqliteConnection _connection = null!;
    private SessionStore _sessions = null!;
    private SessionFeed _feed = null!;

    public async Task InitializeAsync()
    {
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _sessions = await SessionStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.RegisterAsync(
            new Registration("Joined", Adopted: true, null, [], [], [], Entries: 0, Joined: true), Now);
        await service.RegisterAsync(
            new Registration("Homebody", Adopted: true, null, [], [], [], Entries: 0, Joined: false), Now);
        _feed = new SessionFeed(service, _sessions);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private static FedSessionRecord Record(
        string id = "ab12cd34", string repository = "Joined", string state = "completed", string? quest = "abc123") =>
        new(id, quest, repository, "stub", state, null, null, Now, Now);

    [Fact]
    public async Task A_joined_repositorys_records_mirror_under_their_origin()
    {
        var outcome = await _feed.FeedAsync("alice-laptop", [Record()]);

        Assert.Equal(SessionFeedRefusal.None, outcome.Refusal);
        Assert.Equal(1, outcome.Records);
        var mirrored = Assert.Single(await _sessions.ListAsync(includeClosed: true));
        Assert.Equal("alice-laptop/ab12cd34", mirrored.Id);
        Assert.Equal(SessionState.Completed, mirrored.State);
    }

    [Fact]
    public async Task An_unjoined_repositorys_records_are_refused()
    {
        var outcome = await _feed.FeedAsync("alice-laptop", [Record(repository: "Homebody")]);

        Assert.Equal(SessionFeedRefusal.NotJoined, outcome.Refusal);
        Assert.Contains("remote.join", outcome.Message);
        Assert.Empty(await _sessions.ListAsync(includeClosed: true));
    }

    /// <summary>
    /// A conversation mirrors as a conversation (D49 §3): no quest, and the KIND travels. Without it
    /// every chat would arrive at a teammate's deployment looking like planned work — and demanding a
    /// quest would make it unmirrorable, so the repository would simply fall silent instead.
    /// </summary>
    [Fact]
    public async Task A_chat_mirrors_with_no_quest_and_says_it_was_a_chat()
    {
        var outcome = await _feed.FeedAsync(
            "alice-laptop",
            [Record(id: "c0ffee11", quest: null) with { Kind = "Chat" }]);

        Assert.Equal(SessionFeedRefusal.None, outcome.Refusal);
        var mirrored = Assert.Single(await _sessions.ListAsync(includeClosed: true));
        Assert.Equal(SessionKind.Chat, mirrored.Kind);
        Assert.Null(mirrored.Quest);
    }

    /// <summary>A refused feed changes nothing — the good record beside the bad one stays unmirrored.</summary>
    [Fact]
    public async Task A_malformed_record_refuses_the_whole_feed_before_anything_lands()
    {
        var outcome = await _feed.FeedAsync("alice-laptop", [Record(), Record(id: "ef56ab78", state: "99")]);

        Assert.Equal(SessionFeedRefusal.Malformed, outcome.Refusal);
        Assert.Empty(await _sessions.ListAsync(includeClosed: true));
    }
}
