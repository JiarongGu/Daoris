using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

[Trait(Category.Name, Category.Process)]
public sealed class DrivenMoveDowngradeProcessTests : IAsyncLifetime
{
    private readonly DrivenMoveHarness _h = new("driven-move-downgrade");

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _h.DisposeAsync();

    public static TheoryData<string, bool> Boundaries
    {
        get
        {
            var data = new TheoryData<string, bool>();
            foreach (var cut in Cuts.All.Where(cut => cut != Cuts.OldEnded))
            {
                data.Add(cut, true);
                data.Add(cut, false);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Boundaries))]
    public async Task Explore(string cut, bool word)
    {
        await _h.SeedAsync(word);
        await _h.KillAtAsync(cut);
        var left = _h.MoveFrom("s1")!;
        await _h.EndLiveAsync();

        _h.ComeUp();
        await _h.SettleAsync(movesMayStand: true);
        await _h.StopAsync();
        var older = $"older: {Shapes()} claim {_h.MoveFrom("s1")!.ClaimRun}/{_h.MoveFrom("s1")!.ClaimGeneration} | {_h.Seen()}";

        _h.Die(DrivenMoveHarness.Killed);
        _h.ComeUp();
        await _h.SettleAsync();
        await _h.StopAsync();
        var back = $"back: {Shapes()} | {_h.Seen()}";

        _h.ComeUp();
        await _h.SettleAsync();
        await _h.StopAsync();
        Assert.Fail($"cut {cut} word {word} left {left.PhaseWord}\n{older}\n{back}\nthird: {Shapes()} | {_h.Seen()}");
    }

    private string Shapes() =>
        string.Join(" ; ", Enumerable.Range(1, _h.Service.SessionCount).Select(at => $"s{at}: " + string.Join(" | ", _h.Store("run-probe").Of($"s{at}").Select(move => $"{move.PhaseWord}/{move.HeldWhy}/{move.ClosedWhy}/{move.FellBackCode}"))));
}
