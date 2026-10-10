using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Ask Daoris over the bridge (`DriverModule.Help.cs`, MOD5): its start and its proposals. The doors an
/// Apply goes through are `HelpDoorsTests`'.
/// </summary>
public sealed class DriverModuleHelpTests : DriverModuleBridge
{
    /// <summary>
    /// Ask Daoris (HELP1a, D89) is off until its agent is named, and says where to name one — asked
    /// before the loop is, since no service answer changes it.
    /// </summary>
    [Fact]
    public async Task Ask_Daoris_with_no_agent_named_says_where_to_name_one()
    {
        var refusal = await RefusalAsync(Module(), "START_HELP");

        Assert.Contains(Refusals.DriverRefused, refusal);
        Assert.Contains("Settings → AI features", refusal);
        Assert.Contains("daoris driver helper <agent>", refusal);
    }

    /// <summary>
    /// HELP1c: the person's Not now settles a proposal of Ask Daoris's, and a settled one takes no second
    /// press. Listing and applying judge against the registry, so they wait for the loop like the review.
    /// </summary>
    [Fact]
    public async Task Not_now_settles_an_Ask_Daoris_proposal_once_and_listing_waits_for_the_loop()
    {
        var folder = HelpProposals.FolderOf(Path.GetDirectoryName(DriverConfigPath)!);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "p1a2b3c4.json"), """
            { "id": "p1a2b3c4", "proposed": "2026-09-29T10:00:00Z", "by": { "session": "h1" }, "kind": "setting",
              "door": "drive", "target": "engine", "workspace": null, "value": null, "sentence": null,
              "why": "the person asked", "state": "proposed", "note": null }
            """);
        var module = Module();

        var dismissed = await AnswerAsync(module, "HELP_DISMISS", new { id = "p1a2b3c4" });

        Assert.Contains("did not apply `#p1a2b3c4`", dismissed.GetProperty("message").GetString());
        Assert.Equal("dismissed", HelpProposals.Find(Path.GetDirectoryName(DriverConfigPath)!, "p1a2b3c4")!.State);
        Assert.Contains("already dismissed", await RefusalAsync(module, "HELP_DISMISS", new { id = "p1a2b3c4" }));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(module, "HELP_PROPOSALS", new { session = "h1" }));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(module, "HELP_APPLY", new { id = "p1a2b3c4" }));
    }

    /// <summary>
    /// LEFT3 b: a looked-at sync card carries, beside its rows, what they do not say, in the shape the page's
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
            Besides = new HelpSyncBesides([new HelpSyncUnfetched("engine", "fatal: Could not read from remote repository.", then, "ssh")], ["docs"]),
        };

        var shown = JsonSerializer.SerializeToElement(DriverModule.SyncShown(looked), camel);

        Assert.True(shown.GetProperty("looked").GetBoolean());
        Assert.Equal("engine:main", shown.GetProperty("rows")[0].GetProperty("key").GetString());
        var line = Assert.Single(shown.GetProperty("notFetched").EnumerateArray());
        Assert.Equal(
            ("engine", "fatal: Could not read from remote repository.", then, "ssh"),
            (line.GetProperty("repository").GetString(), line.GetProperty("fetch").GetString(), line.GetProperty("lastFetch").GetDateTimeOffset(),
                line.GetProperty("reach").GetString()));
        Assert.Equal(["docs"], shown.GetProperty("apart").EnumerateArray().Select(name => name.GetString()));

        var before = JsonSerializer.SerializeToElement(DriverModule.SyncShown(new HelpSyncPlan(false, [])), camel);
        Assert.Empty(before.GetProperty("notFetched").EnumerateArray());
        Assert.Empty(before.GetProperty("apart").EnumerateArray());
        Assert.Null(DriverModule.SyncShown(null));
    }

    /// <summary>
    /// DRIFT1d2: an accept card carries, in the shape the page's <c>HelpAcceptShown</c> reads, the quest and each departure
    /// its done answered: the requirement's number, the person's words it quotes and its check, the done's reason and the
    /// person's words the reason relied on. Every other kind carries none.
    /// </summary>
    [Fact]
    public void An_accept_card_carries_each_departure_in_the_pages_shape()
    {
        var camel = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var plan = new HelpAcceptPlan("q3done00", "Stream the tiles", [new HelpDeparture(2, "keep it under", "it is under", "it cannot", "keep it under")]);

        var shown = JsonSerializer.SerializeToElement(DriverModule.AcceptShown(plan), camel);

        Assert.Equal(("q3done00", "Stream the tiles"), (shown.GetProperty("quest").GetString(), shown.GetProperty("title").GetString()));
        var departure = Assert.Single(shown.GetProperty("departures").EnumerateArray());
        Assert.Equal(
            (2, "keep it under", "it is under", "it cannot", "keep it under"),
            (departure.GetProperty("requirement").GetInt32(), departure.GetProperty("quote").GetString(), departure.GetProperty("check").GetString(),
                departure.GetProperty("departed").GetString(), departure.GetProperty("words").GetString()));
        Assert.Null(DriverModule.AcceptShown(null));
    }

    /// <summary>
    /// HELP10's guard (LEFT3 d): an Apply names an agent action for the Agents screen to follow only where one started,
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
            DriverModule.ApplyAnswer(proposal, new HelpApplied(applied, "told")),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var action = answer.GetProperty("harnessAction");
        Assert.Equal(followed, action.ValueKind == JsonValueKind.Null ? null : action.GetProperty("action").GetString());
        if (followed is not null) Assert.Equal("claude-code", action.GetProperty("harness").GetString());
        Assert.Equal(("told", applied), (answer.GetProperty("message").GetString(), answer.GetProperty("applied").GetBoolean()));
        Assert.False(answer.GetProperty("stands").GetBoolean());
    }

    /// <summary>
    /// ENTRY1f1 (D161's ENTRY1f note): a go's answer names the one quest or ask it opens beside its place, the page's
    /// <c>HelpSettled.go.item</c>, and a go to a place alone answers no item.
    /// </summary>
    [Theory]
    [InlineData("ask:a2none00")]
    [InlineData(null)]
    public void A_gos_answer_names_its_item(string? item)
    {
        var proposal = new HelpProposal("g1", "go", "go", "quests", null, null, null, "why", "h1", "proposed") { Item = item };

        var answer = JsonSerializer.SerializeToElement(
            DriverModule.ApplyAnswer(proposal, new HelpApplied(true, "told") { Go = new HelpPlace("quests", null, null) { Item = item } }),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var go = answer.GetProperty("go");
        Assert.Equal("quests", go.GetProperty("view").GetString());
        Assert.Equal(JsonValueKind.Null, go.GetProperty("part").ValueKind);
        Assert.Equal(item, go.GetProperty("item").ValueKind == JsonValueKind.Null ? null : go.GetProperty("item").GetString());
        Assert.Equal(JsonValueKind.Null, go.GetProperty("workspace").ValueKind);
    }

    /// <summary>
    /// ENTRY1d2a (D161's ENTRY1d note): a go to Add repository or Import a folder answers the workspace the drawer opens with,
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
            DriverModule.ApplyAnswer(proposal, new HelpApplied(true, "told") { Go = new HelpPlace("projects", null, part) { Workspace = workspace } }),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var go = answer.GetProperty("go");
        Assert.Equal(("projects", part), (go.GetProperty("view").GetString(), go.GetProperty("part").GetString()));
        Assert.Equal(workspace, go.GetProperty("workspace").ValueKind == JsonValueKind.Null ? null : go.GetProperty("workspace").GetString());
        Assert.Equal(JsonValueKind.Null, go.GetProperty("item").ValueKind);
    }

    /// <summary>
    /// ENTRY1f1: the quests and asks are read from the service only while a proposal is judged against them, a delete as
    /// before or a go naming an item on Quests; a go to a place alone, or any other kind, asks the service for none.
    /// ENTRY1f2: a go naming an item on Sessions reads this machine's session records instead, and a go naming one on a view
    /// that holds none reads neither, since the judge refuses it unread.
    /// </summary>
    [Theory]
    [InlineData("delete", "quests", null, true, false)]
    [InlineData("go", "quests", "q1a2b3c4", true, false)]
    [InlineData("go", "Quests ", "ask:a1", true, false)]
    [InlineData("go", "sessions", "s1a2b3c4", false, true)]
    [InlineData("go", " Sessions", "#s1a2b3c4", false, true)]
    [InlineData("go", "overview", "s1a2b3c4", false, false)]
    [InlineData("go", "quests", " ", false, false)]
    [InlineData("go", "sessions", null, false, false)]
    [InlineData("ask", "sessions", "s1a2b3c4", false, false)]
    // ENTRY1d2a: a go's workspace is judged against the registry the snapshot already holds, so it asks for neither.
    [InlineData("go", "projects", null, false, false, "work")]
    public void The_records_are_read_only_for_a_proposal_judged_against_them(
        string kind, string view, string? item, bool records, bool sessions, string? workspace = null)
    {
        var proposal = new HelpProposal("p1", kind, kind, view, workspace, null, null, "why", "h1", "proposed") { Item = item };

        Assert.Equal(records, DriverModule.JudgedAgainstRecords([proposal]));
        Assert.Equal(sessions, DriverModule.JudgedAgainstSessions([proposal]));
        Assert.False(DriverModule.JudgedAgainstRecords([]));
        Assert.False(DriverModule.JudgedAgainstSessions([]));
    }

    /// <summary>
    /// LEFT3 c: a sync card's look leaves the card standing for its press, and the answer says so, so the page writes no
    /// <c>proposal.settled</c> for it; a look that found nothing to do settled the card, and says that.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void An_answer_says_whether_the_card_still_stands(bool stands)
    {
        var proposal = new HelpProposal("p11", "sync", "sync", null, null, null, null, "why", "h1", "proposed");

        var answer = JsonSerializer.SerializeToElement(
            DriverModule.ApplyAnswer(proposal, new HelpApplied(false, "looked") { Stands = stands }),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.Equal(stands, answer.GetProperty("stands").GetBoolean());
    }

    [Fact]
    public async Task Ask_Daoris_asked_for_before_the_loop_is_up_says_so()
    {
        var module = Module();
        await AnswerAsync(module, "SET_HELPER", new { adapter = "claude-code-acp" });

        var refusal = await RefusalAsync(module, "START_HELP");

        Assert.Contains(Refusals.DriverNotReady, refusal);
    }
}
