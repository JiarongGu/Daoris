using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The writer's lease on a place: one in-place record holds a repository's checkout while its turn runs, and in a git place also
/// while its last turn left uncommitted paths of its own. The ledger takes it in the open and the move into a running state, and
/// lets it go in the move out of one; nothing renews it on a clock.
/// </summary>
public sealed class PlaceLeaseTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-11T10:00:00Z");
    private const string Root = "/places/reports";

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private SessionStore _sessions = null!;
    private SessionLedger _ledger = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        _sessions = await SessionStore.OpenAsync(_connection);
        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.RegisterAsync(new Registration("Reports", Adopted: true, "A place.", [], [], [], 0, Root: Root), Now);
        _ledger = new SessionLedger(_quests, _sessions, service);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private Task<Quest> Publish(string title) => _quests.PublishAsync("Asker", "Reports", title, "Why.", Now);

    private async Task<Session> Running(string title)
    {
        var opened = await _ledger.OpenAsync((await Publish(title)).Id, "stub", Now);
        Assert.Equal(SessionOpenRefusal.None, opened.Refusal);
        foreach (var state in new[] { "starting", "working" })
            Assert.Equal(SessionAdvanceRefusal.None, (await _ledger.AdvanceAsync(opened.Session!.Id, state, null, null, null, Now)).Refusal);
        return opened.Session!;
    }

    [Fact]
    public async Task A_second_in_place_open_is_refused_naming_the_holder_and_admitted_after_the_park()
    {
        var first = await Running("First");
        var second = await Publish("Second");

        var refused = await _ledger.OpenAsync(second.Id, "stub", Now);
        Assert.Equal(SessionOpenRefusal.RepositoryBusy, refused.Refusal);
        Assert.Contains($"`{first.Id}`", refused.Message);
        Assert.Contains("one writer at a time", refused.Message);
        Assert.DoesNotContain(Root, refused.Message);

        var parked = await _ledger.AdvanceAsync(first.Id, "awaiting-person", "A question.", null, null, Now.AddMinutes(1));
        Assert.Equal(SessionAdvanceRefusal.None, parked.Refusal);
        Assert.Null(parked.Session!.HoldsPlace);

        Assert.Equal(SessionOpenRefusal.None, (await _ledger.OpenAsync(second.Id, "stub", Now.AddMinutes(2))).Refusal);
    }

    [Fact]
    public async Task A_park_with_uncommitted_paths_of_its_own_keeps_the_place_until_a_turn_commits_them()
    {
        var first = await Running("First");
        var parked = await _ledger.AdvanceAsync(
            first.Id, "awaiting-person", "A question.", null, null, Now.AddMinutes(1), holdsPlace: ["notes/a.md", "b.txt"]);
        Assert.Equal(["notes/a.md", "b.txt"], parked.Session!.HoldsPlace);

        var chat = await _ledger.OpenChatAsync("Reports", "stub", Now.AddMinutes(2));
        Assert.Equal(SessionOpenRefusal.RepositoryBusy, chat.Refusal);
        Assert.Contains("2 uncommitted paths of its own", chat.Message);

        // Its own answer takes it back into a running state, which holds the place while the mark is gone.
        await _ledger.SayAsync(first.Id, "Go on.", null, Now.AddMinutes(3));
        var resumed = await _ledger.AdvanceAsync(first.Id, "working", null, null, null, Now.AddMinutes(4));
        Assert.Equal(SessionAdvanceRefusal.None, resumed.Refusal);
        Assert.Null(resumed.Session!.HoldsPlace);

        // A turn that committed them ends with no mark, and the place is free.
        var done = await _ledger.AdvanceAsync(first.Id, "awaiting-person", "Another.", null, null, Now.AddMinutes(5));
        Assert.Null(done.Session!.HoldsPlace);
        Assert.Equal(SessionOpenRefusal.None, (await _ledger.OpenChatAsync("Reports", "stub", Now.AddMinutes(6))).Refusal);
    }

    [Fact]
    public async Task Letting_others_work_here_clears_the_mark_and_a_discard_clears_it_the_same_way()
    {
        var first = await Running("First");
        await _ledger.AdvanceAsync(first.Id, "awaiting-person", "Q.", null, null, Now.AddMinutes(1), holdsPlace: ["a.txt"]);

        var released = await _ledger.ReleasePlaceAsync(first.Id);
        Assert.NotNull(released);
        Assert.Null(released!.HoldsPlace);
        Assert.Equal(SessionState.AwaitingPerson, released.State);
        Assert.Equal(SessionOpenRefusal.None, (await _ledger.OpenChatAsync("Reports", "stub", Now.AddMinutes(3))).Refusal);
        Assert.Null(await _ledger.ReleasePlaceAsync("no-such"));
    }

    [Fact]
    public async Task A_discard_clears_the_mark_on_the_paths_it_discarded_and_the_place_goes_once_none_is_left()
    {
        var first = await Running("First");
        await _ledger.AdvanceAsync(first.Id, "awaiting-person", "Q.", null, null, Now.AddMinutes(1), holdsPlace: ["a.txt", "notes/b.md"]);

        var some = await _ledger.ReleasePlaceAsync(first.Id, ["a.txt", "never-marked.txt"]);
        Assert.Equal(["notes/b.md"], some!.HoldsPlace);
        Assert.Equal(SessionOpenRefusal.RepositoryBusy, (await _ledger.OpenChatAsync("Reports", "stub", Now.AddMinutes(2))).Refusal);

        var rest = await _ledger.ReleasePlaceAsync(first.Id, ["notes/b.md"]);
        Assert.Null(rest!.HoldsPlace);
        Assert.Equal(SessionOpenRefusal.None, (await _ledger.OpenChatAsync("Reports", "stub", Now.AddMinutes(3))).Refusal);
    }

    [Fact]
    public async Task A_park_going_on_waits_for_the_place_another_turn_holds()
    {
        var first = await Running("First");
        await _ledger.AdvanceAsync(first.Id, "awaiting-person", "Q.", null, null, Now.AddMinutes(1));
        var second = await Running("Second");

        await _ledger.SayAsync(first.Id, "Go on.", null, Now.AddMinutes(2));
        var busy = await _ledger.AdvanceAsync(first.Id, "working", null, null, null, Now.AddMinutes(3));

        Assert.Equal(SessionAdvanceRefusal.Busy, busy.Refusal);
        Assert.Contains($"`{second.Id}`", busy.Message);
        Assert.Equal(SessionState.AwaitingPerson, (await _sessions.FindAsync(first.Id))!.State);
    }

    [Fact]
    public async Task A_cut_off_run_concluded_by_the_sweep_lets_the_place_go_with_its_paths_marked()
    {
        var first = await Running("First");

        var swept = await _ledger.AdvanceAsync(
            first.Id, "stopped", "Cut off.", null, null, Now.AddMinutes(1), interrupted: true, holdsPlace: ["draft.md"]);

        Assert.Equal(["draft.md"], swept.Session!.HoldsPlace);
        Assert.Equal(SessionOpenRefusal.None, (await _ledger.OpenAsync((await Publish("Next")).Id, "stub", Now.AddMinutes(2))).Refusal);
    }

    [Fact]
    public async Task Only_a_move_out_of_a_running_state_writes_the_mark()
    {
        var opened = await _ledger.OpenAsync((await Publish("First")).Id, "stub", Now);

        var refused = await _ledger.AdvanceAsync(opened.Session!.Id, "starting", null, null, null, Now, holdsPlace: ["a.txt"]);

        Assert.Equal(SessionAdvanceRefusal.InvalidMove, refused.Refusal);
    }

    [Fact]
    public async Task A_session_in_a_copy_of_its_own_takes_no_lease_on_the_place()
    {
        await Running("In place");

        var copy = await _ledger.OpenAsync((await Publish("Apart")).Id, "stub", Now, tree: "/srv/daoris/trees/reports/s-1a2b3c4d");

        Assert.Equal(SessionOpenRefusal.None, copy.Refusal);
    }

    [Fact]
    public async Task A_finished_record_with_a_mark_holds_the_place_only_while_it_waits_on_its_own_helper()
    {
        var first = await Running("First");
        var done = await _ledger.AdvanceAsync(first.Id, "completed", null, null, null, Now.AddMinutes(1), holdsPlace: ["a.txt"]);
        Assert.Equal(SessionAdvanceRefusal.None, done.Refusal);

        Assert.Equal(SessionOpenRefusal.None, (await _ledger.OpenChatAsync("Reports", "stub", Now.AddMinutes(2))).Refusal);
    }

    [Fact]
    public async Task A_record_that_ended_waiting_on_its_own_helper_with_a_mark_keeps_the_place()
    {
        var first = await Running("First");
        await _ledger.AdvanceAsync(
            first.Id, "completed", "It asked a helper.", null, null, Now.AddMinutes(1), holdsPlace: ["a.txt"], noteParts: """[{"code":"ended.awaits-helper"}]""");

        Assert.Equal(SessionOpenRefusal.RepositoryBusy, (await _ledger.OpenChatAsync("Reports", "stub", Now.AddMinutes(2))).Refusal);
    }
}
