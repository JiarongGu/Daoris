using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The harness's own conversation id: kept in the desktop store's bindings the moment the wire says it, with the door
/// that opened it; read back when the person answers a park. Never read from the agent's own home.
/// </summary>
public sealed class HarnessConversationsTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-harness-conv-" + Guid.NewGuid().ToString("N")[..8]);

    public HarnessConversationsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private HarnessConversations Kept() => new(_home, () => StoreSettled.Yes);

    [Fact]
    public void A_failed_keep_logs_the_session_door_and_error_kind()
    {
        // A newer Daoris migrated the store and stamped it past this build's: this build reads what it knows and writes nothing.
        using (DesktopStore.Open(DesktopStore.LocationIn(_home), new DesktopStoreOptions
        {
            Writer = DesktopStore.WriterStamp + 1,
            Steps = [.. DesktopStore.Steps, new StoreMigration(DesktopStore.Steps.Count + 1, DesktopStore.WriterStamp + 1, write => write.Execute("CREATE TABLE raised (x TEXT)"))],
            Settled = () => StoreSettled.Yes,
        }))
        {
        }

        var store = Kept();
        store.Keep("s1", "acp-stub", "conv-1");

        Assert.Null(store.Read("s1"));
        var lines = Directory.GetFiles(DaorisHome.Entry(_home, HomeEntry.Logs), "*.jsonl").SelectMany(File.ReadAllLines).ToArray();
        var line = Assert.Single(lines);
        Assert.Contains("conversation.unkept", line);
        Assert.Contains("acp-stub", line);
        Assert.Contains("s1", line);
        Assert.DoesNotContain("conv-1", line);
    }

    [Fact]
    public void A_kept_conversation_reads_back_with_the_door_that_opened_it()
    {
        var kept = Kept();

        kept.Keep("76cdd5db", "claude-code-acp", "0b5e7c1a-1f6e-4c1d-9e47-3f1c7a2b9d10");

        Assert.Equal(new HarnessConversation("claude-code-acp", "0b5e7c1a-1f6e-4c1d-9e47-3f1c7a2b9d10"), kept.Read("76cdd5db"));
    }

    /// <summary>A later id replaces an earlier one: a fallback's new conversation is its own record's, and a resume says the same id again.</summary>
    [Fact]
    public void Keeping_again_replaces_what_was_kept()
    {
        var kept = Kept();
        kept.Keep("s1", "claude-code-acp", "first");
        kept.Keep("s1", "claude-code-acp", "second");

        Assert.Equal("second", kept.Read("s1")!.Conversation);
    }

    /// <summary>🔴 A session id that is not one is neither kept nor read, and makes no store.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("../escape")]
    [InlineData("a/b")]
    [InlineData("..")]
    public void An_id_that_is_not_one_is_neither_kept_nor_read(string session)
    {
        var kept = Kept();

        kept.Keep(session, "claude-code-acp", "abc");

        Assert.Null(kept.Read(session));
        Assert.False(File.Exists(DesktopStore.LocationIn(_home)));
    }

    /// <summary>
    /// A chat keeps its conversation too: what its door is told the id through keeps it under the chat's
    /// record with the door that opened it, read back as the conversation an ended chat goes on in; a blank id keeps
    /// nothing, and a record id that is not one keeps nothing.
    /// </summary>
    [Fact]
    public void What_a_chats_door_is_told_keeps_the_conversation_under_its_record()
    {
        var kept = Kept();

        kept.Keeping("c1a2b3c4", "claude-code")("0b5e7c1a-1f6e");
        kept.Keeping("c1a2b3c4", "claude-code")(" ");
        kept.Keeping("../escape", "claude-code")("abc");

        Assert.Equal(new HarnessConversation("claude-code", "0b5e7c1a-1f6e"), kept.Read("c1a2b3c4"));
        Assert.Null(kept.Read("../escape"));
    }

    /// <summary>A blank conversation id is nothing to keep: the wire said none.</summary>
    [Fact]
    public void A_blank_conversation_is_not_kept()
    {
        var kept = Kept();

        kept.Keep("s1", "claude-code-acp", " ");

        Assert.Null(kept.Read("s1"));
        Assert.False(File.Exists(DesktopStore.LocationIn(_home)));
    }
}
