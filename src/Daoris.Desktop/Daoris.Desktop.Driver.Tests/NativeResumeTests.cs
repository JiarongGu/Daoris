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

        adapter.HandSettings(info, "D:/home/sessions/s1.settings.json");
        adapter.HandServers(info, "D:/home/sessions/s1.mcp.json");

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
