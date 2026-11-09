using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>What the open says of a choice when the agent is gone, not refusing, at the set.</summary>
public sealed class AcpOptionsEndedTests
{
    private const string Offered = """
        [{"id":"model","name":"Model","category":"model","type":"select","currentValue":"default","options":[{"value":"default","name":"Default"},{"value":"opus","name":"Opus"}]}]
        """;

    [Fact]
    public async Task A_stream_that_ends_while_the_model_is_set_is_not_a_refusal_of_it()
    {
        var agent = new AcpBackgroundTests.Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return AcpBackgroundTests.Ok(frame, """{"protocolVersion":1}""");
                case "session/new":
                    return AcpBackgroundTests.Ok(frame, $$"""{"sessionId":"s-1","configOptions":{{Offered}}}""");
                case "session/set_config_option":
                    self.Close();
                    return null;
                default:
                    return null;
            }
        });
        var asked = false;
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { })
        {
            Carry = new ActorAsked("claude-code-acp", new("entry", "mythos", true), new("account-default", null, false)),
            ModelRefused = ChoiceReask.Of(_ => { asked = true; return Task.FromResult<ActorAsked?>(null); }),
        };

        var failed = await Record.ExceptionAsync(() => session.RunAsync("D:/fam/Game", "a task", CancellationToken.None).WaitAsync(Poll.Patience));

        Assert.False(asked, "the stream's end was read as the agent refusing the model");
        Assert.IsNotType<AcpChoiceNotOffered>(failed);
    }

    [Fact]
    public async Task A_stream_that_ends_while_a_refused_model_is_set_back_is_not_the_agent_refusing_the_set_back()
    {
        var sets = 0;
        var agent = new AcpBackgroundTests.Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return AcpBackgroundTests.Ok(frame, """{"protocolVersion":1}""");
                case "session/new":
                    return AcpBackgroundTests.Ok(frame, $$"""{"sessionId":"s-1","configOptions":{{Offered}}}""");
                case "session/set_config_option" when ++sets == 1:
                    return AcpBackgroundTests.Ok(frame, $$"""{"configOptions":{{Offered}}}""");
                case "session/set_config_option":
                    self.Close();
                    return null;
                default:
                    return null;
            }
        });
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { })
        {
            Carry = new ActorAsked("claude-code-acp", new("entry", "opus", true), new("account-default", null, false)),
            ModelRefused = ChoiceReask.Of(_ => Task.FromResult<ActorAsked?>(
                new ActorAsked("claude-code-acp", new("account-default", null, false), new("account-default", null, false)))),
        };

        AcpOutcome? outcome = null;
        _ = await Record.ExceptionAsync(async () =>
            outcome = await session.RunAsync("D:/fam/Game", "a task", CancellationToken.None).WaitAsync(Poll.Patience));

        Assert.Equal(2, sets);
        Assert.NotEqual(AcpOutcome.ChoiceNotOffered, outcome?.StopReason);
    }
}
