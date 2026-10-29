using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTROOM1a (D177 points 1, 2 and 7): <c>daoris-driver meeting</c> at the terminal. <c>ask</c> writes a request naming its
/// agents in order, which the driver's look freezes; <c>show</c> prints the round's packet whole, what a refreshed packet adds and
/// the transcript; <c>send</c> is the person's press for the first round, and for a later round only when they name it; <c>stop</c>
/// ends the meeting and a later send is refused; <c>clear</c> and <c>list</c> as the brainstorm's. Files only: no service, no process.
/// The records sit under the brainstorms' entry, apart from them, and the brainstorm's verb never takes them for one.
/// </summary>
public sealed class MeetingCommandTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-meeting-verb-" + Guid.NewGuid().ToString("N")[..8]);

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
        var code = MeetingCommand.Run(args, _home, output, At);
        return (code, output.ToString());
    }

    private string Asked() => Assert.Single(Files.Requests()).Id;

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
    public void Ask_writes_a_request_naming_its_agents_in_order_under_the_brainstorms_entry_and_says_the_next_look_freezes_it()
    {
        var (code, said) = Run("ask", Question, "--agents", "claude,codex", "--repository", "reports");

        Assert.Equal(0, code);
        var request = Assert.Single(Files.Requests());
        Assert.Equal((Question, "reports"), (request.Question, request.Repository));
        Assert.Equal(["claude", "codex"], request.Agents);
        Assert.Contains(request.Id, said, StringComparison.Ordinal);
        Assert.Contains("next look", said, StringComparison.Ordinal);
        Assert.Equal(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Brainstorms), "meetings", request.Id), Files.Folder(request.Id));
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("claude,Claude")]
    [InlineData("a,b,c,d,e")]
    public void Ask_refuses_a_meeting_of_fewer_than_two_agents_or_more_than_four(string agents)
    {
        var (code, said) = Run("ask", Question, "--agents", agents);

        Assert.Equal(1, code);
        Assert.Contains("2 to 4 different agents", said, StringComparison.Ordinal);
        Assert.Empty(Files.Requests());
    }

    [Theory]
    [InlineData]
    [InlineData("ask")]
    [InlineData("ask", "Why?")]
    [InlineData("ask", "--agents", "a,b")]
    [InlineData("ask", "Why?", "--agents")]
    [InlineData("ask", "Why?", "--agents", "a,b", "--id", "../other")]
    [InlineData("send")]
    [InlineData("send", "m1", "--round")]
    [InlineData("send", "m1", "--round", "two")]
    [InlineData("send", "m1", "--round", "0")]
    [InlineData("show")]
    [InlineData("Why?")]
    public void A_line_the_verb_does_not_read_is_its_usage(params string[] args)
    {
        var (code, said) = Run(args);

        Assert.Equal(2, code);
        Assert.Contains("usage: daoris-driver meeting ask", said, StringComparison.Ordinal);
        Assert.Empty(Files.Requests());
    }

    [Fact]
    public void An_ask_is_idempotent_by_its_id()
    {
        Assert.Equal(0, Run("ask", Question, "--agents", "claude,codex", "--id", "daily-report").Code);
        var (code, said) = Run("ask", Question, "--agents", "claude,codex", "--id", "daily-report");

        Assert.Equal(0, code);
        Assert.Contains("the same meeting, never a second", said, StringComparison.Ordinal);
        Assert.Equal("daily-report", Asked());
        Assert.Equal(1, Run("ask", "Why is the report slow?", "--agents", "claude,codex", "--id", "daily-report").Code);
        Assert.Equal(1, Run("ask", Question, "--agents", "codex,claude", "--id", "daily-report").Code);
        Assert.Single(Files.Requests());
    }

    [Fact]
    public void Show_before_the_look_says_it_waits_and_after_the_freeze_prints_the_packet_whole_with_its_participants()
    {
        Run("ask", Question, "--agents", "claude,codex", "--repository", "reports");
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
    }

    [Fact]
    public void Send_sends_the_first_round_s_packet_once_and_a_later_round_only_when_the_person_names_it()
    {
        Run("ask", Question, "--agents", "claude,codex");
        var id = Asked();
        Assert.Equal(1, Run("send", id).Code);
        var first = Freeze(id);

        Assert.Equal(0, Run("send", id).Code);
        Assert.Equal(first.Digest, Files.Sent(id, 1));
        Assert.Contains("never a second spend", Run("send", id).Out, StringComparison.Ordinal);

        var second = SecondRound(id, first);
        var (unnamed, said) = Run("send", id);
        Assert.Equal(1, unnamed);
        Assert.Contains("--round 2", said, StringComparison.Ordinal);
        Assert.Equal(1, Run("send", id, "--round", "3").Code);
        Assert.Null(Files.Sent(id, 2));

        Assert.Equal(0, Run("send", id, "--round", "2").Code);
        Assert.Equal(second.Digest, Files.Sent(id, 2));
    }

    [Fact]
    public void A_later_round_is_refused_while_a_round_runs_and_any_round_after_a_stop()
    {
        Run("ask", Question, "--agents", "claude,codex");
        var id = Asked();
        var first = Freeze(id);
        Files.WriteSent(id, 1, first.Digest, At);
        Files.WriteState(new MeetingState(id, BrainstormStates.Running, 1, first.Digest, null, At));

        var (running, said) = Run("send", id, "--round", "2");
        Assert.Equal(1, running);
        Assert.Contains("round 1 is running", said, StringComparison.Ordinal);
        Assert.Equal(0, Run("send", id).Code);

        SecondRound(id, first);
        Assert.Equal(0, Run("stop", id).Code);
        Assert.True(Files.Stopped(id));
        var (stopped, why) = Run("send", id, "--round", "2");
        Assert.Equal(1, stopped);
        Assert.Contains("stopped", why, StringComparison.Ordinal);
        Assert.Null(Files.Sent(id, 2));
    }

    [Fact]
    public void Show_of_a_refreshed_packet_says_what_it_adds_beyond_the_round_the_person_sent_and_the_transcript_with_its_authors()
    {
        Run("ask", Question, "--agents", "claude,codex");
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
    public void List_names_each_meeting_stop_asks_it_to_end_and_clear_takes_its_records_never_while_it_runs()
    {
        Assert.Contains("No meeting on this machine.", Run("list").Out, StringComparison.Ordinal);
        Run("ask", Question, "--agents", "claude,codex");
        var id = Asked();
        Assert.Contains($"{id}  asked  {Question}", Run("list").Out, StringComparison.Ordinal);
        var first = Freeze(id);
        Assert.Contains($"{id}  frozen, round 1  {Question}", Run("list").Out, StringComparison.Ordinal);

        Files.WriteState(new MeetingState(id, BrainstormStates.Running, 1, first.Digest, null, At));
        Assert.Equal(1, Run("clear", id).Code);
        Assert.True(Directory.Exists(Files.Folder(id)));

        Files.WriteState(new MeetingState(id, BrainstormStates.Ended, 1, first.Digest, BrainstormStates.Stopped, At));
        Assert.Contains("already ended", Run("stop", id).Out, StringComparison.Ordinal);
        Assert.Equal(0, Run("clear", id).Code);
        Assert.False(Directory.Exists(Files.Folder(id)));
        Assert.Equal(1, Run("stop", id).Code);
        Assert.Equal(1, Run("clear", id).Code);
    }

    /// <summary>
    /// 🔴 A meeting is no brainstorm: the brainstorm's list never names it, its look's reader never takes it, and the folder that
    /// holds every meeting is no brainstorm's id, so <c>brainstorm clear meetings</c> takes nothing.
    /// </summary>
    [Fact]
    public void The_brainstorm_verb_never_takes_a_meeting_or_the_folder_that_holds_them()
    {
        Run("ask", Question, "--agents", "claude,codex");
        var id = Asked();
        Freeze(id);
        var brainstorm = (string[] args) =>
        {
            var output = new StringWriter();
            return (BrainstormCommand.Run(args, _home, output, At), output.ToString());
        };

        Assert.Empty(new BrainstormFiles(_home).Requests());
        Assert.Contains("No brainstorm on this machine.", brainstorm(["list"]).Item2, StringComparison.Ordinal);
        Assert.Equal(1, brainstorm(["clear", MeetingFiles.Under]).Item1);
        Assert.Equal(1, brainstorm(["show", MeetingFiles.Under]).Item1);
        Assert.Equal(1, brainstorm(["stop", MeetingFiles.Under]).Item1);
        Assert.Equal(2, brainstorm([Question, "--id", MeetingFiles.Under]).Item1);
        Assert.True(File.Exists(Path.Combine(Files.Folder(id), "request.json")));
        Assert.False(File.Exists(Path.Combine(Files.Folder(id), "..", "stop.json")));
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
