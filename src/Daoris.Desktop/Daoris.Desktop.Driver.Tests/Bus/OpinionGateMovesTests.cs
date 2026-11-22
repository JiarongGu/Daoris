using System.Collections.Concurrent;
using Daoris.Driver;
using Daoris.Testing;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 <c>quest.moved</c> names what changed: the quests whose records differ from the last read, the chains they sit in (their parents and
/// children) and their repositories. A move round judges only the owed opinions those names reach (a step opinion by its chain's quests, a
/// landing opinion by its repository's too); a move that names nothing, and <c>choices.written</c>, judge every owed opinion. A burst
/// folds without losing a name, and nothing owed to the gate stays armed.
/// </summary>
public sealed class OpinionGateMovesTests : IDisposable
{
    private static readonly TimeSpan Soon = Poll.Patience;

    internal static readonly TimeProvider Machine = TimeProvider.System;

    private static readonly DateTimeOffset At = new(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);

    private static readonly BusQueueOptions Quick = new() { Clock = Machine, RetryFirst = TimeSpan.FromMilliseconds(20), RetryMost = TimeSpan.FromMilliseconds(60) };

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "opinion-gate-moves", Guid.NewGuid().ToString("N")[..8]);

    private readonly ConcurrentQueue<string> _judged = new();

    /// <summary>Three owed opinions: `q-1`'s steps and `q-3`'s steps in engine, `q-2`'s landing in game.</summary>
    public OpinionGateMovesTests()
    {
        Directory.CreateDirectory(_home);
        var dues = new OpinionDues(_home);
        dues.Due(new OpinionDue("s-1", "q-1", "engine", "", "tree-1", OpinionRules.Steps, At));
        dues.Due(new OpinionDue("s-2", "q-2", "game", "", "tree-2", OpinionRules.Landing, At));
        dues.Due(new OpinionDue("s-3", "q-3", "engine", "", "tree-3", OpinionRules.Steps, At));
    }

    public void Dispose() => GitSeed.Discard(_home);

    private static QuestView Quest(string id, string to, string? parent = null, string status = "Open") =>
        new(id, "ask #a1", to, $"Work {id}", "", status) { Parent = parent };

    [Fact]
    public void A_move_of_one_quest_names_it_its_chain_and_its_repositories_and_a_move_of_the_parent_names_its_children()
    {
        var before = new[] { Quest("q-0", "engine"), Quest("q-1", "engine", "q-0"), Quest("q-3", "engine"), Quest("q-2", "game") };
        var moved = before.Select(quest => quest.Id == "q-1" ? quest with { Status = "Done" } : quest).ToList();

        var one = QuestMoves.Between(before, moved)!;
        Assert.Equal(["q-0", "q-1"], one.Quests.Order());
        Assert.Equal(["engine"], one.Repositories);

        var parent = QuestMoves.Between(before, before.Select(quest => quest.Id == "q-0" ? quest with { Status = "Done" } : quest).ToList())!;
        Assert.Contains("q-1", parent.Quests);
        Assert.DoesNotContain("q-3", parent.Quests);
        Assert.DoesNotContain("q-2", parent.Quests);
    }

    [Fact]
    public void A_quest_published_or_gone_is_named_and_what_cannot_be_named_names_nothing()
    {
        var before = new[] { Quest("q-1", "engine"), Quest("q-2", "game") };

        Assert.Equal(["q-3"], QuestMoves.Between(before, [.. before, Quest("q-3", "engine")])!.Quests);
        Assert.Equal(["q-2"], QuestMoves.Between(before, [before[0]])!.Quests);
        // No earlier read, no change found, more changed than a fact lists, or an id a fact cannot carry: the round judges every owed opinion.
        Assert.Null(QuestMoves.Between(null, before));
        Assert.Null(QuestMoves.Between(before, before.Select(quest => quest with { Body = "" }).ToList()));
        Assert.Null(QuestMoves.Between([], [.. Enumerable.Range(0, BusShapes.MostListed + 1).Select(each => Quest($"q-{each}", "engine"))]));
        Assert.Null(QuestMoves.Between(before, [.. before, Quest("not an id", "engine")]));
    }

    [Fact]
    public async Task A_move_judges_only_the_owed_opinions_its_names_reach()
    {
        await using var bus = Open();
        using var gate = Gate();
        await using var queue = await BusQueue.StartAsync(bus, [gate], Quick);
        await Started();
        _judged.Clear();

        bus.Append(OpinionBusFacts.QuestMoved(["quests"], new QuestMoveNames(["q-1"], ["engine"])));

        await Poll.Until(() => Judged("s-1") >= 1, () => "the move was not judged", Soon);
        await Unarmed(bus);
        Assert.Equal(1, Judged("s-1"));
        Assert.Equal(0, Judged("s-2"));
        // q-3's steps sit in engine too, but a step opinion reads its chain's quests, and q-3 is in none of the move's.
        Assert.Equal(0, Judged("s-3"));
    }

    [Fact]
    public async Task A_landing_opinion_is_reached_by_a_move_in_its_repository_and_a_step_opinion_by_its_chain()
    {
        await using var bus = Open();
        using var gate = Gate();
        await using var queue = await BusQueue.StartAsync(bus, [gate], Quick);
        await Started();
        _judged.Clear();

        bus.Append(OpinionBusFacts.QuestMoved(["quests"], new QuestMoveNames(["q-9"], ["game"])));
        await Poll.Until(() => Judged("s-2") >= 1, () => "the landing was not judged", Soon);
        await Unarmed(bus);
        Assert.Equal(1, Judged("s-2"));
        Assert.Equal(0, Judged("s-1") + Judged("s-3"));

        bus.Append(OpinionBusFacts.QuestMoved(["quests"], new QuestMoveNames(["q-3"], ["engine"])));
        await Poll.Until(() => Judged("s-3") >= 1, () => "the step was not judged", Soon);
        await Unarmed(bus);
        Assert.Equal(1, Judged("s-3"));
        Assert.Equal(1, Judged("s-2"));
    }

    [Fact]
    public async Task A_move_naming_nothing_and_the_choices_written_judge_every_owed_opinion()
    {
        await using var bus = Open();
        using var gate = Gate();
        await using var queue = await BusQueue.StartAsync(bus, [gate], Quick);
        await Started();
        _judged.Clear();

        bus.Append(OpinionBusFacts.QuestMoved(["quests"]));
        await Poll.Until(() => Judged("s-1") >= 1 && Judged("s-2") >= 1 && Judged("s-3") >= 1, () => $"not all judged: {string.Join(", ", _judged)}", Soon);
        await Unarmed(bus);
        _judged.Clear();

        bus.Append(OpinionBusFacts.ChoicesWritten());
        await Poll.Until(() => Judged("s-1") >= 1 && Judged("s-2") >= 1 && Judged("s-3") >= 1, () => $"not all judged: {string.Join(", ", _judged)}", Soon);
        await Unarmed(bus);
    }

    [Fact]
    public async Task A_burst_of_named_moves_folds_and_every_name_is_judged()
    {
        var reading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var bus = Open();
        var holding = false;
        using var gate = Gate(async due =>
        {
            if (due.Session == "s-2" && Volatile.Read(ref holding))
            {
                Volatile.Write(ref holding, false);
                reading.TrySetResult();
                await release.Task;
            }
        });
        await using var queue = await BusQueue.StartAsync(bus, [gate], Quick);
        await Started();
        _judged.Clear();
        Volatile.Write(ref holding, true);

        try
        {
            bus.Append(OpinionBusFacts.QuestMoved(["quests"], new QuestMoveNames(["q-2"], [])));
            await reading.Task.WaitAsync(Soon);
            bus.Append(OpinionBusFacts.QuestMoved(["quests"], new QuestMoveNames(["q-1"], [])));
            bus.Append(OpinionBusFacts.QuestMoved(["quests"], new QuestMoveNames(["q-3"], [])));
            await bus.FlushAsync();
        }
        finally
        {
            release.TrySetResult();
        }

        await Poll.Until(() => Judged("s-1") >= 1 && Judged("s-3") >= 1, () => $"a folded name was lost: {string.Join(", ", _judged)}", Soon);
        await Unarmed(bus);
    }

    [Fact]
    public async Task A_round_over_many_repositories_judges_no_more_than_the_gates_places_at_once()
    {
        var dues = new OpinionDues(_home);
        for (var each = 10; each < 26; each++) dues.Due(new OpinionDue($"s-{each}", $"q-{each}", $"repo-{each}", "", $"tree-{each}", OpinionRules.Landing, At));
        var inside = 0;
        var most = 0;
        await using var bus = Open();
        using var gate = Gate(async _ =>
        {
            var now = Interlocked.Increment(ref inside);
            int seen;
            while (now > (seen = Volatile.Read(ref most)) && Interlocked.CompareExchange(ref most, now, seen) != seen)
            {
            }

            await Task.Yield();
            Interlocked.Decrement(ref inside);
        });
        await using var queue = await BusQueue.StartAsync(bus, [gate], Quick);
        await Poll.Until(() => Judged("s-25") >= 1 && Judged("s-10") >= 1, () => "the start did not judge them all", Soon);
        bus.Append(OpinionBusFacts.ChoicesWritten());
        await Poll.Until(() => Judged("s-25") >= 2 && Judged("s-10") >= 2, () => "the round did not judge them all", Soon);

        Assert.InRange(Volatile.Read(ref most), 1, OpinionGateConsumer.Bounds.KeysAtOnce);
    }

    private int Judged(string session) => _judged.Count(subject => subject.StartsWith(session + "/", StringComparison.Ordinal));

    private async Task Unarmed(BusLog bus)
    {
        await bus.FlushAsync();
        await Poll.Until(
            () => !Armed().Any(timer => timer.Obliged == BusTypes.OpinionGate),
            () => "armed: " + string.Join(", ", Armed().Where(timer => timer.Obliged == BusTypes.OpinionGate).Select(timer => timer.Kind)), Soon);
    }

    private Task Started() => Poll.Until(() => Judged("s-1") >= 1 && Judged("s-2") >= 1 && Judged("s-3") >= 1, () => "the start did not judge them all", Soon);

    private BusLog Open() =>
        BusLog.Over(new DesktopBusLogStore(_home), new BusLogOptions { Clock = Machine, Interval = TimeSpan.FromMilliseconds(20), Home = _home });

    private IReadOnlyList<BusTimer> Armed()
    {
        using var store = new DesktopBusLogStore(_home);
        return store.Timers(null, 1000);
    }

    private OpinionGateConsumer Gate(Func<OpinionDue, Task>? during = null) => new(
        async (due, _, _) =>
        {
            _judged.Enqueue(OpinionGateJudge.Subject(due));
            if (during is not null) await during(due);
            return new OpinionFact(new Dictionary<string, OpinionSit>(), null, null);
        },
        (_, _) => Task.FromResult<OpinionView?>(null),
        () => new OpinionDues(_home).Open(), Machine, (_, _) => { });
}
