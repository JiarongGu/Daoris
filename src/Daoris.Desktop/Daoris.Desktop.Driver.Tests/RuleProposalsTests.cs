using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An agent's proposal to change what agents may do (PERM2, D74), as the driver settles it: a change
/// that NARROWS is applied at the next tick, one that WIDENS waits for the person — the owner's answer,
/// 2026-09-24: a widening never applies without the person — and every settling says who did it.
/// </summary>
/// <remarks>
/// Whether a change narrows is read against the rules AS THEY STAND, which is why the connector's door
/// cannot decide it and this can: removing a rule narrows when it was an allow and widens when it was a
/// deny, and making a denied rule an ask loosens it.
/// </remarks>
public sealed class RuleProposalsTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-rule-proposals-" + Guid.NewGuid().ToString("N")[..8]);

    public RuleProposalsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-09-24T10:00:00Z");

    /// <summary>
    /// A proposal exactly as the connector writes it (the service's `RuleProposalBox`) — THE FILE is the
    /// contract, and this is its shape.
    /// </summary>
    private string Proposal(
        string id, string action, string scope = "machine", string? name = null, string? list = null,
        string? rule = null, string? defaultId = null, bool? on = null, string state = "proposed",
        string? session = "s1a2b3c4", string? ask = null)
    {
        var folder = Path.Combine(_home, RuleProposals.Folder);
        Directory.CreateDirectory(folder);
        var node = new JsonObject
        {
            ["id"] = id,
            ["proposed"] = At.AddMinutes(id[^1] - '0').ToString("O"),
            ["by"] = new JsonObject { ["session"] = session, ["ask"] = ask, ["folder"] = "C:/somewhere/engine" },
            ["change"] = new JsonObject
            {
                ["action"] = action, ["scope"] = scope, ["name"] = name, ["list"] = list, ["rule"] = rule,
                ["default"] = defaultId, ["on"] = on,
            },
            ["why"] = "The session's own reason.",
            ["state"] = state,
        };
        var path = Path.Combine(folder, $"{id}.json");
        File.WriteAllText(path, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        return path;
    }

    private static JsonElement Read(string path) => JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();

    private void Rules(Func<PermissionFile, PermissionFile> change) =>
        PermissionRules.Save(_home, change(PermissionRules.Load(_home)));

    // ——— What narrows.

    public static TheoryData<string, string?, string?, string?, bool?, string?, ChangeEffect> Effects => new()
    {
        // action, list, rule, default, on, where the rule already sits in the machine scope, effect
        { "add", "allow", "Bash(npm test:*)", null, null, null, ChangeEffect.Widens },
        { "add", "deny", "Bash(rm:*)", null, null, null, ChangeEffect.Narrows },
        { "add", "ask", "Bash(rm:*)", null, null, null, ChangeEffect.Narrows },
        // A denied rule made an ask is looser: another scope's allow would then be asked, not denied.
        { "add", "ask", "Bash(rm:*)", null, null, "deny", ChangeEffect.Widens },
        { "add", "deny", "Bash(rm:*)", null, null, "deny", ChangeEffect.Nothing },
        { "add", "deny", "Bash(make:*)", null, null, "allow", ChangeEffect.Narrows },
        { "remove", null, "Bash(make:*)", null, null, "allow", ChangeEffect.Narrows },
        { "remove", null, "Bash(rm:*)", null, null, "deny", ChangeEffect.Widens },
        { "remove", null, "Bash(rm:*)", null, null, "ask", ChangeEffect.Widens },
        { "remove", null, "Bash(rm:*)", null, null, null, ChangeEffect.Nothing },
        // A default that allows narrows when it goes off; one that denies or guards widens.
        { "default", null, null, "connector", false, null, ChangeEffect.Narrows },
        { "default", null, null, "commit", false, null, ChangeEffect.Narrows },
        { "default", null, null, "no-push", false, null, ChangeEffect.Widens },
        { "default", null, null, "tree-guard", false, null, ChangeEffect.Widens },
        { "default", null, null, "commit", true, null, ChangeEffect.Nothing },
    };

    [Theory]
    [MemberData(nameof(Effects))]
    public void A_change_narrows_or_widens_by_what_the_rules_hold_now(
        string action, string? list, string? rule, string? defaultId, bool? on, string? sits, ChangeEffect expected)
    {
        var file = PermissionRules.Load(_home);
        if (sits is not null)
        {
            file = PermissionRules.Add(file, RuleScope.Machine, null, Enum.Parse<RuleList>(sits, ignoreCase: true), rule!);
        }

        var change = new ProposedChange(action, RuleScope.Machine, null,
            list is null ? null : Enum.Parse<RuleList>(list, ignoreCase: true), rule, defaultId, on);

        Assert.Equal(expected, RuleProposals.Effect(file, change));
    }

    [Fact]
    public void A_default_switched_back_on_narrows_when_it_denies_and_widens_when_it_allows()
    {
        var file = PermissionRules.SwitchDefault(PermissionRules.SwitchDefault(PermissionRules.Load(_home), "no-push", false), "commit", false);

        Assert.Equal(ChangeEffect.Narrows, RuleProposals.Effect(file, new ProposedChange("default", RuleScope.Machine, null, null, null, "no-push", true)));
        Assert.Equal(ChangeEffect.Widens, RuleProposals.Effect(file, new ProposedChange("default", RuleScope.Machine, null, null, null, "commit", true)));
    }

    // ——— The tick's settling.

    [Fact]
    public void A_narrowing_is_applied_at_once_and_says_which_session_proposed_it()
    {
        var path = Proposal("p0000001", "add", scope: "repository", name: "engine", list: "deny", rule: "Bash(rm:*)");

        var said = RuleProposals.Settle(_home, At);

        Assert.Contains("Bash(rm:*)", PermissionRules.Load(_home).Repositories["engine"].Deny);
        var settled = Read(path);
        Assert.Equal("applied", settled.GetProperty("state").GetString());
        Assert.Equal("the driver", settled.GetProperty("settled").GetProperty("by").GetString());
        Assert.Contains("narrows", settled.GetProperty("settled").GetProperty("note").GetString());
        var line = Assert.Single(said);
        Assert.Contains("session s1a2b3c4", line);
        Assert.Contains("Bash(rm:*)", line);
        // What the session wrote is kept whole — its reason and where it ran.
        Assert.Equal("The session's own reason.", settled.GetProperty("why").GetString());
        Assert.Equal("C:/somewhere/engine", settled.GetProperty("by").GetProperty("folder").GetString());
    }

    /// <summary>🔴 The owner's answer: a widening never applies without the person.</summary>
    [Fact]
    public void A_widening_waits_for_the_person_and_changes_nothing()
    {
        var path = Proposal("p0000002", "add", list: "allow", rule: "Bash(npm test:*)");

        var said = RuleProposals.Settle(_home, At);

        Assert.Empty(PermissionRules.Load(_home).Machine.Allow);
        Assert.False(File.Exists(PermissionRules.PathOf(_home)), "a widening wrote the rules file");
        Assert.Equal("waiting", Read(path).GetProperty("state").GetString());
        Assert.Contains("waits for you", Assert.Single(said));
    }

    [Fact]
    public void A_proposal_the_rules_cannot_take_is_refused_in_the_drivers_words()
    {
        var path = Proposal("p0000003", "add", list: "deny", rule: "rm -rf everything");

        RuleProposals.Settle(_home, At);

        var settled = Read(path);
        Assert.Equal("refused", settled.GetProperty("state").GetString());
        Assert.Contains("not a permission rule", settled.GetProperty("settled").GetProperty("note").GetString());
    }

    [Fact]
    public void A_proposal_that_changes_nothing_is_settled_as_unchanged()
    {
        var path = Proposal("p0000004", "remove", rule: "Bash(rm:*)");

        RuleProposals.Settle(_home, At);

        Assert.Equal("unchanged", Read(path).GetProperty("state").GetString());
    }

    /// <summary>A settled proposal is history: a second tick neither applies it again nor says it again.</summary>
    [Fact]
    public void A_settled_proposal_is_left_alone_by_every_later_tick()
    {
        Proposal("p0000005", "add", list: "deny", rule: "Bash(rm:*)");
        Proposal("p0000006", "add", list: "allow", rule: "WebFetch");
        RuleProposals.Settle(_home, At);

        Assert.Empty(RuleProposals.Settle(_home, At.AddMinutes(1)));
    }

    // ——— The person's answer.

    [Fact]
    public void The_persons_yes_applies_a_widening_and_records_them()
    {
        var path = Proposal("p0000007", "add", scope: "workspace", name: "default", list: "allow", rule: "WebFetch");
        RuleProposals.Settle(_home, At);

        var answered = RuleProposals.Answer(_home, "p0000007", accept: true, note: null, At.AddMinutes(2));

        Assert.Equal(ProposalState.Accepted, answered.State);
        Assert.Contains("WebFetch", PermissionRules.Load(_home).Workspaces["default"].Allow);
        Assert.Equal("the person", Read(path).GetProperty("settled").GetProperty("by").GetString());
    }

    [Fact]
    public void The_persons_no_changes_nothing_and_keeps_their_reason()
    {
        var path = Proposal("p0000008", "add", list: "allow", rule: "Bash(curl:*)");
        RuleProposals.Settle(_home, At);

        RuleProposals.Answer(_home, "#p0000008", accept: false, note: "Not from a session.", At.AddMinutes(2));

        Assert.Empty(PermissionRules.Load(_home).Machine.Allow);
        var settled = Read(path);
        Assert.Equal("declined", settled.GetProperty("state").GetString());
        Assert.Equal("Not from a session.", settled.GetProperty("settled").GetProperty("note").GetString());
    }

    /// <summary>A person may answer one the driver has not seen yet — their yes is enough either way.</summary>
    [Fact]
    public void The_person_may_answer_a_proposal_before_the_driver_has_seen_it()
    {
        Proposal("p0000009", "add", list: "deny", rule: "Bash(rm:*)");

        RuleProposals.Answer(_home, "p0000009", accept: true, note: null, At);

        Assert.Contains("Bash(rm:*)", PermissionRules.Load(_home).Machine.Deny);
    }

    [Fact]
    public void A_settled_proposal_cannot_be_answered_again()
    {
        Proposal("p0000001", "add", list: "deny", rule: "Bash(rm:*)");
        RuleProposals.Settle(_home, At);

        var refusal = Assert.Throws<DriverException>(() => RuleProposals.Answer(_home, "p0000001", accept: false, null, At));
        Assert.Contains("already", refusal.Message);
    }

    [Fact]
    public void An_unknown_proposal_is_refused_by_its_id()
    {
        var refusal = Assert.Throws<DriverException>(() => RuleProposals.Answer(_home, "nope1234", accept: true, null, At));
        Assert.Contains("nope1234", refusal.Message);
    }

    /// <summary>An id names a file under the proposals folder, and a path is not an id.</summary>
    [Theory]
    [InlineData("../permissions")]
    [InlineData("..\\permissions")]
    [InlineData("")]
    public void An_id_that_reaches_out_of_the_folder_is_no_proposal(string id)
    {
        File.WriteAllText(Path.Combine(_home, "permissions.json"), """{"id":"x","change":{"action":"add"}}""");

        var refusal = Assert.Throws<DriverException>(() => RuleProposals.Answer(_home, id, accept: true, null, At));

        Assert.Contains("no proposal", refusal.Message);
    }

    // ——— Reading.

    [Fact]
    public void Proposals_read_newest_first_with_an_unreadable_file_left_out()
    {
        Proposal("p0000001", "add", list: "deny", rule: "Bash(rm:*)");
        Proposal("p0000002", "add", list: "allow", rule: "WebFetch", session: null, ask: "a1b2c3");
        File.WriteAllText(Path.Combine(_home, RuleProposals.Folder, "broken.json"), "{ not json");

        var read = RuleProposals.Load(_home);

        Assert.Equal(["p0000002", "p0000001"], read.Select(p => p.Id));
        Assert.Null(read[0].Session);
        Assert.Equal("a1b2c3", read[0].Ask);
        Assert.Equal(RuleList.Allow, read[0].Change.List);
        Assert.Empty(RuleProposals.Load(Path.Combine(_home, "nowhere")));
    }

    [Fact]
    public void A_proposal_says_who_made_it_and_what_it_changes_in_a_line()
    {
        Proposal("p0000001", "default", defaultId: "tree-guard", on: false, session: "i9n8t7k6", ask: "a1b2c3");

        var proposal = Assert.Single(RuleProposals.Load(_home));

        Assert.Equal("switch the default `tree-guard` off", RuleProposals.Describe(proposal.Change));
        Assert.Equal("session i9n8t7k6 (ask #a1b2c3)", RuleProposals.Author(proposal));
        Assert.Equal("a session the driver did not start", RuleProposals.Author(proposal with { Session = null, Ask = null }));
    }
}
