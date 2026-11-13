using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A scout's start (TEAMWORK1d): the lead's ask for a part to be oriented is a helper's ask that names the part, so it is read from the file as
/// the service writes it, judged without the lead's turn or its work, walked over the Scout role's entries, and handed an instruction that
/// says the part; its answer is a pack kept with the plan, so no word is handed to an intake lead.
/// </summary>
public sealed class ScoutStartTests : IDisposable
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-14T10:00:00Z");

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-scout-start-" + Guid.NewGuid().ToString("N")[..8]);

    public ScoutStartTests() => Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Helpers));

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static HelperAsk Scouting() =>
        new("a1b2c3d4", "lead-1", "Orient the page part.", null, HelperAsk.ByAgent, At) { Scout = new HelperScoutPart("ask-9", "page", "media-web") };

    private static PriorSession Intake() => new("lead-1", null, "working", Repository: "") { Ask = "ask-9", Workspace = "default" };

    [Fact]
    public void A_scouts_ask_is_read_as_the_service_writes_it_and_a_plain_ask_names_no_part()
    {
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Helpers, "s1.ask.json"),
            "{\n  \"id\": \"s1\",\n  \"asker\": \"lead-1\",\n  \"question\": \"Orient the page part.\",\n  \"agent\": null,\n  \"askedBy\": \"agent\",\n"
            + "  \"scout\": { \"ask\": \"ask-9\", \"part\": \"page\", \"repository\": \"media-web\" },\n  \"at\": \"2026-11-14T10:00:00.0000000+00:00\"\n}\n");
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Helpers, "s2.ask.json"),
            "{ \"id\": \"s2\", \"asker\": \"w1\", \"question\": \"Why?\", \"agent\": null, \"askedBy\": \"agent\", \"scout\": { \"ask\": \"ask-9\" }, \"at\": \"2026-11-14T10:01:00.0000000+00:00\" }");

        var asks = new HelperFiles(_home).Asks();

        Assert.Equal(new HelperScoutPart("ask-9", "page", "media-web"), asks[0].Scout);
        Assert.Null(asks[1].Scout);
    }

    [Fact]
    public void An_intake_lead_may_ask_a_scout_and_no_other_helper_and_its_turn_does_not_hold_the_scout()
    {
        var rule = new HelperRule(HelperRules.On, []);

        Assert.Equal(HelperVerdict.Go, HelperJudge.Judge(Scouting(), null, Intake(), rule, 0, slot: true, between: false));
        Assert.Equal(
            HelperVerdict.Refuse(HelperWhy.AskerUnfit),
            HelperJudge.Judge(Scouting() with { Scout = null }, null, Intake(), rule, 0, slot: true));
        Assert.Equal(
            HelperVerdict.Refuse(HelperWhy.AskerUnfit),
            HelperJudge.Judge(Scouting(), null, Intake() with { Serves = "s9" }, rule, 0, slot: true));
    }

    [Fact]
    public void A_scouts_walk_is_over_the_scout_roles_entries_and_a_helpers_over_the_helpers()
    {
        var config = DriverConfig.Empty with
        {
            Actors = new Dictionary<string, IReadOnlyList<ActorChoice>>
            {
                ["helper"] = [new ActorChoice("claude", "haiku")], ["scout"] = [new ActorChoice("claude", "sonnet")],
            },
        };
        var adapters = TestDoors.Registry(new Dictionary<string, Provider>());

        Assert.Equal([new ActorEffective("claude", "sonnet", null, "1")], HelperChoice.Order(Scouting(), HelperRule.Default, adapters, null, config));
        Assert.Equal([new ActorEffective("claude", "haiku", null, "1")], HelperChoice.Order(Scouting() with { Scout = null }, HelperRule.Default, adapters, null, config));
    }

    [Fact]
    public void A_scouts_instruction_names_its_part_the_repository_and_the_commit_and_where_the_pack_goes()
    {
        var packet = new HelperPacket("lead-1", "media-web", "abc1234", "Orient the page part, mind the settings page.", 15) { Scout = new HelperScoutPart("ask-9", "page", "media-web") };

        var text = HelperInstruction.Compose(packet);

        Assert.Equal(ScoutInstruction.Compose(packet), text);
        Assert.Contains("one part of its plan: `page`", text);
        Assert.Contains("`media-web`", text);
        Assert.Contains("`abc1234`", text);
        Assert.Contains("> Orient the page part, mind the settings page.", text);
        Assert.Contains("`helper_answer`", text);
        Assert.Contains("kept with the plan", text);
        Assert.DoesNotContain("scout", HelperInstruction.Compose(packet with { Scout = null }));
    }
}
