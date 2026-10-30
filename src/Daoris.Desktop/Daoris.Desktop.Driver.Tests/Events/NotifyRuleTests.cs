using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 m-v (the foundation design §4.4): the notify rule in one table. The switch gates news alike; a Confirm waiting is a wait on the person,
/// not news, and stays ungated (D196); a look's news is listed and counted and never said aloud.
/// </summary>
public sealed class NotifyRuleTests
{
    /// <summary>Every kind of news has a row, and the table names no kind that is gone.</summary>
    [Fact]
    public void Every_kind_of_news_has_a_row()
    {
        Assert.Equal(Enum.GetValues<NewsKind>().Order(), NotifyRule.Table.Keys.Order());
        foreach (var kind in Enum.GetValues<AttentionKind>()) Assert.True(NotifyRule.Table.ContainsKey(NotifyRule.KindOf(kind)), kind.ToString());
    }

    /// <summary>The switch gates every kind of news alike, off and on.</summary>
    [Theory]
    [InlineData(NewsKind.Parked)]
    [InlineData(NewsKind.Ended)]
    [InlineData(NewsKind.QuestParked)]
    [InlineData(NewsKind.Waiting)]
    [InlineData(NewsKind.Answered)]
    [InlineData(NewsKind.Arrival)]
    [InlineData(NewsKind.Message)]
    [InlineData(NewsKind.HeldCall)]
    public void The_switch_gates_news_alike(NewsKind kind)
    {
        Assert.True(NotifyRule.Tells(kind, switchOn: true));
        Assert.False(NotifyRule.Tells(kind, switchOn: false));
    }

    /// <summary>A Confirm waiting is a wait on the person and stays ungated; a look's news is never said aloud.</summary>
    [Fact]
    public void A_confirm_stays_ungated_and_a_look_is_never_said()
    {
        Assert.True(NotifyRule.Tells(NewsKind.Confirm, switchOn: false));
        Assert.True(NotifyRule.Tells(NewsKind.Confirm, switchOn: true));
        Assert.False(NotifyRule.Tells(NewsKind.Look, switchOn: true));
        Assert.False(NotifyRule.Tells(NewsKind.Look, switchOn: false));
    }

    /// <summary>The switch is read from the machine's choices, and choices that do not read err toward saying it.</summary>
    [Fact]
    public void The_switch_is_read_from_the_choices_and_errs_toward_saying()
    {
        var folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "notify-rule", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "driver.json");
            File.WriteAllText(path, """{ "notify": false }""");
            Assert.False(NotifyRule.SwitchOn(path));
            File.WriteAllText(path, "{ not json");
            Assert.True(NotifyRule.SwitchOn(path));
        }
        finally
        {
            GitSeed.Discard(folder);
        }
    }
}
