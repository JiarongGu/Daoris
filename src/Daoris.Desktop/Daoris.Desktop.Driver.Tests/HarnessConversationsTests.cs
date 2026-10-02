using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The harness's own conversation id (ANSWER1a, D131 §1): kept the moment the wire says it, beside the session's transcript
/// under the home, with the adapter that opened it; read back when the person answers a park. Never read from the agent's
/// own home. Files only, so this is the suite's fast half.
/// </summary>
public sealed class HarnessConversationsTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-harness-conv-" + Guid.NewGuid().ToString("N")[..8]);

    public HarnessConversationsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void A_kept_conversation_reads_back_with_the_adapter_that_opened_it()
    {
        var kept = new HarnessConversations(_home);

        kept.Keep("76cdd5db", "claude-code-acp", "0b5e7c1a-1f6e-4c1d-9e47-3f1c7a2b9d10");

        Assert.Equal(new HarnessConversation("claude-code-acp", "0b5e7c1a-1f6e-4c1d-9e47-3f1c7a2b9d10"), kept.Read("76cdd5db"));
        Assert.Equal(Path.Combine(_home, "sessions", "76cdd5db.harness.json"), kept.PathOf("76cdd5db"));
    }

    /// <summary>The file is Daoris's own JSON, BOM-less and LF, as every file under the home is written.</summary>
    [Fact]
    public void The_file_is_written_as_the_home_writes_its_files()
    {
        new HarnessConversations(_home).Keep("s1", "claude-code", "abc");

        var bytes = File.ReadAllBytes(Path.Combine(_home, "sessions", "s1.harness.json"));
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain("\r", text);
        Assert.EndsWith("\n", text);
        using var document = JsonDocument.Parse(text);
        Assert.Equal("claude-code", document.RootElement.GetProperty("adapter").GetString());
        Assert.Equal("abc", document.RootElement.GetProperty("conversation").GetString());
    }

    /// <summary>A later id replaces an earlier one: a fallback's new conversation is its own record's, and a resume says the same id again.</summary>
    [Fact]
    public void Keeping_again_replaces_what_was_kept()
    {
        var kept = new HarnessConversations(_home);
        kept.Keep("s1", "claude-code-acp", "first");
        kept.Keep("s1", "claude-code-acp", "second");

        Assert.Equal("second", kept.Read("s1")!.Conversation);
    }

    /// <summary>Nothing kept, a file that does not read, or one missing a field is no conversation: the answer falls back, it never throws.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("not json")]
    [InlineData("""{"adapter":"claude-code-acp"}""")]
    [InlineData("""{"conversation":"abc"}""")]
    [InlineData("""{"adapter":"claude-code-acp","conversation":""}""")]
    [InlineData("""{"adapter":"claude-code-acp","conversation":7}""")]
    [InlineData("[]")]
    public void What_does_not_read_is_no_conversation(string? content)
    {
        var kept = new HarnessConversations(_home);
        if (content is not null)
        {
            Directory.CreateDirectory(Path.Combine(_home, "sessions"));
            File.WriteAllText(kept.PathOf("s1"), content);
        }

        Assert.Null(kept.Read("s1"));
    }

    /// <summary>🔴 A session id that is not one names no file, and never a path under the home or outside it.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("../escape")]
    [InlineData("a/b")]
    [InlineData("..")]
    public void An_id_that_is_not_one_is_neither_kept_nor_read(string session)
    {
        var kept = new HarnessConversations(_home);

        kept.Keep(session, "claude-code-acp", "abc");

        Assert.Null(kept.Read(session));
        Assert.False(Directory.Exists(Path.Combine(_home, "sessions")) && Directory.EnumerateFiles(Path.Combine(_home, "sessions")).Any());
    }

    /// <summary>A blank conversation id is nothing to keep: the wire said none.</summary>
    [Fact]
    public void A_blank_conversation_is_not_kept()
    {
        var kept = new HarnessConversations(_home);

        kept.Keep("s1", "claude-code-acp", " ");

        Assert.False(File.Exists(kept.PathOf("s1")));
    }
}
