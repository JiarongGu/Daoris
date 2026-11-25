using Daoris.Driver;
using Daoris.Testing;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A check's runner says once, in its log, that its builds are done (<c>reached-built: {}</c> on a line of its own); the driver reads
/// the log as it grows and says so to the run's record. A line half written is not yet said, and the words inside another line are not it.
/// </summary>
public sealed class BuiltWatchTests : IDisposable
{
    private readonly HeavyFixture _fixture = new("built-watch");

    public void Dispose() => _fixture.Dispose();

    private string Log => Path.Combine(_fixture.Folder, "reached.log");

    [Fact]
    public void A_log_not_yet_written_or_without_the_line_says_nothing()
    {
        var watch = new BuiltWatch(Log);
        Assert.False(watch.Seen());

        File.WriteAllText(Log, "reached-step: {\"step\":\"census\",\"at\":0}\nreached: a run began\n");
        Assert.False(watch.Seen());
    }

    [Fact]
    public void The_line_is_seen_once_it_is_whole_and_stays_seen()
    {
        var watch = new BuiltWatch(Log);
        File.WriteAllText(Log, "reached: something\nreached-built: {");
        Assert.False(watch.Seen(), "a line half written is not said yet");

        File.AppendAllText(Log, "}\r\nreached-step: {}\n");
        Assert.True(watch.Seen());
        Assert.True(watch.Seen());
    }

    [Fact]
    public void The_same_words_inside_another_line_are_not_the_line()
    {
        var watch = new BuiltWatch(Log);
        File.WriteAllText(Log, "reached: a test printed reached-built: {} in its output\n   reached-built: {}\n");

        Assert.False(watch.Seen());
    }

    [Fact]
    public async Task A_watch_says_it_once_the_log_gains_the_line_and_ends()
    {
        var said = 0;
        var clock = new MovedClock();
        var watching = BuiltWatch.RunAsync(Log, () => said++, TimeSpan.FromSeconds(1), clock, CancellationToken.None);
        await Poll.Until(() => clock.Waiting, () => "the watch never waited for its next look");

        File.WriteAllText(Log, "reached: start
");
        clock.Advance(TimeSpan.FromSeconds(1));
        await Poll.Until(() => clock.Waiting, () => "the watch did not look again");
        Assert.Equal(0, said);

        File.AppendAllText(Log, "reached-built: {}
");
        clock.Advance(TimeSpan.FromSeconds(1));
        await watching;
        Assert.Equal(1, said);
    }

    [Fact]
    public async Task A_watch_cut_before_the_line_ends_quietly_without_saying()
    {
        var said = 0;
        var clock = new MovedClock();
        using var ends = new CancellationTokenSource();
        var watching = BuiltWatch.RunAsync(Log, () => said++, TimeSpan.FromSeconds(1), clock, ends.Token);
        await Poll.Until(() => clock.Waiting, () => "the watch never waited for its next look");
        await ends.CancelAsync();

        await watching;
        Assert.Equal(0, said);
    }
}
