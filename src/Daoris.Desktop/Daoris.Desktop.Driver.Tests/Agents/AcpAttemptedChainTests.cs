using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The marks of the words a session is asked to write go in the order asked, and one that fails is that word's alone.</summary>
public sealed class AcpAttemptedChainTests
{
    private static ChatMessage Word(string id, Func<Task<IReadOnlyList<string>>> attempting) =>
        new("words", []) { Id = id, Attempting = attempting };

    [Fact]
    public async Task A_mark_that_fails_unexpectedly_does_not_fault_the_marks_of_the_words_after_it()
    {
        var prompting = new AcpPrompting(null!);
        var first = prompting.Attempted(Word("w1", () => throw new InvalidOperationException("the record answered nonsense")));
        var second = prompting.Attempted(Word("w2", () => Task.FromResult<IReadOnlyList<string>>(["w2"])));

        await Assert.ThrowsAsync<AcpWordsUnmarked>(() => first);
        Assert.True(await second);
    }
}
