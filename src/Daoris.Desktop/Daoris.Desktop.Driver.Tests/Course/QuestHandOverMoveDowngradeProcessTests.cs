using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-v part b (D212; the foundation design §13.5's downgrade table, §19.4, row f-v's proof): a quest's hand-over across a downgrade, as
/// <c>HandOverMoveDowngradeProcessTests</c> proves a conversation's. A driver is killed at each place its look passes
/// (<see cref="QuestHandOverMoveCrashProcessTests"/>), an older build leaves its own <c>&lt;s&gt;.hand-over.json</c> for the quest's session, the
/// build that keeps a box, and this build returns: its look takes the killed move, with the box waiting behind it; the look after imports the box
/// once, as a move of its own, through the driven holder, and sets it aside; and a third does nothing. The quest is carried on once, whichever build
/// asks, and a box with no killed move is imported and taken as the ask it is.
/// </summary>
/// <remarks>
/// The older build's act is its box: a build before the store keeps no move, and the decision it left is the one thing it writes that this build
/// reads. The driven holder imports a quest session's box only, the shell's a conversation's (<see cref="HandOverBoxImport"/>), so a quest's is
/// imported by the loop's look and never by the shell's come-up.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class QuestHandOverMoveDowngradeProcessTests : IAsyncLifetime
{
    private readonly HandOverQuestHarness _h = new("quest-hand-over-downgrade");

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _h.DisposeAsync();

    private const string BoxOnly = "box-only";

    private const string Decided = "decided";

    private const string Quiescing = "quiescing";

    private const string OldEnded = "old-ended";

    public static TheoryData<string> Points => new() { BoxOnly, Decided, Quiescing, OldEnded };

    private string BoxPath => new HandOverBox(_h.Home).PathOf("s1");

    /// <summary>The box's own record, once its move is ended: the phase, and why it was held or closed.</summary>
    private static string[] Ended(MoveRecord move) => [move.PhaseWord, move.HeldWhy ?? "", move.ClosedWhy ?? ""];

    /// <summary>
    /// 🔴 A driver killed at each place (or none), an older build's box for the session, then this build's looks: the killed move is taken and the quest
    /// carried on once, the box is imported once and set aside, never deleted, its move ended by its phase and sending nothing, and a third look does
    /// nothing; with no killed move the box is the ask, imported, taken and delivered.
    /// </summary>
    [Theory]
    [MemberData(nameof(Points))]
    public async Task A_downgrade_at_each_point_carries_the_quest_on_once_and_the_older_builds_box_is_imported_once_at_the_return(string point)
    {
        var account = await _h.StartQuestAsync();
        var run = point == BoxOnly ? null : HandOverQuestHarness.Killed;
        if (run is not null)
        {
            var killed = _h.Moves(run);
            Assert.True((await _h.AskAsync(killed, account)).Kept);
            switch (point)
            {
                case Quiescing:
                    await _h.KilledLookAsync(_h.Driver(killed, stops: (id, note) => { _h.Block(); return true; }), () => _h.MovesOf("s1").Single().PhaseWord == "quiescing");
                    SessionMoves.StopAsync(_h.Processes, _h.Client, "s1", CancellationToken.None, HandOverNotes.QuestHandedOver("acp-stub")).GetAwaiter().GetResult();
                    new HandedQuests(_h.Home).Write("q1", new HandedQuest("acp-stub", account, "s1", HandOverWhy.Press, HandOverBy.Person, DateTimeOffset.UtcNow));
                    break;
                case OldEnded:
                    killed.Cut = name => { if (name == Cuts.OldEnded) _h.Block(); };
                    await _h.KilledLookAsync(_h.Driver(killed), () => _h.MovesOf("s1").Single().PhaseWord == "old-ended");
                    break;
            }

            _h.Die(run);
        }

        // The older build's own ask for the same session: a box, which this build's look reads as the decision it keeps.
        new HandOverBox(_h.Home).Write("s1", new HandOverRequest("acp-stub", account, HandOverWhy.Press, HandOverBy.Person, DateTimeOffset.UtcNow));

        // This build returns: the first look takes the killed move up and carries the quest on, the box waiting behind a move that stood.
        var loop = _h.Driver();
        if (run is not null) await loop.TakeUpMovesAsync(CancellationToken.None);
        if (run is null) await loop.TickAsync().WaitAsync(Poll.Patience);
        await _h.CarryOnAsync(loop);

        // The next looks import the box once, as a record of its own, and set it aside; a third does nothing.
        for (var look = 0; look < 2; look++)
        {
            await loop.TickAsync().WaitAsync(Poll.Patience);
            await loop.Running.SettledAsync().WaitAsync(Poll.Patience);
        }

        var moves = _h.MovesOf("s1");
        var seen = _h.Seen();
        Assert.False(File.Exists(BoxPath), seen);
        Assert.True(File.Exists(BoxPath + ".imported"), seen);
        var expected = point == BoxOnly ? 1 : 2;
        Assert.True(expected == moves.Count, $"{moves.Count} moves, not {expected}: {seen}");
        Assert.All(moves, move => Assert.False(move.Stands, seen));

        // The quest is carried on once: the one delivered move, the one prompt the target heard, whichever build asked.
        Assert.Equal(1, moves.Count(move => move.PhaseWord == "delivered"));
        Assert.Equal(1, _h.Carried);
        Assert.Equal(2, _h.Service.SessionCount);
        Assert.Equal("Done", _h.Service.Status("q1"));
        if (point == OldEnded) Assert.Equal(ConversationMoves.NotOpened, moves[0].FellBackCode);
        if (point is Decided or Quiescing) Assert.Equal(("delivered", "s2"), (moves[0].PhaseWord, moves[0].Destination));

        // The box's own move is the last, imported by the driven holder under this run. Where no standing move was ahead of it, or the dead run's had
        // fallen back, it is the ask the look takes and the carry-on goes on from: delivered, the one move of that delivery. Behind a move that stood
        // it waits until that move has carried the quest on, and is then refused, the quest being carried on, and ended by its phase: nothing sent twice.
        var boxed = moves[^1];
        Assert.Equal((HandOverWhy.Press, HandOverBy.Person, MoveExecutors.Driven), (boxed.Why, boxed.By, boxed.Executor));
        string[] ends = point is BoxOnly or OldEnded ? ["delivered", "", ""] : ["fell-back", "", ""];
        Assert.True(Ended(boxed).SequenceEqual(ends), $"the box's move is {string.Join("/", Ended(boxed))}: {seen}");
        if (ends[0] == "fell-back") Assert.Equal(ConversationMoves.NotOpened, boxed.FellBackCode);

        // A further look finds nothing to do.
        var shape = string.Join(" | ", moves.Select(Shape));
        await loop.TickAsync().WaitAsync(Poll.Patience);
        await loop.Running.SettledAsync().WaitAsync(Poll.Patience);
        Assert.Equal(shape, string.Join(" | ", _h.MovesOf("s1").Select(Shape)));
        Assert.Equal(1, _h.Carried);
    }

    private static string Shape(MoveRecord move) =>
        $"{move.PhaseWord}/{move.HeldWhy}/{move.ClosedWhy}/{move.FellBackCode}/{move.ClaimRun}/{move.ClaimGeneration}";
}
