using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTSPLACE1b3: <c>daoris place … --apply</c> through the running Daoris (the terminal RPC design §7.2): the apply is the person's, sent
/// once to the instance's <c>PLACE_APPLY</c> with the acceptances the words named, which shows one card in its window; the terminal prints the
/// lines the route answers (<c>said</c>, the alone path's own words) and applies nothing itself. <c>--plan</c> stays a read of <c>PLACE_PLAN</c>.
/// </summary>
public sealed class PlaceThroughInstanceTests
{
    private static readonly SetupPresetsTests.Catalog Plugins = new();

    private static Task<int> RunAsync(string[] args, string home, InstanceWorld? instance, TextWriter output) =>
        PlaceCommand.RunAsync(args, output, new PlacePlanTests.World(), DriverConfig.Empty, home, Plugins, SessionWire.Pipe, instance);

    [Fact]
    public async Task The_apply_through_the_instance_is_one_press_waiting_for_the_window_and_prints_the_lines_it_answers()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Confirming = true;
        string[] lines = ["place: `engine` — Integrated into the line", "place: configuration applied."];
        standIn.Data = new { done = true, said = new TreesSaid(lines, [], 0).ToWire() };
        var output = new StringWriter();

        var exit = await RunAsync(
            ["engine", "--preset", "integrated", "--apply", "--accept", "engine:integration"], home.Home, home.World("place"), output);

        Assert.Equal(0, exit);
        Assert.Equal(string.Concat(lines.Select(line => line + Environment.NewLine)), output.ToString());
        Assert.Matches(InstanceStandIn.WaitingAlone("place"), home.Error.ToString());
        var press = Assert.Single(standIn.Presses);
        Assert.Equal("PLACE_APPLY", press.GetProperty("type").GetString());
        Assert.Equal(
            """{"repositories":["engine"],"preset":"integrated","opinion":false,"look":false,"adoption":false,"drive":false,"accept":[{"repository":"engine","purpose":"integration"}],"said":true}""",
            press.GetProperty("payload").GetRawText());
    }

    [Fact]
    public async Task The_plan_through_the_instance_is_a_read_and_waits_on_nothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Data = new { said = new TreesSaid(["place: `engine`"], [], 0).ToWire() };
        var output = new StringWriter();

        var exit = await RunAsync(["engine", "--preset", "integrated", "--plan"], home.Home, home.World("place"), output);

        Assert.Equal(0, exit);
        Assert.Equal("", home.Error.ToString());
        Assert.Equal("PLACE_PLAN", Assert.Single(standIn.Presses).GetProperty("type").GetString());
    }

    [Theory]
    [InlineData("refused")]
    [InlineData("expired")]
    public async Task An_apply_the_person_refuses_or_leaves_unanswered_is_exit_1_and_applies_nothing_here(string code)
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Confirming = true;
        standIn.Press = StandInPress.Refuse;
        standIn.Code = code;
        var output = new StringWriter();

        var exit = await RunAsync(["engine", "--preset", "integrated", "--apply"], home.Home, home.World("place"), output);

        Assert.Equal(1, exit);
        Assert.Equal("", output.ToString());
        Assert.Contains($"({code})", home.Error.ToString());
    }

    [Fact]
    public async Task Words_it_does_not_take_are_its_usage_before_anything_is_sent()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        var output = new StringWriter();

        var exit = await RunAsync(["engine", "--preset", "integrated", "--apply", "--plan"], home.Home, home.World("place"), output);

        Assert.Equal(2, exit);
        Assert.Empty(standIn.Presses);
    }
}
