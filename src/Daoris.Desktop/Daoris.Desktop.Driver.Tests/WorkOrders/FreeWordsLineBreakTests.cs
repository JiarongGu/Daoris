using Daoris.Contracts.Quests;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 Words of several lines reach every verb that takes free words whole (SAYLINES1). A batch file ended the command's arguments at the first line
/// break and said nothing of the rest, so a go for a release reached a session without its steps and its limit; the install's <c>daoris.exe</c>
/// hands the program each argument whole (<c>CommandLauncherProcessTests</c>), and each verb here keeps the line breaks it is handed, in the
/// words it sends, as they were typed: say, ask (its words, its requirements and its through words), quest, reword and helper.
/// </summary>
public sealed class FreeWordsLineBreakTests
{
    private const string Lines = "first line\nsecond line\r\nthird line";

    [Fact]
    public void Session_say_keeps_the_line_breaks_of_its_words()
    {
        var ask = SessionsCommand.Read(["say", "s-1", Lines, "--level", "urgent"], out var problem);

        Assert.Null(problem);
        Assert.Equal(Lines, ask!.Text);
    }

    [Fact]
    public void Session_reword_keeps_the_line_breaks_of_its_words()
    {
        var ask = SessionsCommand.Read(["reword", "s-1", "go-ahead", Lines], out var problem);

        Assert.Null(problem);
        Assert.Equal(Lines, ask!.Text);
    }

    [Fact]
    public void Session_helper_keeps_the_line_breaks_of_its_question()
    {
        var ask = SessionsCommand.Read(["helper", "s-1", Lines, "--to-me"], out var problem);

        Assert.Null(problem);
        Assert.Equal(Lines, ask!.Text);
    }

    [Fact]
    public void A_new_ask_keeps_the_line_breaks_of_its_words_its_requirements_and_its_through_words()
    {
        var read = AskArguments.Read(
            ["--to", "engine", Lines, "--short", "two lines", "--require", "merge it\nonce green", "it is merged\nto main", "--through", "do it\nwithout me"], out var problem);

        Assert.Null(problem);
        Assert.Equal(Lines, read!.Sentence);
        Assert.Equal(new QuestRequirementWire("merge it\nonce green", "it is merged\nto main"), Assert.Single(read.Requirements));
        Assert.Equal("do it\nwithout me", read.Through);
    }

    [Fact]
    public void An_asks_act_keeps_the_line_breaks_of_its_words()
    {
        var read = AskArguments.Read(["--publish", "a1", "--to", "engine", "--require", "merge it\nonce green", "it is merged\nto main"], out var problem);

        Assert.Null(problem);
        Assert.Equal("merge it\nonce green", Assert.Single(read!.Requirements).Quote);
    }

    [Fact]
    public void Quest_done_and_reopen_keep_the_line_breaks_of_their_note()
    {
        var done = QuestDoneCommand.Read(["done", "q1", "--note", Lines], out var doneProblem);
        var reopen = QuestReopenCommand.Read(["reopen", "q1", "--note", Lines], out var reopenProblem);

        Assert.Null(doneProblem);
        Assert.Null(reopenProblem);
        Assert.Equal(Lines, done!.Note);
        Assert.Equal(Lines, reopen!.Note);
    }

    [Fact]
    public void Quest_review_keeps_the_line_breaks_of_its_words()
    {
        var review = QuestReviewCommand.Read(["review", "q1", "not-yet", Lines], out var problem);

        Assert.Null(problem);
        Assert.Equal(Lines, review!.Words);
    }
}
