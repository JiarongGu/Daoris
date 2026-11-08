using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A limit move that carried its conversation is wired as soon as its carried run is handed over, and the run's first prompt ends the
/// move's reading. A run that dies before that prompt never does, so the move's end waits the door's wire bound and then ends the record
/// by its phase, instead of leaving its row to block the destination until the next come-up.
/// </summary>
/// <remarks>Every case has its own scratch home under <c>_fixtures/</c>; the wire bound is the case's own, a few milliseconds.</remarks>
[Collection("agent operation ports")]
public sealed class LimitMoveCarriedEndTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "limit-move-carried-end", Guid.NewGuid().ToString("N")[..8]);

    public LimitMoveCarriedEndTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private (LimitMove Limit, ConversationMove Record, ConversationMoves Moves) World()
    {
        var client = new ChatLedger().Client();
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        var adapters = TestDoors.Registry([]);
        var context = new ChatContext(
            client, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), output: null,
            new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)), events);
        var stand = new ExecutorStand(null!, context);
        stand.Add(new ChatHandOver(stand) { WireWait = TimeSpan.FromMilliseconds(50) });
        var limit = stand.Add(new LimitMove(stand));
        var moves = new ConversationMoves(_home, client, events, store: new MoveStore(_home, () => StoreSettled.Yes, _ => true, "run-shell", StoreProcess.Shell));
        var claim = moves.Store.Decide(new MoveDecision("s1", MoveExecutors.Conversation, MoveHows.Brief, HandOverWhy.Limit, HandOverBy.Daoris)).Claim!;
        var record = new ConversationMove(moves, claim);
        Assert.Null(record.OldEnded());
        Assert.Null(record.Opening("opens-s1"));
        Assert.Null(record.Opened("s2", []));
        Assert.Null(record.Delivering());
        return (limit, record, moves);
    }

    [Fact]
    public async Task A_carried_move_whose_run_never_wrote_its_prompt_is_ended_when_the_wire_bound_passes()
    {
        var (limit, record, moves) = World();
        var read = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);

        await limit.TellEndedAsync("s1", wired: true, read, record);

        Assert.False(moves.Store.Read(record.Claim.Move)!.Stands);
    }

    [Fact]
    public async Task A_carried_move_whose_prompt_was_written_is_left_to_its_reading()
    {
        var (limit, record, moves) = World();
        var read = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
        read.SetResult(Task.CompletedTask);

        await limit.TellEndedAsync("s1", wired: true, read, record);

        Assert.True(moves.Store.Read(record.Claim.Move)!.Stands);
    }
}
