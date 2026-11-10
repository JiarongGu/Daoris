using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Ask Daoris over the bridge (`HelpRoutes.cs`): its start and its proposals. The doors an
/// Apply goes through are `HelpDoorsTests`'.
/// </summary>
public sealed class DriverModuleHelpTests : DriverModuleBridge
{
    /// <summary>
    /// Ask Daoris is off until its agent is named, and says where to name one — asked
    /// before the loop is, since no service answer changes it.
    /// </summary>
    [Fact]
    public async Task Ask_Daoris_with_no_agent_named_says_where_to_name_one()
    {
        var refusal = await RefusalAsync(Module(), "START_HELP");

        Assert.Contains(Refusals.DriverRefused, refusal);
        Assert.Contains("Agents → Roles, Ask Daoris", refusal);
        // The role door names it, the retired `driver helper` never.
        Assert.Contains("daoris role set ask-daoris", refusal);
        Assert.DoesNotContain("driver helper", refusal);
    }

    /// <summary>
    /// The person's Not now settles a proposal of Ask Daoris's, and a settled one takes no second
    /// press. Listing and applying judge against the registry, so they wait for the loop like the review.
    /// </summary>
    [Fact]
    public async Task Not_now_settles_an_Ask_Daoris_proposal_once_and_listing_waits_for_the_loop()
    {
        var folder = HelpProposals.FolderOf(Home);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "p1a2b3c4.json"), """
            { "id": "p1a2b3c4", "proposed": "2026-09-29T10:00:00Z", "by": { "session": "h1" }, "kind": "setting",
              "door": "drive", "target": "engine", "workspace": null, "value": null, "sentence": null,
              "why": "the person asked", "state": "proposed", "note": null }
            """);
        var module = Module();

        var dismissed = await AnswerAsync(module, "HELP_DISMISS", new { id = "p1a2b3c4" });

        Assert.Contains("did not apply `#p1a2b3c4`", dismissed.GetProperty("message").GetString());
        Assert.Equal("dismissed", HelpProposals.Find(Home, "p1a2b3c4")!.State);
        Assert.Contains("already dismissed", await RefusalAsync(module, "HELP_DISMISS", new { id = "p1a2b3c4" }));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(module, "HELP_PROPOSALS", new { session = "h1" }));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(module, "HELP_APPLY", new { id = "p1a2b3c4" }));
    }

    /// <summary>A Not now pressed in another window while the card's Apply holds its claim is refused, and the card stays proposed.</summary>
    [Fact]
    public async Task Not_now_is_refused_while_an_Apply_of_the_same_card_holds_its_claim()
    {
        var folder = HelpProposals.FolderOf(Home);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "p1a2b3c4.json"), """
            { "id": "p1a2b3c4", "proposed": "2026-09-29T10:00:00Z", "by": { "session": "h1" }, "kind": "setting",
              "door": "drive", "target": "engine", "workspace": null, "value": null, "sentence": null,
              "why": "the person asked", "state": "proposed", "note": null }
            """);
        var module = Module();

        using (HelpProposals.Claim(Home, "p1a2b3c4"))
        {
            Assert.Contains("being applied already", await RefusalAsync(module, "HELP_DISMISS", new { id = "p1a2b3c4" }));
            Assert.Equal("proposed", HelpProposals.Find(Home, "p1a2b3c4")!.State);
        }

        await AnswerAsync(module, "HELP_DISMISS", new { id = "p1a2b3c4" });
        Assert.Equal("dismissed", HelpProposals.Find(Home, "p1a2b3c4")!.State);
    }

    /// <summary>
    /// A go card's dismiss is a view act. Its card is settled, so no window draws it again, and the answer says the
    /// conversation is told nothing: a go changes nothing, so its dismiss is no word on the quest it names.
    /// </summary>
    [Fact]
    public async Task Dismissing_a_go_card_settles_it_and_tells_the_conversation_nothing()
    {
        var folder = HelpProposals.FolderOf(Home);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "g1a2b3c4.json"), """
            { "id": "g1a2b3c4", "proposed": "2026-09-29T10:00:00Z", "by": { "session": "h1" }, "kind": "go",
              "door": "go", "target": "quests", "item": "q2taken0", "why": "the person asked where their quest went",
              "state": "proposed", "note": null }
            """);

        var dismissed = await AnswerAsync(Module(), "HELP_DISMISS", new { id = "g1a2b3c4" });

        var message = dismissed.GetProperty("message").GetString();
        Assert.DoesNotContain("did not apply", message);
        Assert.Contains("nothing was said to Ask Daoris", message);
        Assert.Equal("dismissed", HelpProposals.Find(Home, "g1a2b3c4")!.State);
    }

    /// <summary>
    /// A looked-at sync card carries, beside its rows, what they do not say, in the shape the page's
    /// <c>HelpSyncShown</c> reads: each repository not fetched with git's reason, when it last heard from origin and how
    /// origin is reached, and the repositories the look left apart. Before a look, both are empty.
    /// </summary>
    [Fact]
    public void A_sync_card_carries_what_was_not_fetched_and_the_repositories_left_apart_in_the_pages_shape()
    {
        var camel = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var then = new DateTimeOffset(2026, 9, 29, 8, 0, 0, TimeSpan.Zero);
        var looked = new HelpSyncPlan(true, [new HelpSyncRow("engine:main", "line", true, "engine  main  fast-forwards 1 commit(s)")])
        {
            Besides = new HelpSyncBesides([new HelpSyncUnfetched("engine", "fatal: Could not read from remote repository.", then, "ssh", "git.example.com:2222")], ["docs"]),
        };

        var shown = JsonSerializer.SerializeToElement(HelpRoutes.SyncShown(looked), camel);

        Assert.True(shown.GetProperty("looked").GetBoolean());
        Assert.Equal("engine:main", shown.GetProperty("rows")[0].GetProperty("key").GetString());
        var line = Assert.Single(shown.GetProperty("notFetched").EnumerateArray());
        Assert.Equal(
            ("engine", "fatal: Could not read from remote repository.", then, "ssh", "git.example.com:2222"),
            (line.GetProperty("repository").GetString(), line.GetProperty("fetch").GetString(), line.GetProperty("lastFetch").GetDateTimeOffset(),
                line.GetProperty("reach").GetString(), line.GetProperty("host").GetString()));
        Assert.Equal(["docs"], shown.GetProperty("apart").EnumerateArray().Select(name => name.GetString()));

        var before = JsonSerializer.SerializeToElement(HelpRoutes.SyncShown(new HelpSyncPlan(false, [])), camel);
        Assert.Empty(before.GetProperty("notFetched").EnumerateArray());
        Assert.Empty(before.GetProperty("apart").EnumerateArray());
        Assert.Null(HelpRoutes.SyncShown(null));
    }

    /// <summary>
    /// An Apply names an agent action for the Agents screen to follow only where one started,
    /// an update's or a pin's. A default is a file edit that starts nothing, so an applied one names none, and nor does
    /// an update the route refused.
    /// </summary>
    [Theory]
    [InlineData("update", true, "update")]
    [InlineData("pin", true, "pin")]
    [InlineData("default", true, null)]
    [InlineData("update", false, null)]
    public void An_applied_agent_proposal_names_an_action_to_follow_only_where_one_started(string door, bool applied, string? followed)
    {
        var proposal = new HelpProposal("p1", "agent", door, " claude-code ", null, "work", null, "why", "h1", "proposed");

        var answer = JsonSerializer.SerializeToElement(
            HelpRoutes.ApplyAnswer(proposal, new HelpApplied(applied, "told")),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var action = answer.GetProperty("harnessAction");
        Assert.Equal(followed, action.ValueKind == JsonValueKind.Null ? null : action.GetProperty("action").GetString());
        if (followed is not null) Assert.Equal("claude-code", action.GetProperty("harness").GetString());
        Assert.Equal(("told", applied), (answer.GetProperty("message").GetString(), answer.GetProperty("applied").GetBoolean()));
        Assert.False(answer.GetProperty("stands").GetBoolean());
    }

    /// <summary>
    /// A go's answer names the one quest or ask it opens beside its place, the page's
    /// <c>HelpSettled.go.item</c>, and a go to a place alone answers no item.
    /// </summary>
    [Theory]
    [InlineData("ask:a2none00")]
    [InlineData(null)]
    public void A_gos_answer_names_its_item(string? item)
    {
        var proposal = new HelpProposal("g1", "go", "go", "quests", null, null, null, "why", "h1", "proposed") { Item = item };

        var answer = JsonSerializer.SerializeToElement(
            HelpRoutes.ApplyAnswer(proposal, new HelpApplied(true, "told") { Go = new HelpPlace("quests", null, null) { Item = item } }),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var go = answer.GetProperty("go");
        Assert.Equal("quests", go.GetProperty("view").GetString());
        Assert.Equal(JsonValueKind.Null, go.GetProperty("part").ValueKind);
        Assert.Equal(item, go.GetProperty("item").ValueKind == JsonValueKind.Null ? null : go.GetProperty("item").GetString());
        Assert.Equal(JsonValueKind.Null, go.GetProperty("workspace").ValueKind);
    }

    /// <summary>
    /// A go to Add repository or Import a folder answers the workspace the drawer opens with,
    /// the page's <c>HelpSettled.go.workspace</c>, the driver's judged spelling; one with none answers none, as an item.
    /// </summary>
    [Theory]
    [InlineData("add", "work")]
    [InlineData("import", "Team Alpha")]
    [InlineData("add", null)]
    public void A_gos_answer_names_its_workspace(string part, string? workspace)
    {
        var proposal = new HelpProposal("g2", "go", "go", "projects", workspace, null, null, "why", "h1", "proposed") { Part = part };

        var answer = JsonSerializer.SerializeToElement(
            HelpRoutes.ApplyAnswer(proposal, new HelpApplied(true, "told") { Go = new HelpPlace("projects", null, part) { Workspace = workspace } }),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var go = answer.GetProperty("go");
        Assert.Equal(("projects", part), (go.GetProperty("view").GetString(), go.GetProperty("part").GetString()));
        Assert.Equal(workspace, go.GetProperty("workspace").ValueKind == JsonValueKind.Null ? null : go.GetProperty("workspace").GetString());
        Assert.Equal(JsonValueKind.Null, go.GetProperty("item").ValueKind);
    }

    /// <summary>
    /// A go to Sessions' New conversation answers the repository and the words the composer opens with,
    /// the page's <c>HelpSettled.go.repository</c> and <c>sentence</c>; either may be none, and every other go answers both null.
    /// </summary>
    [Theory]
    [InlineData("engine", "fix the build")]
    [InlineData("engine", null)]
    [InlineData(null, "fix the build")]
    [InlineData(null, null)]
    public void A_gos_answer_names_its_repository_and_words(string? repository, string? sentence)
    {
        var proposal = new HelpProposal("g3", "go", "go", "sessions", null, null, sentence, "why", "h1", "proposed") { Part = "new", Repository = repository };

        var answer = JsonSerializer.SerializeToElement(
            HelpRoutes.ApplyAnswer(proposal, new HelpApplied(true, "told") { Go = new HelpPlace("sessions", null, "new") { Repository = repository, Sentence = sentence } }),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var go = answer.GetProperty("go");
        string? Of(string name) => go.GetProperty(name).ValueKind == JsonValueKind.Null ? null : go.GetProperty(name).GetString();
        Assert.Equal(("sessions", "new"), (go.GetProperty("view").GetString(), go.GetProperty("part").GetString()));
        Assert.Equal((repository, sentence), (Of("repository"), Of("sentence")));
        Assert.Null(Of("workspace"));
        Assert.Null(Of("item"));

        // Any other go answers neither.
        var other = JsonSerializer.SerializeToElement(
            HelpRoutes.ApplyAnswer(proposal, new HelpApplied(true, "told") { Go = new HelpPlace("quests", null, null) }),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }).GetProperty("go");
        Assert.Equal(JsonValueKind.Null, other.GetProperty("repository").ValueKind);
        Assert.Equal(JsonValueKind.Null, other.GetProperty("sentence").ValueKind);
    }

    /// <summary>
    /// The quests and asks are read from the service only while a proposal is judged against them, a go naming an item on
    /// Quests; a go to a place alone, or any other kind, asks the service for none. A go naming an item on Sessions reads this
    /// machine's session records instead, and a go naming one on a view that holds none reads neither, since the judge refuses
    /// it unread.
    /// </summary>
    [Theory]
    [InlineData("go", "quests", "q1a2b3c4", true, false)]
    [InlineData("go", "Quests ", "ask:a1", true, false)]
    [InlineData("go", "sessions", "s1a2b3c4", false, true)]
    [InlineData("go", " Sessions", "#s1a2b3c4", false, true)]
    [InlineData("go", "overview", "s1a2b3c4", false, false)]
    [InlineData("go", "quests", " ", false, false)]
    [InlineData("go", "sessions", null, false, false)]
    [InlineData("ask", "sessions", "s1a2b3c4", false, false)]
    // A go's workspace is judged against the registry the snapshot already holds, so it asks for neither.
    [InlineData("go", "projects", null, false, false, "work")]
    public void The_records_are_read_only_for_a_proposal_judged_against_them(
        string kind, string view, string? item, bool records, bool sessions, string? workspace = null)
    {
        var proposal = new HelpProposal("p1", kind, kind, view, workspace, null, null, "why", "h1", "proposed") { Item = item };

        Assert.Equal(records, HelpProposalFacts.JudgedAgainstRecords([proposal]));
        Assert.Equal(sessions, HelpProposalFacts.JudgedAgainstSessions([proposal]));
        Assert.False(HelpProposalFacts.JudgedAgainstRecords([]));
        Assert.False(HelpProposalFacts.JudgedAgainstSessions([]));
    }

    /// <summary>
    /// A sync card's look leaves the card standing for its press, and the answer says so, so the page writes no
    /// <c>proposal.settled</c> for it; a look that found nothing to do settled the card, and says that.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void An_answer_says_whether_the_card_still_stands(bool stands)
    {
        var proposal = new HelpProposal("p11", "sync", "sync", null, null, null, null, "why", "h1", "proposed");

        var answer = JsonSerializer.SerializeToElement(
            HelpRoutes.ApplyAnswer(proposal, new HelpApplied(false, "looked") { Stands = stands }),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.Equal(stands, answer.GetProperty("stands").GetBoolean());
    }

    [Fact]
    public async Task Ask_Daoris_asked_for_before_the_loop_is_up_says_so()
    {
        File.WriteAllText(DriverConfigPath, """{ "actors": { "ask-daoris": [{ "agent": "claude-code-acp" }] } }""");
        var module = Module();

        var refusal = await RefusalAsync(module, "START_HELP");

        Assert.Contains(Refusals.DriverNotReady, refusal);
    }
}
