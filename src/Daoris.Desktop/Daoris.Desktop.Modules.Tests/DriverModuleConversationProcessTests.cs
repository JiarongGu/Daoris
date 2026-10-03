using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A conversation over the bridge where a real process holds the session (MOD8): what a tracked session that takes no
/// person's line answers, and a driven session's inbox beside its process. Apart from
/// <see cref="DriverModuleConversationTests"/> so that class, which starts none, runs in the fast half (MSG1d).
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class DriverModuleConversationProcessTests : DriverModuleBridge
{
    /// <summary>
    /// 🔴 A session that takes no input — an intake, one turn, whose stdin on the protocol door
    /// carries the driver's own frames (INT4h) — is REFUSED in the driver's words, never answered
    /// false. False means "it ended while you were typing", and a page that read it so would tell the
    /// person something untrue about a session that is running fine. Finishing it is refused too:
    /// closing that stream would end the protocol's turn, not a conversation.
    /// </summary>
    [Theory]
    [InlineData("SESSION_INPUT")]
    [InlineData("END_CHAT")]
    [InlineData("CANCEL_TURN")]
    public async Task A_session_that_takes_no_input_is_refused_in_the_drivers_words(string type)
    {
        var loop = Loop();
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "node",
            ArgumentList = { "-e", "setTimeout(() => {}, 60000)" },
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        using var tracked = loop.Processes.Track(
            "i1", process, refusesInput: "ask #a1b2c3's intake takes no messages.");

        try
        {
            var refusal = await RefusalAsync(new DriverModule(Bus, loop), type, new { id = "i1", text = "hello" });

            Assert.Contains(Refusals.DriverRefused, refusal);
            Assert.Contains("ask #a1b2c3's intake takes no messages.", refusal);
        }
        finally
        {
            loop.Processes.Stop("i1");
        }
    }

    /// <summary>
    /// SESS3: a driven session on the protocol door hears what the person adds — its inbox holds the
    /// words ahead of INT4i's refusal, the queue says it is listening and what waits, and the stop sends
    /// what is held now, withdrawing nothing. A finish is still refused: its stream is the driver's.
    /// </summary>
    [Fact]
    public async Task A_driven_session_that_listens_holds_what_the_person_adds_and_sends_it_on_a_stop()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "node",
            ArgumentList = { "-e", "setTimeout(() => {}, 60000)" },
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        using var tracked = loop.Processes.Track("s1", process, refusesInput: "the session on quest #q1 takes no line in its stream.");
        var inbox = loop.Processes.OpenInbox("s1");
        var stops = 0;
        inbox.Attach(() => { stops++; return Task.CompletedTask; });

        try
        {
            var sent = await AnswerAsync(module, "SESSION_INPUT", new { id = "s1", text = "the level file moved" });
            Assert.True(sent.GetProperty("sent").GetBoolean());

            var queue = await AnswerAsync(module, "SESSION_QUEUE", new { id = "s1" });
            Assert.True(queue.GetProperty("listening").GetBoolean());
            Assert.True(queue.GetProperty("taking").GetBoolean());
            Assert.Equal("the level file moved", Assert.Single(queue.GetProperty("queued").EnumerateArray()).GetProperty("text").GetString());

            var stop = await AnswerAsync(module, "CANCEL_TURN", new { id = "s1" });
            Assert.True(stop.GetProperty("cancelled").GetBoolean());
            Assert.Empty(stop.GetProperty("withdrawn").EnumerateArray());
            Assert.Equal(1, stops);
            Assert.Equal("the level file moved", inbox.TakeOrClose()?.Text);

            Assert.Contains(Refusals.DriverRefused, await RefusalAsync(module, "END_CHAT", new { id = "s1" }));

            // Closed, it hears nothing more — and the page is told it is not listening.
            inbox.TakeOrClose();
            var closed = await AnswerAsync(module, "SESSION_QUEUE", new { id = "s1" });
            Assert.False(closed.GetProperty("listening").GetBoolean());
        }
        finally
        {
            loop.Processes.Stop("s1");
        }
    }
}
