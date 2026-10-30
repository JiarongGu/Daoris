using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-v part b (D212; the foundation design §3.3, §13.4, §13.5, §19.4, row f-v's proof): a quest's hand-over keeps its driven move record
/// through a crash at each place its look passes. A hand-over decided by a terminal verb or a loop is taken from the store by the live loop's
/// look: the session written <c>quiescing</c> while it runs, stopped once, the quest held on the agent named (<c>handed/&lt;quest&gt;.json</c>), then
/// <c>old-ended</c>; the carry-on the next look starts goes on from the very move and closes it <c>delivered</c>, the protocol door telling that it
/// wrote the first prompt. Each case kills the run that decided or took it at one place, and a driver that comes after takes it up and carries the
/// quest on once.
/// </summary>
/// <remarks>
/// <para>The places: <c>verb-dead</c>, the ask decided by a verb whose run is gone before the loop's look, received by that look; <c>loop-decided</c>,
/// decided by a loop killed before its look, left at <c>decided</c> by the new loop's first look and then taken; <c>quiescing</c>, a loop killed after
/// the session was stopped and the quest held, before <c>old-ended</c>: the take-up leaves the move, which the look takes without a second stop;
/// <c>old-ended</c>, killed after it, before the carry-on began: a dead run's <c>old-ended</c> falls back (§13.5), and the carry-on, finding the
/// quest held and the session stopped, decides its own move fresh. The carry-on's own boundaries are row f-iv's proof.</para>
/// <para>A text door's carry-on tells no write and closes by its phase (<c>undelivered</c>); <c>delivered</c> is proved on the protocol door, whose first
/// prompt tells <see cref="CarryOnRecord.Written"/>. The text door's one case is <c>HandOverQuestTickTests</c>.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class QuestHandOverMoveCrashProcessTests : IAsyncLifetime
{
    private readonly HandOverQuestHarness _h = new("quest-hand-over-crash");

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _h.DisposeAsync();

    private const string VerbDead = "verb-dead";

    private const string LoopDecided = "loop-decided";

    private const string Quiescing = "quiescing";

    private const string OldEnded = "old-ended";

    public static TheoryData<string> Points => new() { VerbDead, LoopDecided, Quiescing, OldEnded };

    private int _stops;

    /// <summary>The look's stop, counted: the real one, which ends the session's process and moves its record.</summary>
    private bool Stop(string id, Noted note)
    {
        Interlocked.Increment(ref _stops);
        return SessionMoves.StopAsync(_h.Processes, _h.Client, id, CancellationToken.None, note).GetAwaiter().GetResult().Stopped;
    }

    /// <summary>
    /// 🔴 A driver killed at each place, then the loop that comes after: the move ends in one stop (none where the dead run had stopped the session),
    /// one carry-on and one delivery; the quest is done once, and the killed run, let go, changes nothing.
    /// </summary>
    [Theory]
    [MemberData(nameof(Points))]
    public async Task A_driver_killed_at_a_point_of_the_hand_over_comes_up_with_one_stop_and_the_quest_carried_on_once(string point)
    {
        var account = await _h.StartQuestAsync();
        var killed = _h.Moves(point == VerbDead ? "run-verb" : HandOverQuestHarness.Killed, point == VerbDead ? StoreProcess.Verb : StoreProcess.Host);
        Assert.True((await _h.AskAsync(killed, account)).Kept);
        Assert.Equal("decided", _h.MovesOf("s1").Single().PhaseWord);
        Assert.Empty(new HandOverBox(_h.Home).Pending());

        switch (point)
        {
            case Quiescing:
                // The killed look stops the session for real and dies there, before it holds the quest or writes `old-ended`; the quest's hold the
                // look writes next is made here, as the dead look's own write would have.
                await _h.KilledLookAsync(
                    _h.Driver(killed, stops: (id, note) =>
                    {
                        _h.Block();
                        return true;
                    }),
                    () => _h.MovesOf("s1").Single().PhaseWord == "quiescing");
                SessionMoves.StopAsync(_h.Processes, _h.Client, "s1", CancellationToken.None, HandOverNotes.QuestHandedOver("acp-stub")).GetAwaiter().GetResult();
                new HandedQuests(_h.Home).Write("q1", new HandedQuest("acp-stub", account, "s1", HandOverWhy.Press, HandOverBy.Person, DateTimeOffset.UtcNow));
                break;
            case OldEnded:
                killed.Cut = name =>
                {
                    if (name == Cuts.OldEnded) _h.Block();
                };
                await _h.KilledLookAsync(_h.Driver(killed), () => _h.MovesOf("s1").Single().PhaseWord == "old-ended");
                break;
        }

        var left = _h.MovesOf("s1").Single();
        _h.Die(point == VerbDead ? "run-verb" : HandOverQuestHarness.Killed);

        var loop = _h.Driver(stops: Stop);
        if (point != VerbDead)
        {
            // The watch's first look: a dead loop's move is taken up, left at its phase where it waits to be taken, and fallen back at `old-ended`.
            await loop.TakeUpMovesAsync(CancellationToken.None);
            var taken = _h.MovesOf("s1").Single();
            Assert.Equal(StoreProcess.Run, taken.ClaimRun);
            Assert.Equal(point == OldEnded ? "fell-back" : point == LoopDecided ? "decided" : point, taken.PhaseWord);
        }

        await _h.CarryOnAsync(loop);

        var moves = _h.MovesOf("s1");
        var seen = _h.Seen();
        var delivered = moves[^1];
        Assert.True(delivered.PhaseWord == "delivered", $"the carry-on's move is {delivered.PhaseWord}: {seen}");
        Assert.Equal((point == OldEnded ? 2 : 1), moves.Count);
        if (point == OldEnded) Assert.Equal(ConversationMoves.NotOpened, moves[0].FellBackCode);
        Assert.Equal(("s2", "acp-stub", HandOverWhy.Press, HandOverBy.Person, MoveExecutors.Driven), (delivered.Destination, delivered.Agent, delivered.Why, delivered.By, delivered.Executor));
        Assert.True(delivered.ClaimGeneration > left.ClaimGeneration || point == OldEnded, "the take is a claim of its own");

        // One stop of the session by the look that took the move, none where the dead run had stopped it; one carry-on; the quest done once.
        Assert.Equal(point is Quiescing or OldEnded ? 0 : 1, _stops);
        Assert.Equal(1, _h.Carried);
        Assert.Equal(2, _h.Service.SessionCount);
        Assert.Equal(("s1", HandOverWhy.Press, HandOverBy.Person), (_h.Service.Session("s2")["from"]?.GetValue<string>(), _h.Service.Session("s2")["fromWhy"]?.GetValue<string>(), _h.Service.Session("s2")["fromBy"]?.GetValue<string>()));
        Assert.Equal("stopped", _h.Service.Session("s1")["state"]!.GetValue<string>());
        Assert.Equal("Done", _h.Service.Status("q1"));

        // The killed run, let go, finds its claim gone and changes nothing.
        var shape = string.Join(" | ", moves.Select(Shape));
        await _h.ReleaseAsync();
        Assert.Equal(shape, string.Join(" | ", _h.MovesOf("s1").Select(Shape)));
        Assert.Equal(1, _h.Carried);
    }

    /// <summary>
    /// 🔴 Reading 5 of part a, through the real loop: a hand-over a terminal verb decided while it lives is received at the loop's next look, by one
    /// guarded update that raises the generation, never held for as long as the verb's process lives; the verb's next write, under the claim it decided
    /// under, throws <see cref="MoveClaimLostException"/>, and the loop carries the quest on once.
    /// </summary>
    [Fact]
    public async Task A_hand_over_a_living_verb_decided_is_received_at_the_next_look_and_the_verbs_next_write_is_refused()
    {
        var account = await _h.StartQuestAsync();
        var verb = _h.Moves("run-verb", StoreProcess.Verb);
        Assert.True((await _h.AskAsync(verb, account)).Kept);
        var decided = _h.MovesOf("s1").Single();
        Assert.Equal(("run-verb", 1L), (decided.ClaimRun, decided.ClaimGeneration));

        var loop = _h.Driver(stops: Stop);
        await _h.CarryOnAsync(loop);

        var moves = _h.MovesOf("s1");
        var move = Assert.Single(moves);
        Assert.Equal(("delivered", StoreProcess.Run, "s2"), (move.PhaseWord, move.ClaimRun, move.Destination));
        Assert.True(move.ClaimGeneration >= 2, "received, the generation raised");
        Assert.Throws<MoveClaimLostException>(() => verb.Store.Advance(new MoveClaim(decided.Id, "run-verb", decided.ClaimGeneration), MoveRecordPhase.Quiescing));
        Assert.Equal((1, 1), (_stops, _h.Carried));
        Assert.Equal("Done", _h.Service.Status("q1"));
    }

    private static string Shape(MoveRecord move) =>
        $"{move.PhaseWord}/{move.HeldWhy}/{move.ClosedWhy}/{move.FellBackCode}/{move.ClaimRun}/{move.ClaimGeneration}";
}
