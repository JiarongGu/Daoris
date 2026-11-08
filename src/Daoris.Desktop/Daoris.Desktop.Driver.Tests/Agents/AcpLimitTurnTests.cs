using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The protocol door's turn that Claude Code's adapter ends <c>end_turn</c> on its account's limit, the limit's sentence its agent's last
/// message (the install, 2026-11-07): the run ends there as on a refused prompt, its failure the sentence, and nothing more is prompted on
/// that session, a carry-on owed included. A last message in other words, or the sentence followed by a step of the agent's, ends as
/// today. Against a fake agent over in-memory streams: no process and no model.
/// </summary>
public sealed class AcpLimitTurnTests
{
    private const string Sentence = "You've hit your session limit · resets 3am (Australia/Sydney)";

    private static readonly ProviderRegistry Built = ProviderRegistry.Built();

    private static readonly Func<string, bool> Said =
        TurnEndLimits.For(Built, Built.Door("claude-code-acp"), () => DateTimeOffset.UnixEpoch, TimeZoneInfo.Utc);

    private const string Usage =
        """{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"usage_update","used":155233,"size":1000000}}}""";

    private const string Called =
        """{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"tool_call","toolCallId":"t-1","title":"Run the tests","status":"pending"}}}""";

    /// <summary>The install's frames: the sentence as the agent's message, its usage, and the turn's end.</summary>
    private static AcpTransientTests.Agent Limited(params string[] after) => AcpTransientTests.Scripted((n, frame, self) =>
    {
        self.Push(AcpTransientTests.Said(Sentence));
        self.Push(Usage);
        foreach (var each in after) self.Push(each);
        return AcpTransientTests.Answer(frame, "end_turn");
    });

    /// <summary>A done quest's dirty tree, which would carry the session on were its turn not ended on the limit.</summary>
    private static Task<TurnCarry> Dirty(CancellationToken ct) => Task.FromResult(new TurnCarry(true, 1, "Commit your work."));

    [Fact]
    public async Task A_turn_ended_on_the_limit_s_sentence_ends_the_run_as_its_failure_and_prompts_nothing_more()
    {
        var agent = Limited();
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }) { LimitSaid = Said, CarriesOn = Dirty };

        var limited = await Assert.ThrowsAsync<AcpTurnLimited>(
            () => session.RunAsync("D:/fam/Game", "serve the report", CancellationToken.None).WaitAsync(Poll.Patience));

        Assert.Equal(Sentence, limited.Words);
        Assert.Contains(Sentence, limited.Message, StringComparison.Ordinal);
        Assert.Equal([["serve the report"]], agent.Prompts);
    }

    [Fact]
    public async Task A_last_message_in_other_words_ends_as_today_and_a_carry_on_owed_still_goes()
    {
        var turns = 0;
        var agent = AcpTransientTests.Scripted((n, frame, self) =>
        {
            turns = n;
            self.Push(AcpTransientTests.Said("I hit a limit while running the tests, so I stopped there."));
            return AcpTransientTests.Answer(frame, "end_turn");
        });
        var carried = 0;
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { })
        {
            LimitSaid = Said,
            CarriesOn = ct => Task.FromResult(new TurnCarry(Interlocked.Increment(ref carried) == 1, 1, "Commit your work.")),
        };

        var outcome = await session.RunAsync("D:/fam/Game", "serve the report", CancellationToken.None).WaitAsync(Poll.Patience);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Equal(2, turns);
    }

    [Fact]
    public async Task A_carry_on_turn_s_first_message_without_an_id_does_not_join_the_turn_before_it()
    {
        var read = new List<string>();
        var agent = AcpTransientTests.Scripted((n, frame, self) =>
        {
            self.Push(AcpTransientTests.Said(n == 1 ? "The work is done. " : Sentence));
            return AcpTransientTests.Answer(frame, "end_turn");
        });
        var carried = 0;
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { })
        {
            LimitSaid = words => { read.Add(words); return false; },
            CarriesOn = ct => Task.FromResult(new TurnCarry(Interlocked.Increment(ref carried) == 1, 1, "Commit your work.")),
        };

        await session.RunAsync("D:/fam/Game", "serve the report", CancellationToken.None).WaitAsync(Poll.Patience);

        Assert.Equal(["The work is done. ", Sentence], read);
    }

    [Fact]
    public async Task The_sentence_followed_by_a_step_of_the_agent_s_is_no_limit()
    {
        var agent = Limited(Called);
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }) { LimitSaid = Said };

        var outcome = await session.RunAsync("D:/fam/Game", "serve the report", CancellationToken.None).WaitAsync(Poll.Patience);

        Assert.Equal("end_turn", outcome.StopReason);
    }

    [Fact]
    public async Task A_session_handed_no_reader_of_its_limit_ends_as_today()
    {
        var agent = Limited();
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { });

        var outcome = await session.RunAsync("D:/fam/Game", "serve the report", CancellationToken.None).WaitAsync(Poll.Patience);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Single(agent.Prompts);
    }
}
