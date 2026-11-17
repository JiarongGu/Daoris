using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// <c>UX_TRAIL</c>, the window's door onto the person's own UX trail:<c>{}</c> reads whether it is on, <c>{ on }</c> turns it
/// on or off, and <c>{ entries }</c> keeps what the window recorded, nothing while it is off; each answered <c>{ on, kept }</c> from
/// memory, its writes made after.
/// </summary>
public sealed class DriverModuleUxTrailTests : DriverModuleBridge
{
    private static readonly object Page = new { entries = new[] { new { kind = "page", page = "/sessions", from = "/overview" } } };

    /// <summary>The trail's folder, the home's entry asked by name.</summary>
    private string Trail => DaorisHome.Entry(Home, HomeEntry.UxTrail);

    [Fact]
    public async Task Off_it_keeps_nothing_and_on_it_keeps_the_window_s_entries_under_the_home()
    {
        var module = Module();

        Assert.False((await AnswerAsync(module, "UX_TRAIL", new { })).GetProperty("on").GetBoolean());
        Assert.Equal(0, (await AnswerAsync(module, "UX_TRAIL", Page)).GetProperty("kept").GetInt32());
        Assert.False(Directory.Exists(Trail));

        Assert.True((await AnswerAsync(module, "UX_TRAIL", new { on = true })).GetProperty("on").GetBoolean());
        Assert.Equal(1, (await AnswerAsync(module, "UX_TRAIL", Page)).GetProperty("kept").GetInt32());
        await UntilAsync(() => Directory.Exists(Trail) && Directory.EnumerateFiles(Trail, "*.jsonl").Any());
        Assert.True(new UxTrail(Home).On);
        // A module built later reads the switch as it stands.
        Assert.True((await AnswerAsync(Module(), "UX_TRAIL", new { })).GetProperty("on").GetBoolean());

        Assert.False((await AnswerAsync(module, "UX_TRAIL", new { on = false })).GetProperty("on").GetBoolean());
        await UntilAsync(() => !new UxTrail(Home).On);
    }
}
