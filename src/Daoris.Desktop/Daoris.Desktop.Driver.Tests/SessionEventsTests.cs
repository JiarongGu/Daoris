using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A session's structure, kept as typed events on the machine (D76 §1–2, CONV1).
/// </summary>
/// <remarks>
/// The console is a window that forgets; this is the record the page reads a conversation back
/// from, after a restart as well as live. So the tests are about what survives: the order, the
/// sequence across a new instance, a page at a time, and a file that stays bounded and inside its
/// folder.
/// </remarks>
public sealed class SessionEventsTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "daoris-events-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private static SessionEvent Message(string text) => new() { Kind = SessionEventKind.Message, Text = text };

    private static SessionEvent Asked(string text, string origin = "person") =>
        new() { Kind = SessionEventKind.User, Origin = origin, Text = text };

    /// <summary>
    /// PARK1: what a session that parks to ask the person last said, whole — joined from the chunks it was
    /// streamed in, indented lists and all. Its record holds the message as it was written; the transcript
    /// read it as console lines and stopped at the first indented one, so a question's list was lost.
    /// </summary>
    [Fact]
    public void The_last_thing_said_is_the_agents_last_message_whole()
    {
        var events = new SessionEvents(_directory);
        events.Append("park1", Asked("You are report-ui's agent…", origin: "target"));
        events.Append("park1", Message("Looking at it."));
        events.Append("park1", new SessionEvent { Kind = SessionEventKind.Tool, Id = "t1", Title = "Read a file" });
        events.Append("park1", Message("**Decision needed:** which report?\n- **The Angular one:**"));
        events.Append("park1", new SessionEvent { Kind = SessionEventKind.Usage, Used = 10, Size = 100 });
        events.Append("park1", Message("\n  - **Stops:** it keeps them.\n\nThe quest is still taken while I wait."));

        Assert.Equal(
            "**Decision needed:** which report?\n- **The Angular one:**\n  - **Stops:** it keeps them.\n\nThe quest is still taken while I wait.",
            events.LastSaid("park1"));
    }

    /// <summary>
    /// PARK1: a parked session's card quotes its record's last message, and reads the transcript only
    /// where the record has none — a harness that keeps no structure, or a record that could not be kept.
    /// </summary>
    [Fact]
    public void A_park_quotes_the_record_and_falls_back_to_the_transcript()
    {
        var events = new SessionEvents(_directory);
        events.Append("park2", Message("Which report?\n  - **Stops:** kept.\n\nI wait for your answer."));
        Directory.CreateDirectory(_directory);
        var transcript = Path.Combine(_directory, "park2.log");
        File.WriteAllText(transcript, "→ Read a file\nWhich report?\n  - **Stops:** kept.\n\nI wait for your answer.\n");
        var bare = Path.Combine(_directory, "bare1.log");
        File.WriteAllText(bare, "→ Read a file\nShall I go on?\n");

        Assert.Equal("Which report?\n  - **Stops:** kept.\n\nI wait for your answer.", Daoris.Driver.Driver.ParkedWords(events, "park2", transcript));
        Assert.Equal("Shall I go on?", Daoris.Driver.Driver.ParkedWords(events, "bare1", bare));
    }

    /// <summary>PARK1: a turn that said nothing after the person's words has no last words, never an older turn's.</summary>
    [Fact]
    public void Nothing_said_after_the_persons_words_is_no_last_words()
    {
        var events = new SessionEvents(_directory);
        events.Append("quiet1", Message("An earlier answer."));
        events.Append("quiet1", Asked("and now?"));
        events.Append("quiet1", new SessionEvent { Kind = SessionEventKind.Tool, Id = "t1", Title = "Read a file" });

        Assert.Null(events.LastSaid("quiet1"));
        Assert.Null(events.LastSaid("none1"));
        Assert.Null(events.LastSaid("../escape"));
    }

    /// <summary>
    /// RAIL1: a conversation's identity is the first thing the person said in it (working-surface design
    /// §3), read from this machine's record — it never rides the session record, which travels (D47 §4).
    /// A driven session's composed target is not the person speaking, and a session with no record here
    /// is simply absent.
    /// </summary>
    [Fact]
    public void A_sessions_opening_is_the_first_thing_the_person_said_in_it()
    {
        var events = new SessionEvents(_directory);
        events.Append("chat1", Asked("Read README.md and tell me its first heading.\nIn a few words."));
        events.Append("chat1", Message("examples/game"));
        events.Append("chat1", Asked("and the second?"));
        events.Append("drive1", Asked("You are the engine repository's agent…", origin: "target"));
        events.Append("long1", Asked(new string('x', 400)));

        var openings = events.Openings(["chat1", "drive1", "long1", "none1", "../escape"]);

        Assert.Equal("Read README.md and tell me its first heading.", openings["chat1"]);
        Assert.False(openings.ContainsKey("drive1"));
        Assert.False(openings.ContainsKey("none1"));
        Assert.Equal(SessionEvents.OpeningLimit + 1, openings["long1"].Length);
        Assert.EndsWith("…", openings["long1"]);
    }

    /// <summary>
    /// RAIL1: search what sessions said — the person's words and the agent's — with a snippet around the
    /// match. An agent's message is streamed in chunks, so a word split across two is still found; case
    /// does not count; a tool's output is not what anyone said, and is not searched.
    /// </summary>
    [Fact]
    public void Search_finds_what_was_said_across_the_chunks_it_was_streamed_in()
    {
        var events = new SessionEvents(_directory);
        events.Append("chat1", Asked("Cap the hydration per frame"));
        events.Append("chat1", Message("The cap belongs in the stre"));
        events.Append("chat1", Message("amer, not the loader."));
        events.Append("chat1", new SessionEvent { Kind = SessionEventKind.Tool, Id = "t1", Title = "Read streamer.rs", Output = "streamer internals" });
        events.Append("chat2", Asked("把流式加载的上限做成可配置的"));

        var streamer = events.Search("STREAMER");
        var hit = Assert.Single(streamer.Hits);
        Assert.Equal(("chat1", SessionEventKind.Message), (hit.Session, hit.Kind));
        Assert.Contains("belongs in the streamer, not the loader.", hit.Snippet);

        Assert.Equal("chat2", Assert.Single(events.Search("流式加载").Hits).Session);
        Assert.Equal("chat1", Assert.Single(events.Search("hydration").Hits).Session);
        Assert.Empty(events.Search("x").Hits);
    }

    /// <summary>
    /// STEER1 (D136): what a person told a driven session is kept twice — the moment it was said, with when it reaches the
    /// session, and again where the session took it, paired by one id — and a search finds it once, where it was taken.
    /// </summary>
    [Fact]
    public void Words_told_to_a_working_session_are_kept_waiting_then_taken_and_found_once()
    {
        var events = new SessionEvents(_directory);
        var words = new ChatMessage("the budget is in level.json", []) { Id = "said-1" };
        events.Append("drive1", Asked("You are the engine repository's agent…", origin: "target"));
        events.Append("drive1", Daoris.Driver.Driver.Words(words) with { Reaches = "next-step" });
        events.Append("drive1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" });
        var taken = events.Append("drive1", Daoris.Driver.Driver.Words(words));

        var lines = File.ReadAllLines(Path.Combine(_directory, "drive1.events.jsonl"));
        Assert.Contains(lines, line => line.Contains("\"origin\":\"person\"") && line.Contains("\"id\":\"said-1\"") && line.Contains("\"reaches\":\"next-step\""));
        Assert.Contains(lines, line => line.Contains("\"origin\":\"person\"") && line.Contains("\"id\":\"said-1\"") && !line.Contains("reaches"));

        var hit = Assert.Single(events.Search("level.json").Hits);
        Assert.Equal(taken.Seq, hit.Seq);
    }

    /// <summary>The answer is bounded and says so: a few hits per session, the snippet a window, not the whole text.</summary>
    [Fact]
    public void Search_is_bounded_and_says_when_it_left_hits_out()
    {
        var events = new SessionEvents(_directory);
        for (var i = 0; i < 5; i++) events.Append("chat1", Asked($"the budget, pass {i}"));
        events.Append("chat2", Asked(new string('a', 500) + " budget " + new string('b', 500)));

        var answer = events.Search("budget", limit: 4);

        Assert.Equal(SessionEvents.HitsPerSession, answer.Hits.Count(h => h.Session == "chat1"));
        Assert.True(answer.Cut);
        var wide = events.Search("budget").Hits.Single(h => h.Session == "chat2");
        Assert.True(wide.Snippet.Length < 200, wide.Snippet);
        Assert.StartsWith("…", wide.Snippet);
        Assert.EndsWith("…", wide.Snippet);
    }

    /// <summary>
    /// 🔴 A page reading the record never costs the record an event (CONV3b, 2026-09-25). The reader
    /// opened the file denying writers, so an append that landed mid-read failed with a sharing
    /// violation, and its caller — rightly unwilling to fail a session over its record — dropped the
    /// event. Seen as an agent's answer missing from a conversation whose turn had ended.
    /// </summary>
    [Fact]
    public async Task Every_append_lands_while_the_record_is_being_read()
    {
        var events = new SessionEvents(_directory);
        events.Append("s1", Message("first"));

        // Bounded on both sides: an unbounded read loop opened the file thousands of times a second,
        // and a machine's real-time scanner made the whole suite three times slower for it.
        var reading = Task.Run(() =>
        {
            for (var i = 0; i < 150; i++) _ = events.Page("s1");
        });

        var failures = 0;
        for (var i = 0; i < 150; i++)
        {
            try
            {
                events.Append("s1", Message($"m{i}"));
            }
            catch (IOException)
            {
                failures++;
            }
        }

        await reading;

        Assert.Equal(0, failures);
        Assert.Equal(151, events.Page("s1", limit: 1000).Events.Count);
    }

    [Fact]
    public void Each_event_is_numbered_in_order_and_written_as_one_line_beside_the_transcript()
    {
        var events = new SessionEvents(_directory);
        var heard = new List<(string Session, SessionEvent Event)>();
        events.Evented += (session, e) => heard.Add((session, e));

        var first = events.Append("a1b2c3", Message("working on it"));
        var second = events.Append("a1b2c3", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" });

        Assert.Equal(1, first.Seq);
        Assert.Equal(2, second.Seq);
        Assert.NotEqual(default, first.At);
        Assert.Equal([1L, 2L], heard.Select(h => h.Event.Seq));
        Assert.All(heard, h => Assert.Equal("a1b2c3", h.Session));

        var file = Path.Combine(_directory, "a1b2c3.events.jsonl");
        var lines = File.ReadAllLines(file);
        Assert.Equal(2, lines.Length);
        Assert.Contains("\"working on it\"", lines[0]);
        Assert.DoesNotContain("\r", File.ReadAllText(file));
        Assert.False(File.ReadAllBytes(file).AsSpan().StartsWith(Encoding.UTF8.Preamble), "no BOM");
    }

    /// <summary>
    /// 🔴 The whole point (D76 §2): the console's window was gone after a restart, and the page said
    /// "nothing from this session is held here". A new instance reads the record back, and carries
    /// the sequence on rather than starting again at one.
    /// </summary>
    [Fact]
    public void A_new_instance_reads_a_session_back_and_carries_its_sequence_on()
    {
        var before = new SessionEvents(_directory);
        before.Append("a1b2c3", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "cap it" });
        before.Append("a1b2c3", Message("capped at 4 per frame"));

        var after = new SessionEvents(_directory);
        var page = after.Page("a1b2c3");
        var next = after.Append("a1b2c3", Message("and tested"));

        Assert.Equal(["cap it", "capped at 4 per frame"], page.Events.Select(e => e.Text));
        Assert.Equal("person", page.Events[0].Origin);
        Assert.Equal(2, page.Latest);
        Assert.False(page.Earlier);
        Assert.Equal(3, next.Seq);
    }

    /// <summary>
    /// CONV5: a turn's tokens are read back after a restart as they were written — and a count the wire
    /// never gave stays unknown on the way back, rather than becoming a zero.
    /// </summary>
    [Fact]
    public void A_turns_tokens_read_back_as_written_with_an_unknown_count_still_unknown()
    {
        new SessionEvents(_directory).Append("a1b2c3", new SessionEvent
        {
            Kind = SessionEventKind.Turn, StopReason = "end_turn",
            Tokens = new TurnTokens(Input: 4, Output: 80, CacheRead: 51061, CacheWrite: null),
        });

        var turn = Assert.Single(new SessionEvents(_directory).Page("a1b2c3").Events);

        Assert.Equal(new TurnTokens(4, 80, 51061, null), turn.Tokens);
        Assert.DoesNotContain("cacheWrite", File.ReadAllText(Path.Combine(_directory, "a1b2c3.events.jsonl")));
    }

    [Fact]
    public void A_page_is_the_latest_events_and_says_when_there_are_earlier_ones()
    {
        var events = new SessionEvents(_directory);
        for (var i = 1; i <= 5; i++) events.Append("a1b2c3", Message($"m{i}"));

        var latest = events.Page("a1b2c3", limit: 2);
        var earlier = events.Page("a1b2c3", before: latest.Events[0].Seq, limit: 2);
        var first = events.Page("a1b2c3", before: earlier.Events[0].Seq, limit: 2);

        Assert.Equal(["m4", "m5"], latest.Events.Select(e => e.Text));
        Assert.True(latest.Earlier);
        Assert.Equal(["m2", "m3"], earlier.Events.Select(e => e.Text));
        Assert.True(earlier.Earlier);
        Assert.Equal(["m1"], first.Events.Select(e => e.Text));
        Assert.False(first.Earlier);
        Assert.All([latest, earlier, first], page => Assert.Equal(5, page.Latest));
    }

    /// <summary>
    /// SESS1 S1: what the session was asked comes with every page it is not already in, so a long run
    /// reads from its ask rather than from the middle — and a page that holds it carries none twice.
    /// </summary>
    [Fact]
    public void A_page_that_does_not_hold_the_opening_ask_carries_it()
    {
        var events = new SessionEvents(_directory);
        events.Append("a1b2c3", Asked("Your target is the quest…", origin: "target"));
        for (var i = 1; i <= 5; i++) events.Append("a1b2c3", Message($"m{i}"));

        var latest = events.Page("a1b2c3", limit: 2);
        Assert.Equal("Your target is the quest…", latest.Opening?.Text);
        Assert.Equal(1, latest.Opening?.Seq);

        var whole = events.Page("a1b2c3", limit: 20);
        Assert.Null(whole.Opening);
        Assert.Null(events.Page("nothing-asked", limit: 2).Opening);
    }

    /// <summary>
    /// SESS1 S9: a jump to the first failure needs to know where it began, wherever it is in the run —
    /// the call's first event, since the failure itself is an update to it.
    /// </summary>
    [Fact]
    public void A_page_says_where_the_first_failed_call_began()
    {
        var events = new SessionEvents(_directory);
        events.Append("a1b2c3", Asked("go", origin: "target"));
        events.Append("a1b2c3", new SessionEvent { Kind = SessionEventKind.Tool, Id = "c1", Title = "Read a", Status = "completed" });
        var begun = events.Append("a1b2c3", new SessionEvent { Kind = SessionEventKind.Tool, Id = "c2", Title = "npm test", Status = "in_progress" });
        events.Append("a1b2c3", Message("hm"));
        events.Append("a1b2c3", new SessionEvent { Kind = SessionEventKind.Tool, Id = "c2", Status = "failed" });

        Assert.Equal(begun.Seq, events.Page("a1b2c3", limit: 1).FirstFailure);
        events.Append("clean1", Message("nothing failed"));
        Assert.Null(events.Page("clean1").FirstFailure);
    }

    /// <summary>SESS1 S9: one session searched — what was said and the calls by their titles, in order.</summary>
    [Fact]
    public void Within_one_session_finds_its_words_and_its_calls_in_order()
    {
        var events = new SessionEvents(_directory);
        events.Append("a1b2c3", Asked("Run the gates and fix what fails"));
        events.Append("a1b2c3", new SessionEvent { Kind = SessionEventKind.Tool, Id = "c1", Status = "pending" });
        events.Append("a1b2c3", new SessionEvent { Kind = SessionEventKind.Tool, Id = "c1", Title = "npm run gates", Status = "completed" });
        events.Append("a1b2c3", Message("The gates are clean."));
        events.Append("other1", Message("gates elsewhere"));

        var found = events.Within("a1b2c3", "gates");

        Assert.Equal(["user", "tool", "message"], found.Hits.Select(hit => hit.Kind));
        Assert.Equal(2, found.Hits[1].Seq);
        Assert.All(found.Hits, hit => Assert.Equal("a1b2c3", hit.Session));
        Assert.False(found.Cut);
        Assert.Empty(events.Within("a1b2c3", "g").Hits);
    }

    /// <summary>How a page that missed a live batch closes the gap without re-reading the start.</summary>
    [Fact]
    public void After_answers_only_what_is_newer()
    {
        var events = new SessionEvents(_directory);
        for (var i = 1; i <= 4; i++) events.Append("a1b2c3", Message($"m{i}"));

        Assert.Equal(["m3", "m4"], events.After("a1b2c3", 2).Events.Select(e => e.Text));
    }

    [Fact]
    public void A_session_with_no_record_answers_an_empty_page_rather_than_nothing()
    {
        var page = new SessionEvents(_directory).Page("f00d");

        Assert.Empty(page.Events);
        Assert.Equal(0, page.Latest);
        Assert.False(page.Earlier);
    }

    /// <summary>One torn or foreign line must cost that line, never the conversation around it.</summary>
    [Fact]
    public void A_line_that_cannot_be_read_is_skipped_and_the_rest_still_reads()
    {
        var events = new SessionEvents(_directory);
        events.Append("a1b2c3", Message("before"));
        File.AppendAllText(Path.Combine(_directory, "a1b2c3.events.jsonl"), "{not json\n");
        events.Append("a1b2c3", Message("after"));

        Assert.Equal(["before", "after"], new SessionEvents(_directory).Page("a1b2c3").Events.Select(e => e.Text));
    }

    /// <summary>
    /// The id arrives from the page (a history request), so it names a file only when it is an id:
    /// nothing outside the folder is read or written, whatever a caller sends.
    /// </summary>
    [Theory]
    [InlineData("../escape")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("")]
    [InlineData("..")]
    public void An_id_that_is_not_an_id_is_refused(string id)
    {
        var events = new SessionEvents(_directory);

        Assert.Throws<DriverException>(() => events.Append(id, Message("x")));
        Assert.Throws<DriverException>(() => events.Page(id));
    }

    /// <summary>
    /// Live events go out a window at a time, grouped by session, as console lines do — and a reader
    /// that is gone costs its own view, never the record.
    /// </summary>
    [Fact]
    public async Task Events_are_relayed_a_batch_per_session_and_a_lost_reader_costs_nothing()
    {
        var events = new SessionEvents(_directory);
        var batches = new List<(string Session, long[] Seqs)>();
        using (var relay = new EventRelay(events, (session, batch) =>
        {
            batches.Add((session, [.. batch.Select(e => e.Seq)]));
            return Task.CompletedTask;
        }, TimeSpan.FromMinutes(5)))
        {
            events.Append("a1b2c3", Message("one"));
            events.Append("a1b2c3", Message("two"));
            events.Append("d4e5f6", Message("theirs"));
            await relay.FlushAsync();
        }

        Assert.Equal(["a1b2c3", "d4e5f6"], batches.Select(b => b.Session));
        Assert.Equal([1L, 2L], batches[0].Seqs);
        Assert.Equal([1L], batches[1].Seqs);

        using var broken = new EventRelay(events, (_, _) => throw new InvalidOperationException("the window closed"), TimeSpan.FromMinutes(5));
        events.Append("a1b2c3", Message("said anyway"));
        await broken.FlushAsync();
        Assert.Equal("said anyway", events.Page("a1b2c3").Events[^1].Text);
    }

    /// <summary>
    /// One event is one line, and a line is bounded: a tool that printed a megabyte must not make the
    /// record unreadable a page at a time. What was cut is said, never silently lost.
    /// </summary>
    [Fact]
    public void A_huge_field_is_cut_and_says_it_was()
    {
        var events = new SessionEvents(_directory);
        var huge = new string('x', SessionEvents.TextLimit + 500);

        var kept = events.Append("a1b2c3", new SessionEvent
        {
            Kind = SessionEventKind.Tool,
            Id = "c1",
            Content = [new ToolContent("text", Text: huge)],
            Output = huge,
        });

        Assert.True(kept.Content![0].Text!.Length < huge.Length);
        Assert.EndsWith($"({huge.Length} chars)", kept.Content[0].Text);
        Assert.True(kept.Output!.Length <= SessionEvents.RawLimit + 40);
    }
}
