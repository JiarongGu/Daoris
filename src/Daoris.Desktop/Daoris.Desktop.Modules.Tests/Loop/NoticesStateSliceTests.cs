using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// NOTIFY2f's slice (D196 point 6; the notification design §2.5, §5): <i>Done and failed</i> pushed from this process's read of
/// <c>data/notices.json</c>, one partition keyed by notice, its fields allow-listed; the writer pushing its own write; another process's
/// write read again and its notices said once, none older than the first read; a failed read keeping what was pushed; recording
/// whatever the notify switch says; the presses <c>NOTICES_READ</c> and <c>NOTICE_DISMISS</c>; and a press's unanswered end kept.
/// </summary>
/// <remarks>Files only, in the bridge's home, on a held clock. The watch that wakes the slice is the driver's <c>HomeWatchTests</c>'.</remarks>
public sealed class NoticesStateSliceTests : DriverModuleBridge
{
    private readonly HeldClock _clock = new();

    private bool _notify = true;

    private NoticesStateSlice Notices() => new(Home, Bus, clock: _clock, notify: () => _notify);

    private DateTimeOffset Now => _clock.GetUtcNow();

    private Notice Ended(string session, int minutes = 0, string? workspace = "games") =>
        new($"ended-{session}", Daoris.Driver.Notices.Ended, Now.AddMinutes(minutes), Workspace: workspace, Session: session, Repository: "Engine", State: "failed");

    private IReadOnlyList<SlicePush> Pushes() =>
    [
        .. Raised.Where(each => each.Type == StateSlice.PushType).Select(each => (SlicePush)each.Payload!)
            .Where(push => push.Slice == NoticesStateSlice.Name),
    ];

    private IReadOnlyList<JsonObject> Arrivals() =>
    [
        .. Raised.Where(each => each.Type == NoticesStateSlice.ArrivedType).Select(each => (JsonObject)each.Payload!),
    ];

    private static JsonObject Written(object? data) => JsonSerializer.SerializeToNode(data, IpcJson.Options)!.AsObject();

    /// <summary>Another process's write: the record as it would leave it, written here with no mark of this process's.</summary>
    private void Elsewhere(Notice notice)
    {
        var path = Daoris.Driver.Notices.PathOf(Home);
        var record = File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path))!.AsObject() : new JsonObject { ["schemaVersion"] = 1, ["notices"] = new JsonObject() };
        var picked = NoticesStateSlice.Picked(notice);
        picked.Remove("id");
        record["notices"]![notice.Id] = picked;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, record.ToJsonString());
    }

    /// <summary>§2.5: one partition, <c>news</c>, keyed by notice, each with the allow-list's fields alone, an absent one left out.</summary>
    [Fact]
    public async Task Each_notice_is_pushed_under_its_id_with_the_allow_listed_fields()
    {
        var notices = Notices();
        notices.Record(Ended("s1"), said: false);
        notices.Record(new Notice("expired-p1", Daoris.Driver.Notices.Expired, Now, Route: "SESSION_INPUT"), said: false);

        await notices.Slice.JudgeAsync();

        var snapshot = notices.Slice.Snapshot();
        Assert.Equal([NoticesStateSlice.News], snapshot.Partitions.Select(each => each.Partition));
        var news = Written(snapshot.Partitions[0].Data);
        Assert.Equal(["ended-s1", "expired-p1"], news.Select(each => each.Key).Order(StringComparer.Ordinal));
        Assert.Equal(["id", "kind", "at", "read", "workspace", "session", "repository", "state"], news["ended-s1"]!.AsObject().Select(each => each.Key));
        Assert.Equal(["id", "kind", "at", "read", "route"], news["expired-p1"]!.AsObject().Select(each => each.Key));
        Assert.All(news, each => Assert.Subset(NoticesStateSlice.Fields.ToHashSet(), each.Value!.AsObject().Select(field => field.Key).ToHashSet()));
        Assert.Equal("2026-10-20T09:00:00.000Z", news["ended-s1"]!["at"]!.GetValue<string>());
    }

    /// <summary>
    /// The page's <c>Notice</c> type names every field the allow-list holds, and none more (the page's twin, read from its source).
    /// </summary>
    [Fact]
    public void The_allow_list_is_the_page_s_notice_type()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        Assert.NotNull(at);
        var source = File.ReadAllText(Path.Combine(at.FullName, "src", "Daoris.Web", "src", "bridge", "notices.ts"));
        var block = System.Text.RegularExpressions.Regex.Match(
            source, @"export type Notice = \{(?<body>.*?)\n\};", System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(block.Success, "bridge/notices.ts declares no `Notice`");
        var body = System.Text.RegularExpressions.Regex.Replace(block.Groups["body"].Value, @"/\*.*?\*/|//[^\n]*", "", System.Text.RegularExpressions.RegexOptions.Singleline);
        var fields = System.Text.RegularExpressions.Regex.Matches(body, @"^\s{2}(?<name>\w+)\??:", System.Text.RegularExpressions.RegexOptions.Multiline)
            .Select(match => match.Groups["name"].Value);
        Assert.Equal(NoticesStateSlice.Fields, fields);
    }

    /// <summary>§2.4: a read moves the one notice named, pushed as that key alone; the writer pushes its own write with no watch's wake.</summary>
    [Fact]
    public async Task A_read_is_pushed_by_its_writer_and_moves_one_notice()
    {
        var notices = Notices();
        notices.Record(Ended("s1"), said: false);
        notices.Record(Ended("s2", 1), said: false);
        notices.Record(Ended("s3", 2), said: false);
        await notices.Slice.JudgeAsync();
        var before = Pushes().Count;

        Assert.Null(notices.MarkRead(["ended-s1"]));
        await notices.Slice.JudgeAsync();

        var push = Pushes().Skip(before).Single();
        var moved = Written(push.Data);
        Assert.Equal(["ended-s1"], moved.Select(each => each.Key));
        Assert.True(moved["ended-s1"]!["read"]!.GetValue<bool>());
    }

    /// <summary>
    /// §5: a notice another process wrote (the headless host's) is said once as it arrives, and none the first read found; a notice this
    /// process recorded as already said by its own event is not said again; with the switch off, none is said.
    /// </summary>
    [Fact]
    public async Task Another_process_s_notice_is_said_once_and_none_older_than_the_first_read()
    {
        Elsewhere(Ended("before"));
        var notices = Notices();
        await notices.Slice.JudgeAsync();
        Assert.Empty(Arrivals());

        Elsewhere(Ended("headless", 1));
        await notices.Slice.JudgeAsync();
        await notices.Slice.JudgeAsync();
        Assert.Equal(["ended-headless"], Arrivals().Select(each => each["id"]!.GetValue<string>()));
        Assert.Equal("games", Arrivals()[0]["workspace"]!.GetValue<string>());

        notices.Keep([new AttentionEvent(AttentionKind.Ended, "mine", "Engine", "failed") { Workspace = "games" }]);
        await notices.Slice.JudgeAsync();
        Assert.Single(Arrivals());

        _notify = false;
        Elsewhere(Ended("quiet", 2));
        await notices.Slice.JudgeAsync();
        Assert.Single(Arrivals());
        Assert.Contains("ended-quiet", Written(notices.Slice.Snapshot().Partitions[0].Data).Select(each => each.Key));
    }

    /// <summary>
    /// NOTIFY2h (the design §5's table): a look's news, whoever wrote it, is pushed with its code and counted, and never said aloud.
    /// </summary>
    [Fact]
    public async Task A_look_s_news_is_pushed_with_its_code_and_never_said()
    {
        var notices = Notices();
        await notices.Slice.JudgeAsync();

        Elsewhere(new Notice("look-lost-claim-s1", Daoris.Driver.Notices.Look, Now, Session: "s1", Quest: "q1", Code: LookSaid.LostClaim));
        await notices.Slice.JudgeAsync();

        Assert.Empty(Arrivals());
        var pushed = Written(notices.Slice.Snapshot().Partitions[0].Data)["look-lost-claim-s1"]!.AsObject();
        Assert.Equal("look", pushed["kind"]!.GetValue<string>());
        Assert.Equal("lost-claim", pushed["code"]!.GetValue<string>());
    }

    /// <summary>A restart reads the record whole as its baseline: nothing it already held is said again.</summary>
    [Fact]
    public async Task A_restart_says_nothing_the_record_held()
    {
        var first = Notices();
        Elsewhere(Ended("s1"));
        await first.Slice.JudgeAsync();
        Elsewhere(Ended("s2", 1));
        await first.Slice.JudgeAsync();
        Assert.Single(Arrivals());

        var again = Notices();
        await again.Slice.JudgeAsync();
        Assert.Single(Arrivals());
    }

    /// <summary>§2.5: a read that fails (a file half seen) keeps what was pushed, and sets nothing aside.</summary>
    [Fact]
    public async Task A_failed_read_keeps_the_last_snapshot_and_sets_nothing_aside()
    {
        var notices = Notices();
        notices.Record(Ended("s1"), said: false);
        await notices.Slice.JudgeAsync();
        var held = notices.Slice.Snapshot();

        var path = Daoris.Driver.Notices.PathOf(Home);
        File.WriteAllText(path, "{ \"notices\": ");
        await notices.Slice.JudgeAsync();

        Assert.Equal(Written(held.Partitions[0].Data).ToJsonString(), Written(notices.Slice.Snapshot().Partitions[0].Data).ToJsonString());
        Assert.Equal("{ \"notices\": ", File.ReadAllText(path));
        Assert.Single(Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "notices*.json"));
    }

    /// <summary>§2.5: a session's end is recorded with the notify switch off, as the watch observes either way; it is listed, never said.</summary>
    [Fact]
    public async Task An_end_is_recorded_with_the_switch_off()
    {
        _notify = false;
        var notices = Notices();
        await notices.Slice.JudgeAsync();

        notices.Keep([
            new AttentionEvent(AttentionKind.Ended, "s1", "Engine", "failed") { Workspace = "games" },
            new AttentionEvent(AttentionKind.Parked, "s2", "Engine"),
        ]);
        await notices.Slice.JudgeAsync();

        var news = Written(notices.Slice.Snapshot().Partitions[0].Data);
        Assert.Equal(["ended-s1"], news.Select(each => each.Key));
        Assert.Equal(("failed", "games"), (news["ended-s1"]!["state"]!.GetValue<string>(), news["ended-s1"]!["workspace"]!.GetValue<string>()));
        Assert.Empty(Arrivals());
    }

    /// <summary>§2.4's scopes: a window's dismissal of all takes its own workspace's notices and every machine-wide one, never another's.</summary>
    [Fact]
    public void A_window_s_scope_is_its_workspace_and_the_machine_s()
    {
        var notices = Notices();
        notices.Record(Ended("mine", workspace: "Games"), said: false);
        notices.Record(Ended("theirs", workspace: "work"), said: false);
        notices.Record(new Notice("expired-p1", Daoris.Driver.Notices.Expired, Now, Route: "SESSION_INPUT"), said: false);

        Assert.Equal(["ended-mine", "expired-p1"], notices.Scoped("games").Order(StringComparer.Ordinal));
        Assert.Equal(3, notices.Scoped(null).Count);
    }

    /// <summary>
    /// The presses (§2.5): <c>NOTICES_READ</c> reads the ids it names, <c>NOTICE_DISMISS</c> one id or all of a window's scope, each
    /// answering whether it was written and declaring the notices.
    /// </summary>
    [Fact]
    public async Task The_presses_read_and_dismiss_through_the_loop_s_notices()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        loop.Notices.Record(Ended("s1"), said: false);
        loop.Notices.Record(Ended("s2", workspace: "work"), said: false);
        loop.Notices.Record(Ended("s3"), said: false);

        var read = await AnswerAsync(module, "NOTICES_READ", new { ids = new[] { "ended-s1" } });
        Assert.True(read.GetProperty("written").GetBoolean());
        var dismissed = await AnswerAsync(module, "NOTICE_DISMISS", new { id = "ended-s3" });
        Assert.True(dismissed.GetProperty("written").GetBoolean());

        var left = Daoris.Driver.Notices.Read(Home, DateTimeOffset.UtcNow)!;
        Assert.Equal([("ended-s1", true), ("ended-s2", false)], left.Select(notice => (notice.Id, notice.Read)).Order());

        await AnswerAsync(module, "NOTICE_DISMISS", new { all = true, workspace = "games" });
        Assert.Equal(["ended-s2"], Daoris.Driver.Notices.Read(Home, DateTimeOffset.UtcNow)!.Select(notice => notice.Id));
        Assert.Equal(StateScopes.Notices, DriverModule.Declared(new IpcRequest { Id = "x", Module = DriverModule.Name, Type = "NOTICE_DISMISS" })!.Scopes);
    }

    /// <summary>
    /// §2.2: a terminal's press refused by its time is kept as a notice, every window's, its route as the page's catalogue names it;
    /// one confirmed, refused or withdrawn keeps nothing.
    /// </summary>
    [Fact]
    public void A_press_that_ended_unanswered_is_kept()
    {
        var notices = Notices();
        using var confirmations = new PipeConfirmations(Bus, MachineLog.None, _clock) { Unanswered = notices.Unanswered };
        var expired = confirmations.Ask(DriverModule.Name, "SESSION_INPUT", [], new PipeAsker("pwsh", 7))!.Value;
        var refused = confirmations.Ask("DAORIS.REMOTES", "SET", [], new PipeAsker("pwsh", 7))!.Value;
        confirmations.Answer(refused.Shown.Id, confirm: false);

        _clock.Advance(PipeConfirmations.FindWithin);

        Assert.Equal(PipeConfirmationEnd.Expired, expired.Ended.Result);
        var kept = Daoris.Driver.Notices.Read(Home, Now)!;
        var notice = Assert.Single(kept);
        Assert.Equal(($"expired-{expired.Shown.Id}", "expired", "SESSION_INPUT", (string?)null), (notice.Id, notice.Kind, notice.Route, notice.Workspace));
    }

    /// <summary>A press of another module is named as the page's catalogue names its act: the module without its <c>DAORIS.</c>.</summary>
    [Fact]
    public void Another_module_s_press_is_named_as_its_catalogue_key()
    {
        var notices = Notices();
        notices.Unanswered(new PipeConfirmation("p1", "DAORIS.REMOTES", "SET", [], new PipeAsker(null, null), Now, Now));
        Assert.Equal("REMOTES.SET", Daoris.Driver.Notices.Read(Home, Now)!.Single().Route);
    }
}
