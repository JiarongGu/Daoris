using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The native door's half of an answer continuing its session (ANSWER1a, D131 §1): the conversation's id read off the
/// harness's own <c>system</c>/<c>init</c> line, and <c>claude -p &lt;answer&gt; --resume &lt;id&gt;</c> under the posture a
/// start has. 🔴 Both are the maker's published shapes (<c>SDKSystemMessage</c> in <c>@anthropic-ai/claude-agent-sdk</c>
/// 0.3.284, and the CLI's <c>--resume</c>), not yet a run on this machine. No process, so the fast half.
/// </summary>
public sealed class NativeResumeTests
{
    private static SessionTarget Target() => new(
        QuestId: "abc123",
        Title: "Fix the flaky gate",
        Body: "It fails one run in five; the log is attached.",
        Asker: "Platform",
        Repository: "Game",
        Root: "D:/trees/s-1",
        ServiceUrl: "http://localhost:5177");

    [Fact]
    public void A_resume_runs_the_answer_as_the_next_turn_of_the_kept_conversation()
    {
        var info = new ClaudeCodeAdapter().PrepareResume(Target(), ["claude"], "0b5e7c1a", "Port 8080.")!;

        var arguments = info.ArgumentList.ToList();
        Assert.Equal("claude", info.FileName);
        Assert.Equal("Port 8080.", arguments[arguments.IndexOf("-p") + 1]);
        Assert.Equal("0b5e7c1a", arguments[arguments.IndexOf("--resume") + 1]);
        Assert.Equal("acceptEdits", arguments[arguments.IndexOf("--permission-mode") + 1]);
        Assert.Equal("stream-json", arguments[arguments.IndexOf("--output-format") + 1]);
        Assert.Contains("--verbose", arguments);
        Assert.Equal("D:/trees/s-1", info.WorkingDirectory);
        // The composed target is not sent again: the conversation already holds it.
        Assert.DoesNotContain(arguments, argument => argument.Contains("Fix the flaky gate"));
    }

    /// <summary>The same door as a start, so the settings and servers flags a start is handed go on it the same way.</summary>
    [Fact]
    public void A_resume_takes_the_settings_and_servers_flags_as_a_start_does()
    {
        var adapter = new ClaudeCodeAdapter();
        var info = adapter.PrepareResume(Target(), ["claude"], "0b5e7c1a", "Port 8080.")!;

        adapter.HandSettings(info, "D:/daoris-data/sessions/s1.settings.json");
        adapter.HandServers(info, "D:/daoris-data/sessions/s1.mcp.json");

        Assert.Contains("--settings", info.ArgumentList);
        Assert.Contains("--mcp-config", info.ArgumentList);
    }

    /// <summary>Which doors can resume: the native Claude Code door, every protocol door (its agent says), never the pipe stub.</summary>
    [Fact]
    public void Which_doors_can_resume()
    {
        var adapters = AdapterSet.Built();

        Assert.True(adapters.Resolve("claude-code").Resumes);
        Assert.True(adapters.Resolve("claude-code-acp").Resumes);
        Assert.True(adapters.Resolve("acp-stub").Resumes);
        Assert.False(adapters.Resolve("stub").Resumes);
        Assert.Null(adapters.Resolve("stub").PrepareResume(Target(), ["node", "stub.mjs"], "abc", "go on"));
    }

    /// <summary>The conversation's id is the <c>init</c> line's <c>session_id</c>, read without a line rendered for it.</summary>
    [Fact]
    public void The_init_line_names_the_conversation()
    {
        var mapper = new ClaudeStreamJson();
        Assert.Null(mapper.Conversation);

        var mapped = mapper.Read("""{"type":"system","subtype":"init","session_id":"0b5e7c1a-1f6e","cwd":"D:/trees/s-1","tools":[],"model":"x"}""");

        Assert.Equal("0b5e7c1a-1f6e", mapper.Conversation);
        Assert.Empty(mapped.Lines);
        Assert.Empty(mapped.Events);
    }

    /// <summary>A status line and an init line with no id name nothing; the first id said is kept for the run.</summary>
    [Fact]
    public void Only_an_init_line_with_an_id_names_the_conversation()
    {
        var mapper = new ClaudeStreamJson();

        mapper.Read("""{"type":"system","subtype":"status","session_id":"not-this"}""");
        mapper.Read("""{"type":"system","subtype":"init","session_id":7}""");
        Assert.Null(mapper.Conversation);

        mapper.Read("""{"type":"system","subtype":"init","session_id":"first"}""");
        mapper.Read("""{"type":"system","subtype":"init","session_id":"second"}""");
        Assert.Equal("first", mapper.Conversation);
    }

    /// <summary>
    /// 🔴 Words held while a native run worked go on in its own conversation before the record concludes (MSG1b, D137 §2.1):
    /// <c>claude -p &lt;words&gt; --resume &lt;kept id&gt;</c>, under the rules and servers the run before it was handed.
    /// </summary>
    [Fact]
    public void Words_held_while_it_worked_resume_its_own_conversation_under_the_same_rules()
    {
        var info = Daoris.Driver.Driver.GoOnStart(
            new ClaudeCodeAdapter(), Target(), ["claude"], "0b5e7c1a", "Also log the port.",
            settings: "D:/daoris-data/sessions/s1.settings.json", servers: "D:/daoris-data/sessions/s1.mcp.json")!;

        var arguments = info.ArgumentList.ToList();
        Assert.Equal("Also log the port.", arguments[arguments.IndexOf("-p") + 1]);
        Assert.Equal("0b5e7c1a", arguments[arguments.IndexOf("--resume") + 1]);
        Assert.Equal("D:/daoris-data/sessions/s1.settings.json", arguments[arguments.IndexOf("--settings") + 1]);
        Assert.Equal("D:/daoris-data/sessions/s1.mcp.json", arguments[arguments.IndexOf("--mcp-config") + 1]);
        Assert.DoesNotContain(arguments, argument => argument.Contains("Fix the flaky gate"));
    }

    /// <summary>
    /// XAGENT1e (D155 point 7, design §6.3): another agent's findings waiting on an ended record go on in the session's own
    /// conversation on the native door as the one argument it takes, in Daoris's fixed words exactly as the host composed
    /// them, after the person's word and a blank line, never the target again.
    /// </summary>
    [Fact]
    public void Another_agent_s_findings_resume_the_kept_conversation_unchanged()
    {
        const string Findings =
            "Another agent, Codex by OpenAI, read your work at `2222222` and claims what follows. These are its claims, not the "
            + "person's words and not facts.\n\nFinding 1 (must, sure), at `src/report.ts:42`: The window's end is \"exclusive\".";
        var resume = new ResumeAsk(
            "0b5e7c1a",
            [
                new SaidWordView("w1", "Also log the port.", DateTimeOffset.UnixEpoch, [], Reopens: true),
                new SaidWordView("w2", Findings, DateTimeOffset.UnixEpoch, [], Reopens: true) { By = "op1" },
            ],
            Continuations.Opening("claude-code", null, null, answer: false, findings: true, persons: true));

        var info = new ClaudeCodeAdapter().PrepareResume(Target(), ["claude"], resume.Conversation, resume.Prompt)!;

        var arguments = info.ArgumentList.ToList();
        Assert.Equal("Also log the port.\n\n" + Findings, arguments[arguments.IndexOf("-p") + 1]);
        Assert.Equal("0b5e7c1a", arguments[arguments.IndexOf("--resume") + 1]);
        Assert.DoesNotContain(arguments, argument => argument.Contains("Fix the flaky gate"));
        // The record shows them as Daoris's turn around another agent's claims, never the person's words.
        Assert.Equal(["person", "target"], resume.Opening().Skip(1).Select(e => e.Origin));
    }

    /// <summary>A door that cannot resume has no run to go on in: it holds no words, so nothing waits on it.</summary>
    [Fact]
    public void A_door_that_cannot_resume_goes_on_with_nothing()
    {
        Assert.Null(Daoris.Driver.Driver.GoOnStart(AdapterSet.Built().Resolve("stub"), Target(), ["node", "stub.mjs"], "abc", "go on", null, null));
    }

    /// <summary>
    /// Every word held goes in one prompt, in the order said, joined by a blank line on the native door's argument (D137
    /// §2.2), each with where its files are kept.
    /// </summary>
    [Fact]
    public void The_words_held_are_one_argument_joined_by_a_blank_line()
    {
        var prompt = NativeWords.Prompt(
        [
            new ChatMessage("Also log the port.", []),
            new ChatMessage("And read this.", [new KeptFile("trace.txt", "D:/daoris-data/attachments/trace.txt")]),
        ]);

        Assert.StartsWith("Also log the port.\n\nAnd read this.", prompt, StringComparison.Ordinal);
        Assert.Contains("D:/daoris-data/attachments/trace.txt", prompt);
    }

    /// <summary>
    /// The run that takes them opens with the driver's line, then the same words under the ids they were shown with while
    /// they waited (D137 §3.1), the person's, so the page pairs what waited with where it was taken.
    /// </summary>
    [Fact]
    public void The_run_that_takes_them_says_them_again_under_their_ids()
    {
        var opening = NativeWords.Opening(
            "claude-code", [new ChatMessage("Also log the port.", []) { Id = "said-1" }, new ChatMessage("And 9090.", []) { Id = "said-2" }]);

        Assert.Equal(SessionEventKind.Note, opening[0].Kind);
        Assert.Contains("resumed on `claude-code`", opening[0].Text);
        Assert.Equal(
            [("said-1", "Also log the port."), ("said-2", "And 9090.")],
            opening.Skip(1).Select(e => (e.Id, e.Text)));
        Assert.All(opening.Skip(1), e => Assert.Equal(("user", "person"), (e.Kind, e.Origin)));
    }

    /// <summary>
    /// A native run holds the person's words for its turn's end and nothing else: no turn can be stopped and the session
    /// kept on this door, so *Send now* stops nothing, and the run takes every word held at once.
    /// </summary>
    [Fact]
    public void A_native_runs_inbox_holds_words_for_its_end_and_hands_them_over_together()
    {
        var inbox = new DrivenInbox(_ => { });
        var told = new List<DrivenReach>();
        inbox.OnSaid((_, reach) => told.Add(reach));
        inbox.Attach(interrupt: null);

        Assert.True(inbox.Hold(new ChatMessage("Also log the port.", [])));
        Assert.True(inbox.Hold(new ChatMessage("And 9090.", [])));

        Assert.Equal(TurnStop.Nothing, inbox.SendNowAsync().GetAwaiter().GetResult());
        Assert.Equal([DrivenReach.TurnEnd, DrivenReach.TurnEnd], told);
        Assert.Equal(["Also log the port.", "And 9090."], inbox.TakeAllOrClose().Select(word => word.Text));
        Assert.Empty(inbox.TakeAllOrClose());
        // Taking nothing closed it: a word said as the run winds up is refused, for the record's door to keep (MSG1d).
        Assert.False(inbox.Hold(new ChatMessage("Too late.", [])));
    }

    /// <summary>
    /// 🔴 A conversation that goes on, on the native door (MSG1c, D137 §4.2): its own conversation resumed by the id it kept,
    /// <c>--resume &lt;id&gt;</c> beside the structured stdin a conversation always has, so the words go as its first message.
    /// Never <c>--continue</c>, which takes the newest conversation in the folder.
    /// </summary>
    [Fact]
    public void A_conversation_that_goes_on_resumes_its_kept_conversation_on_the_native_door()
    {
        var target = new ChatTarget("Game", "D:/trees/chat-1", "http://localhost:5177") { Resume = "0b5e7c1a" };

        var arguments = new ClaudeCodeAdapter().PrepareChat(target, ["claude"]).ArgumentList.ToList();

        Assert.Equal("0b5e7c1a", arguments[arguments.IndexOf("--resume") + 1]);
        Assert.Equal("stream-json", arguments[arguments.IndexOf("--input-format") + 1]);
        Assert.Equal("acceptEdits", arguments[arguments.IndexOf("--permission-mode") + 1]);
        Assert.DoesNotContain("--continue", arguments);
        Assert.DoesNotContain("-c", arguments);
    }

    /// <summary>A new conversation resumes nothing; and a protocol door resumes on its wire, so its spawn never carries the flag.</summary>
    [Fact]
    public void A_new_conversation_and_a_protocol_door_carry_no_resume_flag()
    {
        var fresh = new ChatTarget("Game", "D:/trees/chat-1", "http://localhost:5177");
        var going = fresh with { Resume = "0b5e7c1a" };

        Assert.DoesNotContain("--resume", new ClaudeCodeAdapter().PrepareChat(fresh, ["claude"]).ArgumentList);
        Assert.DoesNotContain("--resume", new ClaudeAcpAdapter().PrepareChat(going, ["claude-agent-acp"]).ArgumentList);
        Assert.DoesNotContain("--resume", new AcpStubAdapter().PrepareChat(going, ["node", "agent.mjs"]).ArgumentList);
    }

    /// <summary>
    /// The native capture tells the conversation's id the moment its <c>init</c> line names it (MSG1c), once, so a
    /// conversation that lives for hours keeps it before it ends, as the protocol door keeps <c>session/new</c>'s.
    /// </summary>
    [Fact]
    public async Task The_native_capture_tells_the_conversation_once_as_the_init_line_names_it()
    {
        var folder = Path.Combine(Path.GetTempPath(), "daoris-native-named-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        try
        {
            var told = new List<string>();
            var stdout = new StringReader(string.Join('\n',
                """{"type":"system","subtype":"init","session_id":"0b5e7c1a","cwd":"D:/trees/chat-1","tools":[],"model":"x"}""",
                """{"type":"assistant","message":{"role":"assistant","content":[{"type":"text","text":"Logged the port."}]}}""",
                """{"type":"result","subtype":"success","is_error":false,"result":"ok"}""",
                """{"type":"system","subtype":"init","session_id":"0b5e7c1a","cwd":"D:/trees/chat-1","tools":[],"model":"x"}"""));

            await Daoris.Driver.Driver.CaptureStructuredAsync(
                stdout, new StringReader(""), Path.Combine(folder, "s1.log"), "s1", output: null, events: null,
                new ClaudeStreamJson(), prompt: null, CancellationToken.None, named: told.Add);

            Assert.Equal(["0b5e7c1a"], told);
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>A mapper that knows no conversation names none: the default every other wire keeps.</summary>
    [Fact]
    public void A_wire_that_names_no_conversation_says_none()
    {
        IStreamMapper plain = new NoConversation();

        Assert.Null(plain.Conversation);
    }

    private sealed class NoConversation : IStreamMapper
    {
        public StreamMapped Read(string line) => StreamMapped.Nothing;

        public AcpUsage? Usage => null;
    }
}
