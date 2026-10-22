using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// ASKQUESTS1 (the Ask Daoris design §9 item 12): what an Ask Daoris conversation's Applies published, kept with it by id. The ids are
/// the service's answer, never read from a sentence; the list survives a restart, since it is a file beside the conversation's other
/// files, and goes with them at a delete. Files only, so this is the suite's fast half.
/// </summary>
public sealed class HelpPublishedTests : HelpProposalsFixture
{
    [Fact]
    public async Task An_asks_apply_keeps_the_ask_and_the_quest_the_service_answered_with_its_conversation()
    {
        var doors = new HelpStandInDoors { Asked = new AskAnswer(true, "Asked as `#a1b2c3d4` in `work`.", "a1b2c3d4", "q1a2b3c4") };

        var (applied, _, _) = await ApplyAsync(
            new HelpProposal("p7", "ask", "ask", null, "work", null, "start it", "why", "h1", "proposed"), doors);

        Assert.True(applied.Applied);
        var kept = Assert.Single(new HelpPublished(_home).Read("h1"));
        Assert.Equal(("p7", "ask", "work", "a1b2c3d4", "q1a2b3c4"), (kept.Proposal, kept.Kind, kept.Workspace, kept.Ask, kept.Quest));
    }

    /// <summary>
    /// What the conversation is told names the ask and the quest by id, whatever the service's sentence says: an id it names already is
    /// not said twice. Codex's read of the design (D191): the ids reached the agent only where the sentence happened to name them.
    /// </summary>
    [Theory]
    [InlineData("Asked as `#a1b2c3d4` in `work`.", "Asked as `#a1b2c3d4` in `work`. (quest `#q1a2b3c4`)")]
    [InlineData("Asked as `#a1b2c3d4` in `work`. Published quest `#q1a2b3c4` to `engine`.", "Asked as `#a1b2c3d4` in `work`. Published quest `#q1a2b3c4` to `engine`.")]
    [InlineData("Asked.", "Asked. (ask `#a1b2c3d4`, quest `#q1a2b3c4`)")]
    public async Task An_asks_apply_tells_the_ids_its_sentence_does_not_name(string message, string told)
    {
        var doors = new HelpStandInDoors { Asked = new AskAnswer(true, message, "a1b2c3d4", "q1a2b3c4") };

        var (applied, _, _) = await ApplyAsync(
            new HelpProposal("p7", "ask", "ask", null, "work", null, "start it", "why", "h1", "proposed"), doors);

        Assert.EndsWith($" — {told}", applied.Told);
    }

    /// <summary>One Apply settles a proposal (HELPAPPLY1's claim), so a second entry for it is never kept.</summary>
    [Fact]
    public void A_proposal_kept_already_keeps_its_first_entry()
    {
        var kept = new HelpPublished(_home);
        kept.Keep("h1", new HelpPublishedEntry(At("2026-10-22T09:00:00Z"), "p1", "ask", "work") { Ask = "a1" });
        kept.Keep("h1", new HelpPublishedEntry(At("2026-10-22T10:00:00Z"), "p1", "ask", "work") { Ask = "a9" });

        Assert.Equal(["a1"], kept.Read("h1").Select(entry => entry.Ask));
    }

    /// <summary>The entry is kept before the card settles applied, so an applied card never stands without it.</summary>
    [Fact]
    public async Task The_entry_is_kept_by_the_time_the_card_is_settled()
    {
        var (_, _, _) = await ApplyAsync(new HelpProposal("p7", "ask", "ask", null, "work", null, "start it", "why", "h1", "proposed"),
            new HelpStandInDoors { Asked = new AskAnswer(true, "Asked.", "a1b2c3d4", null) });

        Assert.Equal("applied", HelpProposals.Find(_home, "p7")!.State);
        Assert.Equal("a1b2c3d4", Assert.Single(new HelpPublished(_home).Read("h1")).Ask);
    }

    [Fact]
    public async Task A_setups_apply_keeps_its_ask_and_its_quest()
    {
        var doors = new HelpStandInDoors { SetUp = new SetupOutcome(true, "Published.", "a2b3c4d5", "q2b3c4d5", []) };

        await ApplyAsync(new HelpProposal("p8", "setup", "setup", "atlas", null, null, null, "why", "h1", "proposed"), doors, SetupFacts);

        var kept = Assert.Single(new HelpPublished(_home).Read("h1"));
        Assert.Equal(("p8", "setup", "work", "a2b3c4d5", "q2b3c4d5"), (kept.Proposal, kept.Kind, kept.Workspace, kept.Ask, kept.Quest));
    }

    /// <summary>A refused ask, an answer naming no id, and a change that publishes nothing keep nothing: there is nothing to open.</summary>
    [Fact]
    public async Task An_apply_that_published_nothing_keeps_nothing()
    {
        await ApplyAsync(new HelpProposal("p1", "ask", "ask", null, "work", null, "start it", "why", "h1", "proposed"),
            new HelpStandInDoors { Asked = new AskAnswer(false, "the service is down", null, null) });
        await ApplyAsync(new HelpProposal("p2", "ask", "ask", null, "work", null, "start it", "why", "h1", "proposed"),
            new HelpStandInDoors { Asked = new AskAnswer(true, "Asked.", null, null) });
        await ApplyAsync(Setting("drive", "engine") with { Id = "p3" });

        Assert.Empty(new HelpPublished(_home).Read("h1"));
        Assert.False(System.IO.File.Exists(new HelpPublished(_home).PathOf("h1")));
    }

    /// <summary>Kept on disk: a new reader over the same home, as after a restart, reads every entry, the newest first.</summary>
    [Fact]
    public void The_list_survives_a_restart_and_reads_newest_first()
    {
        var first = new HelpPublished(_home);
        first.Keep("h1", new HelpPublishedEntry(At("2026-10-22T09:00:00Z"), "p1", "ask", "work") { Ask = "a1" });
        first.Keep("h1", new HelpPublishedEntry(At("2026-10-22T10:00:00Z"), "p2", "setup", "work") { Ask = "a2", Quest = "q2" });
        first.Keep("h2", new HelpPublishedEntry(At("2026-10-22T11:00:00Z"), "p3", "ask", "default") { Quest = "q3" });

        var again = new HelpPublished(_home);

        Assert.Equal(["p2", "p1"], again.Read("h1").Select(entry => entry.Proposal));
        Assert.Equal(("a2", "q2"), (again.Read("h1")[0].Ask, again.Read("h1")[0].Quest));
        Assert.Null(again.Read("h1")[1].Quest);
        Assert.Equal(["q3"], again.Read("h2").Select(entry => entry.Quest));
    }

    /// <summary>The file beside the conversation's own: one object, its entries oldest first, an absent id left out, as LF UTF-8.</summary>
    [Fact]
    public void The_file_is_kept_beside_the_conversation_as_the_home_writes_its_files()
    {
        var kept = new HelpPublished(_home);
        kept.Keep("h1", new HelpPublishedEntry(At("2026-10-22T09:00:00Z"), "p1", "ask", "work") { Ask = "a1" });

        Assert.Equal(DaorisHome.Entry(_home, HomeEntry.Sessions, "h1.published.json"), kept.PathOf("h1"));
        var bytes = System.IO.File.ReadAllBytes(kept.PathOf("h1"));
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        using var document = JsonDocument.Parse(bytes);
        var entry = Assert.Single(document.RootElement.GetProperty("published").EnumerateArray());
        Assert.Equal("a1", entry.GetProperty("ask").GetString());
        Assert.False(entry.TryGetProperty("quest", out _));
        Assert.Equal("p1", entry.GetProperty("proposal").GetString());
    }

    /// <summary>A file that does not read, a conversation with none and an id that is no session's are nothing kept; a keep starts it again.</summary>
    [Fact]
    public void A_file_that_does_not_read_is_nothing_kept()
    {
        var kept = new HelpPublished(_home);
        Directory.CreateDirectory(Path.GetDirectoryName(kept.PathOf("h1"))!);
        System.IO.File.WriteAllText(kept.PathOf("h1"), "{ not json");

        Assert.Empty(kept.Read("h1"));
        Assert.Empty(kept.Read("h9"));
        Assert.Empty(kept.Read("../escape"));
        Assert.Throws<DriverException>(() => kept.Keep("../escape", new HelpPublishedEntry(At("2026-10-22T09:00:00Z"), "p1", "ask", "work") { Ask = "a1" }));

        kept.Keep("h1", new HelpPublishedEntry(At("2026-10-22T09:00:00Z"), "p1", "ask", "work") { Ask = "a1" });
        Assert.Single(kept.Read("h1"));
    }

    /// <summary>A delete takes the list with the conversation's other files, under Ask Daoris's own name (<see cref="SessionHomeFiles"/>).</summary>
    [Fact]
    public void A_delete_removes_the_list()
    {
        var kept = new HelpPublished(_home);
        kept.Keep("help0005", new HelpPublishedEntry(At("2026-10-22T09:00:00Z"), "p1", "ask", "work") { Ask = "a1" });
        Assert.True(System.IO.File.Exists(kept.PathOf("help0005")));

        var removed = new SessionHomeFiles(_home).Remove(["help0005"], events: null);

        Assert.False(System.IO.File.Exists(kept.PathOf("help0005")));
        Assert.Contains(SessionHomeFiles.Help, removed.Went["help0005"]);
    }

    private static DateTimeOffset At(string when) => DateTimeOffset.Parse(when, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The machine a set-up is judged against: `atlas` nobody adopted, driven here in a tree of its own, as LAYOUT8's tests press it.</summary>
    private static readonly HelpMachineFacts SetupFacts = Facts with
    {
        Setups =
        [
            new SetupPlan("atlas")
            {
                Workspace = "work",
                Facts = new LayoutFacts { Line = "main", Commit = "0123456789abcdef0123456789abcdef01234567" },
                Tools = new SetupTools("node", "v24.1.0", null, "daoris", "0.9.0", null),
                Case = SetupCase.Whole,
                Title = "Set up this repository for every agent (2026-10-22)",
                Body = "What is asked, and whose it is.",
                Landing = LandingRules.Choose(DriverConfig.Empty, "atlas", "work"),
                Adapter = "claude-code-acp",
            },
        ],
    };
}
