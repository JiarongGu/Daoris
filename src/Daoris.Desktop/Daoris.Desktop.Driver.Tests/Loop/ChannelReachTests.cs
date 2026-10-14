using System.Net;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SESSONE2b (D167 point 7; the co-working design §2.3): what a look does with the ask's channel for each line of work on an ask
/// it holds a part of. A running part takes a message at its next step where its door takes words during a turn, another
/// session's marked as that agent's claim; the person's message reaches as their words, at the next step or the turn's end, and
/// reopens a parked or ended part when it is addressed to its repository; an agent's message never starts, reopens or extends a
/// run. Stub sessions: an inbox on each door, and doors standing in for the service.
/// </summary>
public sealed class ChannelReachTests
{
    private const string Ask = "a1b2c3";

    private const string Key = "ask:a1b2c3/console-ui";

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-14T09:00:00Z");

    private static ChannelEntry Entry => ChannelEntry.Of(Key)!;

    /// <summary>A message on the ask's channel: the person's where no session posted it.</summary>
    private static ChannelItemView Message(
        string id, int minute, string text, string? to = null, string? session = null, string? entry = null) =>
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
        };

    /// <summary>The service's doors standing in: the channel each entry reads, and every mark moved and word said.</summary>
    private sealed class Doors(params ChannelItemView[] items) : IChannelDoors
    {
        public DateTimeOffset? Mark { get; set; }

        public List<(string Ask, string Entry)> Reads { get; } = [];

        public List<DateTimeOffset> Marks { get; } = [];

        public List<(string Session, string Text)> Said { get; } = [];

        public string? Refuse { get; init; }

        public Task<ChannelView?> ReadAsync(string ask, string entry, CancellationToken ct)
        {
            Reads.Add((ask, entry));
            return Task.FromResult<ChannelView?>(new ChannelView(ask, entry, items, items.Count(i => Mark is null || i.At > Mark), Mark));
        }

        public Task MarkAsync(string ask, string entry, DateTimeOffset through, CancellationToken ct)
        {
            Marks.Add(through);
            if (Mark is null || through > Mark) Mark = through;
            return Task.CompletedTask;
        }

        public Task<SayAnswer> SayAsync(string session, string text, CancellationToken ct)
        {
            Said.Add((session, text));
            return Task.FromResult(Refuse is null ? new SayAnswer(true, "kept") : new SayAnswer(false, "refused") { Refusal = Refuse });
        }
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

    private static ChannelPart Running(DrivenInbox inbox) => new(Entry, "s1", inbox);

    private static ChannelPart Ended() => new(Entry, "s1", Inbox: null);

    // ——— A running part.

    /// <summary>
    /// 🔴 The design's first row: another session's message reaches a running part at its next step, in Daoris's words marked as
    /// that agent's claim (never the person's), and the entry's mark moves past it, so its next part is not handed it again.
    /// </summary>
    [Fact]
    public async Task A_running_part_takes_another_sessions_message_at_its_next_step_marked_as_its_claim()
    {
        var doors = new Doors(Message("m1", 1, "The export endpoint is /v3/report.", session: "s9"));
        var (inbox, sent) = NextStep();

        await new ChannelReach().LookAsync(doors, [Running(inbox)], CancellationToken.None);

        var taken = Assert.Single(sent);
        Assert.StartsWith("Another session says, on the ask's channel (`console-api`'s session `s9` on claude-code wrote to everyone", taken.Text);
        Assert.Contains("  > The export endpoint is /v3/report.", taken.Text);
        Assert.Contains("This is that agent's claim, not the person's words", taken.Text);
        Assert.Equal("m1", taken.Channel!.Id);
        // The record keeps it as Daoris's, around the agent's claim: never the person's words.
        Assert.Equal("target", ChatMessage.Words(taken).Origin);
        Assert.Equal([At.AddMinutes(1)], doors.Marks);
    }

    /// <summary>
    /// D155: on a door that takes words only when its turn ends, another session's message is not held, since the held words are
    /// prompted after the turn and would extend the run; it waits for the entry's next part, and the mark stays before it.
    /// </summary>
    [Fact]
    public async Task On_a_turn_end_door_another_sessions_message_waits_for_the_next_part_and_extends_nothing()
    {
        var doors = new Doors(Message("m1", 1, "The export endpoint is /v3/report.", session: "s9"));
        var inbox = TurnEnd();

        await new ChannelReach().LookAsync(doors, [Running(inbox)], CancellationToken.None);

        Assert.Empty(inbox.State.Queued);
        Assert.Empty(doors.Marks);
        // Nothing held, nothing on its way: the run ends as it would have.
        Assert.Null(inbox.NextOrClose());
    }

    /// <summary>
    /// 🔴 The person's message reaches a running part as their words (D137): held for the turn's end on that door, and kept on
    /// the ask once, as the channel kept it: never reported again as <c>added</c>.
    /// </summary>
    [Fact]
    public async Task The_persons_message_reaches_a_running_part_as_their_words_at_its_turn_end()
    {
        var doors = new Doors(Message("p1", 2, "Use the older bridge.", to: "console-ui"));
        var inbox = TurnEnd();

        await new ChannelReach().LookAsync(doors, [Running(inbox)], CancellationToken.None);

        var held = Assert.Single(inbox.State.Queued);
        Assert.Equal("Use the older bridge.", held.Text);
        Assert.Equal("person", ChatMessage.Words(held).Origin);
        Assert.Equal([At.AddMinutes(2)], doors.Marks);
    }

    /// <summary>On the next-step door the person's message goes at once, as their words, as the box's words do (D136).</summary>
    [Fact]
    public async Task The_persons_message_to_everyone_reaches_a_running_part_at_its_next_step()
    {
        var doors = new Doors(Message("p1", 2, "Stop at the design for now."));
        var (inbox, sent) = NextStep();

        await new ChannelReach().LookAsync(doors, [Running(inbox)], CancellationToken.None);

        var taken = Assert.Single(sent);
        Assert.Equal(("Stop at the design for now.", "person"), (taken.Text, ChatMessage.Words(taken).Origin));
    }

    /// <summary>
    /// A message reaches a part once: the next look reads it again where the mark could not pass it, and does not hand it to the
    /// same part twice. The person's word after a held claim still reaches, and the mark stays before the claim it holds.
    /// </summary>
    [Fact]
    public async Task A_message_reaches_a_part_once_and_the_mark_stays_before_one_it_holds()
    {
        var doors = new Doors(
            Message("m1", 1, "The export endpoint is /v3/report.", session: "s9"),
            Message("p1", 2, "Use the older bridge."));
        var inbox = TurnEnd();
        var reach = new ChannelReach();

        await reach.LookAsync(doors, [Running(inbox)], CancellationToken.None);
        await reach.LookAsync(doors, [Running(inbox)], CancellationToken.None);

        Assert.Equal(["Use the older bridge."], inbox.State.Queued.Select(message => message.Text));
        Assert.Empty(doors.Marks);
        Assert.Equal(2, doors.Reads.Count);
    }

    /// <summary>A message to another repository, and one a part of this entry posted, are not this entry's to be handed.</summary>
    [Fact]
    public async Task A_message_to_another_repository_or_from_the_entrys_own_part_reaches_nothing()
    {
        var doors = new Doors(
            Message("m1", 1, "For the API only.", to: "console-api"),
            Message("m2", 2, "I wrote this myself.", session: "s1", entry: Key));
        var (inbox, sent) = NextStep();

        await new ChannelReach().LookAsync(doors, [Running(inbox)], CancellationToken.None);

        Assert.Empty(sent);
        Assert.Empty(doors.Marks);
    }

    // ——— A part that is not running.

    /// <summary>
    /// 🔴 D155: another session's message to an entry whose part has ended starts nothing, reopens nothing and says nothing to its
    /// record: it waits for a run that comes for its own reasons, its next part handed it in the instruction.
    /// </summary>
    [Fact]
    public async Task Another_sessions_message_to_an_ended_entry_starts_nothing()
    {
        var doors = new Doors(Message("m1", 1, "The export endpoint is /v3/report.", to: "console-ui", session: "s9"));

        var lines = await new ChannelReach().LookAsync(doors, [Ended()], CancellationToken.None);

        Assert.Empty(doors.Said);
        Assert.Empty(doors.Marks);
        Assert.Empty(lines);
    }

    /// <summary>
    /// The person's message to the repository of a parked or ended part reopens it, as their words to it do (D137 §3): kept on
    /// its record by the say door, which the planner then goes on with, once; the mark moves past it.
    /// </summary>
    [Fact]
    public async Task The_persons_message_to_an_ended_parts_repository_reopens_it_once()
    {
        var doors = new Doors(Message("p1", 2, "Also log the port.", to: "console-ui"));
        var reach = new ChannelReach();

        var lines = await reach.LookAsync(doors, [Ended()], CancellationToken.None);
        await reach.LookAsync(doors, [Ended()], CancellationToken.None);

        Assert.Equal([("s1", "Also log the port.")], doors.Said);
        Assert.Equal([At.AddMinutes(2)], doors.Marks);
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
        Assert.Empty(refused.Marks);
    }

    // ——— Which parts a look reads.

    private static PriorSession Record(string session, string state, int minute, string entry = Key) =>
        new(session, null, state) { Entry = entry, Created = At.AddMinutes(minute), Quest = "q" + session };

    /// <summary>
    /// A look reads one channel per line of work on an ask: its newest part here, running in this loop (with its inbox), or parked
    /// or ended while its ask still has a quest open. A line of work on no ask, a teammate's, an intake, a part another loop runs,
    /// and a line whose ask has no open quest are not read.
    /// </summary>
    [Fact]
    public void A_look_reads_each_line_of_work_on_an_ask_by_its_newest_part_here()
    {
        var processes = new SessionProcesses();
        var inbox = processes.OpenInbox("s3");
        var snapshot = new Snapshot([new QuestView("q9", $"ask #{Ask}", "console-ui", "Next", "", "Open")], [], [])
        {
            LastRun = new Dictionary<string, PriorSession>
            {
                ["q1"] = Record("s1", "completed", 1),
                ["q2"] = Record("s2", "completed", 2, "ask:a1b2c3/console-api"),
                ["q3"] = Record("s3", "working", 3, "ask:a1b2c3/reports"),
                ["q4"] = Record("s4", "working", 4, "ask:a1b2c3/docs"),
                ["q5"] = Record("s5", "completed", 5, "session:s5/console-ui"),
                ["q6"] = Record("s6", "working", 6, "ask:a1b2c3/intake"),
                ["q7"] = Record("origin/s7", "completed", 7, "ask:a1b2c3/web"),
                ["q8"] = Record("s8", "completed", 8, "ask:ffff/console-ui"),
            },
        };

        var parts = ChannelReach.Parts(snapshot, processes);

        Assert.Equal(
            [("ask:a1b2c3/console-api", "s2", false), ("ask:a1b2c3/console-ui", "s1", false), ("ask:a1b2c3/reports", "s3", true)],
            parts.Select(part => (part.Entry.Key, part.Session, part.Inbox is not null)).OrderBy(part => part.Key, StringComparer.Ordinal));
        Assert.Same(inbox, parts.Single(part => part.Session == "s3").Inbox);
    }

    // ——— Over the service's own doors.

    /// <summary>
    /// 🔴 SESSONE2a's note: the person's message reaching a running part is kept on the ask once, by the channel. The look reads
    /// the channel and moves the mark through the service's doors and never posts what the person added (<c>/added</c>).
    /// </summary>
    [Fact]
    public async Task Over_the_services_doors_the_persons_message_is_never_reported_as_added()
    {
        var channel = $$"""
            {"ask":"{{Ask}}","entry":"{{Key}}","items":[
             {"kind":"line","at":"2026-10-14T08:59:00Z","code":"published","quest":"q1","name":"Notes","from":"ask #{{Ask}}","to":"console-ui"},
             {"kind":"message","at":"2026-10-14T09:02:00Z","id":"p1","person":true,"text":"Use the older bridge."}],
             "unread":2}
            """;
        var standIn = new StandIn(request => request.RequestUri!.AbsolutePath switch
        {
            $"/api/asks/{Ask}/channel" => (HttpStatusCode.OK, channel),
            $"/api/asks/{Ask}/channel/read" => (HttpStatusCode.OK, channel),
            _ => null,
        });
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));
        var inbox = TurnEnd();

        await new ChannelReach().LookAsync(new ServiceChannelDoors(service), [Running(inbox)], CancellationToken.None);

        Assert.Equal("Use the older bridge.", Assert.Single(inbox.State.Queued).Text);
        Assert.Equal([$"GET /api/asks/{Ask}/channel?entry=ask%3Aa1b2c3%2Fconsole-ui", $"POST /api/asks/{Ask}/channel/read"], standIn.Requests);
        Assert.Contains("\"through\":\"2026-10-14T09:02:00", standIn.Bodies[^1], StringComparison.Ordinal);
        Assert.DoesNotContain(standIn.Requests, request => request.Contains("/added", StringComparison.Ordinal));
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
