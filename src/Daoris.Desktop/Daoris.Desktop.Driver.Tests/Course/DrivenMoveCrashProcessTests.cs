using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

[Trait(Category.Name, Category.Process)]
public sealed class DrivenMoveCrashProcessTests : IAsyncLifetime
{
    private readonly DrivenMoveHarness _h = new("driven-move-crash");

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _h.DisposeAsync();

    [Fact]
    public async Task Census()
    {
        await _h.SeedAsync(word: true);
        await _h.KillAtAsync(cut: null);
        await Task.Delay(500);
        Assert.Fail($"passed [{string.Join(",", _h.Passed)}] {_h.Seen()}");
    }

    [Theory]
    [InlineData("decided")]
    [InlineData("opening")]
    [InlineData("answered")]
    [InlineData("opened")]
    [InlineData("marked")]
    [InlineData("written")]
    [InlineData("sent")]
    [InlineData("taken")]
    public async Task Explore(string cut)
    {
        await _h.SeedAsync(word: false);
        await _h.KillAtAsync(cut);
        await _h.KillAsync();
        _h.ComeUp();
        await _h.SettleAsync();
        await _h.StopAsync();
        var prompts = string.Join(" || ", _h.Prompts().Select(p => $"{p.Session}:{p.Text.Length}:{DrivenMoveHarness.Count(p.Text, DrivenMoveHarness.Word)}"));
        var codes = string.Join(" ; ", Enumerable.Range(1, _h.Service.SessionCount).Select(at => $"s{at}: " + string.Join(",", _h.Events.After($"s{at}", 0).Events.Select(e => e.Code ?? e.Kind.ToString()))));
        Assert.Fail($"cut {cut} events [{codes}] opened [{string.Join(",", _h.Service.Opened.Select(o => o.Key[^6..]))}] changes [{string.Join(",", _h.Changes)}] prompts [{prompts}] {_h.Seen()}");
    }
}
