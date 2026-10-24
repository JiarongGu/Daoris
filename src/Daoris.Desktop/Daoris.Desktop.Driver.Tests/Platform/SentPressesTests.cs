using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The presses a terminal sent (RPC1j), kept a day, and a press last read <c>waiting</c> kept past it, at most thirty days from when it
/// was sent, until a read finds it ended (CONFIRMWAIT1b, the confirm-wait design §4.2): <see cref="SentPresses.Find"/> and the prune read
/// one rule, each against the time a test hands it.
/// </summary>
public sealed class SentPressesTests : IDisposable
{
    private static readonly DateTimeOffset Sent = new(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-sent-presses-" + Guid.NewGuid().ToString("N")[..8]);

    public SentPressesTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private SentPresses Presses => new(_home);

    private string Keep(DateTimeOffset at)
    {
        var id = Guid.NewGuid().ToString();
        Presses.Keep(new SentPress(id, Guid.NewGuid().ToString("N"), "DAORIS.SETUP.SETUP_PRESS", at));
        return id;
    }

    private string PathOf(string id) => Path.Combine(DaorisHome.Entry(_home, HomeEntry.Pipe), "sent", id + ".json");

    [Fact]
    public void A_press_never_read_waiting_keeps_a_day_from_when_it_was_sent()
    {
        var id = Keep(Sent);

        Assert.NotNull(Presses.Find(id, Sent.AddHours(23)));
        Assert.Null(Presses.Find(id, Sent.AddHours(25)));
        Assert.False(File.Exists(PathOf(id)));
    }

    [Fact]
    public void A_press_read_waiting_keeps_past_its_day_and_at_most_thirty_days_from_when_it_was_sent()
    {
        var id = Keep(Sent);
        Presses.Waiting(id, Sent.AddHours(1));

        var kept = Presses.Find(id, Sent.AddDays(3));
        Assert.NotNull(kept);
        Assert.Equal(Sent.AddHours(1), kept.WaitingAt);
        Assert.Equal(Sent, kept.At);
        Assert.NotNull(Presses.Find(id, Sent.AddDays(29)));
        Assert.Null(Presses.Find(id, Sent.AddDays(31)));
        Assert.False(File.Exists(PathOf(id)));
    }

    [Fact]
    public void Once_a_read_finds_it_ended_the_day_runs_from_that_read()
    {
        var id = Keep(Sent);
        Presses.Waiting(id, Sent.AddHours(1));
        Presses.Ended(id, Sent.AddDays(5));

        var kept = Presses.Find(id, Sent.AddDays(5).AddHours(23));
        Assert.NotNull(kept);
        Assert.Null(kept.WaitingAt);
        Assert.Null(Presses.Find(id, Sent.AddDays(5).AddHours(25)));
    }

    [Fact]
    public void An_ending_read_of_a_press_never_read_waiting_moves_nothing()
    {
        var id = Keep(Sent);
        Presses.Ended(id, Sent.AddHours(20));

        Assert.Null(Presses.Find(id, Sent.AddHours(25)));
    }

    /// <summary>The prune another press's keep runs reads the same rule: a waiting press's file stays past its day, an ended one's goes.</summary>
    [Fact]
    public void The_prune_reads_the_same_rule_as_the_find()
    {
        var waiting = Keep(Sent);
        Presses.Waiting(waiting, Sent.AddHours(1));
        var done = Keep(Sent);

        Keep(Sent.AddDays(2));

        Assert.True(File.Exists(PathOf(waiting)));
        Assert.False(File.Exists(PathOf(done)));

        Keep(Sent.AddDays(31));

        Assert.False(File.Exists(PathOf(waiting)));
    }
}
