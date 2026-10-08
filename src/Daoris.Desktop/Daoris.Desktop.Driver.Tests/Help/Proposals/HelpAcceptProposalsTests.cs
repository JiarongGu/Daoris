using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// Ask Daoris's <c>accept</c> proposal (DRIFT1d2, D133 §4): the person's yes to a done's departure from what they required,
/// judged against the quest as the service answers it — held, with the departures its done answered — and applied through
/// the local host's own <c>POST /api/quests/{id}/accept</c>, the door the quest page's yes and the terminal's use.
/// </summary>
public sealed class HelpAcceptProposalsTests : HelpProposalsFixture
{
    /// <summary>A done that met its first requirement and departed from its second, held, with a next step and a quest waiting on it.</summary>
    private static readonly QuestView Held = new("q3done00", "game", "engine", "Stream the tiles from the cold cache", "the body", "Done")
    {
        Requirements =
        [
            new QuestRequirementView("use the v3 bridge", "the tiles arrive through the v3 bridge"),
            new QuestRequirementView("keep the budget under the ceiling", "a frame never hydrates more than the ceiling"),
        ],
        Answers =
        [
            new QuestAnswerView(1, Met: "the tiles go through the v3 bridge, as its trace shows", Departed: null, Quote: null),
            new QuestAnswerView(2, Met: null, Departed: "the ceiling cannot hold on a cold cache, so it is raised for the first frame",
                Quote: "keep the budget under the ceiling"),
        ],
        Then = [new QuestStepView("game", "Report on {parent}", "Say what was done.")],
        Held = true,
    };

    /// <summary>A quest its taker asked this one of, waiting on it (D79): the yes lets it go on.</summary>
    private static readonly QuestView Waiting = new("w1w1w1w1", "engine", "game", "Show the tiles in the menu", "", "Taken")
    {
        Awaits = "q3done00",
    };

    private static readonly HelpMachineFacts Records = Facts with
    {
        QuestRecords =
        [
            Held,
            Waiting,
            Held with { Id = "q4kept00", Held = false, Then = [] },
            Held with { Id = "q5met000", Held = false, Answers = [new QuestAnswerView(1, "met", null, null), new QuestAnswerView(2, "met", null, null)], Then = [] },
            new QuestView("q6open00", "game", "engine", "Cap it", "", "Open"),
            new QuestView("q7decl00", "game", "engine", "Not ours", "", "Declined"),
        ],
    };

    [Fact]
    public void A_held_departure_is_proposed_and_the_card_says_each_departure_and_what_goes_on()
    {
        var plan = HelpProposals.Plan(Of("accept", "accept", "#q3done00"), DriverConfig.Empty, Records);

        Assert.Null(plan.Refusal);
        Assert.Equal("daoris-driver quest accept q3done00", plan.Terminal);
        Assert.Contains("Accept the departure on quest `#q3done00` “Stream the tiles from the cold cache”, for `engine`", plan.Describe);
        Assert.Contains("it departed from requirement 2", plan.Describe);
        // What the yes lets go on: the chain's next step as the service will publish it, and the quest waiting on it.
        Assert.Contains("its next step, “Report on q3done00” to `game`, is published", plan.Describe);
        Assert.Contains("`#w1w1w1w1` “Show the tiles in the menu”, waiting on it, goes on", plan.Describe);
        var accept = plan.Accept!;
        Assert.Equal("q3done00", accept.Quest);
        var departure = Assert.Single(accept.Departures);
        Assert.Equal(
            new HelpDeparture(2, "keep the budget under the ceiling", "a frame never hydrates more than the ceiling",
                "the ceiling cannot hold on a cold cache, so it is raised for the first frame", "keep the budget under the ceiling"),
            departure);
    }

    [Fact]
    public void A_departure_nothing_follows_says_so()
    {
        var alone = Records with { QuestRecords = [Held with { Then = [] }] };

        var plan = HelpProposals.Plan(Of("accept", "accept", "q3done00"), DriverConfig.Empty, alone);

        Assert.Null(plan.Refusal);
        Assert.Contains("nothing follows it", plan.Describe);
    }

    /// <summary>
    /// Only a quest a departure holds takes a yes, said as the service's door says it: never shown to the person, and the
    /// helper hears why in the route's words.
    /// </summary>
    [Theory]
    [InlineData("q9none00", "there is no quest `#q9none00`")]
    [InlineData("q6open00", "quest `#q6open00` is open: only a quest closed done with a departure from what you required waits for your yes")]
    [InlineData("q7decl00", "quest `#q7decl00` is declined")]
    [InlineData("q4kept00", "quest `#q4kept00`'s departure was already accepted: nothing waits for a yes")]
    [InlineData("q5met000", "quest `#q5met000` closed done departing from none of what you required: nothing waits for a yes")]
    public void A_quest_no_departure_holds_is_never_proposed_for_a_yes(string quest, string says)
    {
        var plan = HelpProposals.Plan(Of("accept", "accept", quest), DriverConfig.Empty, Records);

        Assert.Contains(says, plan.Refusal);
        Assert.Null(plan.Accept);
    }

    /// <summary>
    /// A set-up step held for the person's review alone (REVIEWENV1b2; D154 point 9): a yes accepts a departure or its
    /// evidence, never a review, so the accept door refuses it (409), and the card is never shown. The helper hears the door's
    /// own words, which name what does let it go.
    /// </summary>
    [Fact]
    public void A_set_up_step_held_for_its_review_alone_is_refused_in_the_doors_words()
    {
        var unreviewed = new QuestView("q8show00", "ask #a1", "engine", "Show q3done00 in local for review", "Set it up.", "Done")
        {
            Held = true,
            Hold = "unreviewed",
            SetUpIn = "local",
        };

        var plan = HelpProposals.Plan(Of("accept", "accept", "q8show00"), DriverConfig.Empty, Records with { QuestRecords = [unreviewed] });

        Assert.Equal(
            "quest `#q8show00` waits for your review in `local`, which a yes does not give: say `reviewed` once you have looked "
            + "at what it shows, or skip the review for this work.",
            plan.Refusal);
        Assert.Null(plan.Accept);
        Assert.Equal("daoris-driver quest accept q8show00", plan.Terminal);
    }

    [Fact]
    public void A_door_the_kind_does_not_take_is_refused()
    {
        var plan = HelpProposals.Plan(Of("accept", "publish", "q3done00"), DriverConfig.Empty, Records);

        Assert.Contains("`publish` is not a door of an accept", plan.Refusal);
    }

    [Fact]
    public async Task A_yes_goes_through_the_hosts_own_accept_door_and_its_refusal_is_said_in_the_services_words()
    {
        var (applied, doors, _) = await ApplyAsync(Of("accept", "accept", "q3done00"), facts: Records);
        var refusing = new HelpStandInDoors { Accepted = (false, "Quest `#q3done00`'s departure was already accepted: nothing waits for a yes.") };
        var (refused, _, _) = await ApplyAsync(Of("accept", "accept", "q3done00") with { Id = "p9" }, refusing, Records);

        Assert.True(applied.Applied);
        Assert.Equal(["POST /api/quests/q3done00/accept"], doors.Calls);
        Assert.Contains("Applied: `#p6` (`daoris-driver quest accept q3done00`) — Accepted the departure", applied.Told);
        Assert.Equal("applied", HelpProposals.Find(_home, "p6")!.State);
        Assert.False(refused.Applied);
        Assert.Contains("already accepted", refused.Told);
        Assert.Equal("refused", HelpProposals.Find(_home, "p9")!.State);
    }

    /// <summary>
    /// An accept is judged by the quests as the service answers them, closed ones included: what the person required, how
    /// each done answered and whether a departure holds it — a host from before answers reads as none, never a guess.
    /// </summary>
    [Fact]
    public async Task The_quests_an_accept_is_judged_by_carry_their_answers_as_the_service_answers_them()
    {
        using var client = new ServiceClient("http://stand-in", null, new HttpClient(new Quests()));

        var quests = await client.EveryQuestAsync(CancellationToken.None);

        var held = quests.Single(quest => quest.Id == "q1");
        Assert.True(held.Held);
        Assert.Equal(
            [new QuestAnswerView(1, "it does", null, null), new QuestAnswerView(2, null, "it cannot", "keep it under")],
            held.Answers);
        Assert.Empty(quests.Single(quest => quest.Id == "q2").Answers);
    }

    /// <summary>The local host's quest list with closed ones asked for, one answered and held, one from before answers.</summary>
    private sealed class Quests : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.RequestUri!.PathAndQuery == "/api/quests?includeClosed=true"
                ? """
                    [ { "id": "q1", "from": "game", "to": "engine", "title": "Cap it", "body": "", "status": "Done", "held": true,
                        "requirements": [ { "quote": "do it", "check": "it is done" }, { "quote": "keep it under", "check": "it is under" } ],
                        "answers": [ { "requirement": 1, "met": "it does" }, { "requirement": 2, "departed": "it cannot", "quote": "keep it under" } ] },
                      { "id": "q2", "from": "game", "to": "engine", "title": "Old", "body": "", "status": "Done" } ]
                    """
                : null;
            return Task.FromResult(body is null
                ? new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
                : new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") });
        }
    }
}

public sealed partial class HelpStandInDoors
{
    public (bool Ok, string Message) Accepted { get; set; } =
        (true, "Accepted the departure on quest `#q3done00`: what it held goes on.");

    public Task<(bool Ok, string Message)> AcceptDepartureAsync(string id, CancellationToken ct)
    {
        Calls.Add($"POST /api/quests/{id}/accept");
        return Task.FromResult(Accepted);
    }
}
