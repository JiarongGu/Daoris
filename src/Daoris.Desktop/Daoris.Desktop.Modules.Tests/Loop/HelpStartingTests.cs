namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Which of Ask Daoris's conversations are starting (<see cref="HelpStarting"/>): the marker alone, holding no word. What is said
/// to one meanwhile is kept on its record, proved by <c>DriverModuleHelpStartTests</c>.
/// </summary>
public sealed class HelpStartingTests
{
    [Fact]
    public async Task A_conversation_is_starting_from_its_placing_until_its_start_is_over_and_each_is_its_own()
    {
        var starting = new HelpStarting();
        starting.Placed("h1");
        starting.Placed("h2");

        Assert.True(starting.Is("H1"));
        Assert.True(starting.Queue("h1")!.Opening);
        Assert.Empty(starting.Queue("h1")!.Queued);
        Assert.Null(starting.Queue("h3"));

        await starting.OverAsync("h1");

        Assert.Null(starting.Queue("h1"));
        Assert.True(starting.Is("h2"));
    }

    [Fact]
    public async Task A_start_does_not_end_while_a_word_is_being_kept_and_a_word_not_starting_is_kept_no_way_here()
    {
        var starting = new HelpStarting();
        Assert.Null(await starting.KeepingAsync("h1", CancellationToken.None));

        starting.Placed("h1");
        var keeping = await starting.KeepingAsync("h1", CancellationToken.None);
        Assert.NotNull(keeping);

        var over = starting.OverAsync("h1");
        Assert.False(over.IsCompleted);
        Assert.True(starting.Is("h1"));

        keeping.Dispose();
        await over;
        Assert.False(starting.Is("h1"));
        Assert.Null(await starting.KeepingAsync("h1", CancellationToken.None));
    }
}
