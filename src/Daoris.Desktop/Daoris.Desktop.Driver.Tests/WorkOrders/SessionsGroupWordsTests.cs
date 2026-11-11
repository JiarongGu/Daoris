using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris session list --group</c> takes the five states and the Archived fold, and refuses a group of the list before them,
/// naming the five; its usage says them.
/// </summary>
public sealed class SessionsGroupWordsTests
{
    [Theory]
    [InlineData("needs-you")]
    [InlineData("working")]
    [InlineData("pending")]
    [InlineData("failed")]
    [InlineData("done")]
    [InlineData("archived")]
    public void Each_of_the_five_and_the_fold_is_taken(string group)
    {
        Assert.Equal(group, SessionsCommand.Read(["--group", group], out _)?.Group);
    }

    [Theory]
    [InlineData("you")]
    [InlineData("review")]
    [InlineData("later")]
    [InlineData("ended")]
    public void A_former_group_is_refused_naming_the_five(string former)
    {
        Assert.Null(SessionsCommand.Read(["--group", former], out var problem));
        Assert.Equal("`--group` takes one of needs-you, working, pending, failed, done, archived.", problem);
    }

    [Fact]
    public void The_usage_names_the_five()
    {
        Assert.Contains("[--group needs-you|working|pending|failed|done|archived]", SessionsCommand.Usage, StringComparison.Ordinal);
    }
}
