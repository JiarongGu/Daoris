using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a pass keeps while it takes late unit asks into its own turn: a refreshed checkout reads every branch its first read held, and a
/// running pass takes only so many late asks, the rest left for the next pass.
/// </summary>
public sealed class LateUnitTests
{
    /// <summary>🔴 A checkout refreshed for a late unit still reads a former subject's branch and a frame's, not only the asker's.</summary>
    [Fact]
    public async Task A_refreshed_checkout_keeps_every_branch_the_first_read_held()
    {
        var reads = new List<string[]>();
        var tips = new PassTips(
            (_, branches, _) =>
            {
                reads.Add([.. branches]);
                return Task.FromResult<IReadOnlyDictionary<string, string>>(branches.ToDictionary(branch => branch, branch => $"tip-of-{branch}-{reads.Count}"));
            },
            new Dictionary<string, IReadOnlyCollection<string>> { ["root"] = ["observed"] });

        await tips.ReadAsync("root", ["former-subject", "frame"], CancellationToken.None);
        tips.Refresh("root");
        var asker = await tips.TipAsync("root", "asker", CancellationToken.None);

        Assert.Equal("tip-of-asker-2", asker);
        Assert.Equal(["asker", "former-subject", "frame", "observed"], reads[1]);
        Assert.Equal("tip-of-former-subject-2", tips.Known("root", "former-subject"));
    }

    private static readonly DateTimeOffset Then = new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);

    private static GovernedUnit Governed()
    {
        var node = new NodeDeclaration("landing", NodeKinds.Land, 1, NodeReach.Performed);
        var branch = Assert.Single(WorkInventory.Units(new InventoryFacts(
            [new GrownBranch("engine", "default", "daoris/s1", "main", "f0", null, Then) { Unit = "unit-a" }], [], [], [],
            new HashSet<string>(), _ => false, Then.AddDays(1))));
        var self = new WorkUnit("run:r1", WorkUnitKind.Run, branch.Owner, branch.Subject, branch.End, []);
        return new GovernedUnit(self, new NodeGraph([node], [], ["landing"], "landing"), "gph-1", "r1",
            new Dictionary<string, NodeDeclaration> { ["landing"] = node }, new HashSet<string>(),
            new WatchedUnit("engine", "default", "C:/fam/engine", "daoris/s1", "t1", "main", null, []));
    }

    private static LateUnits Late(GovernedUnit unit, List<string> answered)
    {
        var queue = new Queue<string>([unit.Unit.Id]);
        var look = new NodeRuntimeLook(DriverConfig.Parse("{}"), false)
        {
            Late = () => queue.Count == 0 ? new HashSet<string>() : new HashSet<string>([queue.Dequeue()], StringComparer.Ordinal),
            Failed = id => answered.Add("failed " + id),
            NotJudged = id => answered.Add("not-judged " + id),
            Judged = id => answered.Add("judged " + id),
        };
        return new LateUnits(look, new Dictionary<string, GovernedUnit> { [unit.Unit.Id] = unit });
    }

    private static Task JudgeAsync(LateUnits late, Func<GovernedUnit, Task<(IReadOnlyList<WorkPerformed>, bool)>> reconcile, Func<GovernedUnit, Task> evaluate) =>
        late.JudgeAsync(new NodeRunKeeping(null!, null!, null!, null!, null!, null!, null!, null!, null!),
            new PassTips((_, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>()), new Dictionary<string, IReadOnlyCollection<string>>()),
            [], [], reconcile, evaluate, CancellationToken.None);

    /// <summary>🔴 A late unit is marked judged only once its judgement succeeded: one that failed is left to the turn's own entry for it.</summary>
    [Fact]
    public async Task A_late_unit_is_judged_once_it_succeeds_and_never_when_its_judgement_fails()
    {
        var unit = Governed();
        var answered = new List<string>();
        var late = Late(unit, answered);

        await JudgeAsync(late, _ => Task.FromResult(((IReadOnlyList<WorkPerformed>)[], true)), _ => throw new HttpRequestException("no answer"));

        Assert.False(late.Judged(unit.Unit.Id));
        Assert.Equal(["failed run:r1"], answered);

        answered.Clear();
        var again = Late(unit, answered);
        await JudgeAsync(again, _ => Task.FromResult(((IReadOnlyList<WorkPerformed>)[], true)), _ => Task.CompletedTask);

        Assert.True(again.Judged(unit.Unit.Id));
        Assert.Equal(["judged run:r1"], answered);
    }

    /// <summary>🔴 A late unit with an attempt not asked after (the pass's calls spent) is not evaluated: its door says not judged and the turn keeps it.</summary>
    [Fact]
    public async Task A_late_unit_with_an_attempt_not_asked_after_is_not_evaluated_and_its_door_says_not_judged()
    {
        var unit = Governed();
        var answered = new List<string>();
        var late = Late(unit, answered);
        var evaluated = 0;

        await JudgeAsync(late, _ => Task.FromResult(((IReadOnlyList<WorkPerformed>)[], false)), _ =>
        {
            evaluated++;
            return Task.CompletedTask;
        });

        Assert.Equal(0, evaluated);
        Assert.False(late.Judged(unit.Unit.Id));
        Assert.Equal(["not-judged run:r1"], answered);
    }

    /// <summary>🔴 A pass takes no more than <see cref="PassAsks.LatePerPass"/> late units in all; the overflow is the next pass's asks.</summary>
    [Fact]
    public void A_running_pass_takes_a_bounded_number_of_late_units_and_leaves_the_rest_for_the_next_pass()
    {
        var asks = new PassAsks(() => { });
        var running = asks.Take();
        var units = Enumerable.Range(0, PassAsks.LatePerPass + 5).Select(each => $"unit-{each:D3}").ToList();
        foreach (var unit in units) _ = asks.WaitUnitAsync(unit, TimeSpan.Zero, CancellationToken.None);

        var taken = new HashSet<string>(StringComparer.Ordinal);
        for (var boundary = 0; boundary < 10; boundary++) taken.UnionWith(running.Late());

        Assert.Equal(PassAsks.LatePerPass, taken.Count);
        var next = asks.Take();
        Assert.Equal(units.Except(taken).Order(), next.Units.Order());
        Assert.Equal(5, next.Units.Count);
    }
}
