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
    [InlineData("engine --recheck now", false, null)]
    [InlineData("engine --decide c1", false, null)]
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
                    Steps = steps, At = "replay", Holds = "conflict", Release = "decide", By = "owner",
                    Members = [new IntegrationLookMember("r2", "feature/b", "held") { Said = "conflicts in a.txt" }],
                },
            ],
        };

        var said = IntegrateCommand.Said(look);

        Assert.Equal("integrate: `engine`", said[0]);
        Assert.Contains("  r1  feature/a @ 1111111 into `main`", said);
        Assert.Contains("candidate c1 for `main` on 0000000: at replay", said);
        Assert.Contains("  r2  feature/b: held — conflicts in a.txt", said);
        Assert.Contains("  held: conflict; `--decide` by the owner releases it.", said);
    }

    [Fact]
    public void An_empty_look_says_nothing_is_asked_and_an_act_is_left_to_the_window()
    {
        Assert.Contains("nothing is asked", IntegrateCommand.Said(new IntegrationLook("engine"))[1]);
        Assert.Contains("a card in Daoris's window", IntegrateCommand.ActSaid(new IntegrateAsk("engine") { Act = "drop" }));
    }
}
