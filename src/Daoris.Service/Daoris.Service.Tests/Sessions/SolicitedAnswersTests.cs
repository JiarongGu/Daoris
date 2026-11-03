using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// COMMS1b (the comms design §4.2, §13 points 1 to 5): a helper's answer, a partner's and an opinion's findings go through one door, which
/// walks the line's head as the say door does, answers a retry from the settlement or from the word already waiting, and takes the head by
/// one table of its state. Every row of the table runs for each case, so the desks cannot disagree again.
/// </summary>
public sealed class SolicitedAnswersTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private const string Tip = "2222222222222222222222222222222222222222";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private SessionStore _sessions = null!;
    private OpinionStore _opinions = null!;
    private SessionLedger _ledger = null!;
    private HelperDesk _helpers = null!;
    private PartnerDesk _partners = null!;
    private OpinionDesk _desk = null!;
    private int _tree;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var quests = await QuestStore.OpenAsync(_connection);
        _sessions = await SessionStore.OpenAsync(_connection);
        _opinions = await OpinionStore.OpenAsync(_connection);
        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.RegisterAsync(new Registration("Owner", true, "A repo.", [], [], [], 0, Root: "/trees/owner"), Now);
        _ledger = new SessionLedger(quests, _sessions, service, opinions: _opinions);
        _helpers = new HelperDesk(_sessions);
        _partners = new PartnerDesk(_sessions);
        _desk = new OpinionDesk(_opinions, _sessions);
    }

    public Task DisposeAsync() => _connection.DisposeAsync().AsTask();

    /// <summary>What one hand answered, read the same for every case.</summary>
    private sealed record Hand(string Case, string Id, Func<bool, Task<Result>> Press);

    private sealed record Result(bool Kept, bool Waiting, string? SettledOn, string? Refusal, string? Holder, string? WordId, string Message);

    private async Task<Session> LeadAsync(SessionState state = SessionState.Working, SessionKind kind = SessionKind.Chat, string? from = null)
    {
        var made = await _sessions.CreateAsync(
            quest: null, "Owner", "stub", Now, tree: $"/trees/lead-{_tree++}", kind: kind, from: from, fromWhy: from is null ? null : "press",
            fromBy: from is null ? null : "person", ownEntry: from is null);
        return (await _sessions.SetStateAsync(made.Id, state, null, null, null, Now))!;
    }

    private static Result Of(HelperOutcome o) =>
        new(o.Refusal == HelperRefusal.None && o.Word is not null && !o.Waiting, o.Waiting, o.SettledOn,
            o.Refusal == HelperRefusal.None ? null : o.Refusal.ToString(), o.Session?.Id, o.Word?.Id, o.Message);

    private static Result Of(OpinionOutcome o) =>
        new(o.Refusal == OpinionRefusal.None && o.Word is not null && !o.Waiting, o.Waiting, o.SettledOn,
            o.Refusal == OpinionRefusal.None ? null : o.Refusal.ToString(), o.Session?.Id, o.Word?.Id, o.Message);

    /// <summary>The case's answer waiting to be handed to <paramref name="lead"/>'s line.</summary>
    private async Task<Hand> GivenAsync(string kind, Session lead, bool running = false, string? level = null)
    {
        switch (kind)
        {
            case "helper":
            {
                var helper = await _sessions.CreateAsync(null, "Owner", "stub", Now, kind: SessionKind.Chat, tree: $"/trees/h-{_tree++}", serves: lead.Id, ownEntry: true);
                return new Hand(kind, helper.Id, settled => _helpers.HandAsync(helper.Id, "the claim", Now, running: running, level: level, settled: settled).ContinueWith(t => Of(t.Result)));
            }

            case "partner":
            {
                var partner = await _sessions.CreateAsync(null, "Owner", "stub", Now, kind: SessionKind.Chat, tree: $"/trees/p-{_tree++}", serves: lead.Id, piece: $"p{_tree}", ownEntry: true);
                return new Hand(kind, partner.Id, settled => _partners.HandAsync(partner.Id, "the piece", Now, settled: settled).ContinueWith(t => Of(t.Result)));
            }

            default:
            {
                var asked = await _desk.AskAsync(
                    new OpinionAsk(
                        Opinions.Landing, Opinions.First, lead.Id, new OpinionCandidate("Owner", new string('1', 40), Tip, [Tip]),
                        new OpinionReviewer("stub", Opinions.AnotherMaker), Opinions.CopyAlone, 20, $"/trees/o-{_tree++}"),
                    Now);
                Assert.Equal(OpinionRefusal.None, asked.Refusal);
                await _sessions.SetStateAsync(asked.Session!.Id, SessionState.Working, null, null, null, Now);
                var given = await _desk.GiveAsync(
                    asked.Session.Id, [new OpinionFinding("must", "src/A.cs:1", "claim", "consequence", "ran it", "likely")], "src/", null, null, Now);
                Assert.Equal(OpinionRefusal.None, given.Refusal);
                return new Hand(kind, asked.Opinion!.Id, settled => _desk.HandAsync(asked.Opinion.Id, Now, settled: settled).ContinueWith(t => Of(t.Result)));
            }
        }
    }

    public static TheoryData<string> Cases => ["helper", "partner", "opinion"];

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task A_conversation_between_turns_and_an_ended_head_take_the_word(string kind)
    {
        foreach (var state in new[] { SessionState.Working, SessionState.Completed })
        {
            var lead = await LeadAsync(state);
            var hand = await GivenAsync(kind, lead);

            var result = await hand.Press(false);

            Assert.True(result.Kept, result.Message);
            Assert.Equal(lead.Id, result.Holder);
            var word = (await _sessions.FindAsync(lead.Id))!.Said.Single();
            Assert.StartsWith(kind + ":", word.Key);
            Assert.True(word.Reopens);
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task A_park_unanswered_waits_and_a_park_answered_takes_the_word_beside_the_answer(string kind)
    {
        var lead = await LeadAsync(SessionState.AwaitingPerson);
        var hand = await GivenAsync(kind, lead);

        var waits = await hand.Press(false);
        Assert.Equal("CannotTake", waits.Refusal);
        Assert.Contains("waits on the person's answer", waits.Message);
        Assert.Empty((await _sessions.FindAsync(lead.Id))!.Said);

        await _sessions.KeepSaidAsync(lead.Id, new SaidWord("answer01", "go on", Now, [], Reopens: false));
        var takes = await hand.Press(false);

        Assert.True(takes.Kept, takes.Message);
        var parked = (await _sessions.FindAsync(lead.Id))!;
        Assert.Equal("go on", parked.Said.Single(word => word.Persons).Text);
        Assert.Equal(2, parked.Said.Count);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task A_stood_down_head_and_a_running_step_in_flight_refuse_the_word(string kind)
    {
        var down = await LeadAsync(SessionState.StoodDown);
        Assert.Equal("CannotTake", (await (await GivenAsync(kind, down)).Press(false)).Refusal);

        var driven = await LeadAsync(SessionState.Working, SessionKind.Driven);
        var hand = await GivenAsync(kind, driven);
        var refused = await hand.Press(false);
        Assert.Equal("CannotTake", refused.Refusal);
        Assert.Contains("never into a step in flight", refused.Message);
        Assert.Empty((await _sessions.FindAsync(driven.Id))!.Said);
    }

    [Fact]
    public async Task A_working_driven_head_takes_a_helpers_answer_for_its_run_when_its_driver_says_so_at_its_level()
    {
        var driven = await LeadAsync(SessionState.Working, SessionKind.Driven);
        var hand = await GivenAsync("helper", driven, running: true, level: SaidLevel.Urgent);

        var result = await hand.Press(false);

        Assert.True(result.Kept, result.Message);
        Assert.Equal(SaidLevel.Urgent, (await _sessions.FindAsync(driven.Id))!.Said.Single().Level);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task An_answer_already_waiting_is_answered_with_its_holder_and_word_never_refused(string kind)
    {
        var lead = await LeadAsync(SessionState.Completed);
        var hand = await GivenAsync(kind, lead);
        var first = await hand.Press(false);
        Assert.True(first.Kept, first.Message);

        var again = await hand.Press(false);

        Assert.True(again.Waiting, again.Message);
        Assert.Null(again.Refusal);
        Assert.Equal((lead.Id, first.WordId), (again.Holder, again.WordId));
        Assert.Single((await _sessions.FindAsync(lead.Id))!.Said);

        // Still answered once the head is parked, where a new word would wait.
        await _sessions.SetStateAsync(lead.Id, SessionState.AwaitingPerson, "asked.", null, null, Now);
        Assert.True((await hand.Press(false)).Waiting);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task A_word_taken_is_settled_on_the_record_it_left_for_a_hand_that_asks(string kind)
    {
        var lead = await LeadAsync(SessionState.Completed);
        var hand = await GivenAsync(kind, lead);
        var first = await hand.Press(true);
        Assert.True(first.Kept, first.Message);
        await _sessions.TakeSaidAsync(lead.Id, [first.WordId!]);

        var retry = await hand.Press(true);

        Assert.Equal(lead.Id, retry.SettledOn);
        Assert.Empty((await _sessions.FindAsync(lead.Id))!.Said);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task A_hand_over_after_the_keep_leaves_the_word_to_the_part_that_goes_on_and_a_retry_finds_it_there(string kind)
    {
        var lead = await LeadAsync();
        var hand = await GivenAsync(kind, lead);
        var first = await hand.Press(true);
        Assert.True(first.Kept, first.Message);
        await _sessions.SetStateAsync(lead.Id, SessionState.Completed, null, null, null, Now);

        var opened = await _ledger.OpenChatAsync(
            "Owner", "stub", Now, from: lead.Id, opens: $"move/{kind}", moves: new OpenMoves(lead.Id, [first.WordId!], []));
        Assert.Equal(SessionOpenRefusal.None, opened.Refusal);

        var retry = await hand.Press(true);

        Assert.True(retry.Waiting, retry.Message);
        Assert.Equal((opened.Session!.Id, first.WordId), (retry.Holder, retry.WordId));
        if (kind == "opinion")
        {
            // The part that holds the findings answers them (an authority change, D225's note).
            Assert.Equal(opened.Session.Id, (await _desk.ReadAsync(hand.Id, Now))!.Opinion.Handed!.Session);
            var answered = await _desk.AnswerAsync(opened.Session.Id, null, [new OpinionAnswer(1, "unresolved", Now) { Why = "x" }], Now);
            Assert.Equal(OpinionRefusal.None, answered.Refusal);
            Assert.Equal(OpinionRefusal.NotHanded, (await _desk.AnswerAsync(lead.Id, null, [new OpinionAnswer(1, "unresolved", Now) { Why = "x" }], Now)).Refusal);
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task A_hand_to_a_record_the_line_left_goes_to_its_head(string kind)
    {
        var lead = await LeadAsync(SessionState.Completed);
        var hand = await GivenAsync(kind, lead);
        var next = await LeadAsync(SessionState.Working, from: lead.Id);

        var result = await hand.Press(false);

        Assert.True(result.Kept, result.Message);
        Assert.Equal(next.Id, result.Holder);
        Assert.Empty((await _sessions.FindAsync(lead.Id))!.Said);
    }

    [Fact]
    public async Task A_reviewer_serves_the_working_session_and_is_read_as_a_reviewer_never_a_helper()
    {
        var lead = await LeadAsync();
        var hand = await GivenAsync("opinion", lead);
        var reviewer = (await _desk.ReadAsync(hand.Id, Now))!.Opinion.Session;

        var record = (await _sessions.FindAsync(reviewer))!;
        Assert.Equal((lead.Id, hand.Id), (record.Serves, record.Opinion));

        var asked = await _helpers.OpenAsync(new HelperOpen(reviewer, "stub", "/trees/h-x"), Now);
        Assert.Equal(HelperRefusal.BadShape, asked.Refusal);
        Assert.Contains("a reviewer cannot ask for a helper", asked.Message);
        Assert.Equal(HelperRefusal.NotFound, (await _helpers.HandAsync(reviewer, "words", Now)).Refusal);
    }

    [Fact]
    public async Task A_second_hand_of_findings_is_its_own_key_after_the_first_was_taken_and_the_line_went_on()
    {
        var lead = await LeadAsync();
        var hand = await GivenAsync("opinion", lead);
        var reviewer = (await _desk.ReadAsync(hand.Id, Now))!.Opinion.Session;

        var first = await hand.Press(false);
        Assert.True(first.Kept, first.Message);
        Assert.Equal("opinion:" + hand.Id, (await _sessions.FindAsync(lead.Id))!.Said.Single().Key);
        await _sessions.TakeSaidAsync(lead.Id, [first.WordId!]);
        await _sessions.SetStateAsync(lead.Id, SessionState.Completed, null, null, null, Now);
        var made = await _sessions.CreateAsync(
            null, "Owner", "stub", Now.AddSeconds(1), kind: SessionKind.Chat, tree: lead.Tree, from: lead.Id, fromWhy: "press", fromBy: "person",
            entryOrigin: lead.Entry!.Split('/')[0]);
        var next = (await _sessions.SetStateAsync(made.Id, SessionState.Completed, null, null, null, Now))!;

        var again = await _desk.HandAsync(hand.Id, Now, to: next.Id, settled: true);

        Assert.True(again.Refusal == OpinionRefusal.None, again.Message);
        Assert.Null(again.SettledOn);
        Assert.Equal("opinion:" + hand.Id + ":again", again.Word!.Key);
        Assert.Equal(next.Id, again.Session!.Id);
        Assert.NotEqual(reviewer, again.Session.Id);
    }
}
