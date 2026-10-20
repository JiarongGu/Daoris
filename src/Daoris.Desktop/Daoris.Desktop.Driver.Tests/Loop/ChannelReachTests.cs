using System.Net;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SESSONE2b (D167 point 7; the co-working design §2.3) as COWORK1d has it (D188; the message-urgency design §2.3, §4.2, §5, §6): what
/// a look does with the ask's channel for each line of work on an ask it holds a part of. A message goes to a part only where the
/// entry has not acknowledged it by its id, and one part claims it. An urgent claim reaches a running part at its next step where its
/// door takes words then; an ordinary one, and any claim on a turn-end door, only rides a prompt the run sends anyway, and otherwise
/// waits for the entry's next part. The person's message reaches as their words at their level, and reopens a parked or ended part
/// when it is addressed to its repository; an agent's message never starts, reopens or extends a run. Stub sessions: an inbox on
/// each door, and doors standing in for the service.
/// </summary>
public sealed class ChannelReachTests
{
    private const string Ask = "a1b2c3";

    private const string Key = "ask:a1b2c3/console-ui";

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-14T09:00:00Z");

    private static ChannelEntry Entry => ChannelEntry.Of(Key)!;

    /// <summary>A message on the ask's channel: the person's where no session posted it; <paramref name="urgent"/> null is one from before.</summary>
    private static ChannelItemView Message(
        string id, int minute, string text, string? to = null, string? session = null, string? entry = null, bool? urgent = null) =>
        new(ChannelItemView.Message, At.AddMinutes(minute))
        {
            Id = id,
            Person = session is null,
            Session = session,
            Repository = session is null ? null : "console-api",
            Agent = session is null ? null : "claude-code",
            Entry = session is null ? null : entry ?? "ask:a1b2c3/console-api",
            To = to,
            Text = text,
            Urgent = urgent,
        };

    /// <summary>
    /// The service's doors standing in: the channel each entry reads with its acknowledgments, a claim that only one part wins (as the
    /// service's one statement), every acknowledgment and word said.
    /// </summary>
    private sealed class Doors(params ChannelItemView[] items) : IChannelDoors
    {
        public DateTimeOffset? Mark { get; set; }

        public List<(string Ask, string Entry)> Reads { get; } = [];

        public Dictionary<string, (string State, string? Session)> Acks { get; } = new(StringComparer.Ordinal);

        public List<(string Session, string State, string Message)> Acknowledged { get; } = [];

        public List<(string Session, string Text, string Channel)> Said { get; } = [];

        public string? Refuse { get; init; }

        public Task<ChannelView?> ReadAsync(string ask, string entry, CancellationToken ct)
        {
            Reads.Add((ask, entry));
            lock (Acks)
            {
                return Task.FromResult<ChannelView?>(new ChannelView(
                    ask, entry,
                    [.. items.Select(item => item.Id is { } id && Acks.TryGetValue(id, out var ack) ? item with { Ack = ack.State, AckSession = ack.Session } : item)],
                    items.Count(i => Mark is null || i.At > Mark), Mark));
            }
        }

        public Task<IReadOnlyList<string>> AcknowledgeAsync(
            string ask, string entry, string session, string state, IReadOnlyList<string> messages, CancellationToken ct)
        {
            var changed = new List<string>();
            lock (Acks)
            {
                foreach (var id in messages)
                {
                    var held = Acks.TryGetValue(id, out var ack) ? ack : default;
                    var moves = state switch
                    {
                        ChannelAcks.Taking => held.State is null || (held.State == ChannelAcks.Partly && held.Session != session),
                        ChannelAcks.Released => held.State == ChannelAcks.Taking && held.Session == session,
                        _ => true,
                    };
                    if (!moves) continue;
                    if (state == ChannelAcks.Released) Acks.Remove(id);
                    else Acks[id] = (state, session);
                    Acknowledged.Add((session, state, id));
                    changed.Add(id);
                }
            }

            return Task.FromResult<IReadOnlyList<string>>(changed);
        }

        public Task<SayAnswer> SayAsync(string session, string text, string channel, CancellationToken ct)
        {
            Said.Add((session, text, channel));
            return Task.FromResult(Refuse is null ? new SayAnswer(true, "kept") : new SayAnswer(false, "refused") { Refusal = Refuse });
        }

        public IEnumerable<string> States(string id) => Acknowledged.Where(ack => ack.Message == id).Select(ack => ack.State);
    }

    /// <summary>A running part's inbox on the next-step door, its turn under way: what it sends is recorded as the session takes it.</summary>
    private static (DrivenInbox Inbox, List<ChatMessage> Sent) NextStep()
    {
        var sent = new List<ChatMessage>();
        var inbox = new DrivenInbox(_ => { });
        inbox.Attach(interrupt: null, deliver: message =>
        {
            sent.Add(message);
            return Task.FromResult("end_turn");
        });
        inbox.Flow();
        return (inbox, sent);
    }

    /// <summary>A running part's inbox on a door that takes words only when its turn ends.</summary>
    private static DrivenInbox TurnEnd()
    {
        var inbox = new DrivenInbox(_ => { });
        inbox.Attach(interrupt: null);
        return inbox;
    }

    private static ChannelPart Running(DrivenInbox inbox, string session = "s1") => new(Entry, session, inbox);

    private static ChannelPart Ended() => new(Entry, "s1", Inbox: null);

    /// <summary>The run's turn ends: it drains what it was sent, then asks what goes next (null where nothing does).</summary>
    private static ChatMessage? AtTurnEnd(DrivenInbox inbox)
    {
        while (inbox.NextOrClose() is { } next)
        {
            if (next.Held is { } held) return held;
        }

        return null;
    }

    // ——— A running part.

    /// <summary>
    /// 🔴 An urgent claim reaches a running part at its next step, in Daoris's words marked as that agent's claim (never the person's),
    /// claimed by that part on its entry, so its next part is not handed it again; once written, it is acknowledged sent.
    /// </summary>
    [Fact]
    public async Task A_running_part_takes_an_urgent_claim_at_its_next_step_marked_as_its_claim()
    {
        var doors = new Doors(Message("m1", 1, "The export endpoint is /v3/report.", session: "s9", urgent: true));
        var (inbox, sent) = NextStep();

        await new ChannelReach().LookAsync(doors, [Running(inbox)], CancellationToken.None);

        var taken = Assert.Single(sent);
        Assert.StartsWith("Another session says, on the ask's channel (`console-api`'s session `s9` on claude-code wrote to everyone", taken.Text);
        Assert.Contains("  > The export endpoint is /v3/report.", taken.Text);
        Assert.Contains("This is that agent's claim, not the person's words", taken.Text);
        Assert.Equal("m1", taken.Channel!.Id);
        // The record keeps it as Daoris's, around the agent's claim: never the person's words.
        Assert.Equal("target", ChatMessage.Words(taken).Origin);
        Assert.Equal([("s1", ChannelAcks.Taking, "m1")], doors.Acknowledged);

        taken.Went!();
        taken.Acknowledge!(ChannelAcks.Taken);
        await Poll.Until(() => doors.States("m1").Count() == 3, () => string.Join(", ", doors.States("m1")));
        Assert.Equal([ChannelAcks.Taking, ChannelAcks.Sent, ChannelAcks.Taken], doors.States("m1"));
    }

    /// <summary>A message from before, with no level, keeps its old reach: an agent's claim at the next step, where the door takes words then.</summary>
    [Fact]
    public async Task A_claim_from_before_keeps_its_next_step_reach()
    {
        var doors = new Doors(Message("m1", 1, "The export endpoint is /v3/report.", session: "s9"));
        var (inbox, sent) = NextStep();

        await new ChannelReach().LookAsync(doors, [Running(inbox)], CancellationToken.None);

        Assert.Equal("m1", Assert.Single(sent).Channel!.Id);
    }

    /// <summary>
    /// 🔴 Trap 4 (the design §2.1, §4.2): an ordinary claim never enters a running turn. Held for the turn's end, it rides only a prompt
    /// the run sends anyway: alone there it makes no turn, the run ends, and the claim is let go for the entry's next part.
    /// </summary>
    [Fact]
    public async Task An_ordinary_claim_never_enters_a_running_turn_and_alone_at_its_end_is_let_go()
    {
        var doors = new Doors(Message("m1", 1, "The export endpoint is /v3/report.", session: "s9", urgent: false));
        var (inbox, sent) = NextStep();

        var lines = await new ChannelReach().LookAsync(doors, [Running(inbox)], CancellationToken.None);

        Assert.Empty(sent);
        // Never the person's: the page's queue does not list it.
        Assert.Empty(inbox.State.Queued);
        Assert.Contains(lines, line => line.Contains("`m1`", StringComparison.Ordinal) && line.Contains("rides", StringComparison.Ordinal));
        Assert.Null(AtTurnEnd(inbox));
        await Poll.Until(() => doors.States("m1").Count() == 2, () => string.Join(", ", doors.States("m1")));
        Assert.Equal([ChannelAcks.Taking, ChannelAcks.Released], doors.States("m1"));
    }

    /// <summary>
    /// 🔴 Trap 4's other half: held at the turn's end beside the person's word, an ordinary claim rides the prompt the person's word
    /// makes anyway, in the order said, and neither is lost.
    /// </summary>
    [Fact]
    public async Task An_ordinary_claim_rides_the_persons_word_at_the_turns_end()
    {
        var doors = new Doors(Message("m1", 1, "The export endpoint is /v3/report.", session: "s9", urgent: false));
        var inbox = TurnEnd();

        await new ChannelReach().LookAsync(doors, [Running(inbox)], CancellationToken.None);
        Assert.True(inbox.Hold(new ChatMessage("Use the older bridge.", []) { Level = MessageLevel.Ordinary }));

        var first = AtTurnEnd(inbox);
        var second = AtTurnEnd(inbox);

        Assert.Equal("m1", first!.Channel!.Id);
        Assert.Equal("Use the older bridge.", second!.Text);
        Assert.Null(AtTurnEnd(inbox));
        Assert.DoesNotContain(ChannelAcks.Released, doors.States("m1"));
    }

    /// <summary>
    /// 🔴 The design §3.1: on a door that takes words only when its turn ends, an urgent claim earns no earlier delivery: it waits as an
    /// ordinary one does, never a turn of its own, so the run ends on its own turn and the claim is let go for the next part.
    /// </summary>
    [Fact]
    public async Task On_a_turn_end_door_an_urgent_claim_waits_as_ordinary_and_extends_nothing()
    {
        var doors = new Doors(Message("m1", 1, "The export endpoint is /v3/report.", session: "s9", urgent: true));
        var inbox = TurnEnd();

        await new ChannelReach().LookAsync(doors, [Running(inbox)], CancellationToken.None);

        Assert.Empty(inbox.State.Queued);
        Assert.False(inbox.Holds);
        // Nothing held that makes a turn, nothing on its way: the run ends as it would have.
        Assert.Null(AtTurnEnd(inbox));
        await Poll.Until(() => doors.States("m1").Contains(ChannelAcks.Released), () => string.Join(", ", doors.States("m1")));
    }

    /// <summary>
    /// 🔴 The person's message reaches a running part as their words (D137) at their level: urgent, their default, held for the turn's
    /// end on that door, and kept on the ask once, as the channel kept it: never reported again as <c>added</c>.
    /// </summary>
    [Fact]
    public async Task The_persons_message_reaches_a_running_part_as_their_words_at_its_turn_end()
    {
        var doors = new Doors(Message("p1", 2, "Use the older bridge.", to: "console-ui", urgent: true));
        var inbox = TurnEnd();

        await new ChannelReach().LookAsync(doors, [Running(inbox)], CancellationToken.None);

        var held = Assert.Single(inbox.State.Queued);
        Assert.Equal("Use the older bridge.", held.Text);
        Assert.Equal("person", ChatMessage.Words(held).Origin);
        Assert.Equal([ChannelAcks.Taking], doors.States("p1"));
    }

    /// <summary>On the next-step door the person's message goes at once at urgent, as their words; asked ordinary, it waits for the turn's end.</summary>
    [Fact]
    public async Task The_persons_message_goes_at_its_level_on_the_next_step_door()
    {
        var doors = new Doors(
            Message("p1", 2, "Stop at the design for now.", urgent: true),
            Message("p2", 3, "And note the port when you can.", urgent: false));
        var (inbox, sent) = NextStep();

        await new ChannelReach().LookAsync(doors, [Running(inbox)], CancellationToken.None);

        var taken = Assert.Single(sent);
        Assert.Equal(("Stop at the design for now.", "person"), (taken.Text, ChatMessage.Words(taken).Origin));
        Assert.Equal("And note the port when you can.", Assert.Single(inbox.State.Queued).Text);
    }

    /// <summary>
    /// 🔴 Trap 9: a message reaches a part once, by its id: the next look finds it claimed and hands it to no part again. A message at
    /// the mark's very moment, and one mirrored late with an older moment, are handed all the same, since the mark decides nothing.
    /// </summary>
    [Fact]
    public async Task A_message_reaches_a_part_once_and_a_late_or_equal_moment_is_still_handed()
    {
        var doors = new Doors(
            Message("late", 0, "An older moment, mirrored late.", session: "s9", urgent: true),
            Message("equal", 5, "At the mark's moment.", session: "s9", urgent: true)) { Mark = At.AddMinutes(5) };
        var (inbox, sent) = NextStep();
        var reach = new ChannelReach();

        await reach.LookAsync(doors, [Running(inbox)], CancellationToken.None);
        await reach.LookAsync(doors, [Running(inbox)], CancellationToken.None);

        Assert.Equal(["late", "equal"], sent.Select(message => message.Channel!.Id));
        Assert.Equal(2, doors.Reads.Count);
    }

    /// <summary>
    /// 🔴 Trap 9: an ordinary message waiting while a later urgent one is taken keeps its place: each is its own row, the urgent one goes
    /// at the next step past it, and the ordinary one still rides the turn's end.
    /// </summary>
    [Fact]
    public async Task An_ordinary_message_behind_a_later_urgent_one_keeps_its_place()
    {
        var doors = new Doors(
            Message("m1", 1, "When you can.", session: "s9", urgent: false),
            Message("m2", 2, "Now, please.", session: "s9", urgent: true));
        var (inbox, sent) = NextStep();
        var reach = new ChannelReach();

        await reach.LookAsync(doors, [Running(inbox)], CancellationToken.None);
        await reach.LookAsync(doors, [Running(inbox)], CancellationToken.None);
        Assert.True(inbox.Hold(new ChatMessage("Finish the report.", []) { Level = MessageLevel.Ordinary }));

        Assert.Equal(["m2"], sent.Select(message => message.Channel!.Id));
        Assert.Equal("m1", AtTurnEnd(inbox)!.Channel!.Id);
        Assert.Equal("Finish the report.", AtTurnEnd(inbox)!.Text);
    }

    /// <summary>
    /// 🔴 Trap 21: two live parts of one entry, tried newest first: the first whose door takes the message claims it, and the other takes
    /// nothing.
    /// </summary>
    [Fact]
    public async Task Of_two_live_parts_of_one_entry_one_claims_a_message_and_the_other_takes_nothing()
    {
        var doors = new Doors(Message("m1", 1, "The export endpoint is /v3/report.", session: "s9", urgent: true));
        var (newer, sentNewer) = NextStep();
        var (older, sentOlder) = NextStep();

        await new ChannelReach().LookAsync(doors, [Running(newer, "s2"), Running(older, "s1")], CancellationToken.None);

        Assert.Single(sentNewer);
        Assert.Empty(sentOlder);
        Assert.Equal([("s2", ChannelAcks.Taking, "m1")], doors.Acknowledged);
    }

    /// <summary>
    /// A claim held for the turn's end that the agent read itself with <c>channel_read</c> leaves the inbox at the next look: never
    /// handed twice.
    /// </summary>
    [Fact]
    public async Task A_held_claim_the_agent_read_itself_leaves_the_inbox()
    {
        var doors = new Doors(Message("m1", 1, "When you can.", session: "s9", urgent: false));
        var inbox = TurnEnd();
        var reach = new ChannelReach();
        await reach.LookAsync(doors, [Running(inbox)], CancellationToken.None);
        Assert.True(inbox.Hold(new ChatMessage("Finish the report.", []) { Level = MessageLevel.Ordinary }));

        lock (doors.Acks) doors.Acks["m1"] = (ChannelAcks.Read, "s1");
        await reach.LookAsync(doors, [Running(inbox)], CancellationToken.None);

        Assert.Equal("Finish the report.", AtTurnEnd(inbox)!.Text);
        Assert.Null(AtTurnEnd(inbox));
    }

    /// <summary>A message to another repository, and one a part of this entry posted, are not this entry's to be handed.</summary>
    [Fact]
    public async Task A_message_to_another_repository_or_from_the_entrys_own_part_reaches_nothing()
    {
        var doors = new Doors(
            Message("m1", 1, "For the API only.", to: "console-api", urgent: true),
            Message("m2", 2, "I wrote this myself.", session: "s1", entry: Key, urgent: true));
        var (inbox, sent) = NextStep();

        await new ChannelReach().LookAsync(doors, [Running(inbox)], CancellationToken.None);

        Assert.Empty(sent);
        Assert.Empty(doors.Acknowledged);
    }

    // ——— A part that is not running.

    /// <summary>
    /// 🔴 D155: another session's message to an entry whose part has ended starts nothing, reopens nothing and says nothing to its
    /// record: it waits for a run that comes for its own reasons, its next part handed it in the instruction.
    /// </summary>
    [Fact]
    public async Task Another_sessions_message_to_an_ended_entry_starts_nothing()
    {
        var doors = new Doors(Message("m1", 1, "The export endpoint is /v3/report.", to: "console-ui", session: "s9", urgent: true));

        var lines = await new ChannelReach().LookAsync(doors, [Ended()], CancellationToken.None);

        Assert.Empty(doors.Said);
        Assert.Empty(doors.Acknowledged);
        Assert.Empty(lines);
    }

    /// <summary>
    /// The person's message to the repository of a parked or ended part reopens it, as their words to it do (D137 §3): kept on its
    /// record by the say door, which the planner then goes on with, once; the entry acknowledges it sent, to that record's queue.
    /// </summary>
    [Fact]
    public async Task The_persons_message_to_an_ended_parts_repository_reopens_it_once()
    {
        var doors = new Doors(Message("p1", 2, "Also log the port.", to: "console-ui", urgent: true));
        var reach = new ChannelReach();

        var lines = await reach.LookAsync(doors, [Ended()], CancellationToken.None);
        await reach.LookAsync(doors, [Ended()], CancellationToken.None);

        // SESSONE2e: the message's id goes with the words, so the service keeps no second ask word for them.
        Assert.Equal([("s1", "Also log the port.", "p1")], doors.Said);
        Assert.Equal([ChannelAcks.Sent], doors.States("p1"));
        Assert.Contains(lines, line => line.Contains("`p1`", StringComparison.Ordinal) && line.Contains("session s1", StringComparison.Ordinal));
    }

    /// <summary>
    /// The person's message to everyone reopens no part: it is held for each entry's next part, and the ask keeps it (design §2.3's
    /// third row). A refused reopen is not asked again at every look.
    /// </summary>
    [Fact]
    public async Task The_persons_message_to_everyone_reopens_nothing_and_a_refused_reopen_is_not_asked_again()
    {
        var everyone = new Doors(Message("p1", 2, "Stop at the design for now."));
        await new ChannelReach().LookAsync(everyone, [Ended()], CancellationToken.None);
        Assert.Empty(everyone.Said);

        var refused = new Doors(Message("p2", 3, "Also log the port.", to: "console-ui")) { Refuse = "stood-down" };
        var reach = new ChannelReach();
        await reach.LookAsync(refused, [Ended()], CancellationToken.None);
        await reach.LookAsync(refused, [Ended()], CancellationToken.None);
        Assert.Single(refused.Said);
        Assert.Empty(refused.Acknowledged);
    }

    // ——— What a starting part is handed.

    /// <summary>
    /// 🔴 Trap 22: a start's packet acknowledges what it carried whole as taken, once the part works, and what it cut or left out as
    /// partly, which a new part is handed again; it hands a message another part left partly, and not one already taken.
    /// </summary>
    [Fact]
    public async Task A_starts_packet_acknowledges_whole_messages_taken_and_cut_or_left_out_ones_partly()
    {
        var items = new List<ChannelItemView> { Message("taken", 0, "Had already.", session: "s9", urgent: false) };
        items.AddRange(Enumerable.Range(1, ChannelText.HandedNewest + 1).Select(n => Message($"m{n}", n, $"note {n}", session: "s9", urgent: false)));
        items.Add(Message("long", 40, new string('x', ChannelText.MessageLimit + 10), session: "s9", urgent: false));
        var doors = new Doors([.. items]);
        lock (doors.Acks)
        {
            doors.Acks["taken"] = (ChannelAcks.Taken, "s0");
            doors.Acks["m1"] = (ChannelAcks.Partly, "s0");
        }

        var held = await ChannelReach.HandAsync(doors, Entry, "s3", CancellationToken.None);
        await ChannelReach.HandedAsync(doors, held, CancellationToken.None);

        Assert.DoesNotContain(held!.Messages, message => message.Id == "taken");
        Assert.Contains(held.Messages, message => message.Id == "m1");
        var (shown, left) = ChannelText.Bounded(held.Messages);
        Assert.NotEmpty(left);
        Assert.All(left, message => Assert.Equal(ChannelAcks.Partly, doors.Acks[message.Id!].State));
        Assert.Equal(ChannelAcks.Partly, doors.Acks["long"].State);
        Assert.All(shown.Where(message => message.Id != "long"), message => Assert.Equal((ChannelAcks.Taken, "s3"), doors.Acks[message.Id!]));
    }

    // ——— Which parts a look reads.

    private static PriorSession Record(string session, string state, int minute, string entry = Key) =>
        new(session, null, state) { Entry = entry, Created = At.AddMinutes(minute), Quest = "q" + session };

    /// <summary>
    /// A look reads each line of work on an ask: every part of it this loop runs, newest first (one part claims each message), or else
    /// its newest part here, parked or ended while its ask still has a quest open. A line of work on no ask, a teammate's, an intake, a
    /// part another loop runs, and a line whose ask has no open quest are not read.
    /// </summary>
    [Fact]
    public void A_look_reads_each_line_of_work_on_an_ask_by_its_running_parts_newest_first_or_its_newest_part_here()
    {
        var processes = new SessionProcesses();
        var inbox = processes.OpenInbox("s3");
        var alongside = processes.OpenInbox("s9");
        var snapshot = new Snapshot([new QuestView("q9", $"ask #{Ask}", "console-ui", "Next", "", "Open")], [], [])
        {
            LastRun = new Dictionary<string, PriorSession>
            {
                ["q1"] = Record("s1", "completed", 1),
                ["q2"] = Record("s2", "completed", 2, "ask:a1b2c3/console-api"),
                ["q3"] = Record("s3", "working", 3, "ask:a1b2c3/reports"),
                ["q9"] = Record("s9", "working", 9, "ask:a1b2c3/reports"),
                ["q4"] = Record("s4", "working", 4, "ask:a1b2c3/docs"),
                ["q5"] = Record("s5", "completed", 5, "session:s5/console-ui"),
                ["q6"] = Record("s6", "working", 6, "ask:a1b2c3/intake"),
                ["q7"] = Record("origin/s7", "completed", 7, "ask:a1b2c3/web"),
                ["q8"] = Record("s8", "completed", 8, "ask:ffff/console-ui"),
            },
        };

        var parts = ChannelReach.Parts(snapshot, processes);

        Assert.Equal(
            [("ask:a1b2c3/console-api", "s2", false), ("ask:a1b2c3/console-ui", "s1", false), ("ask:a1b2c3/reports", "s9", true), ("ask:a1b2c3/reports", "s3", true)],
            parts.Select(part => (part.Entry.Key, part.Session, part.Inbox is not null)).OrderBy(part => part.Key, StringComparer.Ordinal));
        Assert.Same(inbox, parts.Single(part => part.Session == "s3").Inbox);
        Assert.Same(alongside, parts.Single(part => part.Session == "s9").Inbox);
    }

    // ——— Over the service's own doors.

    /// <summary>
    /// 🔴 SESSONE2a's note: the person's message reaching a running part is kept on the ask once, by the channel. The look reads the
    /// channel and claims the message through the service's acknowledgment door, by its id, and never posts what the person added
    /// (<c>/added</c>) nor moves a mark by moment.
    /// </summary>
    [Fact]
    public async Task Over_the_services_doors_the_persons_message_is_claimed_by_its_id_and_never_reported_as_added()
    {
        var channel = $$"""
            {"ask":"{{Ask}}","entry":"{{Key}}","items":[
             {"kind":"line","at":"2026-10-14T08:59:00Z","code":"published","quest":"q1","name":"Notes","from":"ask #{{Ask}}","to":"console-ui"},
             {"kind":"message","at":"2026-10-14T09:02:00Z","id":"p1","person":true,"text":"Use the older bridge.","urgent":true}],
             "unread":2}
            """;
        var standIn = new StandIn(request => request.RequestUri!.AbsolutePath switch
        {
            $"/api/asks/{Ask}/channel" => (HttpStatusCode.OK, channel),
            $"/api/asks/{Ask}/channel/ack" => (HttpStatusCode.OK, """{"acknowledged":["p1"]}"""),
            _ => null,
        });
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));
        var inbox = TurnEnd();

        await new ChannelReach().LookAsync(new ServiceChannelDoors(service), [Running(inbox)], CancellationToken.None);

        Assert.Equal("Use the older bridge.", Assert.Single(inbox.State.Queued).Text);
        Assert.Equal([$"GET /api/asks/{Ask}/channel?entry=ask%3Aa1b2c3%2Fconsole-ui", $"POST /api/asks/{Ask}/channel/ack"], standIn.Requests);
        Assert.Contains("\"state\":\"taking\"", standIn.Bodies[^1], StringComparison.Ordinal);
        Assert.Contains("\"session\":\"s1\"", standIn.Bodies[^1], StringComparison.Ordinal);
        Assert.Contains("\"messages\":[\"p1\"]", standIn.Bodies[^1], StringComparison.Ordinal);
        Assert.DoesNotContain(standIn.Requests, request => request.Contains("/added", StringComparison.Ordinal) || request.EndsWith("/read", StringComparison.Ordinal));
    }

    /// <summary>A service standing in: each request recorded with its query, then answered, or 404 where the answer is null.</summary>
    private sealed class StandIn(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)?> answer) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add($"{request.Method} {request.RequestUri!.PathAndQuery}");
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
            var (status, body) = answer(request) ?? (HttpStatusCode.NotFound, """{"error":"none"}""");
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
