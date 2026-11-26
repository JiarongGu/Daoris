using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris queue</c>: the machine's waiting items with their places, through the running Daoris's <c>QUEUES</c> where it answers, so its
/// last look is listed, else this process's plan; the same lines either way (<see cref="QueueCommand.Lines"/>).
/// </summary>
public sealed class QueueCommandTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    private static readonly VerifyLook Slots = new([], [
        new VerifyWaiter(1, 1, "a", "i-a", "Daoris", Now.AddMinutes(-20), 0) { Wait = CheckWait.OfSlots(1, 1) },
        new VerifyWaiter(1, 1, "u", "i-u", "Daoris", Now.AddMinutes(-1), 1)
        {
            Wait = CheckWait.OfSlots(1, 1), Class = PriorityClasses.Urgent, Source = PriorityBook.Quest, By = "the person", Why = "the fix",
        },
    ]);

    private static QueueWorld World(string home, InstanceWorld? instance = null) =>
        new(home, _ => Task.FromResult<IReadOnlyList<Consideration>>([])) { Slots = () => Slots, Instance = instance };

    private static async Task<(int Exit, string Said)> RunAsync(QueueWorld world, params string[] args)
    {
        using var said = new StringWriter();
        var exit = await QueueCommand.RunAsync(args, world, said);
        return (exit, said.ToString());
    }

    [Fact]
    public async Task Alone_it_lists_each_waiting_item_with_its_place_and_the_items_ahead()
    {
        var (exit, said) = await RunAsync(World("unused"));

        Assert.Equal(0, exit);
        var lines = said.TrimEnd().Split(Environment.NewLine);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("checks 1 of 2: the checks of run #u, urgent (set by the person: the fix), first checks;", lines[0], StringComparison.Ordinal);
        Assert.EndsWith("; ahead: the checks of run #u, urgent (set by the person: the fix), first checks.", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_json_it_prints_the_rows_the_route_answers()
    {
        var (exit, said) = await RunAsync(World("unused"), "--json");

        Assert.Equal(0, exit);
        using var answer = JsonDocument.Parse(said);
        var rows = answer.RootElement.GetProperty("rows").EnumerateArray().Select(QueueRows.Parse).ToList();
        Assert.Equal(["a", "u"], rows.Select(row => row!.Item));
        Assert.Equal(["u", "a"], QueueRows.Placed(rows!).Select(spot => spot.Row.Item));
    }

    [Theory]
    [InlineData("--all")]
    [InlineData("--json", "--json")]
    public async Task Words_it_does_not_take_are_the_usage(params string[] args)
    {
        var (exit, said) = await RunAsync(World("unused"), args);

        Assert.Equal(2, exit);
        Assert.Contains(QueueCommand.Usage, said);
    }

    [Fact]
    public async Task Through_the_instance_the_lines_are_the_ones_it_answers_and_this_process_plans_nothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        var lines = new[] { "starts 1 of 1: quest #q, normal, a new start; waits: the cap is spent." };
        standIn.Data = new { rows = Array.Empty<object>(), said = new TreesSaid(lines, [], 0).ToWire() };
        var planned = 0;
        var world = new QueueWorld(home.Home, _ =>
        {
            planned++;
            return Task.FromResult<IReadOnlyList<Consideration>>([]);
        }) { Instance = home.World("queue") };

        var (exit, said) = await RunAsync(world);

        Assert.Equal(0, exit);
        Assert.Equal(string.Join(Environment.NewLine, lines) + Environment.NewLine, said);
        Assert.Equal(0, planned);
        var press = Assert.Single(standIn.Presses);
        Assert.Equal("QUEUES", press.GetProperty("type").GetString());
        Assert.True(press.GetProperty("payload").GetProperty("said").GetBoolean());
    }
}
