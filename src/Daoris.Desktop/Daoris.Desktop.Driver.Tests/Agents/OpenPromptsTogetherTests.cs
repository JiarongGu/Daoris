using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A transient try carries its held words in one prompt: each is the prompt's, so each is unreached when the run ends before any is taken.</summary>
public sealed class OpenPromptsTogetherTests
{
    [Fact]
    public void Every_word_of_an_untaken_try_is_unreached_not_only_its_first()
    {
        var open = new OpenPrompts();
        ChatMessage first = new("first", []), second = new("second", []), third = new("third", []);

        var passage = open.Enter([first, second, third]);
        Assert.True(open.Admit(passage, new OpenPrompt(first, [second, third])));

        Assert.Empty(open.Untaken(retry: null));
        Assert.Equal<IAgentWord>([first, second, third], open.Unreached);
    }
}
