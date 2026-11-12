using System.Text.Json;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// What the shell asks of the service's session records on a home of a working machine's size (<see cref="BenchHome"/>, 3,000
/// records), each held to its budget: a slice's push after one record moved, and a landing's plan. Each reads every record at most once,
/// only as its change since the kept list's mark.
/// </summary>
/// <remarks>
/// The reads are counted at the client's doors (<see cref="BenchDoors"/>), so a read through any of the shell's doors is counted, and a
/// read is held by how often it asked, which a neighbour's load does not move. Each runs once cold and is counted warm, the second time.
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
public sealed class SessionReadsBenchProcessTests(ITestOutputHelper output) : DriverModuleBridge, IAsyncLifetime
{
    private BenchHome? _bench;

    private BenchHome Bench => _bench!;

    public async Task InitializeAsync() => _bench = await BenchHome.MakeAsync(3000, 2, 0);

    public async Task DisposeAsync()
    {
        if (_bench is not null) await _bench.DisposeAsync();
    }

    /// <summary>A loop come up over the bench's service, its home the bench's, so its trees are the loop's own.</summary>
    private async Task<(DriverLoop Loop, DriverModule Module)> UpAsync()
    {
        Redirect("DAORIS_HOME", Bench.Home);
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(Bench.Client);
        await loop.Settling;
        return (loop, module);
    }

    [Fact]
    public async Task A_slice_s_push_after_one_change_reads_that_change_alone()
    {
        var (loop, _) = await UpAsync();
        var sessions = loop.Slices.Find(SessionsStateSlice.Name)!;
        await sessions.JudgeAsync();

        await Bench.Client.Words.SayAsync(Bench.Trees[^1].Session, "The first word.");
        var asked = await CountedAsync("the sessions slice's push", () => sessions.JudgeAsync());

        Assert.Equal(1, BenchDoors.ListReads(asked));
        Assert.Equal(0, asked.GetValueOrDefault(BenchDoors.WholeList));
        Assert.Equal(0, asked.GetValueOrDefault(BenchDoors.Open));

        // The groups' slice, which every change and every look wakes, over the verdicts a running loop's look left: every record once, as
        // its change. Its time is said, not held: with the machine's facts not started here, it walks each tree with git itself.
        loop.Look.Record([]);
        var groups = loop.Slices.Find(GroupsStateSlice.Name)!;
        await groups.JudgeAsync();
        await Bench.Client.Words.SayAsync(Bench.Trees[^1].Session, "The second word.");
        var grouped = await CountedAsync("the groups slice's push", () => groups.JudgeAsync());
        Assert.Equal(1, BenchDoors.ListReads(grouped));
        Assert.Equal(0, grouped.GetValueOrDefault(BenchDoors.WholeList));
    }

    [Fact]
    public async Task A_landing_s_plan_reads_every_record_once()
    {
        var (_, module) = await UpAsync();
        var session = Bench.Trees[^1].Session;
        await AskAsync(module, "LANDING", new { id = session });

        var planned = await CountedAsync("the landing's plan", () => AskAsync(module, "LANDING", new { id = session }));

        Assert.True(BenchDoors.ListReads(planned) <= 1, $"the plan read every record {BenchDoors.ListReads(planned)} times");
        Assert.Equal(0, planned.GetValueOrDefault(BenchDoors.WholeList));
    }

    [Fact]
    public async Task The_groups_look_reads_the_list_once_and_its_text_once_for_every_chat()
    {
        var (loop, _) = await UpAsync();
        var times = new LookTimes();
        await SessionGroups.LookAsync(Bench.Client, DriverConfig.Load(loop.ConfigPath), false, [], loop.Home, [], CancellationToken.None, times: times);
        Bench.Doors.Take();

        times = new LookTimes();
        await SessionGroups.LookAsync(Bench.Client, DriverConfig.Load(loop.ConfigPath), false, [], loop.Home, [], CancellationToken.None, times: times);
        output.WriteLine($"the groups' look: asked {Bench.Doors.Take()}; {string.Join(", ", times.Steps.Select(step => $"{step.Step} {step.Ms} ms"))}");

        // Every record once, as its change; and the words waiting on three thousand ended chats read from one parse of the list's text,
        // where a parse for each made this step 37 seconds.
        Assert.Equal(1, BenchDoors.ListReads(Bench.Doors.LastTaken));
        Assert.Equal(0, Bench.Doors.LastTaken.GetValueOrDefault(BenchDoors.Open));
        var waiting = times.Steps.Single(step => step.Step == "waiting").Ms;
        Assert.True(waiting < WaitingBudget.TotalMilliseconds, $"the words waiting took {waiting} ms, past {WaitingBudget.TotalMilliseconds} ms");
    }

    // Three thousand lookups and a parse of the list, held well above what it takes so a neighbour's load does not break it.
    private static readonly TimeSpan WaitingBudget = TimeSpan.FromSeconds(2);

    /// <summary>What <paramref name="read"/> asked of the service, by path, said beside what it answered.</summary>
    private async Task<IReadOnlyDictionary<string, int>> CountedAsync<T>(string what, Func<Task<T>> read)
    {
        Bench.Doors.Take();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var answered = await read();
        output.WriteLine($"{what}: {watch.ElapsedMilliseconds} ms; asked {Bench.Doors.Take()}");
        var said = JsonSerializer.Serialize(answered);
        output.WriteLine($"  answered {said[..Math.Min(400, said.Length)]}");
        return Bench.Doors.LastTaken;
    }

    private Task<IReadOnlyDictionary<string, int>> CountedAsync(string what, Func<Task> read) =>
        CountedAsync(what, async () =>
        {
            await read();
            return true;
        });
}
