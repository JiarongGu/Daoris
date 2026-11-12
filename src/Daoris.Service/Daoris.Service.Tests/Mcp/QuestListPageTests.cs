using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;

namespace Daoris.Service.Tests;

/// <summary>
/// `quest_list` with closed quests answers a page within the tool's reach, never every closed quest of the workspace at once: each page
/// says which quests it holds, of how many, and the page that holds the next; a list that fits is one page, said as before.
/// </summary>
public sealed class QuestListPageTests : McpToolsFixture
{
    [Fact]
    public async Task A_list_past_the_budget_pages_and_names_the_next_page()
    {
        var owner = new QuestTools(_quests, new QuestExchange(_service, _quests, files: _files), In("Owner"), intake: new IntakeScope(null, "s0a1b2c3"));
        for (var at = 0; at < 30; at++)
        {
            var quest = await _quests.PublishAsync("Asker", "Owner", $"Draw map number {at}", "The map needs one.", DateTimeOffset.UtcNow);
            await owner.RespondToQuestAsync(quest.Id, "take");
            await owner.RespondToQuestAsync(quest.Id, "done", $"Drew map {at} in 4f3e2d{at:00}; its render test passes. " + new string('x', 4000));
        }

        var steward = new QuestTools(_quests, new QuestExchange(_service, _quests, files: _files), In("Asker"), intake: new IntakeScope(null, "s9steward"));
        var first = await steward.ListQuestsAsync(includeClosed: true);
        var second = await steward.ListQuestsAsync(includeClosed: true, page: 2);
        var past = await steward.ListQuestsAsync(includeClosed: true, page: 99);

        Assert.True(first.Length <= QuestListPage.Budget + 500, $"the first page is {first.Length} characters");
        Assert.Contains("30 quest(s)", first);
        Assert.Contains("page 1 of", first);
        Assert.Contains("`page: 2`", first);
        Assert.Contains("Drew map 0 in", first);
        Assert.DoesNotContain("Drew map 0 in", second);
        Assert.Contains("page 2 of", second);
        Assert.Contains("no page 99", past);
    }

    [Fact]
    public void A_list_that_fits_is_one_page_said_as_before()
    {
        QuestListPage.Block[] blocks = [new("app", "- `#a1` **one**\n"), new("app", "- `#a2` **two**\n"), new("web", "- `#b1` **three**\n")];

        Assert.Equal(
            "3 quest(s):\n\n## `app` — 2\n- `#a1` **one**\n- `#a2` **two**\n\n## `web` — 1\n- `#b1` **three**\n\n",
            QuestListPage.Of(blocks, 1, "").Replace("\r\n", "\n"));
    }

    [Fact]
    public void A_page_holds_one_quest_at_least_and_counts_each_repository_whole()
    {
        QuestListPage.Block[] blocks = [new("app", new string('a', 60) + "\n"), new("app", new string('b', 60) + "\n"), new("web", "c\n")];

        var second = QuestListPage.Of(blocks, 2, "", budget: 50).Replace("\r\n", "\n");

        Assert.Contains("page 2 of 3", second);
        Assert.Contains("## `app` — 2", second);
        Assert.Contains(new string('b', 60), second);
        Assert.DoesNotContain(new string('a', 60), second);
        Assert.Contains("`page: 3`", second);
    }
}
