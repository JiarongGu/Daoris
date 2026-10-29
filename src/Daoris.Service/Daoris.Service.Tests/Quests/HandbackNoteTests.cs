using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// HANDBACK1 (D219): a session's closing note is its handback, the one record of what it did that outlives its conversation, so a
/// note that only points elsewhere is refused. The rule is narrow: a note is refused only where every part of it is a pointer to
/// another message, a commit, a file or a place, or a bare word that says nothing (<c>Done</c>, <c>Handback</c>), and one part at
/// least is a pointer. A short real handback, a pointer beside words of its own, and a note with no pointer at all go through.
/// </summary>
public sealed class HandbackNoteTests
{
    /// <summary>🔴 The two closing notes the evidence names, and the shapes the rule names, are each refused, naming the pointer.</summary>
    [Theory]
    [InlineData("Details are in my last message", "Details are in my last message")]
    [InlineData("Handback is in my last message.", "Handback is in my last message")]
    [InlineData("See the commit", "See the commit")]
    [InlineData("See commit 4f3e2d1.", "See commit 4f3e2d1")]
    [InlineData("handback above", "handback above")]
    [InlineData("See above.", "See above")]
    [InlineData("Done. Details are in my last message.", "Details are in my last message")]
    [InlineData("Done — see my last message.", "see my last message")]
    [InlineData("Handback: see my previous message", "see my previous message")]
    [InlineData("Summary in docs/handback.md", "Summary in docs/handback.md")]
    [InlineData("As described above.", "As described above")]
    [InlineData("My last message has the details.", "My last message has the details")]
    [InlineData("It's all in the chat.", "It's all in the chat")]
    [InlineData("Everything is in the commit message.", "Everything is in the commit message")]
    [InlineData("Closing as done, see the PR.", "see the PR")]
    [InlineData("**Handback is in my last message**", "Handback is in my last message")]
    [InlineData("详情见我上一条消息", "详情见我上一条消息")]
    [InlineData("完成。交接在我最后一条消息里。", "交接在我最后一条消息里")]
    // The review of 2026-10-29: emphasis inside a part, a list of commits after a pointer, a Chinese pointer at a commit, and `read`
    // at a message or with the details asked for.
    [InlineData("**Details** are in my last message.", "Details are in my last message")]
    [InlineData("See commits 4f3e2d1, 1a2b3c4.", "See commits 4f3e2d1")]
    [InlineData("See commits 4f3e2d1 and 1a2b3c4", "See commits 4f3e2d1")]
    [InlineData("详情见提交 4f3e2d1。", "详情见提交 4f3e2d1")]
    [InlineData("Read my last message.", "Read my last message")]
    [InlineData("Read docs/handback.md for the details.", "Read docs/handback.md for the details")]
    public void A_note_that_only_points_elsewhere_is_refused_naming_its_pointer(string note, string pointer)
    {
        Assert.Equal(pointer, HandbackNotes.PointsElsewhere(note));
    }

    /// <summary>
    /// A short real handback goes through, as do a pointer beside words of its own, a bare done, and no note at all: the rule
    /// refuses a pointer with nothing beside it, never a note for being short.
    /// </summary>
    [Theory]
    [InlineData("Fixed X in abc123; tests Y pass")]
    [InlineData("Built.")]
    [InlineData("Built again.")]
    [InlineData("Done.")]
    [InlineData("Fixed the login timeout in 4f3e2d1; see the commit for the diff.")]
    [InlineData("See the commit 4f3e2d1 for the fix to the login timeout")]
    [InlineData("The bridge belongs to Owner, see D12.")]
    [InlineData("Check passed on main.")]
    [InlineData("Details are in my last message: the cache is now keyed by tree, and its tests pass.")]
    [InlineData("Read path fixed in src/report.ts.")]
    [InlineData("已修复缓存，测试通过。")]
    // A report of what was read is a handback of a reading quest, never a pointer; and a pointer beside words of its own stays one.
    [InlineData("Read README.md.")]
    [InlineData("Read README.md; read docs/setup.md.")]
    [InlineData("See above: fixed the cache key; tests pass.")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void A_note_with_words_of_its_own_goes_through(string? note)
    {
        Assert.Null(HandbackNotes.PointsElsewhere(note));
    }

    /// <summary>
    /// The refusal is the service's words to an agent, in English as its other refusals are: it quotes the pointer, says the note
    /// is the record whoever reads the quest later reads, names what a handback holds, and says nothing moved.
    /// </summary>
    [Fact]
    public void The_refusal_quotes_the_pointer_and_names_what_a_handback_holds()
    {
        var said = HandbackNotes.Refusal("Details are in my last message");

        Assert.Contains("\"Details are in my last message\"", said);
        Assert.Contains("what changed, where (the commits), what you proved, and what you did not", said);
        Assert.EndsWith("Nothing moved.", said);
    }
}
