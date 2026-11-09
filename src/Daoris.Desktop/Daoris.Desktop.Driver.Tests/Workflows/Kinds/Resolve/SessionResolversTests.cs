using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The loop's resolvers (<see cref="SessionResolvers"/>): the tree's newest session asked through the say door under the row's ask key, its
/// turn's end read from its record, the ask's time and the step's lapse each ending the wait, the person's stop read as declined, and the
/// stop through the session's stop door awaited until nothing runs.
/// </summary>
public sealed class SessionResolversTests
{
    private const string Tree = "trees/default/engine/aurora";

    private static TreeHandOff Row(TimeSpan within) =>
        new(Tree, "engine", "daoris/aurora", "t1", "r-12", "resolve", 1, "stop", "session", TreeHandOff.AskKey("r-12", "resolve", 1, 1),
            TreeHandOffPhases.Asked, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow + within);

    [Fact]
    public async Task The_tree_s_newest_session_is_asked_and_one_the_person_stopped_is_declined_said()
    {
        var world = new World(
            new PriorSession("s-40", Tree, "completed") { Created = DateTimeOffset.UtcNow.AddHours(-2) },
            new PriorSession("s-41", Tree, "completed") { Created = DateTimeOffset.UtcNow.AddHours(-1) },
            new PriorSession("s-50", "trees/default/engine/other", "completed") { Created = DateTimeOffset.UtcNow });

        Assert.Equal(("s-41", (string?)null), await world.Resolvers.SessionAsync(Row(TimeSpan.FromMinutes(30)), CancellationToken.None));

        world.Records[1] = world.Records[1] with { State = "stopped" };
        var (session, why) = await world.Resolvers.SessionAsync(Row(TimeSpan.FromMinutes(30)), CancellationToken.None);
        Assert.Null(session);
        Assert.Contains("`s-41`", why, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_session_its_own_resolve_stopped_is_asked_again()
    {
        var world = new World(new PriorSession("s-41", Tree, "stopped")
        {
            Created = DateTimeOffset.UtcNow, NoteParts = [new NotePart { Code = EndNoteCodes.EndedResolveAsk.Code }],
        });

        Assert.Equal(("s-41", (string?)null), await world.Resolvers.SessionAsync(Row(TimeSpan.FromMinutes(30)), CancellationToken.None));
    }

    [Fact]
    public async Task The_ask_is_said_under_its_key_and_ends_once_the_turn_that_took_it_has_ended()
    {
        var world = new World(new PriorSession("s-41", Tree, "completed") { Created = DateTimeOffset.UtcNow });
        var row = Row(TimeSpan.FromMinutes(30));
        world.OnSaid = () =>
        {
            world.Records[0] = world.Records[0] with { State = "working" };
            world.Running.Add("s-41");
        };

        var asking = world.Resolvers.AskAsync(row, "s-41", "settle it", () => false, CancellationToken.None);
        await world.Polled(3);
        Assert.False(asking.IsCompleted);
        world.Records[0] = world.Records[0] with { State = "completed", Said = [] };
        world.Running.Remove("s-41");

        Assert.Equal(ResolverEnds.Ended, await asking.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal([("s-41", "settle it", row.Ask)], world.Said);
    }

    [Fact]
    public async Task The_ask_s_time_running_out_ends_it_timed_out()
    {
        var world = new World(new PriorSession("s-41", Tree, "completed") { Created = DateTimeOffset.UtcNow });
        world.OnSaid = () => world.Running.Add("s-41");

        Assert.Equal(ResolverEnds.TimedOut, await world.Resolvers.AskAsync(Row(TimeSpan.FromMilliseconds(200)), "s-41", "settle it", () => false, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task A_lapse_ends_the_wait_lapsed_and_the_person_s_stop_declined()
    {
        var world = new World(new PriorSession("s-41", Tree, "completed") { Created = DateTimeOffset.UtcNow });
        world.OnSaid = () => world.Running.Add("s-41");
        var lapsed = false;

        var asking = world.Resolvers.AskAsync(Row(TimeSpan.FromMinutes(30)), "s-41", "settle it", () => lapsed, CancellationToken.None);
        await world.Polled(2);
        lapsed = true;
        Assert.Equal(ResolverEnds.Lapsed, await asking.WaitAsync(TimeSpan.FromSeconds(10)));

        var stopping = world.Resolvers.AskAsync(Row(TimeSpan.FromMinutes(30)), "s-41", "settle it", () => false, CancellationToken.None);
        await world.Polled(2);
        world.Records[0] = world.Records[0] with { State = "stopped" };
        world.Running.Remove("s-41");
        Assert.Equal(ResolverEnds.Declined, await stopping.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task Words_the_say_door_did_not_keep_end_the_ask_untold()
    {
        var world = new World(new PriorSession("s-41", Tree, "completed") { Created = DateTimeOffset.UtcNow }) { Keeps = false };

        Assert.Equal(ResolverEnds.Untold, await world.Resolvers.AskAsync(Row(TimeSpan.FromMinutes(30)), "s-41", "settle it", () => false, CancellationToken.None));
    }

    [Fact]
    public async Task The_stop_goes_through_the_session_s_stop_door_as_the_resolve_s_and_waits_until_nothing_runs()
    {
        var world = new World(new PriorSession("s-41", Tree, "working") { Created = DateTimeOffset.UtcNow });
        world.Running.Add("s-41");
        world.OnStopped = () => _ = Task.Run(async () =>
        {
            await Task.Delay(100);
            world.Running.Remove("s-41");
        });

        await world.Resolvers.StopAsync(Row(TimeSpan.FromMinutes(30)) with { Session = "s-41" }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal([("s-41", EndNoteCodes.EndedResolveAsk.Code)], world.Stopped);
        Assert.DoesNotContain("s-41", world.Running);
    }

    [Fact]
    public async Task A_resolver_that_runs_nothing_is_stopped_with_no_stop_said()
    {
        var world = new World(new PriorSession("s-41", Tree, "completed") { Created = DateTimeOffset.UtcNow });

        await world.Resolvers.StopAsync(Row(TimeSpan.FromMinutes(30)) with { Session = "s-41" }, CancellationToken.None);

        Assert.Empty(world.Stopped);
    }

    /// <summary>The service's records, the say door and the session processes, held in memory.</summary>
    private sealed class World
    {
        private int _polls;

        public World(params PriorSession[] records)
        {
            Records = [.. records];
            Resolvers = new SessionResolvers(new ResolverSessions(
                _ =>
                {
                    Interlocked.Increment(ref _polls);
                    return Task.FromResult<IReadOnlyCollection<PriorSession>>(Records.ToArray());
                },
                (session, text, key, _) =>
                {
                    if (!Keeps) return Task.FromResult(new SayAnswer(false, "not kept"));
                    Said.Add((session, text, key));
                    var at = Records.FindIndex(each => each.Session == session);
                    Records[at] = Records[at] with { Said = [new SaidWordView(null, text, DateTimeOffset.UtcNow, [], false) { Key = key }] };
                    OnSaid?.Invoke();
                    return Task.FromResult(new SayAnswer(true, "kept"));
                },
                Running.Contains,
                (session, note) =>
                {
                    if (!Running.Contains(session)) return false;
                    Stopped.Add((session, note.Parts[0].Code));
                    OnStopped?.Invoke();
                    return true;
                })
            {
                Poll = TimeSpan.FromMilliseconds(10),
            });
        }

        public List<PriorSession> Records { get; }

        public SessionResolvers Resolvers { get; }

        public bool Keeps { get; init; } = true;

        public RunningSet Running { get; } = new();

        public List<(string Session, string Text, string Key)> Said { get; } = [];

        public List<(string Session, string? Code)> Stopped { get; } = [];

        public Action? OnSaid { get; set; }

        public Action? OnStopped { get; set; }

        /// <summary>Completes once the resolver has read the records <paramref name="times"/> more times.</summary>
        public async Task Polled(int times)
        {
            var until = Volatile.Read(ref _polls) + times;
            while (Volatile.Read(ref _polls) < until) await Task.Delay(5);
        }
    }

    /// <summary>The sessions whose process runs here, read by the resolver's wait while the test moves them.</summary>
    private sealed class RunningSet : IEnumerable<string>
    {
        private readonly HashSet<string> _running = [];

        public void Add(string session)
        {
            lock (_running) _running.Add(session);
        }

        public void Remove(string session)
        {
            lock (_running) _running.Remove(session);
        }

        public bool Contains(string session)
        {
            lock (_running) return _running.Contains(session);
        }

        public IEnumerator<string> GetEnumerator()
        {
            lock (_running) return _running.ToList().GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
