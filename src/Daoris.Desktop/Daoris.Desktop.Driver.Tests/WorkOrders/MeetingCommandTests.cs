using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTROOM1a (D177 points 1, 2 and 7), ONEWAY1t: <c>daoris-driver meeting</c> at the terminal. <c>ask</c>, <c>send</c>, <c>stop</c> and
/// <c>clear</c> are presses of the running service's brainstorm desk, its meeting kind, which writes the records and says the sentence
/// the verb prints; <c>show</c> prints the round's packet whole, what a refreshed packet adds and the transcript, and <c>list</c> names
/// each, read here. The records sit under the brainstorms' entry, apart from them, and the brainstorm's verb never takes them for one.
/// </summary>
public sealed class MeetingCommandTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-meeting-verb-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly BrainstormDoorStandIn _service = new();

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-19T10:00:00Z");

    private const string Question = "How should the daily report stop dropping its last day?";

    private static readonly MeetingParticipant First = new("a1", "anthropic", "claude");

    private static readonly MeetingParticipant Second = new("a2", "openai", "codex");

    public MeetingCommandTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private MeetingFiles Files => new(_home);

    private (int Code, string Out) Run(params string[] args)
    {
        var output = new StringWriter();
        var code = MeetingCommand.Run(args, _home, output, At, _service);
        return (code, output.ToString());
    }

    /// <summary>A meeting the desk asked: its request on file, as the desk lays it.</summary>
    private string Asked() => Files.WriteRequest(Question, "reports", ["stub", "acp-stub"], At);

    /// <summary>The look's freeze of the first round, as the driver writes it: the packet and the state naming its digest.</summary>
    private MeetingPacket Freeze(string id)
    {
        var basis = BrainstormPackets.Freeze(
            Question, "reports", "words", "0123456789abcdef",
            [new BrainstormFound("decision", "docs/decisions/D7.md", 3, 9, "The window is exclusive of its end.", "daily report")],
            BrainstormBounds.First);
        var packet = MeetingPackets.Freeze(basis, [First, Second]);
        Files.WritePacket(id, 1, MeetingPackets.Bytes(packet));
        Files.WriteState(new MeetingState(id, BrainstormStates.Frozen, 1, packet.Digest, null, At));
        return packet;
    }

    /// <summary>The first round ended with both messages, and the second's packet frozen from them, as the driver leaves it.</summary>
    private MeetingPacket SecondRound(string id, MeetingPacket first)
    {
        MeetingMessage[] messages =
        [
            new(1, "a1", "anthropic", "claude", "Make the window inclusive of its end.", first.Digest),
            new(1, "a2", "openai", "codex", "Cache the report, and read the window once.", first.Digest),
        ];
        Files.WriteTranscript(id, new MeetingTranscript(messages, []));
        var second = MeetingPackets.Refresh(first, messages);
        Files.WritePacket(id, 2, MeetingPackets.Bytes(second));
        Files.WriteState(new MeetingState(id, BrainstormStates.Frozen, 2, second.Digest, null, At));
        return second;
    }

    [Fact]
    public void Ask_is_one_press_of_the_desks_meeting_kind_naming_its_agents_and_prints_the_desks_sentence()
    {
        _service.Answer = (_, _, _) => new ServiceReply(200, """{ "message": "Meeting `ab12cd34` asked of `stub`, `acp-stub`, in that order." }""");

        var (code, said) = Run("ask", Question, "--agents", "stub,acp-stub", "--repository", "reports", "--id", "weekly");

        Assert.Equal(0, code);
        var (verb, path, json) = Assert.Single(_service.Sent);
        Assert.Equal((ServiceVerb.Post, "/api/brainstorms"), (verb, path));
        var body = JsonDocument.Parse(json!).RootElement;
        Assert.Equal(("meeting", Question, "reports", "weekly"),
            (body.GetProperty("kind").GetString(), body.GetProperty("question").GetString(), body.GetProperty("repository").GetString(), body.GetProperty("id").GetString()));
        Assert.Equal(["stub", "acp-stub"], body.GetProperty("agents").EnumerateArray().Select(agent => agent.GetString()));
        Assert.Equal("Meeting `ab12cd34` asked of `stub`, `acp-stub`, in that order.", said.Trim());
        Assert.Empty(Files.Requests());
    }

    [Fact]
    public void A_refusal_of_the_desk_is_printed_as_its_sentence_and_is_exit_1()
    {
        _service.Answer = (_, _, _) => new ServiceReply(400, """{ "error": "A meeting names 2 to 4 different agents, each once; 1 were named. Nothing was asked." }""");

        var (code, said) = Run("ask", Question, "--agents", "claude");

        Assert.Equal(1, code);
        Assert.Contains("daoris-driver: A meeting names 2 to 4 different agents", said, StringComparison.Ordinal);
    }

    /// <summary>🔴 One writer: with no service the verb says so, and writes no file of its own.</summary>
    [Fact]
    public void With_no_service_the_verb_says_so_and_writes_nothing()
    {
        _service.Down = new DriverException("no DAORIS_SERVICE_URL");

        foreach (var line in new[] { new[] { "ask", Question, "--agents", "a,b" }, ["send", "m1"], ["stop", "m1"], ["clear", "m1"] })
        {
            var (code, said) = Run(line);
            Assert.Equal(1, code);
            Assert.Contains("Daoris is not running", said, StringComparison.Ordinal);
        }

        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.Brainstorms)));
    }

    [Theory]
    [InlineData]
    [InlineData("ask")]
    [InlineData("ask", "Why?")]
    [InlineData("ask", "--agents", "a,b")]
    [InlineData("ask", "Why?", "--agents")]
    [InlineData("ask", "Why?", "--agents", "a,b", "--source", "docs/a.md")]
    [InlineData("send")]
    [InlineData("send", "m1", "--round")]
    [InlineData("send", "m1", "--round", "two")]
    [InlineData("send", "m1", "--round", "0")]
    [InlineData("show")]
    [InlineData("Why?")]
    public void A_line_the_verb_does_not_read_is_its_usage_and_asks_the_service_nothing(params string[] args)
    {
        var (code, said) = Run(args);

        Assert.Equal(2, code);
        Assert.Contains("usage: daoris-driver meeting ask", said, StringComparison.Ordinal);
        Assert.Empty(_service.Sent);
    }

    [Fact]
    public void Send_names_the_round_the_person_typed_and_stop_and_clear_press_the_meeting_by_its_id()
    {
        Run("send", "weekly");
        Run("send", "weekly", "--round", "2");
        Run("stop", "weekly");
        Run("clear", "weekly");

        Assert.Equal(
            [
                (ServiceVerb.Post, "/api/brainstorms/weekly/send?kind=meeting"), (ServiceVerb.Post, "/api/brainstorms/weekly/send?kind=meeting&round=2"),
                (ServiceVerb.Post, "/api/brainstorms/weekly/stop?kind=meeting"), (ServiceVerb.Delete, "/api/brainstorms/weekly?kind=meeting"),
            ],
            _service.Sent.Select(each => (each.Verb, each.Path)));
    }

    [Fact]
    public void Show_before_the_look_says_it_waits_and_after_the_freeze_prints_the_packet_whole_with_its_participants()
    {
        var id = Asked();
        Assert.Contains("waits for Daoris's next look", Run("show", id).Out, StringComparison.Ordinal);

        var packet = Freeze(id);
        var (code, printed) = Run("show", id);

        Assert.Equal(0, code);
        Assert.Contains($"Meeting `{id}`, round 1, packet `{packet.Digest}`", printed, StringComparison.Ordinal);
        Assert.Contains($"> {Question}", printed, StringComparison.Ordinal);
        Assert.Contains("Participants, in their order: a1 `claude` (maker anthropic), a2 `codex` (maker openai).", printed, StringComparison.Ordinal);
        Assert.Contains("S1  decision  docs/decisions/D7.md:3-9", printed, StringComparison.Ordinal);
        Assert.Contains("    The window is exclusive of its end.", printed, StringComparison.Ordinal);
        Assert.Contains($"Nothing has gone to any agent. Send it with `daoris-driver meeting send {id}`", printed, StringComparison.Ordinal);
        Assert.Equal(1, Run("show", "nothing-here").Code);
        Assert.Empty(_service.Sent);
    }

    [Fact]
    public void Show_says_where_a_later_round_is_sent_and_where_a_stop_stands()
    {
        var id = Asked();
        var first = Freeze(id);
        Files.WriteSent(id, 1, first.Digest, At);
        Assert.Contains("Sent: round 1 goes at Daoris's next look.", Run("show", id).Out, StringComparison.Ordinal);

        SecondRound(id, first);
        Assert.Contains($"Round 2 has not gone to any agent. Send it with `daoris-driver meeting send {id} --round 2`", Run("show", id).Out, StringComparison.Ordinal);

        Files.WriteStop(id, At);
        Assert.Contains("Stopped: nothing more goes to any agent", Run("show", id).Out, StringComparison.Ordinal);
    }

    [Fact]
    public void Show_of_a_refreshed_packet_says_what_it_adds_beyond_the_round_the_person_sent_and_the_transcript_with_its_authors()
    {
        var id = Asked();
        var first = Freeze(id);
        var second = SecondRound(id, first);

        var printed = Run("show", id).Out;

        Assert.Contains($"Meeting `{id}`, round 2, packet `{second.Digest}`", printed, StringComparison.Ordinal);
        Assert.Contains("Round 2's packet adds to round 1's, which you sent: 2 messages of round 1, another maker's words to each participant "
            + "that did not say them. Its question, its sources and its participants are as they were.", printed, StringComparison.Ordinal);
        Assert.Contains("[round 1] a1 `claude` (maker anthropic):\n    Make the window inclusive of its end.", printed.ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.Contains("[round 1] a2 `codex` (maker openai):\n    Cache the report, and read the window once.", printed.ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.Contains($"Send it with `daoris-driver meeting send {id} --round 2`", printed, StringComparison.Ordinal);
    }

    [Fact]
    public void List_names_each_meeting_with_where_it_stands()
    {
        Assert.Contains("No meeting on this machine.", Run("list").Out, StringComparison.Ordinal);
        var id = Asked();
        Assert.Contains($"{id}  asked  {Question}", Run("list").Out, StringComparison.Ordinal);
        var first = Freeze(id);
        Assert.Contains($"{id}  frozen, round 1  {Question}", Run("list").Out, StringComparison.Ordinal);

        Files.WriteState(new MeetingState(id, BrainstormStates.Ended, 1, first.Digest, BrainstormStates.Stopped, At));
        Assert.Contains($"{id}  ended (stopped), round 1", Run("list").Out, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 A meeting is no brainstorm: the brainstorm's list never names it, its look's reader never takes it, and the folder that
    /// holds every meeting is no brainstorm's id, so a brainstorm's show of <c>meetings</c> reads nothing.
    /// </summary>
    [Fact]
    public void The_brainstorm_verb_never_takes_a_meeting_or_the_folder_that_holds_them()
    {
        var id = Asked();
        Freeze(id);
        var brainstorm = (string[] args) =>
        {
            var output = new StringWriter();
            return (BrainstormCommand.Run(args, _home, output, At, _service), output.ToString());
        };

        Assert.Empty(new BrainstormFiles(_home).Requests());
        Assert.Contains("No brainstorm on this machine.", brainstorm(["list"]).Item2, StringComparison.Ordinal);
        Assert.Equal(1, brainstorm(["show", MeetingFiles.Under]).Item1);
        Assert.True(File.Exists(Path.Combine(Files.Folder(id), "request.json")));
    }

    [Fact]
    public void The_host_wires_the_verb_and_its_usage_names_it()
    {
        var usage = DriverCommand.Usage.ReplaceLineEndings("\n");
        Assert.Contains("\n  meeting ask \"<question>\" --agents <a,b> [--repository <name>] [--id <id>]  ·  meeting show|stop|clear <id>\n", usage);
        Assert.Contains("\n  meeting send <id> [--round <n>]  ·  meeting list\n", usage);

        var program = File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Host", "Program.cs"));
        Assert.Contains("if (args is [\"meeting\", .. var meetingArgs])", program, StringComparison.Ordinal);
        Assert.Contains("MeetingCommand.Run(meetingArgs, ", program, StringComparison.Ordinal);
    }
}
