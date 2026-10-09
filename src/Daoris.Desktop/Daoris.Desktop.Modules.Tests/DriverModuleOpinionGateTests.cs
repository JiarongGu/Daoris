using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The second opinion's gate over the bridge (XAGENT1f, D155 points 9 and 10; the second-agent design §8.1, §8.5): beside a
/// landing's plan and press, <c>LANDING</c> and <c>LAND_SESSION_TREE</c> answer what the opinion's part of the gate says while it
/// holds the work, for the page to draw where *Accept…* would be (XAGENT1g), with the token a press sends back to answer what it
/// showed; and the presses' bridge halves (<c>OPINION_GATE</c>, <c>ASK_OPINION</c>, <c>OPINION_ANYWAY</c>, <c>OPINION_MYSELF</c>,
/// <c>STOP_OPINION</c>), each the driver's own press. The presses themselves are held by the driver's <c>OpinionLookTests</c>.
/// </summary>
public sealed class DriverModuleOpinionGateTests : DriverModuleBridge
{
    private static readonly JsonSerializerOptions Camel = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private const string Read = "1111111111111111111111111111111111111111";

    [Fact]
    public void A_disputed_opinion_answers_its_state_reader_counts_sentence_and_the_token_a_press_sends_back()
    {
        var opinion = new OpinionView("o1", "landing", "first", "s1", "r-o1", "web-app", "0000000000000000000000000000000000000000", Read,
            "codex-acp", ReviewerLabels.AnotherMaker, "given")
        {
            Product = "Codex", Maker = "OpenAI", Findings = [new OpinionFindingView(1, "must", "src/a.ts:1", "claim", "", "", "sure")],
        };
        var gate = new OpinionGateState(OpinionGateStates.Disputed, "web-app")
        {
            Session = "s1", Tip = Read, Opinion = opinion, Disputes = new([1], []), Since = 0,
            Rule = new ResolvedOpinion(new OpinionRule(["codex-acp"], [OpinionRules.Landing], Required: true), OpinionSource.Repository),
        };

        var answer = JsonSerializer.SerializeToElement(DriverModule.Opinion(gate), Camel);

        Assert.Equal("disputed", answer.GetProperty("state").GetString());
        Assert.True(answer.GetProperty("holds").GetBoolean());
        Assert.True(answer.GetProperty("required").GetBoolean());
        Assert.Equal(("o1", "codex-acp", "Codex", "OpenAI", ReviewerLabels.AnotherMaker, "r-o1"),
            (answer.GetProperty("opinion").GetString(), answer.GetProperty("reviewer").GetString(), answer.GetProperty("product").GetString(),
             answer.GetProperty("maker").GetString(), answer.GetProperty("label").GetString(), answer.GetProperty("reviewing").GetString()));
        Assert.Equal((1, 1, 0), (answer.GetProperty("findings").GetInt32(), answer.GetProperty("disputes").GetInt32(), answer.GetProperty("since").GetInt32()));
        Assert.Equal(gate.Token, answer.GetProperty("answers").GetString());
        Assert.Equal(gate.Says, answer.GetProperty("says").GetString());
    }

    [Fact]
    public void A_level_that_asks_no_opinion_answers_nothing_and_a_state_no_press_answers_carries_no_token()
    {
        Assert.Null(DriverModule.Opinion(new OpinionGateState(OpinionGateStates.None, "web-app")));

        var reading = JsonSerializer.SerializeToElement(DriverModule.Opinion(new OpinionGateState(OpinionGateStates.NotAsked, "web-app") { Session = "s1" }), Camel);
        Assert.Equal(("not-asked", true), (reading.GetProperty("state").GetString(), reading.GetProperty("holds").GetBoolean()));
        Assert.True(!reading.TryGetProperty("answers", out var token) || token.ValueKind == JsonValueKind.Null);
    }

    [Theory]
    [InlineData("OPINION_GATE")]
    [InlineData("ASK_OPINION")]
    [InlineData("OPINION_ANYWAY")]
    [InlineData("OPINION_MYSELF")]
    public async Task Each_press_on_a_sessions_opinion_waits_for_the_driver_s_service(string route)
    {
        var refused = await RefusalAsync(Module(), route, new { id = "s1", words = "fine" });

        Assert.StartsWith(Refusals.DriverNotReady, refused, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stopping_a_reviewer_waits_for_the_driver_s_service()
    {
        var refused = await RefusalAsync(Module(), "STOP_OPINION", new { opinion = "o1" });

        Assert.StartsWith(Refusals.DriverNotReady, refused, StringComparison.Ordinal);
    }

    /// <summary>
    /// XAGENT1g (the second-agent design §9): the review's *Second opinion* draws each finding beside the working session's answer,
    /// how the driver read it, the recheck's word and whether it is disputed, from the gate the driver judged.
    /// </summary>
    [Fact]
    public void The_detail_carries_each_finding_beside_its_answer_its_reading_and_whether_it_is_disputed()
    {
        var first = new OpinionView("o1", "landing", "first", "s1", "r-o1", "web-app", "0000000000000000000000000000000000000000", Read,
            "codex-acp", ReviewerLabels.AnotherMaker, "given")
        {
            Product = "Codex", Maker = "OpenAI", Read = "src/ and its tests", Limits = "did not run the build",
            Findings =
            [
                new OpinionFindingView(1, "must", "src/a.ts:12", "drops the last row", "a page loses data", "npm test", "sure") { Proposal = "use <=" },
                new OpinionFindingView(2, "should", "general", "no test names it", "", "", "likely"),
            ],
            Answers = [new OpinionAnswerView(1, "rejected") { Evidence = "the loop is inclusive already" }, new OpinionAnswerView(2, "fixed") { Commit = "abc1234" }],
        };
        var recheck = first with { Id = "o2", Pass = "recheck", Rechecked = new Dictionary<int, string> { [1] = "stands" }, Findings = [], Answers = [] };
        var gate = new OpinionGateState(OpinionGateStates.Disputed, "web-app")
        {
            Session = "s1", Tip = Read, Opinion = first, Recheck = recheck, Disputes = new([1], []), Since = 0, Passes = 1,
            Answers = new OpinionReading("s1", Read, DateTimeOffset.UnixEpoch,
            [
                new OpinionAnswerRead(1, "must", "rejected") { Said = "rejected" },
                new OpinionAnswerRead(2, "should", "fixed") { Said = "fixed", Commit = "abc1234", Fix = "abc1234def" },
            ]),
        };

        var detail = JsonSerializer.SerializeToElement(DriverModule.OpinionDetail(gate), Camel);

        Assert.Equal(("agent", true, 1), (detail.GetProperty("tier").GetString(), detail.GetProperty("covers").GetBoolean(), detail.GetProperty("passes").GetInt32()));
        var pass = detail.GetProperty("first");
        Assert.Equal(("o1", "Codex", "OpenAI", "r-o1", "src/ and its tests", "did not run the build"),
            (pass.GetProperty("id").GetString(), pass.GetProperty("product").GetString(), pass.GetProperty("maker").GetString(),
             pass.GetProperty("reviewing").GetString(), pass.GetProperty("read").GetString(), pass.GetProperty("limits").GetString()));
        var findings = pass.GetProperty("findings").EnumerateArray().ToList();
        Assert.Equal(2, findings.Count);

        var must = findings[0];
        Assert.Equal(("must", "src/a.ts:12", "drops the last row", "use <="),
            (must.GetProperty("weight").GetString(), must.GetProperty("where").GetString(), must.GetProperty("claim").GetString(), must.GetProperty("proposal").GetString()));
        var beside = must.GetProperty("beside");
        Assert.Equal(("rejected", "the loop is inclusive already"), (beside.GetProperty("answer").GetProperty("said").GetString(), beside.GetProperty("answer").GetProperty("evidence").GetString()));
        Assert.Equal("rejected", beside.GetProperty("counts").GetProperty("counts").GetString());
        Assert.Equal("stands", beside.GetProperty("rechecked").GetString());
        Assert.True(beside.GetProperty("disputed").GetBoolean());

        var fixedOne = findings[1].GetProperty("beside");
        Assert.Equal(("fixed", "abc1234def"), (fixedOne.GetProperty("counts").GetProperty("counts").GetString(), fixedOne.GetProperty("counts").GetProperty("fix").GetString()));
        Assert.False(fixedOne.GetProperty("disputed").GetBoolean());
        Assert.Equal("o2", detail.GetProperty("recheck").GetProperty("id").GetString());
    }

    [Fact]
    public void The_detail_says_its_tier_none_where_no_agent_read_it_and_person_where_the_person_did_and_nothing_where_none_is_asked()
    {
        Assert.Null(DriverModule.OpinionDetail(new OpinionGateState(OpinionGateStates.None, "web-app")));

        var none = JsonSerializer.SerializeToElement(DriverModule.OpinionDetail(
            new OpinionGateState(OpinionGateStates.Unavailable, "web-app") { Code = ReviewerUnavailable.NoReviewer }), Camel);
        Assert.Equal("none", none.GetProperty("tier").GetString());
        Assert.Equal(JsonValueKind.Null, none.GetProperty("first").ValueKind);

        var myself = JsonSerializer.SerializeToElement(DriverModule.OpinionDetail(new OpinionGateState(OpinionGateStates.Myself, "web-app")
        {
            Person = new OpinionPersonWord(OpinionPersonSaid.Myself, Read, DateTimeOffset.UnixEpoch) { Words = "read it all", Door = ReviewDoors.Screen },
        }), Camel);
        Assert.Equal("person", myself.GetProperty("tier").GetString());
        Assert.Equal(("myself", "read it all"), (myself.GetProperty("person").GetProperty("said").GetString(), myself.GetProperty("person").GetProperty("words").GetString()));
    }

    /// <summary>
    /// XAGENT1g (design §9's *What needs you*): a dispute, a required opinion none could be had for, and commits nobody read where
    /// the work lands by itself wait on the person; nothing else does.
    /// </summary>
    [Theory]
    [InlineData(OpinionGateStates.Disputed, false, false, true)]
    [InlineData(OpinionGateStates.Disputed, false, true, true)]
    [InlineData(OpinionGateStates.Unavailable, true, false, true)]
    [InlineData(OpinionGateStates.Unavailable, false, true, false)]
    [InlineData(OpinionGateStates.CommitsSince, false, true, true)]
    [InlineData(OpinionGateStates.CommitsSince, false, false, false)]
    [InlineData(OpinionGateStates.Reading, true, true, false)]
    [InlineData(OpinionGateStates.WithSession, true, true, false)]
    [InlineData(OpinionGateStates.Settled, true, true, false)]
    [InlineData(OpinionGateStates.Anyway, true, true, false)]
    public void What_waits_on_the_person(string state, bool required, bool auto, bool waits)
    {
        var gate = new OpinionGateState(state, "web-app")
        {
            Rule = new ResolvedOpinion(new OpinionRule(["codex-acp"], [OpinionRules.Landing], Required: required), OpinionSource.Repository),
        };

        Assert.Equal(waits, DriverModule.WaitsOnPerson(gate, auto));
    }

    [Fact]
    public async Task What_waits_on_the_person_waits_for_the_driver_s_service()
    {
        var refused = await RefusalAsync(Module(), "OPINION_WAITS", new { });

        Assert.StartsWith(Refusals.DriverNotReady, refused, StringComparison.Ordinal);
    }
}
