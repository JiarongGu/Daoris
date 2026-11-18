using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris work held &lt;run&gt; &lt;node&gt; &lt;n&gt; again|…</c> through the running Daoris (SWITCHGAPS1d): the person's word is the
/// window's <c>WORK_HELD</c> press, sent to the instance, which records it under the lease it holds; the terminal prints the lines the
/// route answers and refuses nothing with "say it there". Seen failing first.
/// </summary>
public sealed class WorkHeldThroughInstanceTests
{
    [Theory]
    [InlineData(new[] { "q7", "release-push", "2", "again" }, """{"unit":"q7","node":"release-push","attempt":2,"resolution":"again","said":true}""")]
    [InlineData(new[] { "q7", "go", "1", "done-outside", "https://x.example/r/1", "shipped" },
        """{"unit":"q7","node":"go","attempt":1,"resolution":"done-outside","link":"https://x.example/r/1","note":"shipped","said":true}""")]
    [InlineData(new[] { "q7", "go", "1", "stop", "--epoch", "3" }, """{"unit":"q7","node":"go","attempt":1,"resolution":"stop","epoch":3,"said":true}""")]
    public async Task A_word_through_the_instance_is_its_press_and_prints_the_lines_it_answers(string[] args, string payload)
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Confirming = true;
        string[] lines = ["work: your word is recorded; the next look applies it."];
        standIn.Data = new { said = new TreesSaid(lines, [], 0).ToWire() };
        var output = new StringWriter();

        var exit = await HeldAttempts.RunAsync(home.Home, args, output, new StringWriter(), home.World("work"));

        Assert.Equal(0, exit);
        Assert.Equal(lines[0] + Environment.NewLine, output.ToString());
        var press = Assert.Single(standIn.Presses);
        Assert.Equal("WORK_HELD", press.GetProperty("type").GetString());
        Assert.Equal(payload, press.GetProperty("payload").GetRawText());
    }

    /// <summary>A list is a read of the records, and a line that is no word is the usage, exit 2: neither is sent.</summary>
    [Fact]
    public async Task A_list_and_a_mistyped_word_are_not_sent()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        var output = new StringWriter();
        var error = new StringWriter();

        Assert.Equal(0, await HeldAttempts.RunAsync(home.Home, [], output, error, home.World("work")));
        Assert.Equal(2, await HeldAttempts.RunAsync(home.Home, ["q7", "go", "x", "again"], output, error, home.World("work")));
        Assert.Empty(standIn.Presses);
    }
}
