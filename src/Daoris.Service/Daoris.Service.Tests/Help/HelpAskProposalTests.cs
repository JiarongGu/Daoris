namespace Daoris.Service.Tests;

/// <summary>The <c>ask</c> kind's writer (HELP1c): something to start, with its words and its workspace.</summary>
public sealed class HelpAskProposalTests : HelpProposalBoxFixture
{
    [Fact]
    public void An_ask_is_written_with_its_words_and_its_workspace()
    {
        var (id, _) = Box().ProposeAsk("fix the chunk streamer's cold-cache stall", "work", "the person wants it started", "h1", Now);

        var file = Written(id!);
        Assert.Equal("ask", file.GetProperty("kind").GetString());
        Assert.Equal("fix the chunk streamer's cold-cache stall", file.GetProperty("sentence").GetString());
        Assert.Equal("work", file.GetProperty("workspace").GetString());
    }
}
