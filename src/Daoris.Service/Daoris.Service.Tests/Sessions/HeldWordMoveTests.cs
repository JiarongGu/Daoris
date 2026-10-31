using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// LINESTOGETHER1b (the review-flow design §2): a word held on a part's record moves to the head in one step, taken off the part and kept at
/// the head inside one transaction, under the key <c>move:&lt;part&gt;:&lt;word&gt;</c>, so a retry after a lost answer answers moved and
/// nothing is said twice.
/// </summary>
public sealed class HeldWordMoveTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private SessionStore _sessions = null!;
    private SessionLedger _ledger = null!;

    // The settlement's moment is the clock's, so the moves are said now.
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var quests = await QuestStore.OpenAsync(_connection);
        _sessions = await SessionStore.OpenAsync(_connection);
        _ledger = new SessionLedger(quests, _sessions);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private async Task<Session> RecordAsync(SessionState state = SessionState.Completed, string? from = null)
    {
        var made = await _sessions.CreateAsync(null, "Owner", "stub", Now, kind: SessionKind.Chat, from: from, fromWhy: from is null ? null : "press", fromBy: from is null ? null : "person");
        return (await _sessions.SetStateAsync(made.Id, state, null, null, null, Now))!;
    }

    /// <summary>A part beside the head with the person's word waiting on it, and the head, ended.</summary>
    private async Task<(Session Part, SaidWord Word, Session Head)> HeldAsync()
    {
        var part = await RecordAsync();
        var word = (await _ledger.SayAsync(part.Id, "Use the blue one.", null, Now)).Word!;
        return (part, word, await RecordAsync());
    }

    private async Task<IReadOnlyList<SaidWord>> SaidAsync(string id) => (await _sessions.FindAsync(id))!.Said;

    [Fact]
    public async Task A_held_word_moves_once_off_its_part_onto_the_head_under_its_key()
    {
        var (part, word, head) = await HeldAsync();

        var moved = await _ledger.MoveSaidAsync(part.Id, word.Id, head.Id, Now);

        Assert.Equal(SessionSayRefusal.None, moved.Refusal);
        Assert.Equal(head.Id, moved.MovedTo);
        Assert.Empty(await SaidAsync(part.Id));
        var kept = Assert.Single(await SaidAsync(head.Id));
        Assert.Equal(("Use the blue one.", $"move:{part.Id}:{word.Id}", true), (kept.Text, kept.Key, kept.Persons));
        Assert.Equal((kept.Id, word.Id), (moved.Word!.Id, moved.Taken!.Id));
    }

    [Fact]
    public async Task A_retry_after_the_answer_was_lost_answers_moved_and_says_nothing_twice()
    {
        var (part, word, head) = await HeldAsync();
        await _ledger.MoveSaidAsync(part.Id, word.Id, head.Id, Now);

        var again = await _ledger.MoveSaidAsync(part.Id, word.Id, head.Id, Now);

        Assert.Equal((SessionSayRefusal.None, true, head.Id), (again.Refusal, again.Again, again.MovedTo));
        Assert.Null(again.Word);
        Assert.Single(await SaidAsync(head.Id));
        Assert.Empty(await SaidAsync(part.Id));
    }

    [Fact]
    public async Task A_retry_after_the_head_took_the_word_answers_moved_and_keeps_nothing()
    {
        var (part, word, head) = await HeldAsync();
        var moved = await _ledger.MoveSaidAsync(part.Id, word.Id, head.Id, Now);
        await _sessions.TakeSaidAsync(head.Id, [moved.Word!.Id]);

        var again = await _ledger.MoveSaidAsync(part.Id, word.Id, head.Id, Now);

        Assert.Equal((SessionSayRefusal.None, true, head.Id), (again.Refusal, again.Again, again.MovedTo));
        Assert.Empty(await SaidAsync(head.Id));
        Assert.Empty(await SaidAsync(part.Id));
    }

    /// <summary>
    /// A successor opened from the head after the move: the retry names the head the page showed or the successor, and the word, still on
    /// the head or carried to the successor by its open, answers moved wherever it is, never kept a second time.
    /// </summary>
    [Fact]
    public async Task A_retry_after_a_successor_opened_answers_moved_whichever_head_it_names()
    {
        var (part, word, head) = await HeldAsync();
        await _ledger.MoveSaidAsync(part.Id, word.Id, head.Id, Now);
        var successor = await RecordAsync(from: head.Id);

        var waiting = await _ledger.MoveSaidAsync(part.Id, word.Id, successor.Id, Now);
        await _sessions.MoveSaidAsync(head.Id, successor.Id, _ => true);
        var carried = await _ledger.MoveSaidAsync(part.Id, word.Id, head.Id, Now);
        var named = await _ledger.MoveSaidAsync(part.Id, word.Id, successor.Id, Now);

        Assert.Equal((true, head.Id), (waiting.Again, waiting.MovedTo));
        Assert.Equal((true, true), (carried.Again, named.Again));
        Assert.Equal(SessionSayRefusal.None, carried.Refusal);
        Assert.Empty(await SaidAsync(head.Id));
        Assert.Single(await SaidAsync(successor.Id));
    }

    /// <summary>A fault after the take, before the keep, rolls the step back whole: the word waits on its part, the head holds nothing.</summary>
    [Fact]
    public async Task An_interruption_between_the_take_and_the_keep_leaves_neither()
    {
        var (part, word, head) = await HeldAsync();
        await using (var fault = _connection.CreateCommand())
        {
            fault.CommandText = $"CREATE TEMP TRIGGER keep_fails BEFORE UPDATE OF said ON sessions WHEN NEW.id = '{head.Id}' BEGIN SELECT RAISE(ABORT, 'the machine stopped'); END";
            await fault.ExecuteNonQueryAsync();
        }

        await Assert.ThrowsAsync<SqliteException>(() => _ledger.MoveSaidAsync(part.Id, word.Id, head.Id, Now));

        Assert.Equal(word.Id, Assert.Single(await SaidAsync(part.Id)).Id);
        Assert.Empty(await SaidAsync(head.Id));
    }

    [Fact]
    public async Task A_word_on_a_part_that_is_not_held_is_refused_in_words_and_left_where_it_waits()
    {
        var part = await RecordAsync(SessionState.Working);
        var word = (await _ledger.SayAsync(part.Id, "Use the blue one.", null, Now, running: true)).Word!;
        var head = await RecordAsync();

        var refused = await _ledger.MoveSaidAsync(part.Id, word.Id, head.Id, Now);

        Assert.Equal(SessionSayRefusal.NotHeld, refused.Refusal);
        Assert.Contains($"`{part.Id}`", refused.Message, StringComparison.Ordinal);
        Assert.Single(await SaidAsync(part.Id));
        Assert.Empty(await SaidAsync(head.Id));
    }

    [Fact]
    public async Task A_running_head_refuses_the_move_naming_itself_and_the_word_stays()
    {
        var (part, word, _) = await HeldAsync();
        var head = await RecordAsync(SessionState.Working);

        var refused = await _ledger.MoveSaidAsync(part.Id, word.Id, head.Id, Now);

        Assert.Equal((SessionSayRefusal.Running, head.Id), (refused.Refusal, refused.MovedTo));
        Assert.Single(await SaidAsync(part.Id));
        Assert.Empty(await SaidAsync(head.Id));
    }

    [Fact]
    public async Task A_word_no_longer_on_its_part_answers_taken()
    {
        var (part, word, head) = await HeldAsync();
        await _ledger.TakeBackAsync(part.Id, word.Id, null, Now);

        var taken = await _ledger.MoveSaidAsync(part.Id, word.Id, head.Id, Now);

        Assert.Equal(SessionSayRefusal.Taken, taken.Refusal);
        Assert.Empty(await SaidAsync(head.Id));
    }
}
