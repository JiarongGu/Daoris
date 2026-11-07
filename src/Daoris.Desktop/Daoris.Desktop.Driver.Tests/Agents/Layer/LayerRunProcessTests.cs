using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <see cref="LayerRun"/>, <see cref="AgentRun"/> over the layer one turn per call (the agent-lyntai design §4.1, §5.4, §5.5): a turn hands
/// its writer today's records in order and the transcript its lines, its end read as the conclusion reads it with the exit off the runner,
/// its failure through the ingress, a stop before the terminal a stop, a resume a start of its own with its own container, two starts
/// sharing nothing, and an operation the layer's members do not carry refused before a write. Real node processes over the tests' provider.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class LayerRunProcessTests : IDisposable
{
    private readonly string _scratch = ConformanceScratch.New("layer-run");

    public void Dispose() => ConformanceScratch.Remove(_scratch);

    private (LayerRun Run, List<WireReading> Readings) Run(object spec, string name = "scenario", string transcript = "transcript.log")
    {
        var command = LayerTestProvider.Scenario(_scratch, spec, name);
        var run = new LayerRun(
            LayerTestProvider.Registration(command),
            new LayerStart(LayerTestProvider.Id, "the prompt", Path.Combine(_scratch, transcript), TimeSpan.FromHours(1)),
            new LauncherSeat(ContainedKind.Agent, StartClass.Background, Home: null, Log: null),
            exists: _ => true);
        var readings = new List<WireReading>();
        run.HandSink(ReadingSinks.On<WireReading>(reading => { lock (readings) readings.Add(reading); }));
        return (run, readings);
    }

    private static string Said(WireReading reading) => reading switch
    {
        WireReading.Shown shown => "shown " + shown.Text,
        WireReading.Said said => "said " + said.Event.Kind + (said.Event.Text is { } text ? " " + text : ""),
        WireReading.ConversationNamed named => "named " + named.Conversation,
        WireReading.Failed failed => "failed " + failed.Failure.Words,
        _ => reading.GetType().Name,
    };

    [Fact]
    public async Task A_turn_hands_its_writer_today_s_records_in_order_and_ends_as_the_conclusion_reads_it()
    {
        var (run, readings) = Run(new
        {
            @out = new object[]
            {
                new { t = "start", id = "conv-1" }, "the harness's own line", new { t = "text", text = "hello" },
                new { t = "end", subtype = "success" },
            },
        });
        await using var _ = run;

        await run.OpenAsync(CancellationToken.None);
        var end = await run.EndAsync(CancellationToken.None);

        Assert.Equal(
            ["named conv-1", "shown the harness's own line", "shown hello", "said message hello", "shown — the turn ended: success", "said turn"],
            readings.Select(Said));
        Assert.Equal((0, true, false), (end.Exit, end.Named, end.Ending.Failed));
        Assert.Equal(["the harness's own line"], end.Ending.Lines);
        Assert.Null(end.Usage);
        Assert.Equal(
            ["the harness's own line", "hello", "— the turn ended: success"], await File.ReadAllLinesAsync(Path.Combine(_scratch, "transcript.log")));
    }

    [Fact]
    public async Task A_failed_turn_s_failure_enters_through_the_ingress_and_is_the_harness_s_ending()
    {
        var (run, readings) = Run(new { @out = new object[] { new { t = "end", kind = "Limit", by = "tests:limit", why = "the limit's words" } } });
        await using var _ = run;

        await run.OpenAsync(CancellationToken.None);
        var end = await run.EndAsync(CancellationToken.None);

        Assert.Equal("failed the limit's words", Said(readings[0]));
        Assert.Equal(("layer:tests:limit", AgentErrorKind.Limit), (run.Failure!.By, run.Failure.Kind));
        Assert.Equal("the limit's words", end.Ending.Failure);
        Assert.False(end.Named);
    }

    [Fact]
    public async Task A_stop_before_the_terminal_is_a_stop_never_the_fatal_exit_the_layer_reads_after_the_kill()
    {
        var (run, readings) = Run(new { @out = new[] { "up" }, hang = true });
        await using var _ = run;

        await run.OpenAsync(CancellationToken.None);
        while (!readings.ToArray().Any(each => each is WireReading.Shown { Text: "up" })) await Task.Delay(20);
        await run.StopTurnAsync(CancellationToken.None).WaitAsync(Poll.Patience);
        var end = await run.EndAsync(CancellationToken.None);

        Assert.True(run.Stopped);
        Assert.Null(run.Failure);
        Assert.DoesNotContain(readings, each => each is WireReading.Failed);
        Assert.Contains("shown — the turn was stopped", readings.Select(Said));
        Assert.Null(end.Ending.Failure);
    }

    [Fact]
    public async Task A_resume_is_a_start_of_its_own_with_its_own_container_and_a_closed_run_disposes_its_own()
    {
        var (first, _) = Run(new { @out = new object[] { new { t = "start", id = "conv-1" }, new { t = "end" } } });
        await first.OpenAsync(CancellationToken.None);
        await first.EndAsync(CancellationToken.None);
        var (resumed, readings) = Run(new { @out = new object[] { new { t = "end" } } }, "resumed");

        await resumed.ResumeAsync("conv-1", CancellationToken.None);
        await resumed.EndAsync(CancellationToken.None);
        await first.CloseAsync(CancellationToken.None);

        Assert.Equal("shown resumed conv-1", Said(readings[0]));
        Assert.NotSame(first.Container, resumed.Container);
        Assert.True(first.Container!.Disposed);
        Assert.False(resumed.Container!.Disposed);
        await resumed.CloseAsync(CancellationToken.None);
        Assert.True(resumed.Container.Disposed);
    }

    [Fact]
    public async Task Two_starts_overlapping_share_nothing_and_a_stop_of_one_leaves_the_other_running()
    {
        var (one, oneSaid) = Run(new { @out = new[] { "one up" }, hang = true }, "one", "one.log");
        var (two, twoSaid) = Run(new { @out = new[] { "two up" }, wait = 1500, after = new object[] { new { t = "end" } } }, "two", "two.log");
        await using var _ = one;
        await using var __ = two;

        await one.OpenAsync(CancellationToken.None);
        await two.OpenAsync(CancellationToken.None);
        while (!oneSaid.ToArray().Any(each => each is WireReading.Shown)) await Task.Delay(20);
        await one.StopTurnAsync(CancellationToken.None);
        var twoEnd = await two.EndAsync(CancellationToken.None);

        Assert.NotSame(one.Container, two.Container);
        Assert.Equal(0, twoEnd.Exit);
        Assert.Contains("shown — the turn ended: success", twoSaid.Select(Said));
        Assert.DoesNotContain(oneSaid, each => each is WireReading.Shown { Text: "two up" });
    }

    [Fact]
    public async Task An_operation_the_layer_s_members_do_not_carry_is_refused_before_a_write()
    {
        var (run, readings) = Run(new { @out = new object[] { new { t = "end" } } });
        await using var _ = run;

        var task = await Assert.ThrowsAsync<DriverException>(() => run.StopTaskAsync("t1", CancellationToken.None));

        Assert.Contains(LayerRun.TaskAbility, task.Message);
        Assert.Null(run.Words);
        Assert.Empty(readings);
        Assert.Null(run.Container);
    }
}
