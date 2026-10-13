using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// REVIEWENV1c (D154 point 8; the review environment design §3.3–§3.4, D50): <c>daoris-driver quest review</c>, the terminal's door
/// to the person's verdict. It prints what the set-up step showed and how to show it again, sends the verdict with that set-up by
/// its <c>machine</c> and <c>sequence</c> (REVIEWENV1b3), says a <i>not yet</i>'s words to the step's session as its next turn, and
/// says what the gate now says. A service stood in (<see cref="ReviewStandIn"/>): the fast half (MOD8).
/// </summary>
public sealed class QuestReviewCommandTests
{
    public static TheoryData<string[]> Problems => new()
    {
        new[] { "review" },
        new[] { "review", "q2" },
        // REVIEWENV1j: a word shaped as an environment's name is a choice of one, so a word no environment can be named stands here.
        new[] { "review", "q2", "Approve!" },
        new[] { "review", "q2", "not-yet" },
        new[] { "review", "q2", "not-yet", "   " },
        new[] { "review", "--yes", "reviewed" },
    };

    [Theory]
    [MemberData(nameof(Problems))]
    public void Words_it_does_not_take_are_a_problem(string[] args)
    {
        Assert.True(QuestReviewCommand.Asks(args));
        Assert.Null(QuestReviewCommand.Read(args, out var problem));
        Assert.False(string.IsNullOrWhiteSpace(problem));
    }

    [Fact]
    public void It_reads_the_verdict_and_the_persons_words()
    {
        Assert.Equal(new QuestReviewAsk("q2", ReviewVerdicts.Reviewed, null), QuestReviewCommand.Read(["review", "#q2", "reviewed"], out _));
        Assert.Equal(new QuestReviewAsk("q2", ReviewVerdicts.NotYet, "the total is off"),
            QuestReviewCommand.Read(["review", "q2", "not-yet", "the", "total", "is", "off"], out _));
        Assert.Equal(new QuestReviewAsk("q1", ReviewVerdicts.Skipped, "a readme change"), QuestReviewCommand.Read(["review", "q1", "skip", "a readme change"], out _));
        Assert.False(QuestReviewCommand.Asks(["check", "q2"]));
    }

    /// <summary>
    /// A reviewed: what was shown is printed first, the verdict names that set-up whole, the service's sentence follows, the log
    /// keeps <c>review.verdict</c>, and the gate says what may land now.
    /// </summary>
    [Fact]
    public async Task A_reviewed_prints_what_was_shown_and_names_that_set_up()
    {
        var (service, heard) = StandIn(Step());
        var logged = new List<LandingLine>();
        var output = new StringWriter();

        var exit = await QuestReviewCommand.RunAsync(new QuestReviewAsk("q2", ReviewVerdicts.Reviewed, null), new QuestReviewWorld(service) { Log = logged.Add }, output);

        Assert.Equal(0, exit);
        Assert.Equal(
            "daoris-driver: set-up step `#q2` showed it in `local` at `01234567`, said by session s7: the new column\n"
            + "  look: http://localhost:4200/reports\n"
            + "  again: open reports\n"
            + "daoris-driver: Reviewed set-up step `#q2` in `local` at `01234567`: what its review held goes on.\n"
            + "daoris-driver: the gate: work up to `01234567` in `web-app` may land: the review's Accept, `daoris-driver trees land <session>`, "
            + "or the next look where it is accepted automatically. Work added after it waits for its own review.\n",
            output.ToString().ReplaceLineEndings("\n"));
        var (path, body) = heard.Single(each => each.Path.EndsWith("/review", StringComparison.Ordinal));
        Assert.Equal("/api/quests/q2/review", path);
        using var sent = JsonDocument.Parse(body);
        Assert.Equal("reviewed", sent.RootElement.GetProperty("verdict").GetString());
        Assert.Equal("desk", sent.RootElement.GetProperty("setUp").GetProperty("machine").GetString());
        Assert.Equal(41, sent.RootElement.GetProperty("setUp").GetProperty("sequence").GetInt64());
        Assert.False(sent.RootElement.TryGetProperty("words", out _));
        var line = Assert.Single(logged);
        Assert.Equal(("review.verdict", "reviewed", "terminal"), (line.Event, Value(line, "said"), Value(line, "door")));
    }

    /// <summary>A not yet sends the person's words with the verdict, then to the step's session as its next turn (design §3.4).</summary>
    [Fact]
    public async Task A_not_yet_goes_to_the_steps_session_as_its_next_turn()
    {
        var (service, heard) = StandIn(Step());
        var said = new List<(string Session, string Words)>();
        var output = new StringWriter();

        var exit = await QuestReviewCommand.RunAsync(
            new QuestReviewAsk("q2", ReviewVerdicts.NotYet, "the total is off"),
            new QuestReviewWorld(service)
            {
                Say = (session, words, _) =>
                {
                    said.Add((session, words));
                    return Task.FromResult(0);
                },
            },
            output);

        Assert.Equal(0, exit);
        Assert.Equal([("s7", "the total is off")], said);
        using var sent = JsonDocument.Parse(heard.Single(each => each.Path.EndsWith("/review", StringComparison.Ordinal)).Body);
        Assert.Equal(("not-yet", "the total is off"), (sent.RootElement.GetProperty("verdict").GetString(), sent.RootElement.GetProperty("words").GetString()));
        Assert.EndsWith("daoris-driver: the gate: it waits for the set-up `#q2` shows next, in `local`.\n", output.ToString().ReplaceLineEndings("\n"));
    }

    /// <summary>A skip names no set-up, on a set-up step or on the chain's work; a quest that is no set-up step takes only a skip.</summary>
    [Fact]
    public async Task A_skip_names_no_set_up_and_a_work_quest_takes_only_a_skip()
    {
        var work = new QuestView("q1", "ask #a1", "web-app", "Add the compare setting", "", "Done");
        var (service, heard) = StandIn(work);
        var output = new StringWriter();

        Assert.Equal(1, await QuestReviewCommand.RunAsync(new QuestReviewAsk("q1", ReviewVerdicts.Reviewed, null), new QuestReviewWorld(service), output));
        Assert.Contains("quest `#q1` is no set-up step", output.ToString());
        Assert.DoesNotContain(heard, each => each.Path.EndsWith("/review", StringComparison.Ordinal));

        Assert.Equal(0, await QuestReviewCommand.RunAsync(new QuestReviewAsk("q1", ReviewVerdicts.Skipped, "a readme change"), new QuestReviewWorld(service), output));
        using var sent = JsonDocument.Parse(heard.Single(each => each.Path.EndsWith("/review", StringComparison.Ordinal)).Body);
        Assert.Equal("skipped", sent.RootElement.GetProperty("verdict").GetString());
        Assert.False(sent.RootElement.TryGetProperty("setUp", out _));
        Assert.EndsWith("the gate: `web-app`'s work in this chain lands without a review.\n", output.ToString().ReplaceLineEndings("\n"));
    }

    /// <summary>A step that has shown nothing yet has no set-up to answer: refused here, and nothing is sent.</summary>
    [Fact]
    public async Task A_step_that_has_shown_nothing_is_refused_before_anything_is_sent()
    {
        var (service, heard) = StandIn(Step() with { SetUps = [] });
        var output = new StringWriter();

        Assert.Equal(1, await QuestReviewCommand.RunAsync(new QuestReviewAsk("q2", ReviewVerdicts.Reviewed, null), new QuestReviewWorld(service), output));
        Assert.Contains("has shown nothing yet in `local`", output.ToString());
        Assert.DoesNotContain(heard, each => each.Path.EndsWith("/review", StringComparison.Ordinal));
    }

    [Fact]
    public void The_usage_names_the_terminals_verdict()
    {
        Assert.Contains("\n  quest review <id> reviewed|not-yet|skip [\"…\"]\n", DriverCommand.Usage.ReplaceLineEndings("\n"));
        Assert.StartsWith("usage: daoris-driver quest review <id> reviewed|not-yet|skip [\"…\"]", QuestReviewCommand.Usage);
    }

    private static QuestView Step() => ReviewStandIn.Step();

    private static (ServiceClient Service, List<(string Path, string Body)> Heard) StandIn(QuestView quest) => ReviewStandIn.Of(quest);

    private static object? Value(LandingLine line, string key) => line.Data.Single(pair => pair.Key == key).Value;
}
