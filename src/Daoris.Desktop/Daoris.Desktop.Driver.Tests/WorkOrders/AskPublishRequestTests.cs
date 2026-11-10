using Daoris.Contracts.Quests;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A terminal's publish names the session it runs in, so the quest names its publisher, and carries the requirements it is given.</summary>
public sealed class AskPublishRequestTests
{
    [Fact]
    public void A_sessions_terminal_publish_names_its_session_and_carries_its_requirements()
    {
        var sent = AskPublishRequest.Of("#654c46", " media-api ", "ab98e9dc", [new QuestRequirementWire("use the media config", "the name is read from it")]);

        Assert.Equal(("654c46", "media-api", "ab98e9dc"), (sent.Id, sent.To, sent.Session));
        Assert.Equal("use the media config", Assert.Single(sent.Requirements!)!.Quote);
    }

    [Fact]
    public void A_persons_bare_publish_names_no_session_and_makes_no_draft()
    {
        var sent = AskPublishRequest.Of("654c46", "media-api", null, []);

        Assert.Null(sent.Session);
        Assert.Null(sent.Requirements);
    }
}
