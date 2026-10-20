using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HELPMODE1c2a (D183's note; the Ask Daoris modes design §4.1 and §7): Ask Daoris's housekeeping grant. The person's record of
/// what is chosen; each start composing it again onto the allow-list and the receipt; and the driver's judgement of each tool's
/// request, at every call, before the cleanup's own judges: the record is Ask Daoris's, the operation is chosen now and was handed
/// at this start, and the repository is the conversation's workspace's, with a checkout here. No git runs here: every refusal
/// comes before the checkout, and a discard's keep is read off the layout and the ledger. The git half is
/// <c>HelpHousekeepingGitTests</c>.
/// </summary>
public sealed class HelpHousekeepingTests : IDisposable
{
    private const string Branch = "daoris/s-1a2b3c4d";

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-housekeeping-" + Guid.NewGuid().ToString("N")[..8]);

    public HelpHousekeepingTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Checkout(string name)
    {
        var root = Path.Combine(_home, "checkouts", name);
        Directory.CreateDirectory(root);
        return root;
    }

    private void Choose(string json) => File.WriteAllText(ConfiguredAccess.PathOf(_home), json);

    /// <summary>What this start was handed, as its receipt keeps it.</summary>
    private void Handed(string session, params string[] housekeeping) =>
        new HelpWired(_home).Keep(session, new HelpReceipt
        {
            At = DateTimeOffset.UtcNow, Harness = "codex-acp", Folder = HelpRoom.PathOf(_home, "aurora"), Housekeeping = housekeeping,
        });

    /// <summary>
    /// The ledger: Ask Daoris's conversation <c>h1</c> in workspace <c>aurora</c>'s room, a driven session <c>s1</c>, and the
    /// registry's <c>engine</c> (aurora) and <c>rival</c> (borealis), each with a checkout here.
    /// </summary>
    private HousekeepingLedger Ledger() => new()
    {
        Records = [("h1", HelpRoom.Repository, "aurora", HelpRoom.PathOf(_home, "aurora")), ("s1", "engine", "aurora", null)],
        Registry = [("engine", "aurora", Checkout("engine")), ("rival", "borealis", Checkout("rival"))],
    };

    private static SessionRequest Request(string operation, string? repository = null, params string[] branches) =>
        new("h1", SessionMove.Housekeep, DateTimeOffset.UtcNow)
        {
            By = RequestDoor.Help, Key = "0123456789ab", Operation = operation, Repository = repository, Branches = branches,
        };

    private Task<HousekeepingAnswer> AnswerAsync(HousekeepingLedger ledger, SessionRequest request) =>
        new HelpHousekeeping(_home, ledger.Client).AnswerAsync(request, CancellationToken.None);

    /// <summary>
    /// The record keeps the operations chosen by the page's codes, read in the table's order, once each: one this row does not
    /// build (dropping recovery refs) grants nothing, and no file, one that does not read, or a grant that is no list, is none.
    /// </summary>
    [Fact]
    public void The_record_reads_the_operations_chosen_and_none_where_it_does_not_read()
    {
        Assert.Empty(ConfiguredAccess.Read(_home).Housekeeping);

        Choose("""{ "housekeeping": ["discard-branch", "inspect", "drop-recovery", "inspect", 7], "checkouts": ["read"] }""");
        Assert.Equal([ConfiguredAccess.Inspect, ConfiguredAccess.DiscardBranch], ConfiguredAccess.Read(_home).Housekeeping);
        Assert.Equal(["housekeeping_inspect", "housekeeping_discard_branch"], ConfiguredAccess.Read(_home).HousekeepingTools);

        Choose("{ \"housekeeping\": ");
        Assert.Empty(ConfiguredAccess.Read(_home).Housekeeping);
        Choose("""{ "housekeeping": "inspect" }""");
        Assert.Empty(ConfiguredAccess.Read(_home).Housekeeping);
    }

    /// <summary>
    /// HELPMODE1c2b: the person's choice of one operation edits that grant's list alone, made onto the file as it reads at the write,
    /// so what it holds besides (another grant, a code this build does not act on, a key it does not know) stays as written.
    /// </summary>
    [Fact]
    public void A_choice_edits_its_grant_alone_and_keeps_what_the_file_holds_besides()
    {
        Choose("""{ "housekeeping": ["drop-recovery"], "checkouts": ["read"], "later": { "kept": 1 } }""");

        ConfiguredAccess.Choose(_home, ConfiguredAccess.HousekeepingGrant, ConfiguredAccess.CleanUp, true);
        ConfiguredAccess.Choose(_home, ConfiguredAccess.HousekeepingGrant, ConfiguredAccess.Inspect, true);
        var chosen = ConfiguredAccess.Choose(_home, ConfiguredAccess.HousekeepingGrant, ConfiguredAccess.CleanUp, false);

        Assert.Equal([ConfiguredAccess.Inspect], chosen.Housekeeping);
        using var file = JsonDocument.Parse(File.ReadAllText(ConfiguredAccess.PathOf(_home)));
        Assert.Equal(["inspect", "drop-recovery"], file.RootElement.GetProperty("housekeeping").EnumerateArray().Select(each => each.GetString()));
        Assert.Equal(["read"], file.RootElement.GetProperty("checkouts").EnumerateArray().Select(each => each.GetString()));
        Assert.Equal(1, file.RootElement.GetProperty("later").GetProperty("kept").GetInt32());
        Assert.DoesNotContain("\r", File.ReadAllText(ConfiguredAccess.PathOf(_home)));
    }

    /// <summary>
    /// A choice this build does not act on is refused in a sentence and nothing is written: an operation it does not know, dropping
    /// recovery refs (not built), and another grant (the browser's is its plugin's approval, the others not built). A file that does
    /// not read is refused and left as it is, never written over.
    /// </summary>
    [Fact]
    public void A_choice_it_does_not_act_on_or_a_file_that_does_not_read_is_refused()
    {
        Assert.Contains("`everything`", Assert.Throws<DriverException>(() => ConfiguredAccess.Choose(_home, "housekeeping", "everything", true)).Message);
        Assert.Contains("not built", Assert.Throws<DriverException>(() => ConfiguredAccess.Choose(_home, "housekeeping", "drop-recovery", true)).Message);
        Assert.Contains("plugin", Assert.Throws<DriverException>(() => ConfiguredAccess.Choose(_home, "browser", "reads", true)).Message);
        Assert.Contains("not built", Assert.Throws<DriverException>(() => ConfiguredAccess.Choose(_home, "shell", "run", true)).Message);
        Assert.False(File.Exists(ConfiguredAccess.PathOf(_home)));

        Choose("{ not json");
        Assert.Throws<DriverException>(() => ConfiguredAccess.Choose(_home, "housekeeping", "inspect", true));
        Assert.Equal("{ not json", File.ReadAllText(ConfiguredAccess.PathOf(_home)));
    }

    /// <summary>
    /// 🔴 Each start composes the grant again (design §4.1's trap): the place a start is handed reads the record then, its allow-list
    /// names the chosen operations' tools beside the room's own, and the room's own list names none of them, so nothing is allowed
    /// by a list the person did not choose. Full access keeps the grant, since the tools judge it either way.
    /// </summary>
    [Fact]
    public void Each_start_hands_the_housekeeping_chosen_then_on_its_allow_list()
    {
        var file = PermissionRules.Load(_home);
        Assert.DoesNotContain(HelpRoom.Allowed, rule => rule.Contains("housekeeping_", StringComparison.Ordinal));
        var none = ChatRunner.HelpPlaceIn(_home, HelpRoom.PathOf(_home), []);
        Assert.Empty(none.Housekeeping);
        Assert.DoesNotContain(none.Rules(file, "h1").Allow, rule => rule.Contains("housekeeping_", StringComparison.Ordinal));

        Choose("""{ "housekeeping": ["clean-up", "inspect"] }""");
        var place = ChatRunner.HelpPlaceIn(_home, HelpRoom.PathOf(_home), []);

        Assert.Equal([ConfiguredAccess.Inspect, ConfiguredAccess.CleanUp], place.Housekeeping);
        var allow = place.Rules(file, "h1").Allow;
        Assert.Contains($"mcp__{KnowledgeConnector.ServerName}__housekeeping_inspect", allow);
        Assert.Contains($"mcp__{KnowledgeConnector.ServerName}__housekeeping_clean_up", allow);
        Assert.DoesNotContain($"mcp__{KnowledgeConnector.ServerName}__housekeeping_discard_branch", allow);
        Assert.Equal(place.Housekeeping, ChatRunner.HelpUnder(place, HelpAccess.For(HelpModes.Full, AdapterSet.Built().Resolve("claude-code"))).Housekeeping);
    }

    /// <summary>
    /// 🔴 A grant off refuses each operation, whatever the agent (Codex takes no allow-list, so the call's judgement is what holds
    /// it), before any repository is looked at.
    /// </summary>
    [Theory]
    [InlineData(ConfiguredAccess.Inspect)]
    [InlineData(ConfiguredAccess.CleanUp)]
    [InlineData(ConfiguredAccess.DiscardBranch)]
    public async Task A_grant_off_refuses_each_operation(string operation)
    {
        var ledger = Ledger();
        Handed("h1");

        var answer = await AnswerAsync(ledger, Request(operation, "engine", Branch));

        Assert.Equal((operation, false, HousekeepingAnswer.NotGranted), (answer.Operation, answer.Done, answer.Code));
        Assert.Contains("configured access", answer.Said);
        Assert.Contains("Nothing was done", answer.Said);
        Assert.DoesNotContain("/api/registry", ledger.Asked);
    }

    /// <summary>
    /// 🔴 A revoke refuses the next call, mid-conversation and on a resume alike, since each call reads the record; and a grant
    /// chosen after this start began waits for the next start, as its room and its allow-list do (a widening, D183's HELPMODE1a2).
    /// </summary>
    [Fact]
    public async Task A_revoke_refuses_the_next_call_and_a_widening_takes_the_next_start()
    {
        var ledger = Ledger();
        Choose("""{ "housekeeping": ["inspect"] }""");
        Handed("h1");
        Assert.Equal(HousekeepingAnswer.NotHanded, (await AnswerAsync(ledger, Request(ConfiguredAccess.Inspect))).Code);

        // A start that was handed it, then the person revokes it while the conversation runs.
        Handed("h1", ConfiguredAccess.Inspect);
        Choose("""{ "housekeeping": [] }""");
        Assert.Equal(HousekeepingAnswer.NotGranted, (await AnswerAsync(ledger, Request(ConfiguredAccess.Inspect))).Code);

        // Resumed after the revoke: its new start was handed nothing, and the record still says none.
        Handed("h1");
        Assert.Equal(HousekeepingAnswer.NotGranted, (await AnswerAsync(ledger, Request(ConfiguredAccess.Inspect))).Code);
        Assert.DoesNotContain("/api/registry", ledger.Asked);
    }

    /// <summary>
    /// 🔴 A request for a session that is not an Ask Daoris conversation is refused (a driven session shares the connector), and
    /// so is one for a repository outside the conversation's workspace, read from its record and never from the request; a
    /// repository nobody registered, or one with no checkout here, is said so.
    /// </summary>
    [Fact]
    public async Task Another_session_and_a_repository_outside_the_workspace_are_refused()
    {
        var ledger = Ledger();
        Choose("""{ "housekeeping": ["inspect", "clean-up", "discard-branch"] }""");
        Handed("h1", ConfiguredAccess.Inspect, ConfiguredAccess.CleanUp, ConfiguredAccess.DiscardBranch);
        Handed("s1", ConfiguredAccess.Inspect);

        var driven = await AnswerAsync(ledger, Request(ConfiguredAccess.Inspect) with { Session = "s1" });
        Assert.Equal(HousekeepingAnswer.NotHelp, driven.Code);
        Assert.Contains("`s1`", driven.Said);

        var outside = await AnswerAsync(ledger, Request(ConfiguredAccess.CleanUp, "rival", Branch));
        Assert.Equal(HousekeepingAnswer.Outside, outside.Code);
        Assert.Contains("`aurora`", outside.Said);
        Assert.Contains("`rival`", outside.Said);

        Assert.Equal(HousekeepingAnswer.Unknown, (await AnswerAsync(ledger, Request(ConfiguredAccess.CleanUp, "nowhere", Branch))).Code);
        Directory.Delete(Path.Combine(_home, "checkouts", "engine"));
        Assert.Equal(HousekeepingAnswer.NoCheckout, (await AnswerAsync(ledger, Request(ConfiguredAccess.CleanUp, "engine", Branch))).Code);
    }

    /// <summary>
    /// A clean-up names its branches and a discard names one session branch: a path or a branch that is no session branch is
    /// refused before the checkout. A discard of a branch whose tree a running session holds reaches the discard's own judge,
    /// which keeps it with the sentence both doors say, unforced.
    /// </summary>
    [Fact]
    public async Task Targets_are_names_and_a_live_sessions_tree_keeps_its_branch()
    {
        var ledger = Ledger();
        Choose("""{ "housekeeping": ["clean-up", "discard-branch"] }""");
        Handed("h1", ConfiguredAccess.CleanUp, ConfiguredAccess.DiscardBranch);

        Assert.Equal(HousekeepingAnswer.Named, (await AnswerAsync(ledger, Request(ConfiguredAccess.CleanUp, "engine"))).Code);
        Assert.Equal(HousekeepingAnswer.Named, (await AnswerAsync(ledger, Request(ConfiguredAccess.DiscardBranch, "engine", "main"))).Code);
        Assert.Equal(HousekeepingAnswer.Named, (await AnswerAsync(ledger, Request(ConfiguredAccess.DiscardBranch, "engine", "X:/daoris/trees/s-1"))).Code);
        Assert.Equal(HousekeepingAnswer.Named, (await AnswerAsync(ledger, Request(ConfiguredAccess.DiscardBranch, "engine", Branch, "daoris/s-other"))).Code);

        var tree = DaorisHome.Entry(_home, HomeEntry.Trees, "aurora", "engine", "s-1a2b3c4d");
        Directory.CreateDirectory(tree);
        ledger.Live = [("s9", "working", tree)];
        var kept = await AnswerAsync(ledger, Request(ConfiguredAccess.DiscardBranch, "engine", Branch));

        Assert.Equal((false, HousekeepingAnswer.Kept), (kept.Done, kept.Code));
        Assert.Contains(SessionBranchDiscard.Held("engine", Branch), kept.Said);
        Assert.True(Directory.Exists(tree));
    }
}

/// <summary>
/// The service standing in for housekeeping's judgement: the records (Ask Daoris's and others, each with its workspace and tree),
/// the registry's checkouts, and the sessions running or waiting with their trees.
/// </summary>
internal sealed class HousekeepingLedger : HttpMessageHandler
{
    private readonly List<string> _asked = [];

    public (string Id, string Repository, string Workspace, string? Tree)[] Records { get; set; } = [];

    public (string Repository, string Workspace, string Root)[] Registry { get; set; } = [];

    public (string Id, string State, string Tree)[] Live { get; set; } = [];

    public IReadOnlyList<string> Asked
    {
        get { lock (_asked) return [.. _asked]; }
    }

    public ServiceClient? Client() => new("http://stand-in", null, new HttpClient(this, disposeHandler: false));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var uri = request.RequestUri!;
        lock (_asked) _asked.Add(uri.AbsolutePath);
        object body = uri.AbsolutePath switch
        {
            "/api/registry" => Registry.Select(row => new { repository = row.Repository, workspace = row.Workspace, root = row.Root }).ToArray(),
            "/api/sessions" when uri.Query.Contains("includeClosed", StringComparison.Ordinal) =>
                Records.Select(each => new { id = each.Id, repository = each.Repository, state = "working", workspace = each.Workspace, tree = each.Tree })
                    .Concat(Live.Select(each => new { id = each.Id, repository = "engine", state = each.State, workspace = "aurora", tree = (string?)each.Tree }))
                    .ToArray(),
            "/api/sessions" => Live.Select(each => new { id = each.Id, repository = "engine", state = each.State, tree = each.Tree }).ToArray(),
            _ => Array.Empty<object>(),
        };
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        });
    }
}
