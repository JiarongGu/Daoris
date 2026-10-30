using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1d-ii part 5 (the foundation design §3.4 rule 3): a conversation's pending start whose agent refused it at the open, as its
/// replacement's keyed open reads it: the moving words kept on the refused record, which that open names so they move in its own step, in the
/// order said; and a producer's retry that joined the moving words under the key of one kept there, which goes nowhere a second time. The
/// replacement itself, which needs an agent, is <see cref="ChatPendingProcessTests"/>'s.
/// </summary>
public sealed class ChatPendingTests
{
    private static readonly ActorEffective Entry = new("acp-stub", "opus", null, "1");

    private static ChatPending Pending() =>
        new("c1", "chat", [Entry], Entry, null, null, (_, _) => Task.FromResult(new ChatStart(null, "opens nothing here")));

    /// <summary>
    /// The words a replacement's open names are the moving words the refused record keeps, by their ids there, in the order said: a word held in
    /// this process alone, which no record keeps, is no word of its; one kept and said meanwhile is. Once moved, none is moving.
    /// </summary>
    [Fact]
    public void The_words_a_replacements_open_names_are_the_moving_words_kept_on_the_refused_record_in_the_order_said()
    {
        var pending = Pending();
        pending.Moving([new ChatMessage("make the port configurable", []), new ChatMessage("then the tests", []) { Id = "k1", Kept = true, Key = "press/k1" }]);
        Assert.True(pending.Join(new ChatMessage("and the docs", []) { Id = "k2", Kept = true }));
        Assert.True(pending.Join(new ChatMessage("held here alone", [])));

        Assert.Equal(["k1", "k2"], pending.KeptIds);

        pending.Moved("c2", _ => true);
        Assert.Empty(pending.KeptIds);
    }

    /// <summary>
    /// 🔴 A producer's retry that joined the moving words under the key of a word the refused record keeps among them is that word said again: it
    /// is handed nowhere, and the kept one goes once. A word under a key of its own goes in its place in the order said.
    /// </summary>
    [Fact]
    public void A_producers_retry_joined_under_a_kept_moving_words_key_is_handed_nowhere()
    {
        var pending = Pending();
        pending.Moving([new ChatMessage("then the tests", []) { Id = "k1", Kept = true, Key = "press/k1" }]);
        Assert.True(pending.Join(new ChatMessage("then the tests", []) { Key = "press/k1" }));
        Assert.True(pending.Join(new ChatMessage("and the docs", []) { Key = "press/k2" }));

        var said = new List<(string? Id, string? Key)>();
        var left = pending.Moved("c2", word =>
        {
            said.Add((word.Id, word.Key));
            return true;
        });

        Assert.Equal([("k1", "press/k1"), (null, "press/k2")], said);
        Assert.Empty(left);
    }
}
