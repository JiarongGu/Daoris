using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The presses a terminal sent (RPC1j), each with the key that reads it, kept until its answer or a read finds it ended and a day after,
/// thirty days from when it was sent at most (CONFIRMWAIT1b; CONFIRMWAIT1, the confirm-wait design §15.2 item 6), in the home's kept
/// <c>profiles/pipe-sent</c>: <see cref="SentPresses.Find"/> and the prune read one rule, each against the time a test hands it.
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

    private string PathOf(string id) => Path.Combine(DaorisHome.Entry(_home, HomeEntry.PipeSent), id + ".json");

    /// <summary>
    /// CONFIRMWAIT1 (the confirm-wait design §15.2 item 6): a press nothing has found ended keeps its key thirty days from when it was sent,
    /// so a verb killed before it read the press waiting leaves no card without its key; the next command's prune does not take it.
    /// </summary>
    [Fact]
    public void A_press_nothing_found_ended_keeps_thirty_days_from_when_it_was_sent()
    {
        var id = Keep(Sent);
        Keep(Sent.AddDays(2));

        Assert.NotNull(Presses.Find(id, Sent.AddDays(2)));
        Assert.True(File.Exists(PathOf(id)));
        Assert.NotNull(Presses.Find(id, Sent.AddDays(29)));
        Assert.Null(Presses.Find(id, Sent.AddDays(31)));
        Assert.False(File.Exists(PathOf(id)));
    }

    [Fact]
    public void A_press_read_waiting_keeps_at_most_thirty_days_from_when_it_was_sent()
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

    /// <summary>Once its answer or a read finds it ended, waited on or not, its day runs from then.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Once_it_is_found_ended_the_day_runs_from_then(bool waited)
    {
        var id = Keep(Sent);
        if (waited) Presses.Waiting(id, Sent.AddHours(1));
        Presses.Ended(id, Sent.AddDays(5));

        var kept = Presses.Find(id, Sent.AddDays(5).AddHours(23));
        Assert.NotNull(kept);
        Assert.Null(kept.WaitingAt);
        Assert.Null(Presses.Find(id, Sent.AddDays(5).AddHours(25)));
    }

    /// <summary>The prune another press's keep runs reads the same rule: an ended press's file goes after its day, any other's stays.</summary>
    [Fact]
    public void The_prune_reads_the_same_rule_as_the_find()
    {
        var waiting = Keep(Sent);
        Presses.Waiting(waiting, Sent.AddHours(1));
        var done = Keep(Sent);
        Presses.Ended(done, Sent.AddHours(1));

        Keep(Sent.AddDays(2));

        Assert.True(File.Exists(PathOf(waiting)));
        Assert.False(File.Exists(PathOf(done)));

        Keep(Sent.AddDays(31));

        Assert.False(File.Exists(PathOf(waiting)));
    }

    /// <summary>
    /// CONFIRMWAIT1 (§15.2 item 6): the keys live in <c>profiles/pipe-sent</c>, so clearing <c>run/</c> while Daoris is closed, as D172
    /// allows, loses none; a press kept under <c>run/pipe/sent</c> by a build before is still found there.
    /// </summary>
    [Fact]
    public void Clearing_run_loses_no_key_and_a_press_kept_by_a_build_before_is_still_found()
    {
        var id = Keep(Sent);
        var run = Path.GetDirectoryName(DaorisHome.Entry(_home, HomeEntry.Pipe))!;
        if (Directory.Exists(run)) Directory.Delete(run, recursive: true);

        Assert.NotNull(Presses.Find(id, Sent.AddHours(1)));

        var before = Guid.NewGuid().ToString();
        var legacy = Path.Combine(DaorisHome.Entry(_home, HomeEntry.Pipe), "sent");
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, before + ".json"),
            $"{{\"id\":\"{before}\",\"key\":\"k0123456789abcdef\",\"route\":\"DAORIS.SETUP.SETUP_PRESS\",\"at\":\"{Sent:O}\"}}" + "\n");

        Assert.Equal("k0123456789abcdef", Presses.Find(before, Sent.AddHours(1))?.Key);
    }
}
