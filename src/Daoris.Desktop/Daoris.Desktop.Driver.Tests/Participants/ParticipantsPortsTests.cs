using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 mod-xii, the Participants cuts: the seams they add. Ask Daoris's room spelled once by Events, with every higher spelling
/// held to it; the harness conversation's record and file owned by Participants; the page's no and the chat run's bind taking the
/// events' and the agents' interfaces (and the terminal's no and the run's plugin catalogue taking the installed lower operations)
/// where they took the context's and the plugins' own types. What the cuts keep is <c>ParticipantsBoundaryTests</c>'.
/// </summary>
public sealed class ParticipantsPortsTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "participants-ports", Guid.NewGuid().ToString("N")[..8]);

    public ParticipantsPortsTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Remove(_home);

    [Fact]
    public void Ask_Daoris_s_room_is_spelled_once_by_Events_and_each_higher_spelling_holds_it()
    {
        Assert.Equal("daoris:help", AskDaorisRoom.HelpRepository);
        Assert.Equal(AskDaorisRoom.HelpRepository, HelpRoom.Repository);
        Assert.Equal(AskDaorisRoom.HelpRepository, KnowledgeRoomIdentity.Repository);
        Assert.Equal(AskDaorisRoom.HelpRepository, ParticipantWork.HelpRepository);
        Assert.Equal(ParticipantWork.AskDaoris, ParticipantWork.Of(new WorkMarks(false, "chat", AskDaorisRoom.HelpRepository, null, null, null, null)));
    }

    [Fact]
    public void A_conversation_file_is_the_two_fields_written_whole_and_read_back_by_the_store_s_own_files()
    {
        var store = new HarnessConversations(_home);
        var path = store.PathOf("a1b2c3d1");

        HarnessConversationFiles.Write(path, "claude-code-acp", "c-1");

        Assert.Equal(new HarnessConversation("claude-code-acp", "c-1"), HarnessConversationFiles.Read(path));
        Assert.Equal(new HarnessConversation("claude-code-acp", "c-1"), store.Read("a1b2c3d1"));
        Assert.Null(HarnessConversationFiles.Read(Path.Combine(_home, "gone.harness.json")));
        File.WriteAllText(path, "{\"adapter\":\"claude-code-acp\"}");
        Assert.Null(HarnessConversationFiles.Read(path));
    }

    [Fact]
    public void The_page_s_no_keeps_its_note_and_transcript_line_through_the_supplied_interfaces()
    {
        var files = Held("a1");
        var events = new Events();
        var output = new Output();

        var answer = HelperPersonWord.Decline(files, events, output, "s1", "a1");

        Assert.True(answer.Stands);
        var (session, kept) = Assert.Single(events.Kept);
        Assert.Equal(("s1", SessionEventKind.Note), (session, kept.Kind));
        Assert.Equal(["helper.refused"], NoteAssert.Codes(kept.Parts!));
        Assert.Equal(("s1", $"— {kept.Text}"), Assert.Single(output.Lines));
    }

    [Fact]
    public void The_terminal_s_no_opens_the_record_the_context_installs_at_the_home_s_sessions()
    {
        var record = WorkplaceEventRecords.Current.Open(DaorisHome.Entry(_home, HomeEntry.Sessions));

        Assert.IsType<SessionEvents>(record);
        var files = Held("a1");
        Assert.True(HelperPersonWord.Decline(files, record, null, null, "a1").Stands);
        Assert.Single(((SessionEvents)record).After("s1", 0).Events, each => each.Kind == SessionEventKind.Note);
    }

    [Fact]
    public void A_chat_run_bind_says_its_workflow_through_the_supplied_events_and_reads_the_catalogue_the_plugins_install()
    {
        WorkflowStore.Add(_home, new WorkflowVersionAdded("chat-steps", "A conversation's steps", "terminal", "2026-10-09T09:00:00Z",
            System.Text.Json.Nodes.JsonNode.Parse("""
                [{"id":"work","kind":"work","checks":"light"},
                 {"id":"landing","kind":"landing","form":"branch","accept":"you","pattern":"chat/{session}-{slug}","plugin":"none"}]
                """)), kept: []);
        var events = new Events();
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(new Unanswered()));

        new ChatRunBinds(_home, service, events).Opened(
            "c1a2b3c4", "engine", "aurora", DriverConfig.Parse("""{"workflows":{"engine":{"default":"chat-steps"}},"sessionChecks":{"engine":"light"}}"""));

        var (session, kept) = Assert.Single(events.Kept);
        Assert.Equal(("c1a2b3c4", SessionEventKind.Note), (session, kept.Kind));
        Assert.Equal("This conversation's work follows `chat-steps` v1, the workflow `engine` chose.", kept.Text);
        Assert.IsType<PluginCatalog>(WorkplacePlugins.Current.Load(_home));
    }

    private HelperFiles Held(string id)
    {
        var files = new HelperFiles(_home);
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Helpers));
        File.WriteAllText(files.PathOf(id, "ask"),
            $$"""{"id":"{{id}}","asker":"s1","question":"Why?","agent":null,"askedBy":"{{HelperAsk.ByAgent}}","at":"{{At:O}}"}""");
        files.WriteState(new HelperState(id, HelperStates.Waiting, null, null, At));
        return files;
    }

    private sealed class Events : ISessionEvents
    {
        public List<(string Session, SessionEvent Event)> Kept { get; } = [];

        public event Action<string, SessionEvent>? Evented { add { } remove { } }

        public void Keep(string sessionId, SessionEvent e, Action<string>? say) => Kept.Add((sessionId, e));
    }

    private sealed class Output : ISessionOutput
    {
        public List<(string Session, string Text)> Lines { get; } = [];

        public event Action<string, ConsoleLine>? Lined { add { } remove { } }

        public void Append(string sessionId, string text) => Lines.Add((sessionId, text));

        public ISessionStreams ForSession(string sessionId) => throw new NotSupportedException();
    }

    private sealed class Unanswered : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("the stand-in answers nothing");
    }
}
