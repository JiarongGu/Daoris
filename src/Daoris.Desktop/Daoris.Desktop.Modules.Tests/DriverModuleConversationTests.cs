using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A conversation over the bridge (`DriverModule.Conversation.cs`, MOD5): its record, its start, the
/// person's messages and files, finishing, stopping a turn, and its queue.
/// </summary>
public sealed class DriverModuleConversationTests : DriverModuleBridge
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

    [Fact]
    public async Task Sending_to_a_session_that_is_not_listening_answers_false()
    {
        var state = await AnswerAsync(Module(), "SESSION_INPUT", new { id = "nothing-here", text = "hello" });

        Assert.False(state.GetProperty("sent").GetBoolean());
    }

    /// <summary>CONV4c: the payload's files read back as the names and bytes the page sent; none is none.</summary>
    [Fact]
    public void A_messages_files_are_read_from_the_payload_as_names_and_bytes()
    {
        using var payload = JsonDocument.Parse("""
            {"id":"s1","text":"look","files":[{"name":"run.log","content":"ZXhpdCAz"},{"name":"shot.png","content":""}]}
            """);
        using var bare = JsonDocument.Parse("""{"id":"s1","text":"look"}""");

        var files = DriverModule.FilesOf(payload.RootElement);

        Assert.Equal(["run.log", "shot.png"], files.Select(file => file.Name));
        Assert.Equal("exit 3", System.Text.Encoding.UTF8.GetString(files[0].Content));
        Assert.Empty(files[1].Content);
        Assert.Empty(DriverModule.FilesOf(bare.RootElement));
    }

    /// <summary>
    /// CONV4c: a message's files arrive as bytes the way a quest's do, base64 in the payload — and bytes
    /// that are not base64 are refused in a sentence, never kept as something else.
    /// </summary>
    [Fact]
    public async Task A_file_that_is_not_base64_is_refused_in_a_sentence()
    {
        var refusal = await RefusalAsync(Module(), "SESSION_INPUT", new
        {
            id = "nothing-here", text = "look", files = new[] { new { name = "a.png", content = "not base64 at all!" } },
        });

        Assert.Contains(Refusals.DriverRefused, refusal);
        Assert.Contains("`a.png` did not arrive as a file's bytes", refusal);
    }

    /// <summary>
    /// CONV4a: stopping a turn nothing here holds stops nothing and withdraws nothing — an answer, as
    /// the page asking a moment late deserves, never an error.
    /// </summary>
    [Fact]
    public async Task Stopping_a_turn_nothing_here_holds_answers_that_nothing_ran()
    {
        var stop = await AnswerAsync(Module(), "CANCEL_TURN", new { id = "nothing-here" });

        Assert.False(stop.GetProperty("cancelled").GetBoolean());
        Assert.Empty(stop.GetProperty("withdrawn").EnumerateArray());
    }

    /// <summary>
    /// CONV4a: what a conversation has waiting is asked for by a page that just opened it, and takes
    /// every change after that live — nothing waiting is an empty list.
    /// </summary>
    [Fact]
    public async Task A_conversation_with_nothing_waiting_answers_an_empty_queue()
    {
        var queue = await AnswerAsync(Module(), "SESSION_QUEUE", new { id = "nothing-here" });

        Assert.Equal("nothing-here", queue.GetProperty("session").GetString());
        Assert.Empty(queue.GetProperty("queued").EnumerateArray());
        Assert.False(queue.GetProperty("taking").GetBoolean());
        // HELP4: nothing is opening either, so nothing waits on a door.
        Assert.False(queue.GetProperty("opening").GetBoolean());
        // RAIL2: no turn ended here, so no last move is claimed — the page keeps the record's.
        Assert.Equal(JsonValueKind.Null, queue.GetProperty("lastTurn").ValueKind);
    }

    /// <summary>
    /// D76 §2 (CONV1): a session's conversation is read back over the bridge a page at a time — the
    /// newest first, then earlier, then only what is newer — from the record under the home, so it
    /// answers after a restart when the console's window is long gone.
    /// </summary>
    [Fact]
    public async Task A_sessions_conversation_is_read_back_a_page_at_a_time()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        for (var i = 1; i <= 5; i++)
        {
            loop.Events.Append("s1", new SessionEvent { Kind = SessionEventKind.Message, Text = $"m{i}" });
        }

        var latest = await AnswerAsync(module, "SESSION_HISTORY", new { id = "s1", limit = 2 });
        Assert.Equal("s1", latest.GetProperty("session").GetString());
        Assert.Equal(["m4", "m5"], latest.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("text").GetString()));
        Assert.True(latest.GetProperty("earlier").GetBoolean());
        Assert.Equal(5, latest.GetProperty("latest").GetInt64());
        Assert.Equal("message", latest.GetProperty("events")[0].GetProperty("kind").GetString());

        var earlier = await AnswerAsync(module, "SESSION_HISTORY", new { id = "s1", before = 4, limit = 2 });
        Assert.Equal(["m2", "m3"], earlier.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("text").GetString()));

        var newer = await AnswerAsync(module, "SESSION_HISTORY", new { id = "s1", after = 3 });
        Assert.Equal(["m4", "m5"], newer.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("text").GetString()));

        var none = await AnswerAsync(module, "SESSION_HISTORY", new { id = "nothing-here" });
        Assert.Empty(none.GetProperty("events").EnumerateArray());
    }

    /// <summary>An id arrives from the page, so one that is not an id is refused in a sentence, never read.</summary>
    [Fact]
    public async Task A_history_asked_for_by_a_path_is_refused()
    {
        var refusal = await RefusalAsync(Module(), "SESSION_HISTORY", new { id = "../escape" });

        Assert.Contains("is not a session id", refusal);
    }

    /// <summary>
    /// A cold start is the state a person meets most often, and it must be a SENTENCE: the loop's
    /// service is not answering yet, so there is nothing to put behind a conversation.
    /// </summary>
    [Fact]
    public async Task A_chat_asked_for_before_the_loop_is_up_says_so_rather_than_crashing()
    {
        var refusal = await RefusalAsync(Module(), "START_CHAT", new { repository = "engine" });

        Assert.Contains(Refusals.DriverNotReady, refusal);
    }
}
