using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A move whose destination could not be read is kept for the next look of this run. A read that fails at the store (a busy file) is no
/// reading either: the move stays kept, so the look after reads it, instead of standing under its own run for good.
/// </summary>
/// <remarks>Every case has its own scratch home under <c>_fixtures/</c>; the store that will not take a write is its file marked read-only, which reads and refuses every write.</remarks>
[Collection("agent operation ports")]
public sealed class ConversationMovesReadAgainTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "move-read-again", Guid.NewGuid().ToString("N")[..8]);

    private readonly MoveSaidStandIn _said = new();

    private readonly ChatLedger _ledger = new();

    public ConversationMovesReadAgainTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private MoveStore Store(string run) => new(_home, () => StoreSettled.Yes, _ => true, run, StoreProcess.Shell);

    /// <summary>The store's file refusing writes while <paramref name="on"/>, as a store another writer holds does.</summary>
    private static void Refusing(ConversationMoves moves, bool on)
    {
        SqliteConnection.ClearAllPools();
        var file = moves.Store.Location;
        File.SetAttributes(file, on ? File.GetAttributes(file) | FileAttributes.ReadOnly : File.GetAttributes(file) & ~FileAttributes.ReadOnly);
    }

    private ConversationMoves Moves() =>
        new(_home, _ledger.Client(), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), store: Store("run-shell"), said: _said);

    /// <summary>A move of <c>s1</c> opened onto <c>s2</c> with one word waiting there never attempted, standing under this run.</summary>
    private MoveClaim Opened(ConversationMoves moves)
    {
        var claim = moves.Store.Decide(new MoveDecision("s1", MoveExecutors.Conversation, MoveHows.Brief, HandOverWhy.Limit, HandOverBy.Daoris) { FromEnded = true }).Claim!;
        moves.Store.Opening(claim, "opens-s1");
        moves.Store.Opened(claim, "s2", [new MoveRecordWord("w1", null, "opens-s1/w1", 0)]);
        _said.Wait("s2", "w1");
        return claim;
    }

    [Fact]
    public async Task A_kept_move_whose_read_fails_at_the_store_is_read_at_the_look_after()
    {
        var moves = Moves();
        var claim = Opened(moves);
        _said.Unreadable.Add("s2");
        await moves.ReadAsync(claim, CancellationToken.None);
        _said.Unreadable.Clear();
        Refusing(moves, true);

        await moves.LookAsync(CancellationToken.None);
        Refusing(moves, false);
        Assert.True(moves.Store.Read(claim.Move)!.Stands);

        await moves.LookAsync(CancellationToken.None);
        Assert.False(moves.Store.Read(claim.Move)!.Stands);
    }

    [Fact]
    public async Task A_first_read_that_fails_at_the_store_is_kept_to_read_at_the_next_look()
    {
        var moves = Moves();
        var claim = Opened(moves);
        Refusing(moves, true);

        await new ConversationMove(moves, claim).ReadAsync();
        Refusing(moves, false);
        Assert.True(moves.Store.Read(claim.Move)!.Stands);

        await moves.LookAsync(CancellationToken.None);
        Assert.False(moves.Store.Read(claim.Move)!.Stands);
    }
}
