using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// Ask Daoris's conversations, kept (ASKHIST1): each is a help record the host already keeps, its words this machine's own
/// record of it, and what the person adds to it (a name, a pin, the earlier conversation it started from) a file beside it
/// under the home. The list says each one's title, when, and a line of what it was about; a search finds them by words. Files
/// only, so this is the suite's fast half.
/// </summary>
public sealed class HelpConversationsTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-help-history-" + Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset Monday = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    public HelpConversationsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private SessionEvents Events => new(Path.Combine(_home, "sessions"));

    private static SessionRecord Help(string id, string state = "completed", DateTimeOffset? created = null) =>
        new(id, HelpRoom.Repository, state) { Kind = "chat", Created = created ?? Monday, Updated = created ?? Monday };

    /// <summary>A conversation as its record keeps it: the person's question, then the agent's answer in chunks.</summary>
    private void Said(string id, string question, params string[] answer) => SaidAt(Monday, id, question, answer);

    /// <summary>The same, every event stamped when it was said.</summary>
    private void SaidAt(DateTimeOffset at, string id, string question, params string[] answer)
    {
        var events = Events;
        events.Append(id, new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = question, At = at });
        foreach (var chunk in answer) events.Append(id, new SessionEvent { Kind = SessionEventKind.Message, Text = chunk, At = at });
        events.Append(id, new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn", At = at });
    }

    private static Func<string, bool> Resumes => adapter => adapter == "claude-code-acp";

    [Fact]
    public void A_conversation_is_listed_by_its_first_question_with_a_line_of_its_last_answer()
    {
        Said("h1", "how do I make engine land on a feature branch?\nand keep its tree?", "Repositories → engine → Setup.", "\nThen Line and landing.");

        var row = Assert.Single(new HelpConversations(_home).List([Help("h1")], Events, Resumes).Conversations);

        Assert.Equal("h1", row.Session);
        Assert.Equal("how do I make engine land on a feature branch?", row.Title);
        Assert.Null(row.Name);
        Assert.Equal("Repositories → engine → Setup.", row.About);
        Assert.Equal(Monday, row.Created);
        Assert.False(row.Live);
    }

    /// <summary>Only Ask Daoris's own records of this machine's, and only those the person spoke in: one opened ahead and never spoken in is no conversation.</summary>
    [Fact]
    public void Only_ask_daoris_conversations_the_person_spoke_in_are_listed()
    {
        Said("h1", "what is a workspace?", "A circle of repositories.");
        Said("c1", "fix the build", "Done.");

        var listed = new HelpConversations(_home).List(
            [
                Help("h1"),
                Help("h2"),
                new SessionRecord("c1", "engine", "completed") { Kind = "chat" },
                Help("laptop/h9"),
            ],
            Events, Resumes).Conversations;

        Assert.Equal(["h1"], listed.Select(row => row.Session));
    }

    /// <summary>Pinned first, the newest pin first; then the newest by when each was last spoken in, its record's last word.</summary>
    [Fact]
    public void Pinned_conversations_come_first_then_the_newest()
    {
        SaidAt(Monday, "h1", "question h1", "answer");
        SaidAt(Monday.AddDays(2), "h2", "question h2", "answer");
        SaidAt(Monday.AddDays(1), "h3", "question h3", "answer");
        SaidAt(Monday.AddDays(3), "h4", "question h4", "answer");
        var kept = new HelpConversations(_home);
        kept.Pin("h1", true, Monday.AddHours(1));
        kept.Pin("h3", true, Monday.AddHours(2));

        var listed = kept.List([Help("h1"), Help("h2"), Help("h3"), Help("h4")], Events, Resumes).Conversations;

        Assert.Equal(["h3", "h1", "h4", "h2"], listed.Select(row => row.Session));
        Assert.Equal(Monday.AddHours(2), listed[0].Pinned);
        Assert.Null(listed[2].Pinned);
        Assert.Equal(Monday.AddDays(3), listed[2].Last);
    }

    /// <summary>Unpinning puts it back among the rest by when it was last spoken in.</summary>
    [Fact]
    public void Unpinning_puts_a_conversation_back_among_the_rest()
    {
        SaidAt(Monday, "h1", "question h1", "answer");
        SaidAt(Monday.AddDays(1), "h2", "question h2", "answer");
        var kept = new HelpConversations(_home);
        kept.Pin("h1", true, Monday);
        kept.Pin("h1", false, Monday);

        Assert.Equal(["h2", "h1"], kept.List([Help("h1"), Help("h2")], Events, Resumes).Conversations.Select(row => row.Session));
        Assert.False(File.Exists(kept.PathOf("h1")));
    }

    /// <summary>A name the person gives is the title; clearing it gives the first question back.</summary>
    [Fact]
    public void A_name_is_the_title_until_it_is_cleared()
    {
        Said("h1", "what is a workspace?", "A circle.");
        var kept = new HelpConversations(_home);

        kept.Rename("h1", "  Workspaces, explained  ");
        var named = Assert.Single(kept.List([Help("h1")], Events, Resumes).Conversations);
        Assert.Equal(("Workspaces, explained", "Workspaces, explained"), (named.Name, named.Title));

        kept.Rename("h1", null);
        var cleared = Assert.Single(kept.List([Help("h1")], Events, Resumes).Conversations);
        Assert.Equal((null, "what is a workspace?"), (cleared.Name, cleared.Title));
        Assert.False(File.Exists(kept.PathOf("h1")), "nothing kept is no file");
    }

    /// <summary>A name is one line of the person's words: a line break or a name longer than a title is refused, in a sentence.</summary>
    [Theory]
    [InlineData("two\nlines")]
    [InlineData("a name far longer than any title in the history ought to be, so the list would cut it anyway, and more")]
    public void A_name_that_is_not_one_short_line_is_refused(string name)
    {
        var refused = Assert.Throws<DriverException>(() => new HelpConversations(_home).Rename("h1", name));
        Assert.Contains("name", refused.Message);
    }

    /// <summary>An id that names no session names no file: nothing under the home is reached through it.</summary>
    [Fact]
    public void An_id_that_is_not_a_sessions_is_refused()
    {
        Assert.Throws<DriverException>(() => new HelpConversations(_home).Rename("../escape", "x"));
        Assert.Throws<DriverException>(() => new HelpConversations(_home).Pin("../escape", true, Monday));
    }

    /// <summary>The file is Daoris's own JSON, BOM-less and LF, beside the conversation's other files under the home.</summary>
    [Fact]
    public void The_file_is_written_beside_the_sessions_files_as_the_home_writes_them()
    {
        var kept = new HelpConversations(_home);
        kept.Rename("h1", "Landing");
        kept.Pin("h1", true, Monday);

        Assert.Equal(Path.Combine(_home, "sessions", "h1.help.json"), kept.PathOf("h1"));
        var bytes = File.ReadAllBytes(kept.PathOf("h1"));
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF);
        var text = Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain("\r", text);
        using var document = JsonDocument.Parse(text);
        Assert.Equal("Landing", document.RootElement.GetProperty("name").GetString());
        Assert.Equal(new HelpKept("Landing", Monday, null, null), kept.Read("h1"));
    }

    /// <summary>A file that does not read is nothing kept: the conversation lists by its question, and the next write starts it again.</summary>
    [Fact]
    public void A_file_that_does_not_read_is_nothing_kept()
    {
        Said("h1", "what is a workspace?", "A circle.");
        var kept = new HelpConversations(_home);
        File.WriteAllText(kept.PathOf("h1"), "{ \"name\": ");

        Assert.Equal(HelpKept.None, kept.Read("h1"));
        Assert.Equal("what is a workspace?", Assert.Single(kept.List([Help("h1")], Events, Resumes).Conversations).Title);
    }

    /// <summary>
    /// Whether a conversation can go on in itself: its harness conversation's id kept, on an agent whose door resumes. One from
    /// before its id was kept is listed all the same, and offered a new conversation from its words instead.
    /// </summary>
    [Fact]
    public void A_conversation_goes_on_where_its_id_was_kept_on_a_door_that_resumes()
    {
        foreach (var id in new[] { "h1", "h2", "h3" }) Said(id, $"question {id}", "answer");
        var conversations = new HarnessConversations(_home);
        conversations.Keep("h1", "claude-code-acp", "conv-1");
        conversations.Keep("h3", "text-only", "conv-3");

        var listed = new HelpConversations(_home).List([Help("h1"), Help("h2"), Help("h3")], Events, Resumes).Conversations
            .ToDictionary(row => row.Session, row => row.Resumable);

        Assert.True(listed["h1"]);
        Assert.False(listed["h2"]);
        Assert.False(listed["h3"]);
    }

    /// <summary>A live one is listed live, and the earlier conversation one started from is said with what it was handed.</summary>
    [Fact]
    public void A_live_conversation_and_where_one_started_from_are_said()
    {
        Said("h1", "what is a workspace?", "A circle.");
        Said("h2", "and a remote?", "A shared host.");
        new HelpConversations(_home).StartedFrom("h2", "h1", HelpConversations.Transcript);

        var listed = new HelpConversations(_home).List([Help("h1"), Help("h2", "working")], Events, Resumes).Conversations
            .ToDictionary(row => row.Session);

        Assert.True(listed["h2"].Live);
        Assert.Equal(("h1", "transcript"), (listed["h2"].From, listed["h2"].Handed));
        Assert.Null(listed["h1"].From);
    }

    /// <summary>
    /// A search finds conversations by words: in a name, in a question, or in what either side said, each with a snippet of
    /// where; one that holds none of them is not listed. Case is ignored, as the rail's search ignores it.
    /// </summary>
    [Fact]
    public void A_search_finds_conversations_by_their_words()
    {
        Said("h1", "what is a workspace?", "A circle of repositories that share a remote.");
        Said("h2", "how do I land on a branch?", "Repositories → Setup → Line and landing.");
        Said("h3", "which agent runs intake?", "Settings → AI features.");
        new HelpConversations(_home).Rename("h3", "Intake agent");

        HelpListing Find(string words) => new HelpConversations(_home).List([Help("h1"), Help("h2"), Help("h3")], Events, Resumes, words);

        var remote = Find("REMOTE");
        Assert.Equal(["h1"], remote.Conversations.Select(row => row.Session));
        Assert.Contains("share a remote", remote.Conversations[0].Found);
        Assert.Equal(["h2"], Find("landing").Conversations.Select(row => row.Session));
        Assert.Equal(["h3"], Find("intake agent").Conversations.Select(row => row.Session));
        Assert.Empty(Find("nothing like this").Conversations);
    }

    /// <summary>A search of one letter finds nothing rather than everything, as the rail's search answers it.</summary>
    [Fact]
    public void A_search_too_short_to_mean_anything_finds_nothing()
    {
        Said("h1", "what is a workspace?", "A circle.");

        var found = new HelpConversations(_home).List([Help("h1")], Events, Resumes, " a ");
        Assert.Empty(found.Conversations);
        Assert.Equal((0, null, false), (found.Total, found.Next, found.Cut));
    }

    /// <summary>
    /// ASKHIST1d: one Han character is a word, so it is searched for, in the title and in what was said; one letter of any other
    /// script still finds nothing rather than nearly everything. The page said the minimum rather than search for 圈.
    /// </summary>
    [Fact]
    public void One_han_character_is_searched_for_and_one_other_letter_is_not()
    {
        Said("h1", "工作区是什么?", "一个仓库的圈子。");
        Said("h2", "what is a workspace?", "A circle.");
        HelpListing Find(string words) => new HelpConversations(_home).List([Help("h1"), Help("h2")], Events, Resumes, words);

        Assert.Equal(["h1"], Find("圈").Conversations.Select(row => row.Session));
        Assert.Equal("工作区是什么?", Assert.Single(Find(" 区 ").Conversations).Found);
        Assert.Empty(Find("a").Conversations);
        Assert.Empty(Find("é").Conversations);
    }

    /// <summary>Conversations h000 to h(n-1), each opened a minute after the one before, and spoken in as it was opened.</summary>
    private List<SessionRecord> Many(int count)
    {
        var records = new List<SessionRecord>();
        for (var at = 0; at < count; at++)
        {
            var id = $"h{at:000}";
            SaidAt(Monday.AddMinutes(at), id, $"question {id}", $"answer {id}");
            records.Add(Help(id, created: Monday.AddMinutes(at)));
        }

        return records;
    }

    /// <summary>
    /// ASKHIST1d: every conversation is ordered before a page is taken, so one pinned, or spoken in lately, comes first however
    /// long ago it was opened. The list took the newest 200 by when each was opened and only then read pins and last words, so
    /// both of these fell off it.
    /// </summary>
    [Fact]
    public void A_pinned_or_lately_spoken_conversation_comes_first_however_long_ago_it_was_opened()
    {
        var records = Many(250);
        SaidAt(Monday.AddDays(3), "h001", "and one more thing?", "Here.");
        var kept = new HelpConversations(_home);
        kept.Pin("h000", true, Monday.AddDays(1));

        var listing = kept.List(records, Events, Resumes);

        Assert.Equal(["h000", "h001", "h249", "h248"], listing.Conversations.Take(4).Select(row => row.Session));
        Assert.Equal(Monday.AddDays(3), listing.Conversations[1].Last);
        Assert.Equal("Here.", listing.Conversations[1].About);
        Assert.Equal(HelpConversations.PageLimit, listing.Conversations.Count);
        Assert.Equal((250, HelpConversations.PageLimit, true), (listing.Total, listing.Next, listing.Cut));
    }

    /// <summary>
    /// ASKHIST1d: pages asked one after the next hold every conversation once, in the list's order, and the last one says it is
    /// the last. Two spoken in at the same moment are ordered the same way every time, the newer opened first, so a page's edge
    /// never moves between asks.
    /// </summary>
    [Fact]
    public void Pages_hold_every_conversation_once_in_the_lists_order()
    {
        var records = Many(250);
        var kept = new HelpConversations(_home);
        kept.Pin("h010", true, Monday.AddDays(1));
        kept.Pin("h200", true, Monday.AddDays(2));
        SaidAt(Monday.AddDays(3), "h005", "again?", "Yes.");
        SaidAt(Monday.AddDays(3), "h006", "again?", "Yes.");

        var seen = new List<string>();
        var nexts = new List<int?>();
        int? offset = 0;
        while (offset is { } at)
        {
            var page = kept.List(records, Events, Resumes, offset: at, limit: 60);
            Assert.Equal(250, page.Total);
            Assert.Equal(page.Next is not null, page.Cut);
            seen.AddRange(page.Conversations.Select(row => row.Session));
            nexts.Add(page.Next);
            offset = page.Next;
        }

        string[] first = ["h200", "h010", "h006", "h005"];
        Assert.Equal(first.Concat(Enumerable.Range(0, 250).Reverse().Select(n => $"h{n:000}").Except(first)), seen);
        Assert.Equal([60, 120, 180, 240, null], nexts);

        var past = kept.List(records, Events, Resumes, offset: 300);
        Assert.Equal((0, 250, null), (past.Conversations.Count, past.Total, past.Next));
        Assert.Equal(HelpConversations.PageLimit, kept.List(records, Events, Resumes, limit: 0).Conversations.Count);
        Assert.Equal(HelpConversations.PageLimit, kept.List(records, Events, Resumes, limit: 5000).Conversations.Count);
    }

    /// <summary>
    /// ASKHIST1d: a search reads every conversation, not only the newest 200 the list once read, and its finds page as the list
    /// does, counted whole.
    /// </summary>
    [Fact]
    public void A_search_reads_every_conversation_and_pages_its_finds()
    {
        var records = Many(250);
        SaidAt(Monday, "h000", "and the needle?", "In the haystack.");
        var kept = new HelpConversations(_home);

        var needle = kept.List(records, Events, Resumes, "HAYSTACK");
        Assert.Equal("h000", Assert.Single(needle.Conversations).Session);
        Assert.Equal((1, null, false), (needle.Total, needle.Next, needle.Cut));

        var first = kept.List(records, Events, Resumes, "answer h0", limit: 60);
        var second = kept.List(records, Events, Resumes, "answer h0", offset: 60, limit: 60);
        Assert.Equal((100, 60, true), (first.Total, first.Next, first.Cut));
        Assert.Equal((100, null, false), (second.Total, second.Next, second.Cut));
        Assert.Equal(
            Enumerable.Range(0, 100).Reverse().Select(n => $"h{n:000}"),
            first.Conversations.Concat(second.Conversations).Select(row => row.Session));
    }

    /// <summary>
    /// ASKHIST1d: the line of what it was about is the answer's first line whole, so the page parses its Markdown before it cuts
    /// it to a row; the driver cut it at 160 characters first, and a delimiter could lose its partner. Only a line longer than
    /// any row shows is cut, and says so.
    /// </summary>
    [Fact]
    public void The_line_of_what_it_was_about_is_the_first_line_whole_for_the_page_to_parse()
    {
        var line = new string('w', 150) + " **the colour #d208d4** and [the guide](https://example.com/guide) after it";
        Said("h1", "what colour?", line + "\nSecond line.");
        Said("h2", "and a long one?", new string('x', HelpConversations.PreviewLimit + 50));

        var rows = new HelpConversations(_home).List([Help("h1"), Help("h2")], Events, Resumes).Conversations
            .ToDictionary(row => row.Session);

        Assert.Equal(line, rows["h1"].About);
        Assert.Equal(new string('x', HelpConversations.PreviewLimit) + "…", rows["h2"].About);
    }

    /// <summary>
    /// ASKHIST1d: where a search found its words in what was said, the row has the line they are on as Markdown, whole, for the
    /// page to parse before it cuts around them; the snippet the rows read until now is kept beside it as it was. A line longer
    /// than any row shows is a window around the words, cut between words and saying so. A find in the title has no line: the
    /// title is marked where it is.
    /// </summary>
    [Fact]
    public void A_search_gives_the_line_it_found_words_on_whole_beside_its_snippet()
    {
        const string held = "- **Remote** hosts share [records](https://example.com/a/rather/long/path/to/the/guide) with each other";
        var words = string.Join(' ', Enumerable.Repeat("word", 400));
        Said("h1", "what is a remote?", $"A shared host.\n{held}\nThat is all.");
        Said("h2", "and a long line?", $"{words} needle {words}");
        HelpConversation Find(string id, string search) =>
            Assert.Single(new HelpConversations(_home).List([Help(id)], Events, Resumes, search).Conversations);

        var line = Find("h1", "with each other");
        Assert.Equal(held, line.FoundLine);
        Assert.StartsWith("…", line.Found);
        Assert.DoesNotContain("\n", line.Found);

        var window = Find("h2", "needle").FoundLine!;
        Assert.Contains(" needle ", window);
        Assert.StartsWith("…word ", window);
        Assert.EndsWith(" word…", window);
        Assert.True(window.Length <= HelpConversations.PreviewLimit + 2, $"{window.Length}");

        var titled = Find("h1", "a remote");
        Assert.Equal("what is a remote?", titled.Found);
        Assert.Null(titled.FoundLine);
    }

    /// <summary>
    /// The earlier conversation's transcript, handed to a new one (ASKHIST1): the person's words and Ask Daoris's answers in
    /// order, its chunks joined, its title and id at the head; the tools it used and what Daoris told it are left out.
    /// </summary>
    [Fact]
    public void A_transcript_holds_both_sides_words_in_order_and_nothing_else()
    {
        Said("h1", "what is a workspace?", "A circle ", "of repositories.");
        var events = Events;
        events.Append("h1", new SessionEvent { Kind = SessionEventKind.Note, Text = "told where the person is: Sessions" });
        events.Append("h1", new SessionEvent { Kind = SessionEventKind.Tool, Id = "t1", Title = "knowledge_search", Status = "completed" });
        Said("h1", "and a remote?", "A shared host.");

        var transcript = HelpTranscript.Of(events, "h1", "what is a workspace?")!;

        Assert.StartsWith("# An earlier Ask Daoris conversation: what is a workspace?\n", transcript);
        Assert.Contains("`h1`", transcript);
        var person = transcript.IndexOf("## The person\n\nwhat is a workspace?", StringComparison.Ordinal);
        var answer = transcript.IndexOf("## Ask Daoris\n\nA circle of repositories.", StringComparison.Ordinal);
        var second = transcript.IndexOf("## The person\n\nand a remote?", StringComparison.Ordinal);
        Assert.True(person >= 0 && answer > person && second > answer, transcript);
        Assert.DoesNotContain("told where the person is", transcript);
        Assert.DoesNotContain("knowledge_search", transcript);
        Assert.DoesNotContain("\r", transcript);
    }

    /// <summary>A conversation longer than a file carries keeps its newest words, and says the start was left out.</summary>
    [Fact]
    public void A_transcript_too_long_keeps_its_newest_words_and_says_so()
    {
        Said("h1", "first question " + new string('x', 200), "first answer");
        Said("h1", "last question", "last answer");

        var transcript = HelpTranscript.Of(Events, "h1", "first", limit: 70)!;

        Assert.Contains("last answer", transcript);
        Assert.DoesNotContain("first answer", transcript);
        Assert.Contains("left out", transcript);
    }

    /// <summary>Nothing said is no transcript: a conversation nobody spoke in hands nothing on.</summary>
    [Fact]
    public void A_conversation_nobody_spoke_in_has_no_transcript()
    {
        Assert.Null(HelpTranscript.Of(Events, "h9", null));
    }
}
