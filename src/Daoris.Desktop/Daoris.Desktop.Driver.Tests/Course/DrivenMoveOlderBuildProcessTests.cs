using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

[Trait(Category.Name, Category.Process)]
public sealed class DrivenMoveOlderBuildProcessTests : IAsyncLifetime
{
    private readonly DrivenMoveHarness _h = new("driven-move-older");

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _h.DisposeAsync();

    private string Shapes() =>
        string.Join(" ; ", Enumerable.Range(1, _h.Service.SessionCount).Select(at => $"s{at}: " + string.Join(" | ", _h.Store("run-probe").Of($"s{at}").Select(move => $"{move.PhaseWord}/{move.HeldWhy}/{move.ClosedWhy}/{move.FellBackCode}"))));

    [Fact]
    public async Task ExploreSuccessor()
    {
        await _h.SeedAsync(word: true);
        await _h.KillAtAsync(Cuts.Opened);
        var s3 = await _h.OlderOpenAsync();
        await _h.KillAsync();
        _h.ComeUp();
        await _h.SettleAsync();
        await _h.StopAsync();
        Assert.Fail($"successor {s3}: {Shapes()} | {_h.Seen()}");
    }

    [Fact]
    public async Task ExploreDelayed()
    {
        await _h.SeedAsync(word: true);
        await _h.KillAtAsync(cut: null);
        var s3 = await _h.OlderOpenAsync();
        Assert.Equal(200, await _h.AdvanceAsync(s3, "stopped", interrupted: true));
        Assert.Equal("w2", await _h.Service.SayAsync(s3, DrivenMoveHarness.Word));
        await _h.KillAsync();
        _h.ComeUp();
        await _h.SettleAsync();
        await _h.StopAsync();
        Assert.Fail($"delayed {s3}: {Shapes()} | {_h.Seen()}");
    }
}
