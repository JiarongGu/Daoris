using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>Ask Daoris's <c>delete</c> proposal (HELP6): a quest or an ask made by mistake, by the service's own <c>deletable</c>.</summary>
public sealed class HelpDeleteProposalsTests : HelpProposalsFixture
{
    [Fact]
    public void A_quest_nobody_started_on_is_deleted_and_the_card_says_what_goes()
    {
        var plan = HelpProposals.Plan(Of("delete", "quest", "q1a2b3c4"), DriverConfig.Empty, Machine);

        Assert.Null(plan.Refusal);
        Assert.Equal("daoris-driver quest delete q1a2b3c4", plan.Terminal);
        Assert.Contains("Delete quest `#q1a2b3c4`", plan.Describe);
        Assert.Contains("Cap the chunk budget", plan.Describe);
    }

    /// <summary>
    /// A taken, done or declined quest keeps its record (D95), and so does an open one a session started
    /// on: never shown to the person, and the refusal says what to do instead.
    /// </summary>
    [Theory]
    [InlineData("q2taken0", "is taken", "Decline it")]
    [InlineData("q3done00", "is done", "already leaves the list")]
    [InlineData("q4declin", "is declined", "already leaves the list")]
    [InlineData("q5start0", "was started on", "Decline it")]
    [InlineData("q9none00", "no quest `#q9none00`", "")]
    public void A_quest_anything_stands_on_is_never_proposed_for_deleting(string quest, string says, string instead)
    {
        var plan = HelpProposals.Plan(Of("delete", "quest", quest), DriverConfig.Empty, Machine);

        Assert.Contains(says, plan.Refusal);
        Assert.Contains(instead, plan.Refusal);
    }

    [Fact]
    public void An_ask_goes_with_its_untaken_quests_or_alone_and_one_that_must_stay_is_refused()
    {
        var with = HelpProposals.Plan(Of("delete", "ask", "a1b2c3d4"), DriverConfig.Empty, Machine);
        var alone = HelpProposals.Plan(Of("delete", "ask", "a2none00"), DriverConfig.Empty, Machine);
        var kept = HelpProposals.Plan(Of("delete", "ask", "a3kept00"), DriverConfig.Empty, Machine);

        Assert.Null(with.Refusal);
        Assert.Equal("daoris-driver ask --delete a1b2c3d4", with.Terminal);
        Assert.Contains("with the quest it became: `#q1a2b3c4` “Cap the chunk budget”", with.Describe);
        Assert.Contains("goes alone", alone.Describe);
        Assert.Contains("must stay", kept.Refusal);
        Assert.Contains("close the ask instead", kept.Refusal);
        Assert.Contains("no ask `#a9`", HelpProposals.Plan(Of("delete", "ask", "a9"), DriverConfig.Empty, Machine).Refusal);
    }

    [Fact]
    public async Task A_delete_goes_through_the_hosts_own_route_and_its_refusal_is_said_in_the_services_words()
    {
        var (quest, doors, _) = await ApplyAsync(Of("delete", "quest", "q1a2b3c4"));
        var refusing = new HelpStandInDoors { Deleted = (false, "Quest `#a1b2c3d4` is Taken — someone is working it.") };
        var (ask, asked, _) = await ApplyAsync(Of("delete", "ask", "a1b2c3d4") with { Id = "p9" }, refusing);

        Assert.True(quest.Applied);
        Assert.Equal(["DELETE /api/quests/q1a2b3c4"], doors.Calls);
        Assert.Equal(["DELETE /api/asks/a1b2c3d4"], asked.Calls);
        Assert.False(ask.Applied);
        Assert.Contains("someone is working it", ask.Told);
        Assert.Equal("refused", HelpProposals.Find(_home, "p9")!.State);
    }

    /// <summary>
    /// A delete is judged by the service's own <c>deletable</c>, read with every quest and ask, closed ones
    /// included — the same answer the drawer and the record show their Delete by. A host from before the
    /// field answers without it, which reads as not deletable: nothing is offered that might not go.
    /// </summary>
    [Fact]
    public async Task The_records_a_delete_is_judged_by_are_the_services_own_answer()
    {
        using var client = new ServiceClient("http://stand-in", null, new HttpClient(new Records()));

        var (quests, asks) = await HelpProposals.RecordsAsync(client, CancellationToken.None);

        Assert.Equal(
            [new HelpQuestFacts("q1", "Cap it", "engine", "Open", true), new HelpQuestFacts("q2", "Old", "engine", "Done", false)],
            quests);
        var ask = Assert.Single(asks);
        Assert.Equal(("a1", "cap it", "work", "Closed", true), (ask.Id, ask.Sentence, ask.Workspace, ask.State, ask.Deletable));
        Assert.Equal(["q1"], ask.Quests);
    }

    /// <summary>The local host's two lists, as it answers them with closed records asked for.</summary>
    private sealed class Records : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.RequestUri!.PathAndQuery switch
            {
                "/api/quests?includeClosed=true" => """
                    [ { "id": "q1", "from": "game", "to": "engine", "title": "Cap it", "body": "", "status": "Open", "deletable": true },
                      { "id": "q2", "from": "game", "to": "engine", "title": "Old", "body": "", "status": "Done" } ]
                    """,
                "/api/asks?includeClosed=true" => """
                    [ { "id": "a1", "workspace": "work", "sentence": "cap it", "state": "Closed", "tier": "named", "quests": ["q1"], "deletable": true } ]
                    """,
                _ => null,
            };
            return Task.FromResult(body is null
                ? new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
                : new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") });
        }
    }
}

public sealed partial class HelpStandInDoors
{
    public (bool Ok, string Message) Deleted { get; set; } = (true, "Deleted it.");

    public Task<(bool Ok, string Message)> DeleteQuestAsync(string id, CancellationToken ct)
    {
        Calls.Add($"DELETE /api/quests/{id}");
        return Task.FromResult(Deleted);
    }

    public Task<(bool Ok, string Message)> DeleteAskAsync(string id, CancellationToken ct)
    {
        Calls.Add($"DELETE /api/asks/{id}");
        return Task.FromResult(Deleted);
    }
}
