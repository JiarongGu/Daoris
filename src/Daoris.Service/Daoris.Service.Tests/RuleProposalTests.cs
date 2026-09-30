using System.Text.Json;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// An agent proposes a change to what agents may do (PERM2, D74) — through the connector, because an
/// agent's only voice is its tools, and into a FILE under the Daoris home, beside the rules it would
/// change, because the rules are machine-local and so is anything that would change them.
/// </summary>
/// <remarks>
/// The door decides nothing about narrowing or widening: it cannot see the rules file, and the answer
/// depends on what that file holds. It checks the proposal is well formed, records who made it, and
/// says what happens next. The driver's tick applies a narrowing; a widening waits for the person.
/// </remarks>
public sealed class RuleProposalTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-proposals-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private KnowledgeService _service = null!;
    private QuestStore _quests = null!;

    private string Home => Path.Combine(_root, "home");

    public async Task InitializeAsync()
    {
        var dir = Path.Combine(_root, "family", "engine");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "daoris.json"), """
            { "source": "s", "packs": [], "domain": { "summary": "s", "owns": ["o"], "accepts": ["a"] } }
            """);

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        var store = new InMemoryKnowledgeStore();
        _service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await _service.ImportAsync(Path.Combine(_root, "family"), DateTimeOffset.UtcNow);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private KnowledgeTools Tools(RuleProposalBox box, string? session = "s1a2b3c4", string? ask = null) => new(
        _service, _quests, new QuestExchange(_service, _quests),
        new AmbientWorkspace(Path.Combine(_root, "family", "engine")),
        intake: new IntakeScope(ask, session), proposals: box);

    private string[] Written() =>
        Directory.Exists(Path.Combine(Home, RuleProposalBox.Folder))
            ? Directory.GetFiles(Path.Combine(Home, RuleProposalBox.Folder), "*.json")
            : [];

    [Fact]
    public void A_proposal_is_written_under_the_home_naming_the_session_that_made_it()
    {
        var answer = Tools(new RuleProposalBox(Home)).ProposePermission(
            "add", "repository", "The session ran `rm` on a build folder it did not need to.",
            list: "deny", rule: "Bash(rm:*)", name: "engine");

        var file = Assert.Single(Written());
        using var document = JsonDocument.Parse(File.ReadAllText(file));
        var root = document.RootElement;
        var id = root.GetProperty("id").GetString()!;
        Assert.Equal($"{id}.json", Path.GetFileName(file));
        Assert.Contains($"#{id}", answer);
        Assert.Equal("proposed", root.GetProperty("state").GetString());
        Assert.Equal("s1a2b3c4", root.GetProperty("by").GetProperty("session").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("by").GetProperty("ask").ValueKind);
        var change = root.GetProperty("change");
        Assert.Equal("add", change.GetProperty("action").GetString());
        Assert.Equal("deny", change.GetProperty("list").GetString());
        Assert.Equal("Bash(rm:*)", change.GetProperty("rule").GetString());
        Assert.Equal("repository", change.GetProperty("scope").GetString());
        Assert.Equal("engine", change.GetProperty("name").GetString());
        Assert.Contains("did not need to", root.GetProperty("why").GetString());
        // Written as a file person and driver read: LF, and no byte-order mark.
        var bytes = File.ReadAllBytes(file);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB);
        Assert.DoesNotContain("\r", File.ReadAllText(file));
    }

    /// <summary>
    /// The door says what happens next without deciding it: it cannot see the rules, and whether a
    /// change narrows depends on them (a deny made an ask is wider; an allow removed is narrower).
    /// </summary>
    [Fact]
    public void The_answer_says_a_narrowing_applies_and_a_widening_waits_for_the_person()
    {
        var answer = Tools(new RuleProposalBox(Home)).ProposePermission(
            "add", "machine", "It needs to run the tests.", list: "allow", rule: "Bash(npm test:*)");

        Assert.Contains("next look", answer);
        Assert.Contains("person", answer);
    }

    [Fact]
    public void A_default_switch_is_proposed_by_its_id()
    {
        Tools(new RuleProposalBox(Home)).ProposePermission(
            "default", "machine", "This repository commits through its own script.", id: "commit", on: false);

        using var document = JsonDocument.Parse(File.ReadAllText(Assert.Single(Written())));
        var change = document.RootElement.GetProperty("change");
        Assert.Equal("default", change.GetProperty("action").GetString());
        Assert.Equal("commit", change.GetProperty("default").GetString());
        Assert.False(change.GetProperty("on").GetBoolean());
    }

    /// <summary>An intake answers an ask, and its proposal says so — the ask is what a reader follows.</summary>
    [Fact]
    public void An_intakes_proposal_names_its_ask()
    {
        Tools(new RuleProposalBox(Home), session: "i9n8t7k6", ask: "a1b2c3").ProposePermission(
            "add", "workspace", "It could not read the ask's file.", list: "allow", rule: "Read(//C:/somewhere/data/asks/**)", name: "default");

        using var document = JsonDocument.Parse(File.ReadAllText(Assert.Single(Written())));
        Assert.Equal("a1b2c3", document.RootElement.GetProperty("by").GetProperty("ask").GetString());
    }

    [Theory]
    [InlineData("add", "deny", "rm -rf everything", "machine", null, "not a permission rule")]
    [InlineData("add", "maybe", "Bash(ls)", "machine", null, "`allow`, `ask` or `deny`")]
    [InlineData("add", "deny", "Bash(ls)", "repository", null, "names the repository")]
    [InlineData("add", "deny", "Bash(ls)", "galaxy", null, "`machine`, a `workspace` or a `repository`")]
    [InlineData("rename", null, "Bash(ls)", "machine", null, "`add`, `remove` or `default`")]
    public void A_malformed_proposal_is_refused_at_the_door_and_nothing_is_written(
        string action, string? list, string rule, string scope, string? name, string said)
    {
        var answer = Tools(new RuleProposalBox(Home)).ProposePermission(
            action, scope, "A reason.", list: list, rule: rule, name: name);

        Assert.Contains(said, answer);
        Assert.Contains("Nothing was proposed", answer);
        Assert.Empty(Written());
    }

    [Fact]
    public void A_proposal_needs_its_reason()
    {
        var answer = Tools(new RuleProposalBox(Home)).ProposePermission(
            "add", "machine", "   ", list: "deny", rule: "Bash(rm:*)");

        Assert.Contains("reason", answer);
        Assert.Empty(Written());
    }

    [Fact]
    public void A_default_proposal_names_a_default_and_whether_it_goes_on_or_off()
    {
        var answer = Tools(new RuleProposalBox(Home)).ProposePermission("default", "machine", "A reason.", id: "commit");

        Assert.Contains("on or off", answer);
        Assert.Empty(Written());
    }

    /// <summary>No home, no box (D63): a proposal with nowhere to go says so rather than guessing a place.</summary>
    [Fact]
    public void With_no_home_the_proposal_is_refused_with_the_homes_sentence()
    {
        var answer = Tools(new RuleProposalBox(null)).ProposePermission(
            "add", "machine", "A reason.", list: "deny", rule: "Bash(rm:*)");

        Assert.Contains("DAORIS_HOME", answer);
    }

    /// <summary>
    /// Which home: the driver names the one it keeps its rules in (a quest's or an intake's connector
    /// carries it), and a connector the driver did not start falls back to the account's home.
    /// </summary>
    [Fact]
    public void The_box_is_the_drivers_named_home_first_and_the_accounts_home_after()
    {
        var named = RuleProposalBox.FromEnvironment(name => name switch
        {
            RuleProposalBox.HomeVariable => "C:/somewhere/driver-home",
            DaorisHome.Variable => "C:/somewhere/data",
            _ => null,
        });
        var fallback = RuleProposalBox.FromEnvironment(name => name == DaorisHome.Variable ? "C:/somewhere/data" : null);
        var none = RuleProposalBox.FromEnvironment(_ => null);

        Assert.Equal("C:/somewhere/driver-home", named.Home);
        Assert.Equal("C:/somewhere/data", fallback.Home);
        Assert.Null(none.Home);
    }
}
