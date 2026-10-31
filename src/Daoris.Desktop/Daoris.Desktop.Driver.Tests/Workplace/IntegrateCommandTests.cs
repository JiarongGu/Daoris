using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>INTEGRATE1h (the integration design §5.8): <c>daoris-driver integrate</c>'s words, and the owner's acts left to the window.</summary>
public sealed class IntegrateCommandTests
{
    [Theory]
    [InlineData("engine", true, null)]
    [InlineData("engine --request feature/a", true, "request")]
    [InlineData("engine --recheck", true, "recheck")]
    [InlineData("engine --decide c1 hand-back", true, "decide")]
    [InlineData("engine --order r1 first", true, "order")]
    [InlineData("engine --plan feature/a", true, null)]
    [InlineData("engine --recheck now", false, null)]
    [InlineData("engine --decide c1", false, null)]
    [InlineData("engine --decide c1 merge", false, null)]
    [InlineData("engine --order r1 middle", false, null)]
    [InlineData("engine --plan", false, null)]
    [InlineData("--request x", false, null)]
    [InlineData("engine --frobnicate", false, null)]
    public void The_words_are_read_or_refused(string words, bool read, string? act)
    {
        var ask = IntegrateCommand.Read(words.Split(' '), out var problem);

        Assert.Equal(read, ask is not null);
        Assert.Equal(!read, problem is not null);
        Assert.Equal(act, ask?.Act);
    }

    [Fact]
    public void A_look_is_said_as_a_queue_a_candidate_with_its_steps_and_what_holds_it()
    {
        var steps = IntegrationStepNames.All.Select(step => new IntegrationLookStep(step, "waiting")).ToList();
        var look = new IntegrationLook("engine")
        {
            Queue = [new IntegrationLookRequest("r1", "main", "feature/a", "1111111aaaa", DateTimeOffset.UnixEpoch)],
            Candidates =
            [
                new IntegrationLookCandidate("c1", "main", "0000000bbbb", DateTimeOffset.UnixEpoch)
                {
                    Steps = steps, At = "replay", Holds = "conflict", By = "owner",
                    Members = [new IntegrationLookMember("r2", "feature/b", "held") { Said = "conflicts in a.txt" }],
                },
            ],
        };

        var said = IntegrateCommand.Said(look);

        Assert.Equal("integrate: `engine`", said[0]);
        Assert.Contains("  r1  feature/a @ 1111111 into `main`", said);
        Assert.Contains("candidate c1 for `main` on 0000000: at replay", said);
        Assert.Contains("  r2  feature/b: held — conflicts in a.txt", said);
        Assert.Contains("  held: a member's conflict, which waits for the steward's or the owner's act.", said);
    }

    /// <summary>INTEGRATE1h: a candidate held for a decide names the act whole, a recorded decide is said until its run acts, and a moved request says where.</summary>
    [Fact]
    public void A_decide_hold_names_its_act_a_recorded_decide_waits_for_the_run_and_the_queue_says_its_order()
    {
        var look = new IntegrationLook("engine")
        {
            Queue = [new IntegrationLookRequest("r3", "main", "feature/c", "3333333cccc", DateTimeOffset.UnixEpoch) { Ordered = IntegrationOrders.First }],
            Candidates =
            [
                new IntegrationLookCandidate("c1", "main", "0000000bbbb", DateTimeOffset.UnixEpoch) { Holds = "decide", Release = "decide", By = "owner", Why = "an unconfirmed failure" },
                new IntegrationLookCandidate("c2", "release", "0000000bbbb", DateTimeOffset.UnixEpoch) { Holds = "decide", By = "owner", Decided = IntegrationDecides.Land },
            ],
        };

        var said = IntegrateCommand.Said(look);

        Assert.Contains("queued, in order:", said);
        Assert.Contains("  r3  feature/c @ 3333333 into `main` (moved first)", said);
        Assert.Contains("  held for a decide (an unconfirmed failure); `--decide c1 split|hand-back|land` by the owner releases it.", said);
        Assert.Contains("  decided `land` by the owner: the line's run acts on it at its next look.", said);
    }

    [Fact]
    public void A_decide_and_an_order_are_pressed_with_their_two_words_apart()
    {
        Assert.Equal(
            """{"repository":"engine","act":"decide","candidate":"c1","decide":"hand-back"}""",
            System.Text.Json.JsonSerializer.Serialize(IntegrateCommand.Payload(new IntegrateAsk("engine") { Act = "decide", Target = "c1 hand-back" })));
        Assert.Equal(
            """{"repository":"engine","act":"order","request":"r1","place":"last"}""",
            System.Text.Json.JsonSerializer.Serialize(IntegrateCommand.Payload(new IntegrateAsk("engine") { Act = "order", Target = "r1 last" })));
        Assert.Equal("feature/a", IntegrateCommand.Read(["engine", "--plan", "feature/a"], out _)!.Plan);
    }

    [Fact]
    public void An_empty_look_says_nothing_is_asked_and_an_act_is_left_to_the_window()
    {
        Assert.Contains("nothing is asked", IntegrateCommand.Said(new IntegrationLook("engine"))[1]);
        Assert.Contains("a card in Daoris's window", IntegrateCommand.ActSaid(new IntegrateAsk("engine") { Act = "drop" }));
    }
}
