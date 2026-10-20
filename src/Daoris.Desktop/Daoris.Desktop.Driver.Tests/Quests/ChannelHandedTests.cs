using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SESSONE2b (D167 point 7; the co-working design §2.3, §2.6): what a part starting on a line of work on an ask is handed from the
/// ask's channel. The messages for its entry it has not acknowledged (COWORK1d), the newest within the bounds and the older counted,
/// the person's as their words and another session's as that agent's claim; each is acknowledged once the part has started. The
/// golden of the instruction is <see cref="TargetPromptGoldenTests"/>'s <c>channel</c>.
/// </summary>
public sealed class ChannelHandedTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-14T09:00:00Z");

    private const string Key = "ask:a1b2c3/console-ui";

    private static ChannelItemView Message(string id, int minute, string text, bool person = true) =>
        new(ChannelItemView.Message, At.AddMinutes(minute))
        {
            Id = id, Person = person, Text = text, Session = person ? null : "s9", Repository = person ? null : "console-api",
        };

    private static ChannelHeld Held(params ChannelItemView[] messages) => new("a1b2c3", Key, messages, "s3");

    // ——— The bounds.

    /// <summary>The newest twenty are handed (SESSONE2a's <c>ChannelBounds.HandedNewest</c>), and the older counted in one line.</summary>
    [Fact]
    public void A_next_part_is_handed_the_newest_twenty_and_told_how_many_older_were_left_out()
    {
        var text = ChannelText.Beneath(Held([.. Enumerable.Range(0, 21).Select(i => Message($"m{i}", i, $"word {i}"))]));

        Assert.Contains("- … 1 older message, written from 2026-10-14 09:00 UTC to 2026-10-14 09:00 UTC, is left out here", text);
        Assert.DoesNotContain("  > word 0\n", text);
        Assert.Contains("  > word 1\n", text);
        Assert.Contains("  > word 20\n", text);
        Assert.Equal(20, text.Split("\n- The person wrote").Length - 1);
    }

    /// <summary>
    /// Within the instruction's share (the native door's one argument): each message cut at 1,500 characters, said to be, and the
    /// newest that fit in 6,000 kept.
    /// </summary>
    [Fact]
    public void Long_messages_are_cut_and_the_newest_that_fit_are_kept()
    {
        var text = ChannelText.Beneath(Held([.. Enumerable.Range(0, 5).Select(i => Message($"m{i}", i, new string((char)('a' + i), 2_000)))]));

        Assert.Contains("- … 1 older message", text);
        Assert.DoesNotContain(new string('a', 100), text);
        Assert.Contains(new string('e', 1_500), text);
        Assert.DoesNotContain(new string('e', 1_501), text);
        Assert.Contains("  (… and 500 more characters of this one, left out here; `channel_read` shows it whole.)\n", text);
    }

    /// <summary>Nothing held, nothing said: the instruction reads as it did.</summary>
    [Fact]
    public void Nothing_held_writes_nothing()
    {
        Assert.Equal("", ChannelText.Beneath(null));
        Assert.Equal("", ChannelText.Beneath(Held()));
    }

    // ——— The wire and the entry.

    /// <summary>
    /// The channel as SESSONE2a answers it, an item without its kind or a moment passed over, a message without words none; COWORK1d's
    /// level and the entry's acknowledgment read beside each message, and an entry handed what it has not had, whatever the mark.
    /// </summary>
    [Fact]
    public void The_channel_is_read_as_the_service_answers_it()
    {
        var read = ChannelView.Read("""
            {"ask":"a1b2c3","entry":"ask:a1b2c3/console-ui","unread":3,"mark":"2026-10-14T09:00:00+00:00","items":[
             {"kind":"line","at":"2026-10-14T08:59:00+00:00","code":"done","quest":"q1","name":"Notes","from":"ask #a1b2c3","to":"console-ui"},
             {"kind":"message","at":"2026-10-14T09:01:00+00:00","id":"m1","person":false,"session":"s9","repository":"console-api",
              "agent":"claude-code","entry":"ask:a1b2c3/console-api","to":"console-ui","re":"p0","question":true,"open":true,"text":"Null or empty?",
              "urgent":true,"ack":"partly","ackSession":"s0"},
             {"kind":"message","at":"2026-10-14T09:02:00+00:00","id":"p1","person":true,"text":"Use the older bridge.","urgent":false},
             {"kind":"message","at":"2026-10-14T08:00:00+00:00","id":"p0","person":true,"text":"Earlier, and taken.","ack":"taken","ackSession":"s0"},
             {"kind":"message","at":"2026-10-14T09:03:00+00:00","id":"p2","person":true},
             {"at":"2026-10-14T09:04:00+00:00","id":"x"},
             {"kind":"message","id":"y","text":"no moment"}]}
            """)!;

        Assert.Equal(("a1b2c3", Key, 3, At), (read.Ask, read.Entry, read.Unread, read.Mark!.Value));
        Assert.Equal(["line", "message", "message", "message", "message"], read.Items.Select(item => item.Kind));
        var claim = read.Items[1];
        Assert.Equal(("s9", "console-api", "claude-code", "console-ui", "p0", true, false), (claim.Session, claim.Repository, claim.Agent, claim.To, claim.Re, claim.Question, claim.Person));
        Assert.Equal((true, "partly", "s0"), (claim.Urgent, claim.Ack, claim.AckSession));
        Assert.Equal((false, (bool?)null), (read.Items[2].Urgent, read.Items[4].Urgent));
        // Handed partly to another part, a new part is handed it again; never the part that had it, nor one already taken.
        Assert.Equal(["m1", "p1"], read.ForEntry("s3").Select(message => message.Id));
        Assert.Equal(["p1"], read.ForEntry("s0").Select(message => message.Id));
        Assert.Equal(At.AddMinutes(3), read.Newest);
        Assert.Null(ChannelView.Read("""{"error":"none"}"""));
    }

    /// <summary>
    /// The key a line of work on an ask carries (D167 point 2): an ask's origin and a repository. An intake's, a conversation's and a
    /// quest's own origin are on no channel.
    /// </summary>
    [Theory]
    [InlineData("ask:a1b2c3/console-ui", "a1b2c3", "console-ui")]
    [InlineData("ask:a1b2c3/intake", null, null)]
    [InlineData("session:s1/console-ui", null, null)]
    [InlineData("quest:q1/console-ui", null, null)]
    [InlineData("ask:a1b2c3", null, null)]
    [InlineData("ask:/console-ui", null, null)]
    public void An_entry_on_an_ask_is_read_from_its_key(string key, string? ask, string? repository)
    {
        var entry = ChannelEntry.Of(key);

        Assert.Equal((ask, repository), (entry?.Ask, entry?.Repository));
    }

    /// <summary>
    /// A quest's line of work: an ask's quest by its sender (the service's first rule), a sibling's request by the origin its
    /// publisher's record holds; a repository's request on no ask is on no channel.
    /// </summary>
    [Fact]
    public void A_quests_line_of_work_is_its_askers_or_its_publishers()
    {
        var snapshot = new Snapshot([], [], [])
        {
            Entries = new Dictionary<string, string> { ["s9"] = "ask:a1b2c3/console-api", ["s8"] = "session:s8/console-api" },
        };
        var asked = new QuestView("q1", "ask #a1b2c3", "console-ui", "t", "b", "Open");
        var sibling = new QuestView("q2", "console-api", "console-ui", "t", "b", "Open") { PublishedBy = "s9" };
        var chat = new QuestView("q3", "console-api", "console-ui", "t", "b", "Open") { PublishedBy = "s8" };

        Assert.Equal(Key, ChannelEntry.ForQuest(asked, snapshot)!.Key);
        Assert.Equal(Key, ChannelEntry.ForQuest(sibling, snapshot)!.Key);
        Assert.Null(ChannelEntry.ForQuest(chat, snapshot));
    }

    // ——— Handed at the start, and acknowledged.

    /// <summary>The service's doors standing in: one channel, and the acknowledgments written.</summary>
    private sealed class Doors(ChannelView? read, bool fails = false) : IChannelDoors
    {
        public List<(string Entry, string Session, string State, string Message)> Acks { get; } = [];

        public Task<ChannelView?> ReadAsync(string ask, string entry, CancellationToken ct) =>
            fails ? throw new HttpRequestException("refused") : Task.FromResult(read);

        public Task<IReadOnlyList<string>> AcknowledgeAsync(
            string ask, string entry, string session, string state, IReadOnlyList<string> messages, CancellationToken ct)
        {
            Acks.AddRange(messages.Select(message => (entry, session, state, message)));
            return Task.FromResult(messages);
        }

        public Task<SayAnswer> SayAsync(string session, string text, string channel, CancellationToken ct) => throw new InvalidOperationException("no say");
    }

    /// <summary>
    /// 🔴 A starting part is handed the messages for its entry it has not had, and acknowledges each, once the part has started, by its
    /// own session (COWORK1d, the message-urgency design §6): so the next part is not handed them again, and none passes by its moment.
    /// </summary>
    [Fact]
    public async Task A_starting_part_is_handed_what_waited_and_acknowledges_it_once_it_started()
    {
        var line = new ChannelItemView(ChannelItemView.Line, At.AddMinutes(5)) { Code = "done", Quest = "q1" };
        var doors = new Doors(new ChannelView("a1b2c3", Key, [Message("p1", 1, "Use the older bridge."), Message("m1", 2, "Null?", person: false), line], 3, null));

        var held = await ChannelReach.HandAsync(doors, ChannelEntry.Of(Key)!, "s3", CancellationToken.None);
        Assert.Empty(doors.Acks);
        await ChannelReach.HandedAsync(doors, held, CancellationToken.None);

        Assert.Equal(["p1", "m1"], held!.Messages.Select(message => message.Id));
        Assert.Equal([(Key, "s3", ChannelAcks.Taken, "p1"), (Key, "s3", ChannelAcks.Taken, "m1")], doors.Acks);
    }

    /// <summary>A channel that could not be read hands nothing and holds nothing: the start goes on, and nothing is acknowledged.</summary>
    [Fact]
    public async Task A_channel_that_could_not_be_read_hands_nothing_and_acknowledges_nothing()
    {
        var doors = new Doors(null, fails: true);

        var held = await ChannelReach.HandAsync(doors, ChannelEntry.Of(Key)!, "s3", CancellationToken.None);
        await ChannelReach.HandedAsync(doors, held, CancellationToken.None);

        Assert.Null(held);
        Assert.Empty(doors.Acks);
    }
}
