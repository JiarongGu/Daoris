using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-i part 3 (D212; the foundation design §13.4 step 4, §19.4): the service's door to a move's destination's <c>said</c>, which
/// rows f-ii to f-vi hand the move's readings, over the real client and the in-process ledger. It reads each waiting word's id and attempt
/// mark, takes words off as taken, and settles uncertain ones by the take-back door with the <c>lost</c> note written by the move itself
/// (round 8, <i>O7-F4</i>). It reads no session event: a word its destination shows asked and the person took back reads <i>left</i>.
/// </summary>
[Collection("agent operation ports")]
public sealed class MoveSaidDoorTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "move-said-door", Guid.NewGuid().ToString("N")[..8]);

    public MoveSaidDoorTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    [Fact]
    public async Task The_door_reads_a_record_s_waiting_words_and_their_attempts_takes_words_off_and_settles_uncertain_ones_with_the_lost_note()
    {
        var ledger = new ChatLedger();
        ledger.Chat("s2", "ended", "claude-code-acp", _home).Say("w1", "one").Say("w2", "two").Say("w3", "three");
        var client = ledger.Client();
        await client.AttemptAsync("s2", ["w2", "w3"]);

        // w3's own `User` event, recorded by the delivery before the write: the display's, which no older come-up writes the note beside.
        Events.Keep("s2", new SessionEvent { Kind = SessionEventKind.User, Id = "w3", Text = "three" }, say: null);
        var door = new MoveSaidDoor(client, Events);

        Assert.Equal([new MoveWaiting("w1", false), new MoveWaiting("w2", true), new MoveWaiting("w3", true)], await door.WaitingAsync("s2", default));
        Assert.Empty((await door.WaitingAsync("s9", default))!);

        await door.TakeAsync("s2", ["w1"], default);
        await door.SettleAsync("s2", ["w2", "w3"], default);

        Assert.Equal([("s2", "w1")], ledger.Taken);
        Assert.Equal([("s2", "w2"), ("s2", "w3")], ledger.TakenBack);
        Assert.Empty((await door.WaitingAsync("s2", default))!);
        var lost = Assert.Single(Events.After("s2", 0).Events, each => each.Code == SessionEventCodes.Lost);
        Assert.Equal(["w2", "w3"], lost.Words);
    }

    [Fact]
    public async Task The_door_answers_that_a_record_cannot_be_told_where_the_service_does_not_answer_and_never_faults()
    {
        var ledger = new ChatLedger { BeforeRequest = (_, _) => throw new HttpRequestException("the service went") };
        var door = new MoveSaidDoor(ledger.Client(), Events);

        Assert.Null(await door.WaitingAsync("s2", default));
        await door.TakeAsync("s2", ["w1"], default);
        await door.SettleAsync("s2", ["w1"], default);
        Assert.Empty(Events.After("s2", 0).Events);
    }

    [Fact]
    public async Task Over_the_door_a_moved_word_its_destination_shows_asked_and_the_person_then_took_back_is_left_never_taken()
    {
        var ledger = new ChatLedger();
        ledger.Chat("s2", "ended", "claude-code-acp", _home).Say("w1", "one");
        var client = ledger.Client();
        var dead = new HashSet<string>(StringComparer.Ordinal);
        MoveStore Store(string run) => new(_home, () => StoreSettled.Yes, each => !dead.Contains(each.Run), run, StoreProcess.Shell);
        var executor = Store("run-dead");
        var claim = executor.Decide(new MoveDecision("s1", MoveExecutors.Conversation, MoveHows.Brief, HandOverWhy.Limit, HandOverBy.Daoris) { FromEnded = true }).Claim!;
        executor.Opening(claim, "opens-s1");
        executor.Opened(claim, "s2", [new("w1", null, "opens-s1/w1", 0)]);
        executor.Advance(claim, MoveRecordPhase.Delivering);
        executor.MarkEffect(claim, MoveEffects.Prompt);
        await client.AttemptAsync("s2", ["w1"]);

        // The word's event recorded before the write, the write never made, and the person's take-back meanwhile.
        Events.Keep("s2", new SessionEvent { Kind = SessionEventKind.User, Id = "w1", Text = "one" }, say: null);
        Assert.True((await client.TakeBackAsync("s2", "w1")).Done);
        dead.Add("run-dead");
        var taker = Store("run-taker");
        var taken = taker.Take(claim.Move, claim.Generation).Claim!;

        var read = await new MoveDelivery(taker, new MoveSaidDoor(client, Events)).ReadAsync(taken);

        Assert.Equal(MoveDeliveryOutcome.Held, read.Outcome);
        Assert.Equal(["w1"], read.Left.Select(word => word.Word));
        Assert.Empty(read.Taken);
        Assert.Empty(ledger.Taken);
    }
}
