using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// NOTIFY2i's route (D196 point 8, the notification design §9): <c>NOTIFICATIONS</c>, what <c>daoris-driver notices</c> prints through the
/// running Daoris, read from what the windows hold — the confirmation channel of both sources, the <c>attention</c> slice's rows and the
/// <c>notices</c> slice's messages — and the notices' record, changing nothing.
/// </summary>
public sealed partial class NotificationsReadTests : DriverModuleBridge
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    private static SliceSnapshot Snapshot(string slice, string partition, JsonObject data) =>
        new(slice, "e1", StateSlice.Ready, [new SlicePartition(partition, 1, StateSlice.Ready, data)]);

    private static JsonObject Row(string kind, string id, string? session = null, string? workspace = "games", JsonObject? extra = null)
    {
        var row = new JsonObject { ["kind"] = kind, ["id"] = id, ["since"] = "2026-10-22T07:00:00.000Z" };
        if (session is not null) row["session"] = session;
        if (workspace is not null) row["workspace"] = workspace;
        foreach (var (name, value) in extra?.ToList() ?? []) row[name] = value?.DeepClone();
        return row;
    }

    /// <summary>
    /// The look: a terminal's press by its route and the session its wording names, never a value; a service's press waiting by its own
    /// act, one no longer waiting left out; every row of <i>What needs you</i> but a press's own, in its group, a hand-over ask whose session
    /// stands and a stale go-ahead each moved as the page moves them; and each ask the messages partition holds.
    /// </summary>
    [Fact]
    public void The_look_is_what_the_windows_hold()
    {
        var pipe = new PipeConfirmationsNow(3,
        [
            new PipeConfirmation("p1", DriverModule.Name, "SESSION_INPUT",
                [new("/id", "string", "s1a2b3c4"), new("/text", "string", "Run the database tests")], new PipeAsker("bash", 4120),
                Now.AddMinutes(-1), Now.AddSeconds(100), ShownAt: Now.AddSeconds(-20)),
            new PipeConfirmation("p2", "DAORIS.REMOTES", "SET", [new("/url", "string", "https://example.invalid")], new PipeAsker(null, null),
                Now, Now.AddMinutes(10)),
        ]);
        var service = new ServiceConfirmationsNow(2,
        [
            JsonSerializer.SerializeToElement(new { id = "c1", state = "waiting", method = "POST", route = "/api/quests", act = "Publish a quest to Engine",
                asked = "2026-10-22T08:59:30.000Z", expires = "2026-10-22T09:01:00.000Z" }),
            JsonSerializer.SerializeToElement(new { id = "c2", state = "confirmed", act = "Close an ask", expires = "2026-10-22T09:01:00.000Z" }),
        ]);
        var rows = new JsonObject
        {
            ["confirmation:p1"] = Row("confirmation", "p1"),
            ["parked:s1"] = Row("parked", "s1", session: "s1", extra: new JsonObject { ["note"] = new JsonObject { ["note"] = "Should the feed keep yesterday's rows?\nMore." } }),
            ["hand-over:a1"] = Row("hand-over", "a1", session: "s2", extra: new JsonObject { ["stands"] = true }),
            ["go-ahead:g1"] = Row("go-ahead", "g1", workspace: null, extra: new JsonObject { ["ask"] = "#1ebb5c", ["stale"] = new JsonObject { ["since"] = null } }),
            ["review:s3"] = Row("review", "s3", session: "s3"),
        };
        var messages = new JsonObject
        {
            ["#1ebb5c"] = new JsonObject
            {
                ["ask"] = "#1ebb5c", ["workspace"] = "games", ["teammate"] = false, ["unread"] = 3,
                ["newest"] = new JsonObject { ["id"] = "m9", ["at"] = "2026-10-22T08:50:00.000Z", ["who"] = "agent", ["line"] = "The schema moved." },
            },
            ["#2"] = new JsonObject { ["ask"] = "#2", ["workspace"] = "work", ["teammate"] = true, ["unread"] = 0 },
        };

        var look = NoticesRoutes.Look(pipe, service, Snapshot(AttentionStateSlice.Name, AttentionStateSlice.Rows, rows),
            Snapshot(NoticesStateSlice.Name, NoticesStateSlice.Messages, messages), []);

        Assert.Equal(
            [
                new PressWaiting("terminal", "p1", "SESSION_INPUT", "s1a2b3c4", Now.AddMinutes(-1), Now.AddSeconds(100), true),
                new PressWaiting("terminal", "p2", "REMOTES.SET", null, Now, Now.AddMinutes(10), false),
                new PressWaiting("service", "c1", "Publish a quest to Engine", null, Now.AddSeconds(-30), Now.AddMinutes(1), false),
            ],
            look.Presses!);
        Assert.Equal(
            [("holding", "parked", "s1", "Should the feed keep yesterday's rows?"), ("holding", "hand-over", "s2", null), ("word", "go-ahead", "#1ebb5c", null),
                ("ready", "review", "s3", null)],
            look.Needs!.Select(need => (need.Group, need.Kind, need.Subject, need.Says)));
        Assert.Equal(
            [new AskUnread("#1ebb5c", "games", false, 3, "agent", "The schema moved.", Now.AddMinutes(-10)), new AskUnread("#2", "work", true, 0, null, null, null)],
            look.Messages!);

        var lines = NoticesRead.Lines(look, Now);
        Assert.Equal(
            [
                "Waiting for your confirmation (3)",
                "  Publish a quest to Engine · at the service's door · 1m 0s left",
                "  SESSION_INPUT · session s1a2b3c4 · from a terminal · shown · 1m 40s left",
                "  REMOTES.SET · from a terminal · not shown yet · 10m 0s left",
                "What needs you (4)",
                "  Holding work (2)",
                "    parked · s1 · games · 2h ago — Should the feed keep yesterday's rows?",
                "    hand-over · s2 · games · 2h ago",
                "  Waiting for your word (1)",
                "    go-ahead · #1ebb5c · 2h ago",
                "  Ready for you (1)",
                "    review · s3 · games · 2h ago",
                "Messages (3 unread)",
                "  ask #1ebb5c · games · 3 unread — agent: The schema moved.",
            ],
            lines);
    }

    /// <summary>A slice not judged yet holds no partition: its rows are none, and the messages not read (null), never judged for the asking.</summary>
    [Fact]
    public void Nothing_judged_reads_as_nothing_and_the_messages_as_not_read()
    {
        var look = NoticesRoutes.Look(null, new ServiceConfirmationsNow(0, []), null, null, []);

        Assert.Empty(look.Presses!);
        Assert.Empty(look.Needs!);
        Assert.Null(look.Messages);
        Assert.Equal(["notices: nothing waits for you, and nothing is unread."], NoticesRead.Lines(look, Now));
    }

    /// <summary>
    /// The route answers the verb's lines (<c>said</c>), as text or as one JSON element, from the record and what the windows hold, and
    /// changes nothing: the record's bytes stay as they were, a notice unread stays unread.
    /// </summary>
    [Fact]
    public async Task The_route_answers_the_verb_s_lines_and_changes_nothing()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        var at = DateTimeOffset.UtcNow.AddMinutes(-12);
        Assert.Null(Daoris.Driver.Notices.Add(Home, new Notice("ended-s1", Daoris.Driver.Notices.Ended, at, Workspace: "games", Session: "s1",
            Repository: "Engine", State: "failed"), at));
        var before = File.ReadAllBytes(Daoris.Driver.Notices.PathOf(Home));

        var text = await AnswerAsync(module, "NOTIFICATIONS", new { said = true });
        var json = await AnswerAsync(module, "NOTIFICATIONS", new { json = true, said = true });

        var said = TreesSaid.Read(text)!;
        Assert.Equal(0, said.Code);
        Assert.Equal(["Done and failed (1 unread)", "  • ended failed · s1 · Engine · games · 12m ago"], said.Out);
        var read = JsonNode.Parse(Assert.Single(TreesSaid.Read(json)!.Out))!.AsObject();
        Assert.Equal("instance", read["from"]!.GetValue<string>());
        Assert.Empty(read["presses"]!.AsArray());
        Assert.False(read["notices"]![0]!["read"]!.GetValue<bool>());
        Assert.Equal(before, File.ReadAllBytes(Daoris.Driver.Notices.PathOf(Home)));
        Assert.False(Assert.Single(Daoris.Driver.Notices.Read(Home, DateTimeOffset.UtcNow)!).Read);
    }

    /// <summary>
    /// The terminal's groups are the page's: every kind of <c>ATTENTION_GROUP</c> (<c>work/attention/attention.ts</c>) in the same group,
    /// and none more, read from the page's source.
    /// </summary>
    [Fact]
    public void Each_kind_s_group_is_the_page_s()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        Assert.NotNull(at);
        var source = File.ReadAllText(Path.Combine(at.FullName, "src", "Daoris.Web", "src", "work", "attention", "attention.ts"));
        var block = GroupTable().Match(source);
        Assert.True(block.Success, "the page declares no ATTENTION_GROUP");
        var page = KindGroup().Matches(Comments().Replace(block.Groups["body"].Value, ""))
            .ToDictionary(each => each.Groups["kind"].Value, each => each.Groups["group"].Value);

        Assert.Equal(page.OrderBy(each => each.Key, StringComparer.Ordinal), NoticesRead.KindGroups.OrderBy(each => each.Key, StringComparer.Ordinal));
        Assert.Equal("holding", NoticesRead.Group("hand-over", stands: true, stale: false));
        Assert.Equal("word", NoticesRead.Group("hand-over", stands: false, stale: false));
        Assert.Equal("word", NoticesRead.Group("go-ahead", stands: false, stale: true));
        Assert.Equal("word", NoticesRead.Group("a-later-kind", stands: false, stale: false));
    }

    [GeneratedRegex(@"export const ATTENTION_GROUP[^=]*= \{(?<body>.*?)\n\};", RegexOptions.Singleline)]
    private static partial Regex GroupTable();

    [GeneratedRegex(@"^\s*'?(?<kind>[\w-]+)'?: '(?<group>\w+)',", RegexOptions.Multiline)]
    private static partial Regex KindGroup();

    [GeneratedRegex(@"//[^\n]*")]
    private static partial Regex Comments();
}
