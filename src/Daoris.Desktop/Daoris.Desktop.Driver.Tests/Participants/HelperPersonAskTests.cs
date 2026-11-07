using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Talking to a helper is asking again. The person's ask names the ask it follows and where its
/// answer goes; one for the session keeps <c>helper.person-asked</c> on it, which the answers-alone count reads as the person's word; one
/// kept for the person keeps none, and reaches the session only by <i>Pass to the lead</i>, which marks the state for the look.
/// </summary>
public sealed class HelperPersonAskTests : IDisposable
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-06T10:00:00Z");
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-helper-person-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly Events _events = new();

    public HelperPersonAskTests() => Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Helpers));

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private HelperFiles Files => new(_home);

    /// <summary>An earlier ask of <c>s1</c>'s, answered by its helper's tool.</summary>
    private string Answered(string answer = "The window keeps its own copy.")
    {
        var id = Files.WriteAsk("s1", "Why does the list flicker?", "codex-acp", At);
        Files.WriteState(new HelperState(id, HelperStates.Answered, null, "h1", At) { Agent = "codex-acp" });
        File.WriteAllText(Files.PathOf(id, "answer"), $"{{ \"id\": \"{id}\", \"helper\": \"h1\", \"answer\": \"{answer}\", \"at\": \"2026-11-06T10:05:00Z\" }}");
        return id;
    }

    /// <summary>🔴 The person's ask for the session writes neither field and keeps `helper.person-asked` on it, at the ask.</summary>
    [Fact]
    public void An_ask_for_the_session_keeps_the_persons_word_on_it_and_writes_no_follow_up_fields()
    {
        var asked = HelperPersonAsk.Write(Files, _events, "s1", "Where is the cache?", null, after: null, toPerson: false, At);

        Assert.NotNull(asked.Asked);
        var text = File.ReadAllText(Files.PathOf(asked.Asked!, "ask"));
        Assert.DoesNotContain("\"after\"", text);
        Assert.DoesNotContain("\"to\"", text);
        var (session, note) = Assert.Single(_events.Kept);
        Assert.Equal("s1", session);
        Assert.Equal(["helper.person-asked"], NoteAssert.Codes(note.Parts!));
    }

    /// <summary>
    /// 🔴 *Ask again…* from a member's thread: `after` and `to: person` written after `askedBy`, read back as the reader holds them, and no note
    /// on the session, since the answer never reaches it by itself.
    /// </summary>
    [Fact]
    public void A_follow_up_for_the_person_names_the_ask_it_follows_and_keeps_no_note()
    {
        var earlier = Answered();

        var asked = HelperPersonAsk.Write(Files, _events, "s1", "And on resize?", null, after: $"#{earlier}", toPerson: true, At);

        Assert.Null(asked.Why);
        var text = File.ReadAllText(Files.PathOf(asked.Asked!, "ask"));
        Assert.Contains($"\"askedBy\": \"person\",\n  \"after\": \"{earlier}\",\n  \"to\": \"person\",\n  \"at\"", text);
        var read = Files.Asks().Single(ask => ask.Id == asked.Asked);
        Assert.Equal((earlier, HelperAsk.ToPerson, true), (read.After, read.To, read.ForPerson));
        Assert.Empty(_events.Kept);
    }

    /// <summary>🔴 An ask again after an ask that is not the session's, or that has not answered, is refused by its code, and nothing is written.</summary>
    [Fact]
    public void A_follow_up_after_another_sessions_ask_or_an_unanswered_one_is_refused()
    {
        var earlier = Answered();
        var open = Files.WriteAsk("s1", "Still reading?", null, At);

        var other = HelperPersonAsk.Write(Files, _events, "s2", "Again?", null, earlier, toPerson: true, At);
        var waiting = HelperPersonAsk.Write(Files, _events, "s1", "Again?", null, open, toPerson: true, At);

        Assert.Equal((null, HelperAskWhy.After), (other.Asked, other.Why));
        Assert.Equal((null, HelperAskWhy.Unanswered), (waiting.Asked, waiting.Why));
        Assert.Equal(2, Files.Asks().Count);
    }

    /// <summary>
    /// 🔴 *Pass to the lead*: an answer kept for the person is marked passed for the look, `handed` cleared so the look hands it; again, it
    /// stands with nothing written. An answer for the session already, or none yet, is refused by its code.
    /// </summary>
    [Fact]
    public void A_kept_answer_is_passed_once_and_one_for_the_session_or_unanswered_is_refused()
    {
        var earlier = Answered();
        var kept = HelperPersonAsk.Write(Files, _events, "s1", "And on resize?", null, earlier, toPerson: true, At).Asked!;
        Files.WriteState(new HelperState(kept, HelperStates.Answered, null, "h2", At) { Handed = HelperState.KeptForPerson });
        var unanswered = HelperPersonAsk.Write(Files, _events, "s1", "And on scroll?", null, earlier, toPerson: true, At).Asked!;

        var passed = HelperPassToLead.Pass(Files, "s1", kept, At);
        var again = HelperPassToLead.Pass(Files, "s1", kept, At);

        Assert.Equal((true, true), (passed.Stands, passed.Wrote));
        Assert.Equal((true, false), (again.Stands, again.Wrote));
        var state = Files.ReadState(kept)!;
        Assert.Equal((null, (DateTimeOffset?)At), (state.Handed, state.Passed));
        Assert.Equal(HelperAskWhy.Lead, HelperPassToLead.Pass(Files, "s1", earlier, At).Why);
        Assert.Equal(HelperAskWhy.Unanswered, HelperPassToLead.Pass(Files, "s1", unanswered, At).Why);
        Assert.Equal(HelperAskWhy.Gone, HelperPassToLead.Pass(Files, "s2", kept, At).Why);
    }

    /// <summary>🔴 `helper.person-asked` is the person's word on the line: the answer that follows is the first alone, not the third.</summary>
    [Fact]
    public void The_persons_ask_resets_the_answers_alone_count()
    {
        SessionEvent Helper(string id) => new() { Kind = SessionEventKind.User, Origin = "target", Helper = "h1", Id = id, Text = "Another agent answered." };
        IEnumerable<SessionEvent> Turn(params SessionEvent[] prompt) =>
            [.. prompt, new SessionEvent { Kind = SessionEventKind.Message, Text = "I went on." }, new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" }];
        var noted = HelperNotes.PersonAsked();
        var asked = new SessionEvent { Kind = SessionEventKind.Note, Text = noted.Note, Parts = noted.Parts };
        IEnumerable<SessionEvent> twice = [.. Turn(new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = "The instruction." }), .. Turn(Helper("w1")), .. Turn(Helper("w2"))];

        Assert.Equal(2, AnswersAlone.Count(twice));
        Assert.Equal(1, AnswersAlone.Count([.. twice, asked, .. Turn(Helper("w3"))]));
    }

    /// <summary>
    /// 🔴 A follow-up's helper is handed the ask it follows as Daoris's record, the answer cut at its bound, then the person's words; an answer
    /// kept for the person says it goes to them.
    /// </summary>
    [Fact]
    public void A_follow_ups_instruction_quotes_the_earlier_exchange_and_says_where_its_answer_goes()
    {
        var earlier = Answered(new string('a', HelperEarlier.Bound + 10));
        var asked = HelperPersonAsk.Write(Files, _events, "s1", "And on resize?", null, earlier, toPerson: true, At).Asked!;
        var follow = Files.Asks().Single(ask => ask.Id == asked);

        var record = Files.EarlierOf(follow)!;
        var text = HelperInstruction.Compose(new HelperPacket("s1", "core", "abc123", follow.Question, 15) { Earlier = record, ForPerson = true });

        Assert.Equal((earlier, true, HelperEarlier.Bound), (record.Id, record.Cut, record.Answer.Length));
        Assert.Contains("## What was asked before", text);
        Assert.Contains("> Why does the list flicker?", text);
        Assert.Contains($"cut at {HelperEarlier.Bound} characters", text);
        Assert.Contains("The person asks now", text);
        Assert.Contains("it goes to the person", text);
        Assert.True(text.IndexOf("Why does the list flicker?", StringComparison.Ordinal) < text.IndexOf("And on resize?", StringComparison.Ordinal));
        Assert.Null(Files.EarlierOf(Files.Asks().Single(ask => ask.Id == earlier)));
    }

    private sealed class Events : ISessionEvents
    {
        public List<(string Session, SessionEvent Event)> Kept { get; } = [];

        public event Action<string, SessionEvent>? Evented { add { } remove { } }

        public void Keep(string sessionId, SessionEvent e, Action<string>? say) => Kept.Add((sessionId, e));
    }
}
