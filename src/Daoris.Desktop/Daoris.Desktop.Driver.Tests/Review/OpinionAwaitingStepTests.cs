using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The work's session awaiting its run's step while that step is the second opinion (OPINIONWAKE1): the opinion's findings, and the person's
/// words, are its next turn through the one go-on every record with words waiting takes, never held for a turn's end that has come. The
/// service's own table is <c>SolicitedAnswersTests</c>; the host here is <see cref="OpinionStandIn"/>, in process.
/// </summary>
public sealed class OpinionAwaitingStepTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-await-" + Guid.NewGuid().ToString("N")[..8]);

    public OpinionAwaitingStepTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly OpinionPassAsk Asked =
        new(OpinionRules.Landing, "s1", "reports", "D:/checkouts/reports", OpinionStandIn.Base, OpinionStandIn.Tip,
            new OpinionRule(["codex-acp"], [OpinionRules.Landing]));

    private Daoris.Driver.Driver Driver(OpinionStandIn host) =>
        new(host.Client(), DriverConfig.Empty with { Actors = StaffedRoles.On("claude-code-acp") }, ProviderRegistry.Built(), _home)
        {
            Passes = (_, _, _) => throw new InvalidOperationException("no recheck is asked here"),
        };

    /// <summary>
    /// 🔴 An opinion given while its author awaits the step: its findings are kept on the author's record once, as the opinion's word, and
    /// the course's answer is its own conversation resumed; the look after reads them with the session, handing nothing again.
    /// </summary>
    [Fact]
    public async Task An_opinion_given_to_an_author_awaiting_its_step_wakes_it_with_the_findings_once()
    {
        var host = new OpinionStandIn().Session("s1", "awaiting-step").Opinion("op1", "s1", weights: ["must", "must"]);
        host.Answers = true;
        var heard = new List<LineCourse>();
        var driver = Driver(host);
        driver.Deliverer.Decided = heard.Add;

        var handed = await driver.DeliverAsync("op1", Asked);
        var again = await driver.DeliverAsync("op1", Asked);

        Assert.Equal((OpinionDeliveryStates.Handed, "s1"), (handed.State, handed.Session));
        Assert.Equal(["op1"], host.Hands);
        var word = Assert.Single(host.Said("s1"));
        Assert.Equal("op1", word!["by"]!.GetValue<string>());
        Assert.IsType<LineCourse.Resume>(Assert.Single(heard));
        Assert.Equal(OpinionDeliveryStates.WithSession, again.State);
    }

    /// <summary>
    /// 🔴 Words kept on a record awaiting its step, the person's or another agent's findings, go on in its own conversation as its next turn:
    /// the course resumes it as it resumes an ended record, so the step waiting on its answer is never waited on in turn.
    /// </summary>
    [Theory]
    [InlineData("person")]
    [InlineData("findings")]
    public void Words_kept_on_a_record_awaiting_its_step_go_on_in_its_own_conversation(string whose)
    {
        var record = new PriorSession("s1", _home, SessionStates.AwaitingStep, "Its turn ended and it waits on its workflow's next step.", "engine")
        {
            Said = whose == "person"
                ? PersonWords.Of("Answer the opinion's findings.")
                : [new SaidWordView("op-w1", "Another agent read your work.", DateTimeOffset.UtcNow, [], true) { By = "op1" }],
            Profile = "account-1",
            Adapter = "claude-code-acp",
        };

        Assert.IsType<LineCourse.Resume>(DrivenCourse.GoOn(record, "Taken", null, agentGone: false));
        Assert.True(SolicitedHand.TakesNow(record));
    }
}
