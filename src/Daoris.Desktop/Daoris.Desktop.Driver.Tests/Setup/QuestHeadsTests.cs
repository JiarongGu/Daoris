using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The first lines of the quests a press publishes, as its Confirm card shows them: each is the opening the quest's own brief is composed
/// from, so what the person reads is what the repository's session is asked first. A department's set-up and its work step, and a room's.
/// </summary>
public sealed class QuestHeadsTests
{
    private static readonly DateOnly Day = new(2026, 11, 12);

    /// <summary>The set-up quest of a new department is the whole set-up, titled by the day and opening as its brief opens.</summary>
    [Fact]
    public void A_departments_set_up_is_the_whole_set_up_opening_as_its_brief_does()
    {
        var heads = CreateQuests.Of(new CreateAsk("ledger", "keeps the books"), Day);

        var setup = Assert.Single(heads);
        Assert.Equal(("setup", "Set up this repository for every agent (2026-11-12)"), (setup.Role, setup.Title));
        Assert.Equal(QuestHeads.Plain(SetupBrief.Request(SetupCase.Whole)), setup.Head);
        Assert.StartsWith("What is asked, and whose it is. Take up the shared doctrine", setup.Head);
        Assert.DoesNotContain("**", setup.Head);
    }

    /// <summary>The person's work follows the set-up as a second quest, headed by the first line of the request and the request's opening.</summary>
    [Fact]
    public void The_work_a_department_is_opened_for_is_a_second_quest_headed_by_the_request()
    {
        var heads = CreateQuests.Of(new CreateAsk("ledger", "keeps the books") { Work = "Build the first ledger screen.\nWith totals by month." }, Day);

        Assert.Equal(["setup", "work"], heads.Select(each => each.Role));
        var work = heads[1];
        Assert.Equal("Build the first ledger screen.", work.Title);
        Assert.StartsWith("Build the first ledger screen.", work.Head);
        Assert.DoesNotContain("{parent}", work.Head);
    }

    [Fact]
    public void A_long_request_is_cut_at_a_bound_with_the_mark()
    {
        var heads = CreateQuests.Of(new CreateAsk("ledger", "keeps the books") { Work = new string('x', 900) }, Day);

        var work = heads[1];
        Assert.True(work.Head.Length <= CreateQuests.HeadLength + 1);
        Assert.EndsWith(TextBounds.Ellipsis, work.Head);
    }

    /// <summary>A brief's opening is markdown a card does not draw: its marks go, its paragraphs and quoted lines run on as one line of words.</summary>
    [Fact]
    public void A_markdown_opening_is_plain_words_in_one_line()
    {
        Assert.Equal("A. b the reason why c", QuestHeads.Plain("**A.** b\n\n> the reason\n> why\n\nc"));
        Assert.Equal("a `code` b", QuestHeads.Plain("  a `code`\r\n b  "));
    }

    /// <summary>A folder department has no set-up to ask: it publishes no quest.</summary>
    [Fact]
    public void A_folder_department_publishes_no_quest()
    {
        Assert.Empty(CreateQuests.Of(new CreateAsk("notes", "loose files") { Kind = PlaceKind.Folder }, Day));
    }

    /// <summary>A room's title and opening are the ones its quest's own brief is composed from, the person's reason quoted.</summary>
    [Fact]
    public void A_rooms_quest_opens_as_its_brief_does_with_the_reason_quoted()
    {
        var (title, body) = RoomBrief.Compose(new RoomBriefInput("engine", "tools/web", "the web app needs its own conventions", "main", "abc1234abc1234", [], [], []));

        Assert.Equal(title, RoomBrief.TitleOf("tools/web"));
        var opening = RoomBrief.Opening("tools/web", "the web app needs its own conventions");
        Assert.StartsWith(opening, body);
        Assert.Contains("the web app needs its own conventions", opening);
        Assert.Contains("`tools/web`", opening);
        Assert.DoesNotContain("\n", QuestHeads.Plain(opening));
    }
}
