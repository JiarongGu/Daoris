using System.Text.Json;

namespace Daoris.Service.Tests;

/// <summary>
/// The <c>sync</c> kind's writer (HELP10): bringing repositories up to date after a pull request merged (WSR6, D109), for
/// one repository or every one with a checkout. What the look lists, and the press, are the driver's.
/// </summary>
public sealed class HelpSyncProposalTests : HelpProposalBoxFixture
{
    [Fact]
    public void Bringing_up_to_date_is_written_with_its_repository_or_none()
    {
        var (one, message) = Box().ProposeSync(" engine ", "the person's pull request merged", "h1", Now);
        var (every, _) = Box().ProposeSync(null, "the person's pull requests merged", "h1", Now);

        Assert.Contains($"#{one}", message);
        var file = Written(one!);
        Assert.Equal(("sync", "sync", "engine"),
            (file.GetProperty("kind").GetString(), file.GetProperty("door").GetString(), file.GetProperty("target").GetString()));
        Assert.Equal(JsonValueKind.Null, file.GetProperty("value").ValueKind);
        Assert.False(file.TryGetProperty("listed", out _));
        Assert.Equal(JsonValueKind.Null, Written(every!).GetProperty("target").ValueKind);
    }

    [Fact]
    public void A_repository_that_is_not_one_word_or_no_reason_is_refused_with_nothing_written()
    {
        Assert.Contains("A repository is one word", Box().ProposeSync("two words", "a reason", "h1", Now).Message);
        Assert.Null(Box().ProposeSync("engine", " ", "h1", Now).Id);
        Assert.False(Directory.Exists(Path.Combine(_home, "help", "proposals")));
    }
}
