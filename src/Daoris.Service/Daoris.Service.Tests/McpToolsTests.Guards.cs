using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;

namespace Daoris.Service.Tests;

/// <summary>
/// REVIEWENV1b3: what only the connector's door decides. A connector is always an agent's, whether or not the driver named
/// its session, so it never publishes with the person's authority; and a chain's step or requirement left null is refused
/// naming which, as a blank one is, rather than thrown.
/// </summary>
public sealed partial class McpToolsTests
{
    /// <summary>
    /// 🔴 A connector that speaks for no session, and an intake's connector the driver named no session for, set no review
    /// choice off the person's words: `off` unquoted is refused as any agent's is, and nothing is published.
    /// </summary>
    [Fact]
    public async Task A_connector_that_names_no_session_publishes_as_an_agent()
    {
        var refused = await _tools.PublishQuestAsync("Asker", "Owner", "Turn the review off", "b", review: new ReviewChoice(Reviews.Off, null));

        Assert.Contains("only on the person's own words", refused);
        Assert.Empty(await _quests.ListAsync(receiver: "Owner"));

        var asks = await AskStore.OpenAsync(_connection);
        var exchange = new QuestExchange(_service, _quests, files: _files, asks: asks);
        var desk = new AskDesk(_service, asks, exchange, _files);
        var ask = (await desk.AskAsync(new AskRequest("default", "fix the typo in the report"), DateTimeOffset.UtcNow)).Ask!;
        var room = Path.Combine(_root, "home", "intake", "default");
        var unbound = new KnowledgeTools(
            _service, _quests, exchange, new AmbientWorkspace(room, ask.Workspace), desk, new IntakeScope(ask.Id, Session: null));

        var off = await unbound.PublishQuestAsync("intake", "Owner", "Fix the typo", "b", review: new ReviewChoice(Reviews.Off, null));

        Assert.Contains("only on the person's own words", off);
        Assert.Empty((await desk.FindAsync(ask.Id))!.Quests);
    }

    /// <summary>🔴 A chain's step or a requirement sent as null is refused naming which, and nothing is published.</summary>
    [Fact]
    public async Task A_null_step_or_requirement_is_refused_naming_which()
    {
        var step = await _tools.PublishQuestAsync("Asker", "Owner", "A null step", "b", then: [null!]);
        var requirement = await _tools.PublishQuestAsync("Asker", "Owner", "A null requirement", "b", requirements: [null!]);

        Assert.Contains("Step 1 of the chain needs whom to ask", step);
        Assert.Contains("Requirement 1", requirement);
        Assert.Empty(await _quests.ListAsync(receiver: "Owner"));
    }
}
