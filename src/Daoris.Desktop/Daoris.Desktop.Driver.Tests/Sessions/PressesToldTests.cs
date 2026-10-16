using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A terminal's press reaches an open window (STATEHUB1e, D169 point 8): `daoris-driver trees land|remove|clean|sync|hand` run in a
/// process with no bus, so each tells what it pressed in the request folder every loop already watches, and the loop that hears
/// them (the shell's) takes each once.
/// </summary>
public sealed class PressesToldTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-told-" + Guid.NewGuid().ToString("N")[..8]);

    public PressesToldTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private string Folder => DaorisHome.Entry(_home, HomeEntry.Requests);

    private IEnumerable<string> Told() =>
        Directory.Exists(Folder) ? Directory.EnumerateFiles(Folder, "*" + PressesTold.Suffix) : [];

    private SessionRequestWatch Hearing(List<PressTold> heard, DateTimeOffset? now = null) =>
        new(_home, runsHere: _ => false, stop: _ => true, service: () => null, clock: () => now ?? Now) { Told = heard.Add };

    private SessionRequestWatch Deaf(DateTimeOffset? now = null) =>
        new(_home, runsHere: _ => false, stop: _ => true, service: () => null, clock: () => now ?? Now);

    [Fact]
    public async Task A_press_told_by_another_process_is_heard_once_oldest_first_by_the_loop_that_hears_them()
    {
        var presses = new PressesTold(_home);
        Assert.True(presses.Tell(new PressTold(PressesTold.Sweep, null, Now.AddSeconds(-2))));
        Assert.True(presses.Tell(new PressTold(PressesTold.Land, "s1a2b3c4", Now.AddSeconds(-5))));
        var heard = new List<PressTold>();
        await using var watch = Hearing(heard);

        Assert.Equal(2, watch.Hear().Count);
        Assert.Empty(watch.Hear());

        Assert.Equal(
            [new PressTold(PressesTold.Land, "s1a2b3c4", Now.AddSeconds(-5)), new PressTold(PressesTold.Sweep, null, Now.AddSeconds(-2))],
            heard);
        Assert.Empty(Told());
    }

    [Fact]
    public async Task A_loop_that_hears_none_leaves_them_for_one_that_does_and_drops_one_nobody_took_in_time()
    {
        var presses = new PressesTold(_home);
        presses.Tell(new PressTold(PressesTold.Land, "s1a2b3c4", Now.AddSeconds(-5)));
        presses.Tell(new PressTold(PressesTold.Sweep, null, Now - PressesTold.Lifetime - TimeSpan.FromSeconds(1)));
        await using var headless = Deaf();

        // A headless loop has no window to tell: it takes nothing, and drops only what nobody took in time.
        Assert.Empty(headless.Hear());
        Assert.Single(Told());

        var heard = new List<PressTold>();
        await using var shell = Hearing(heard);
        shell.Hear();
        Assert.Equal([new PressTold(PressesTold.Land, "s1a2b3c4", Now.AddSeconds(-5))], heard);
    }

    [Fact]
    public async Task One_told_long_ago_is_dropped_unheard_by_the_loop_that_hears_them()
    {
        new PressesTold(_home).Tell(new PressTold(PressesTold.Land, "s1a2b3c4", Now.AddMinutes(-2)));
        var heard = new List<PressTold>();
        await using var watch = Hearing(heard);

        Assert.Empty(watch.Hear());
        Assert.Empty(heard);
        Assert.Empty(Told());
    }

    [Fact]
    public async Task Two_loops_hearing_at_once_hear_each_press_once()
    {
        var presses = new PressesTold(_home);
        for (var i = 0; i < 20; i++) presses.Tell(new PressTold(PressesTold.Sweep, null, Now.AddSeconds(-i)));
        var one = new List<PressTold>();
        var two = new List<PressTold>();
        await using var first = Hearing(one);
        await using var second = Hearing(two);

        await Task.WhenAll(Task.Run(first.Hear), Task.Run(second.Hear));
        // A note something else held at that moment waits for the next look, and is heard then, once.
        first.Hear();

        Assert.Equal(20, one.Count + two.Count);
        Assert.Equal(20, one.Concat(two).Distinct().Count());
    }

    [Fact]
    public async Task What_does_not_read_as_a_press_is_dropped_and_a_session_that_is_no_id_of_this_machine_is_none()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "a1b2c3d4e5f6" + PressesTold.Suffix), "not json");
        File.WriteAllText(Path.Combine(Folder, "b1b2c3d4e5f6" + PressesTold.Suffix), """{"session":"s1"}""");
        File.WriteAllText(Path.Combine(Folder, "c1b2c3d4e5f6" + PressesTold.Suffix),
            """{"route":"LAND_SESSION_TREE","session":"../../somewhere","at":"2026-10-16T08:59:59Z"}""");
        var heard = new List<PressTold>();
        await using var watch = Hearing(heard);

        watch.Hear();

        // Read without the path a session cannot be: the change is about any session, never a path on the wire.
        Assert.Equal([new PressTold(PressesTold.Land, null, Now.AddSeconds(-1))], heard);
        Assert.Empty(Told());
    }

    [Fact]
    public async Task A_press_told_is_no_request_and_a_request_no_press()
    {
        var requests = new SessionRequests(_home);
        requests.Write(new SessionRequest("s1", SessionMove.Stop, Now));
        new PressesTold(_home).Tell(new PressTold(PressesTold.DiscardTree, null, Now));
        var heard = new List<PressTold>();
        await using var watch = Hearing(heard);

        Assert.Equal(["s1"], requests.Pending(Now).Select(request => request.Session));
        Assert.Single(watch.Hear());
        Assert.True(requests.IsPending("s1"));
    }

    [Fact]
    public void Telling_where_nothing_can_be_written_is_false_and_never_an_error()
    {
        var file = Path.Combine(_home, "a-file");
        File.WriteAllText(file, "");

        Assert.False(new PressesTold(file).Tell(new PressTold(PressesTold.Land, "s1a2b3c4", Now)));
    }
}
