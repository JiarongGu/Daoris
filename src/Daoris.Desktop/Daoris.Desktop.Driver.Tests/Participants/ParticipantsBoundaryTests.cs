using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 mod-xii, the Participants cuts (the five small families): the behaviour they keep, written against the surface that
/// exists on both sides of them (the harness conversation file as <c>HarnessConversations</c> reads it, the page's no to a helper's ask
/// with its transcript line and its note, a conversation's chat run bound under a named workflow, and the attention headline of Ask
/// Daoris's answer), so the same cases pass on the base and after it. The seams the cuts add are <c>ParticipantsPortsTests</c>'.
/// </summary>
public sealed class ParticipantsBoundaryTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "participants-boundary", Guid.NewGuid().ToString("N")[..8]);
    private string Sessions => DaorisHome.Entry(_home, HomeEntry.Sessions);

    public ParticipantsBoundaryTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Remove(_home);

    // ——— The harness conversation file, as the store reads it

    [Fact]
    public void A_conversation_file_reads_with_its_two_ids_trimmed_and_anything_less_reads_as_nothing()
    {
        Directory.CreateDirectory(Sessions);
        var store = new HarnessConversations(_home);
        Write(store, "a1b2c3d1", """{"adapter":" claude-code-acp ","conversation":" c-1 "}""");
        Write(store, "a1b2c3d2", """{"adapter":"claude-code-acp","conversation":"c-2","extra":true}""");
        Write(store, "a1b2c3d3", """["claude-code-acp","c-3"]""");
        Write(store, "a1b2c3d4", """{"adapter":"  ","conversation":"c-4"}""");
        Write(store, "a1b2c3d5", """{"adapter":"claude-code-acp"}""");
        Write(store, "a1b2c3d6", """{"adapter":"claude-code-acp","conversation":7}""");
        Write(store, "a1b2c3d7", "not json at all");

        Assert.Equal(new HarnessConversation("claude-code-acp", "c-1"), store.Read("a1b2c3d1"));
        Assert.Equal(new HarnessConversation("claude-code-acp", "c-2"), store.Read("a1b2c3d2"));
        foreach (var unread in new[] { "a1b2c3d3", "a1b2c3d4", "a1b2c3d5", "a1b2c3d6", "a1b2c3d7", "a1b2c3d8" })
        {
            Assert.Null(store.Read(unread));
        }
    }

    [Fact]
    public void A_kept_conversation_is_the_two_fields_written_whole_and_replaces_the_one_before()
    {
        var store = new HarnessConversations(_home);

        store.Keep("a1b2c3d1", "claude-code-acp", "c-1");
        store.Keep("a1b2c3d1", "codex-acp", "c-2");

        var bytes = File.ReadAllBytes(store.PathOf("a1b2c3d1"));
        Assert.NotEqual(0xEF, bytes[0]);
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain('\r', text);
        using var document = System.Text.Json.JsonDocument.Parse(text);
        Assert.Equal(["adapter", "conversation"], document.RootElement.EnumerateObject().Select(each => each.Name));
        Assert.Equal(new HarnessConversation("codex-acp", "c-2"), store.Read("a1b2c3d1"));
    }

    private void Write(HarnessConversations store, string session, string json) => File.WriteAllText(store.PathOf(session), json);

    // ——— The page's no to a helper's ask

    [Fact]
    public void A_no_keeps_the_refusal_on_the_ask_s_state_in_the_loop_s_transcript_and_on_the_asker_s_record()
    {
        var files = Held("a1", HelperStates.Waiting);
        var events = new SessionEvents(Sessions);
        var output = new SessionOutput();

        var answer = HelperPersonWord.Decline(files, events, output, "s1", "a1");

        Assert.Equal((true, (string?)null, true), (answer.Stands, answer.Why, answer.Wrote));
        Assert.Equal((HelperStates.Refused, HelperWhy.Declined), (files.ReadState("a1")!.State, files.ReadState("a1")!.Code));
        var note = Assert.Single(events.After("s1", 0).Events, each => each.Kind == SessionEventKind.Note);
        Assert.Equal(["helper.refused"], NoteAssert.Codes(note.Parts!));
        Assert.Equal($"— {note.Text}", Assert.Single(output.Tail("s1").Lines).Text);
    }

    [Fact]
    public void A_no_with_no_transcript_writes_the_record_alone_and_a_second_no_changes_nothing()
    {
        var files = Held("a1", HelperStates.Waiting);
        var events = new SessionEvents(Sessions);

        var first = HelperPersonWord.Decline(files, events, null, null, "a1");
        var second = HelperPersonWord.Decline(files, events, null, null, "a1");

        Assert.Equal((true, true), (first.Stands, first.Wrote));
        Assert.Equal((true, false, "a1 has your no."), (second.Stands, second.Wrote, second.Message.Replace("#", "")));
        Assert.Single(events.After("s1", 0).Events, each => each.Kind == SessionEventKind.Note);
    }

    private HelperFiles Held(string id, string state)
    {
        var files = new HelperFiles(_home);
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Helpers));
        File.WriteAllText(files.PathOf(id, "ask"),
            $$"""{"id":"{{id}}","asker":"s1","question":"Why?","agent":null,"askedBy":"{{HelperAsk.ByAgent}}","at":"{{At:O}}"}""");
        files.WriteState(new HelperState(id, state, null, null, At));
        return files;
    }

    // ——— A conversation's chat run, bound

    [Fact]
    public void A_conversation_s_chat_run_binds_under_the_named_workflow_and_says_so_once()
    {
        StoredSteps.AddGraph(WorkflowPorts.Store(_home), "chat-steps", "A conversation's steps", "2026-10-09T09:00:00Z",
            System.Text.Json.Nodes.JsonNode.Parse("""
                {"schemaVersion":2,"entries":["claimed"],"end":"landing",
                 "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed","options":{"checks":"light"}},
                          {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"branch","pattern":"chat/{session}-{slug}"}}],
                 "wires":[{"id":"c>l","kind":"after","from":"claimed","to":"landing"}]}
                """)!);
        var config = DriverConfig.Parse("""{"workflows":{"engine":{"default":"chat-steps"}},"sessionChecks":{"engine":"light"}}""");
        var events = new SessionEvents(Sessions);
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(new Unanswered()));
        var binds = new ChatRunBinds(_home, service, events);

        var process = binds.Opened("c1a2b3c4", "engine", "aurora", config);
        binds.Opened("c1a2b3c4", "engine", "aurora", config);

        Assert.Equal(("chat-steps", 1), (WorkflowRunBindings.Read(_home, "chat-c1a2b3c4")!.Workflow, WorkflowRunBindings.Read(_home, "chat-c1a2b3c4")!.Version));
        Assert.True(process.Named);
        Assert.Equal((LandingForm.Branch, "chat/{session}-{slug}"), (process.Landing.Form, process.Landing.Pattern));
        var note = Assert.Single(events.After("c1a2b3c4", 0).Events, each => each.Kind == SessionEventKind.Note);
        Assert.Equal("This conversation's work follows `chat-steps` v1, the workflow `engine` chose.", note.Text);
        Assert.Equal(ChatNoteCodes.ChatWorkflow.Code, Assert.Single(note.Parts!).Code);
    }

    [Fact]
    public void A_chat_run_under_current_binds_current_and_says_nothing()
    {
        var events = new SessionEvents(Sessions);
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(new Unanswered()));
        var binds = new ChatRunBinds(_home, service, events);

        var process = binds.Opened("d1a2b3c4", "engine", "aurora", DriverConfig.Parse("""{"sessionChecks":{"engine":"light"}}"""));

        Assert.True(WorkflowRunBindings.Read(_home, "chat-d1a2b3c4")!.IsCurrent);
        Assert.False(process.Named);
        Assert.DoesNotContain(events.After("d1a2b3c4", 0).Events, each => each.Kind == SessionEventKind.Note);
    }

    private sealed class Unanswered : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("the stand-in answers nothing");
    }

    // ——— The attention headline of Ask Daoris's answer, and the posture's mark

    [Fact]
    public void Ask_Daoris_s_answer_is_told_by_the_room_s_exact_spelling_alone()
    {
        Assert.Equal("Ask Daoris answered you", new AttentionEvent(AttentionKind.Answered, "h1", "daoris:help").Headline);
        Assert.Equal("DAORIS:HELP — a conversation answered you", new AttentionEvent(AttentionKind.Answered, "h1", "DAORIS:HELP").Headline);
        Assert.Equal("engine — a conversation answered you", new AttentionEvent(AttentionKind.Answered, "h1", "engine").Headline);
        Assert.Equal("daoris:help — a session needs you", new AttentionEvent(AttentionKind.Parked, "h1", "daoris:help").Headline);
    }

    [Theory]
    [InlineData(AccessRules.Asking)]
    [InlineData(AccessRules.Unrestricted)]
    public void An_asking_or_full_start_on_a_provider_with_no_mode_mapped_is_refused_naming_it(string permissions)
    {
        var access = AccessRules.ResolveLevels(
            new AccessSetting { Pauses = new Dictionary<string, string> { [AccessRules.Permissions] = permissions } }, null, null);
        var refused = DrivenAccess.For(access, "engine", new NoModes());
        Assert.NotNull(refused.Refusal);
        Assert.Contains("`no-modes`", refused.Refusal, StringComparison.Ordinal);
        Assert.Null(DrivenAccess.For(AccessRules.ResolveLevels(null, null, null), "engine", new NoModes()).Refusal);

        var help = HelpAccess.For(HelpModes.Full, new ChatAgent(new NoModes()));
        Assert.Contains("`no-modes`", help.Refusal, StringComparison.Ordinal);
    }

    private sealed class NoModes : ISessionAdapter
    {
        public string Name => "no-modes";
        public SessionWire Wire => SessionWire.Acp;
        public System.Diagnostics.ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new("no-modes");
    }

    [Fact]
    public void The_required_posture_is_the_one_value_the_protocol_door_reads()
    {
        Assert.Equal("!auto", new PostureAsked("auto", Required: true).Wire());
        Assert.Equal("!?auto", new PostureAsked("auto", Required: true, PersonAnswers: true).Wire());
        Assert.Equal(new PostureAsked("auto|acceptEdits", true, true), PostureAsked.Parse("?!auto|acceptEdits"));
        Assert.Equal(new PostureAsked("auto"), PostureAsked.Parse("auto"));
        Assert.Equal(new PostureAsked(""), PostureAsked.Parse(null));
        Assert.Equal("`auto` or `acceptEdits`", new PostureAsked("auto|acceptEdits").Spelled());
    }
}
