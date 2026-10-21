using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// NOTIFY2g's <i>Messages</i> (D196 points 6 and 7; the notification design §2.4, §2.5, §5): the <c>notices</c> slice's <c>messages</c>
/// partition, one entry per ask with the service's own unread count and the newest message an agent or a teammate wrote, and each
/// urgent message to everyone said once as it arrives, past a watermark per ask whose first judgement is a baseline.
/// </summary>
/// <remarks>The channels are a stand-in for the service's reads, on a held clock; the record is the bridge's home.</remarks>
public sealed class AskMessagesTests : DriverModuleBridge
{
    private static readonly DateTimeOffset At = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    private readonly HeldClock _clock = new();
    private readonly Dictionary<string, AskChannel> _channels = new(StringComparer.Ordinal);
    private bool _notify = true;
    private int _reads;
    private Exception? _failing;

    private NoticesStateSlice Notices() => new(Home, Bus, clock: _clock, notify: () => _notify, channels: _ =>
    {
        _reads++;
        if (_failing is { } error) throw error;
        return Task.FromResult<IReadOnlyList<AskChannel>?>([.. _channels.Values]);
    });

    private static ChannelItemView Message(
        string id, int minutes, string text, bool? urgent = false, string? to = null, string? repository = "Engine", bool person = false,
        string? origin = null) =>
        new(ChannelItemView.Message, At.AddMinutes(minutes))
        {
            Id = id, Text = text, Urgent = urgent, To = to, Person = person, Origin = origin,
            Session = person ? null : $"s-{id}", Repository = person ? null : repository,
        };

    private static ChannelItemView Line(int minutes) => new(ChannelItemView.Line, At.AddMinutes(minutes)) { Code = "taken", Quest = "q1" };

    private void Channel(string ask, int unread, params ChannelItemView[] items) => Channel(ask, unread, false, items);

    private void Channel(string ask, int unread, bool teammate, params ChannelItemView[] items) =>
        _channels[ask] = new(ask, "games", teammate, new ChannelView(ask, "person", items, unread, null));

    private IReadOnlyList<JsonObject> Arrivals() =>
    [
        .. Raised.Where(each => each.Type == AskMessages.ArrivedType).Select(each => (JsonObject)each.Payload!),
    ];

    private static JsonObject Written(object? data) => JsonSerializer.SerializeToNode(data, IpcJson.Options)!.AsObject();

    private static JsonObject Messages(NoticesStateSlice notices) =>
        Written(notices.Slice.Snapshot().Partitions.Single(each => each.Partition == NoticesStateSlice.Messages).Data);

    /// <summary>
    /// §2.5: one entry per ask, a teammate's among them, with the service's own unread and the newest message an agent or a teammate
    /// wrote, its first line and who; the person's own posts and Daoris's lines are never newest; an ask with none has no newest.
    /// </summary>
    [Fact]
    public async Task Each_ask_is_an_entry_with_the_service_s_count_and_the_newest_of_theirs()
    {
        Channel("a1", 2, Message("m1", 1, "First line\nsecond line", repository: "Engine"), Message("m2", 3, "my words", person: true), Line(4));
        Channel("a2", 0, Message("m3", 2, "only mine", person: true), Line(3));
        Channel("t1", 1, true, Message("m4", 5, "  \n  from a teammate  ", urgent: true, person: true, origin: "their-machine"));
        var notices = Notices();

        await notices.Slice.JudgeAsync();

        Assert.Equal([NoticesStateSlice.Messages, NoticesStateSlice.News], notices.Slice.Snapshot().Partitions.Select(each => each.Partition));
        var messages = Messages(notices);
        Assert.Equal(["a1", "a2", "t1"], messages.Select(each => each.Key).Order(StringComparer.Ordinal));
        Assert.Equal(AskMessages.Fields, messages["a1"]!.AsObject().Select(each => each.Key));
        Assert.Equal(
            ("a1", "games", false, 2),
            (messages["a1"]!["ask"]!.GetValue<string>(), messages["a1"]!["workspace"]!.GetValue<string>(),
                messages["a1"]!["teammate"]!.GetValue<bool>(), messages["a1"]!["unread"]!.GetValue<int>()));
        var newest = messages["a1"]!["newest"]!.AsObject();
        Assert.Equal(AskMessages.NewestFields, newest.Select(each => each.Key));
        Assert.Equal(
            ("m1", "2026-10-22T09:01:00.000Z", "agent", "Engine", "First line", "ordinary", true),
            (newest["id"]!.GetValue<string>(), newest["at"]!.GetValue<string>(), newest["who"]!.GetValue<string>(),
                newest["repository"]!.GetValue<string>(), newest["line"]!.GetValue<string>(), newest["level"]!.GetValue<string>(),
                newest["toEveryone"]!.GetValue<bool>()));

        Assert.Equal(["ask", "workspace", "teammate", "unread"], messages["a2"]!.AsObject().Select(each => each.Key));
        Assert.Equal(0, messages["a2"]!["unread"]!.GetValue<int>());

        Assert.True(messages["t1"]!["teammate"]!.GetValue<bool>());
        Assert.Equal(("teammate", "from a teammate", "urgent"), (messages["t1"]!["newest"]!["who"]!.GetValue<string>(),
            messages["t1"]!["newest"]!["line"]!.GetValue<string>(), messages["t1"]!["newest"]!["level"]!.GetValue<string>()));
        Assert.False(messages["t1"]!["newest"]!.AsObject().ContainsKey("repository"));
    }

    /// <summary>A message's line is its first with words, cut at the bound with an ellipsis.</summary>
    [Fact]
    public void A_line_is_the_first_with_words_and_bounded()
    {
        Assert.Equal("words", AskMessages.Line("\n \r\n words \nmore"));
        var cut = AskMessages.Line(new string('x', 500));
        Assert.Equal(AskMessages.LineBound, cut.Length);
        Assert.EndsWith("…", cut, StringComparison.Ordinal);
    }

    /// <summary>
    /// §5: an ask's first judgement is a baseline; an urgent message to everyone after it is said once, also when an ordinary one follows
    /// before the next judgement; a snapshot read again says nothing; an ordinary one, one to a single repository, one from before the
    /// levels and the person's own are never said; an ask first seen later is a baseline too.
    /// </summary>
    [Fact]
    public async Task An_urgent_message_to_everyone_is_said_once_past_the_watermark()
    {
        Channel("a1", 1, Message("old", 1, "already there", urgent: true));
        var notices = Notices();
        await notices.Slice.JudgeAsync();
        Assert.Empty(Arrivals());

        Channel("a1", 6,
            Message("old", 1, "already there", urgent: true),
            Message("urgent", 2, "Stop the export", urgent: true),
            Message("plain", 3, "ordinary after it"),
            Message("one", 4, "to one", urgent: true, to: "Engine"),
            Message("before", 5, "from before the levels", urgent: null),
            Message("mine", 6, "my urgent words", urgent: true, person: true));
        Channel("a2", 1, Message("new-ask", 2, "an ask first seen now", urgent: true));
        notices.ChannelsMoved();
        await notices.Slice.JudgeAsync();
        notices.ChannelsMoved();
        await notices.Slice.JudgeAsync();

        var said = Assert.Single(Arrivals());
        Assert.Equal(
            ["id", "at", "who", "repository", "line", "level", "toEveryone", "ask", "workspace", "teammate"],
            said.Select(each => each.Key));
        Assert.Equal(("urgent", "a1", "Stop the export", "urgent", true), (said["id"]!.GetValue<string>(), said["ask"]!.GetValue<string>(),
            said["line"]!.GetValue<string>(), said["level"]!.GetValue<string>(), said["toEveryone"]!.GetValue<bool>()));
        Assert.Equal("ordinary", Messages(notices)["a1"]!["newest"]!["level"]!.GetValue<string>());

        Channel("a2", 2, Message("new-ask", 2, "an ask first seen now", urgent: true), Message("next", 3, "after its baseline", urgent: true));
        notices.ChannelsMoved();
        await notices.Slice.JudgeAsync();
        Assert.Equal(["urgent", "next"], Arrivals().Select(each => each["id"]!.GetValue<string>()));
    }

    /// <summary>§5: a teammate's urgent message to everyone is said too, as a teammate's.</summary>
    [Fact]
    public async Task A_teammate_s_urgent_message_is_said()
    {
        Channel("t1", 0, true);
        var notices = Notices();
        await notices.Slice.JudgeAsync();

        Channel("t1", 1, true, Message("m1", 1, "from them", urgent: true, person: true, origin: "their-machine"));
        notices.ChannelsMoved();
        await notices.Slice.JudgeAsync();

        var said = Assert.Single(Arrivals());
        Assert.Equal(("teammate", true), (said["who"]!.GetValue<string>(), said["teammate"]!.GetValue<bool>()));
    }

    /// <summary>§5: a restart judges its first read as a baseline, so nothing the channels held then is said again.</summary>
    [Fact]
    public async Task A_restart_says_nothing_older_than_its_first_judgement()
    {
        Channel("a1", 0);
        var first = Notices();
        await first.Slice.JudgeAsync();
        Channel("a1", 1, Message("m1", 1, "said once", urgent: true));
        first.ChannelsMoved();
        await first.Slice.JudgeAsync();
        Assert.Single(Arrivals());

        var again = Notices();
        await again.Slice.JudgeAsync();
        again.ChannelsMoved();
        await again.Slice.JudgeAsync();
        Assert.Single(Arrivals());
    }

    /// <summary>The notify switch governs the saying, never the listing; turned on again, what arrived while it was off is not said late.</summary>
    [Fact]
    public async Task With_the_switch_off_nothing_is_said_and_everything_is_listed()
    {
        Channel("a1", 0);
        var notices = Notices();
        await notices.Slice.JudgeAsync();

        _notify = false;
        Channel("a1", 1, Message("m1", 1, "while off", urgent: true));
        notices.ChannelsMoved();
        await notices.Slice.JudgeAsync();
        _notify = true;
        notices.ChannelsMoved();
        await notices.Slice.JudgeAsync();

        Assert.Empty(Arrivals());
        Assert.Equal(1, Messages(notices)["a1"]!["unread"]!.GetValue<int>());
    }

    /// <summary>
    /// The channels are read again only where they moved or past the deadline: the record's own wakes (a notice read, another process's
    /// write) judge the news alone.
    /// </summary>
    [Fact]
    public async Task The_channels_are_read_where_they_moved_or_past_the_deadline()
    {
        Channel("a1", 0);
        var notices = Notices();
        await notices.Slice.JudgeAsync();
        await notices.Slice.JudgeAsync();
        Assert.Equal(1, _reads);

        notices.ChannelsMoved();
        await notices.Slice.JudgeAsync();
        Assert.Equal(2, _reads);

        _clock.Advance(NoticesStateSlice.ChannelsDeadline);
        await notices.Slice.JudgeAsync();
        Assert.Equal(3, _reads);
    }

    /// <summary>A read that fails keeps what was pushed, and is asked again at the next judgement; a slice never read names no partition.</summary>
    [Fact]
    public async Task A_failed_read_keeps_the_last_partition()
    {
        _failing = new DriverException("the service is away");
        var notices = Notices();
        await notices.Slice.JudgeAsync();
        Assert.Equal([NoticesStateSlice.News], notices.Slice.Snapshot().Partitions.Select(each => each.Partition));

        _failing = null;
        Channel("a1", 3);
        await notices.Slice.JudgeAsync();
        Assert.Equal(3, Messages(notices)["a1"]!["unread"]!.GetValue<int>());

        _failing = new DriverException("away again");
        notices.ChannelsMoved();
        await notices.Slice.JudgeAsync();
        await notices.Slice.JudgeAsync();
        Assert.Equal(3, Messages(notices)["a1"]!["unread"]!.GetValue<int>());
        Assert.Equal(4, _reads);
    }

    /// <summary>§2.5: an ask whose channel moved is pushed as that key alone.</summary>
    [Fact]
    public async Task A_moved_ask_is_pushed_alone()
    {
        Channel("a1", 0);
        Channel("a2", 0);
        Channel("a3", 0);
        var notices = Notices();
        await notices.Slice.JudgeAsync();
        var before = Raised.Count(each => each.Type == StateSlice.PushType);

        Channel("a2", 1, Message("m1", 1, "new"));
        notices.ChannelsMoved();
        await notices.Slice.JudgeAsync();

        var push = (SlicePush)Raised.Where(each => each.Type == StateSlice.PushType).Skip(before).Single().Payload!;
        Assert.Equal(NoticesStateSlice.Messages, push.Partition);
        Assert.Equal(["a2"], Written(push.Data).Select(each => each.Key));
    }

    /// <summary>The allow-lists are the page's <c>AskMessages</c> and <c>NewestMessage</c> types, read from its source.</summary>
    [Fact]
    public void The_allow_lists_are_the_page_s_types()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        Assert.NotNull(at);
        var source = File.ReadAllText(Path.Combine(at.FullName, "src", "Daoris.Web", "src", "bridge", "notices.ts"));
        Assert.Equal(AskMessages.Fields, FieldsOf(source, "AskMessages"));
        Assert.Equal(AskMessages.NewestFields, FieldsOf(source, "NewestMessage"));
    }

    private static IEnumerable<string> FieldsOf(string source, string type)
    {
        var block = System.Text.RegularExpressions.Regex.Match(
            source, $@"export type {type} = \{{(?<body>.*?)\n\}};", System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(block.Success, $"bridge/notices.ts declares no `{type}`");
        var body = System.Text.RegularExpressions.Regex.Replace(block.Groups["body"].Value, @"/\*.*?\*/|//[^\n]*", "", System.Text.RegularExpressions.RegexOptions.Singleline);
        return [.. System.Text.RegularExpressions.Regex.Matches(body, @"^\s{2}(?<name>\w+)\??:", System.Text.RegularExpressions.RegexOptions.Multiline)
            .Select(match => match.Groups["name"].Value)];
    }
}
