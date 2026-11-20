using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris notices</c>: through the running Daoris, its <c>NOTIFICATIONS</c>
/// lines as it answers them, this process reading nothing; alone, the notices' record, the presses, what needs the person and the
/// messages said not read; as text and as <c>--json</c>; and nothing changed either way.
/// </summary>
public sealed class NoticesReadTests
{
    private static readonly MovedClock Clock = StoodAt24th();

    private static MovedClock StoodAt24th()
    {
        var clock = new MovedClock();
        clock.Advance(TimeSpan.FromDays(4));
        return clock;
    }

    private static async Task<int> RunAsync(InstanceTestHome home, params string[] args) =>
        await NoticesRead.RunAsync(args, home.Home, home.World("notices"), home.Out, home.Error, clock: Clock);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Through_the_instance_the_verb_prints_the_lines_it_answers_and_reads_no_record(bool json)
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        string[] lines = json ? ["""{"from":"instance"}"""] : ["Waiting for your confirmation (1)", "  SESSION_INPUT · session s1a2b3c4 · from a terminal · shown · 1m 40s left"];
        standIn.Data = new { said = new TreesSaid(lines, [], 0).ToWire() };
        Directory.CreateDirectory(Path.GetDirectoryName(Notices.PathOf(home.Home))!);
        File.WriteAllText(Notices.PathOf(home.Home), "{ not a record");

        var exit = await RunAsync(home, json ? ["--json"] : []);

        Assert.Equal(0, exit);
        Assert.Equal(string.Concat(lines.Select(line => line + Environment.NewLine)), home.Out.ToString());
        Assert.Equal("", home.Error.ToString());
        var press = Assert.Single(standIn.Presses);
        Assert.Equal("NOTIFICATIONS", press.GetProperty("type").GetString());
        Assert.Equal($$"""{"json":{{(json ? "true" : "false")}},"said":true}""", press.GetProperty("payload").GetRawText());
    }

    /// <summary>
    /// Alone, the record is all there is: the live groups said not read, an unread answer under <i>Messages</i>, each other notice under
    /// <i>Done and failed</i> newest first, the unread marked; and the record left as it is, an unread notice still unread.
    /// </summary>
    [Fact]
    public async Task Alone_the_record_is_read_and_the_live_groups_said_not_read()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var now = Clock.GetUtcNow();
        Assert.Null(Notices.Add(home.Home, new Notice("ended-s1", Notices.Ended, now.AddMinutes(-40.5), Read: true, Workspace: "games",
            Session: "s1", Repository: "Engine", State: "failed"), now));
        Assert.Null(Notices.Add(home.Home, new Notice("expired-p1", Notices.Expired, now.AddMinutes(-5.5), Route: "SESSION_INPUT"), now));
        Assert.Null(Notices.Add(home.Home, new Notice("answered-c1", Notices.Answered, now.AddMinutes(-2.5), Workspace: "games", Session: "c1",
            Repository: "Engine"), now));
        var before = File.ReadAllBytes(Notices.PathOf(home.Home));

        var exit = await RunAsync(home);

        Assert.Equal(0, exit);
        Assert.Equal(
            [
                "notices: read without the running Daoris, from the notices' record alone. What waits for your confirmation, what needs you "
                + "and your asks' messages are the running Daoris's to read, and are not read here.",
                "Messages (1 unread)",
                "  answered · c1 · Engine · games · 2m ago",
                "Done and failed (1 unread)",
                "  • expired · SESSION_INPUT · 5m ago",
                "    ended failed · s1 · Engine · games · 40m ago",
            ],
            home.Out.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal(before, File.ReadAllBytes(Notices.PathOf(home.Home)));
    }

    /// <summary>Alone, <c>--json</c> is one element: <c>from</c> the record, each live group null, every notice read or not.</summary>
    [Fact]
    public async Task Alone_the_json_names_the_record_and_leaves_the_live_groups_null()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var now = Clock.GetUtcNow();
        Assert.Null(Notices.Add(home.Home, new Notice("look-lost-claim-s2", Notices.Look, now, Read: true, Session: "s2", Code: "lost-claim"), now));

        var exit = await RunAsync(home, "--json");

        Assert.Equal(0, exit);
        var read = JsonNode.Parse(home.Out.ToString())!.AsObject();
        Assert.Equal(["from", "presses", "needs", "messages", "notices"], read.Select(each => each.Key));
        Assert.Equal("record", read["from"]!.GetValue<string>());
        Assert.Null(read["presses"]);
        Assert.Null(read["needs"]);
        Assert.Null(read["messages"]);
        var notice = Assert.Single(read["notices"]!.AsArray())!.AsObject();
        Assert.Equal(("look-lost-claim-s2", "look", true, "lost-claim"),
            (notice["id"]!.GetValue<string>(), notice["kind"]!.GetValue<string>(), notice["read"]!.GetValue<bool>(), notice["code"]!.GetValue<string>()));
    }

    /// <summary>
    /// A terminal's press has no time, so its line says it waits until the person answers, and its JSON names no
    /// <c>expires</c>; the service door's keeps its time left. A terminal's is listed after every timed one.
    /// </summary>
    [Fact]
    public void A_terminal_s_press_waits_until_the_person_answers_and_the_service_s_keeps_its_time()
    {
        var now = new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);
        var look = new NoticesLook(
            [
                new PressWaiting("terminal", "p1", "SET_DRIVABLE", null, now.AddHours(-3), null, true),
                new PressWaiting("service", "h1", "an ask", null, now, now.AddSeconds(90), false),
            ],
            [], [], []);

        var said = NoticesRead.Said(look, json: false, now);
        var json = NoticesRead.Said(look, json: true, now);

        Assert.Contains("  an ask · at the service's door · 1m 30s left", said.Out);
        Assert.Contains("  SET_DRIVABLE · from a terminal · shown · waits until you answer", said.Out);
        Assert.True(said.Out.ToList().IndexOf("  an ask · at the service's door · 1m 30s left")
            < said.Out.ToList().IndexOf("  SET_DRIVABLE · from a terminal · shown · waits until you answer"));
        var presses = JsonNode.Parse(json.Out[0])!["presses"]!.AsArray();
        Assert.Null(presses.Single(each => each!["id"]!.GetValue<string>() == "p1")!["expires"]);
        Assert.NotNull(presses.Single(each => each!["id"]!.GetValue<string>() == "h1")!["expires"]);
    }

    /// <summary>The terminal counts what the bell counts: a row a role takes is listed under <i>With the agents</i>, counted nowhere.</summary>
    [Fact]
    public void A_row_a_role_takes_is_listed_with_the_agents_and_counted_nowhere()
    {
        var now = new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
        Assert.Equal(NoticesRead.WithTheAgents,NoticesRead.Group("parked", stands: false, stale: false, agents: true));
        Assert.Equal("holding", NoticesRead.Group("parked", stands: false, stale: false));
        var look = new NoticesLook([], [
            new NeedWaiting(NoticesRead.Group("parked", false, false, agents: true), "parked", "s1", "default", now, null),
            new NeedWaiting(NoticesRead.Group("work", false, false, agents: true), "work", "s2", "default", now, null),
            new NeedWaiting(NoticesRead.Group("unanswerable", false, false), "unanswerable", "q1", "default", now, null),
        ], [], []);

        var said = NoticesRead.Said(look, json: false, now).Out.ToList();

        Assert.Contains("What needs you (1)", said);
        Assert.Contains("  Waiting for your word (1)", said);
        Assert.Contains("  With the agents (2)", said);
        Assert.True(said.IndexOf("  Waiting for your word (1)") < said.IndexOf("  With the agents (2)"));
    }

    /// <summary>A row the person set aside is a marker, no notice: neither the text nor the JSON lists it, in Done and failed or anywhere.</summary>
    [Fact]
    public async Task A_row_set_aside_is_no_notice_in_the_text_or_the_json()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var now = Clock.GetUtcNow();
        Assert.Null(Notices.Add(home.Home, new Notice("ended-s1", Notices.Ended, now.AddMinutes(-5), Session: "s1", State: "failed"), now));
        Assert.Null(Notices.Renew(home.Home, Notices.SetAsideOf("check-failed", "q1", "2026-10-22T08:00:00.000Z", "games", now.AddMinutes(-3)), now));

        Assert.Equal(0, await RunAsync(home));
        var text = home.Out.ToString();
        Assert.Contains("Done and failed (1 unread)", text);
        Assert.DoesNotContain("set-aside", text);
        Assert.DoesNotContain("check-failed", text);

        var json = new InstanceTestHome();
        await using (json)
        {
            Assert.Null(Notices.Renew(json.Home, Notices.SetAsideOf("check-failed", "q1", null, null, now), now));
            Assert.Equal(0, await RunAsync(json, "--json"));
            Assert.Equal(0, JsonNode.Parse(json.Out.ToString())!["notices"]!.AsArray().Count);
        }
    }

    /// <summary>
    /// <c>--set-aside &lt;kind&gt;</c> is the person's press of <c>NOTICE_DISMISS</c> for the whole kind, confirmed in the window, and prints the
    /// lines the route answers.
    /// </summary>
    [Fact]
    public async Task Set_aside_sends_the_kind_as_the_person_s_press_and_prints_what_it_answers()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Confirming = true;
        string[] lines = ["notices: set aside 98 rows of kind `check-failed`."];
        standIn.Data = new { said = new TreesSaid(lines, [], 0).ToWire() };

        var exit = await RunAsync(home, "--set-aside", "check-failed");

        Assert.Equal(0, exit);
        Assert.Equal($"{lines[0]}{Environment.NewLine}", home.Out.ToString());
        var press = Assert.Single(standIn.Presses);
        Assert.Equal("NOTICE_DISMISS", press.GetProperty("type").GetString());
        Assert.Equal("""{"rowKind":"check-failed","all":true,"said":true}""", press.GetProperty("payload").GetRawText());
    }

    /// <summary>Where no Daoris runs nothing judges what needs the person, so the verb sets nothing aside and says why, exit 2.</summary>
    [Fact]
    public async Task Set_aside_where_no_daoris_runs_changes_nothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();

        var exit = await NoticesRead.RunAsync(["--set-aside", "check-failed"], home.Home, null, home.Out, home.Error);

        Assert.Equal(2, exit);
        Assert.StartsWith("notices: what needs you is judged by the running Daoris", home.Error.ToString());
        Assert.False(File.Exists(Notices.PathOf(home.Home)));
    }

    /// <summary>The kind's words say how many rows went, or that none waits, or that the record refused the write.</summary>
    [Fact]
    public void The_set_aside_says_how_many_rows_went()
    {
        Assert.Equal(["notices: set aside 1 row of kind `parked`."], NoticesAside.Said("parked", 1).Out);
        Assert.Equal(["notices: set aside 3 rows of kind `parked`."], NoticesAside.Said("parked", 3).Out);
        Assert.Equal(["notices: no row of kind `parked` is waiting for you."], NoticesAside.Said("parked", 0).Out);
        Assert.Equal(2, NoticesAside.Said("parked", null).Code);
    }

    [Fact]
    public async Task Alone_with_no_record_it_says_so()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();

        Assert.Equal(0, await RunAsync(home));
        Assert.EndsWith($"notices: the record holds no notice.{Environment.NewLine}", home.Out.ToString());
    }

    /// <summary>A record that does not read is said, exit 2, and left where it is: a reader never sets one aside.</summary>
    [Fact]
    public async Task Alone_a_record_that_does_not_read_is_said_and_left_as_it_is()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        Directory.CreateDirectory(Path.GetDirectoryName(Notices.PathOf(home.Home))!);
        File.WriteAllText(Notices.PathOf(home.Home), "{ not a record");

        Assert.Equal(2, await RunAsync(home));
        Assert.StartsWith("notices: the notices' record did not read this moment", home.Error.ToString());
        Assert.Equal("{ not a record", File.ReadAllText(Notices.PathOf(home.Home)));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(Notices.PathOf(home.Home))!, "notices*"));
    }

    /// <summary>A word it does not take is its usage, exit 2, and nothing is asked of the instance.</summary>
    [Fact]
    public async Task A_word_it_does_not_take_is_its_usage_and_nothing_is_sent()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();

        Assert.Equal(2, await RunAsync(home, "--all"));
        Assert.Equal($"notices: {NoticesRead.Usage}{Environment.NewLine}", home.Error.ToString());
        Assert.Empty(standIn.Presses);
    }

    /// <summary>A show of a subject goes to the running Daoris with its subject, and prints what it answers.</summary>
    [Fact]
    public async Task A_show_names_its_subject_to_the_instance_and_prints_the_answer()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Data = new { said = new TreesSaid(["memory s1 waits for your word"], [], 0).ToWire() };

        Assert.Equal(0, await RunAsync(home, "--show", "s1"));

        Assert.Equal($"memory s1 waits for your word{Environment.NewLine}", home.Out.ToString());
        Assert.Equal("""{"json":false,"said":true,"show":"s1"}""", Assert.Single(standIn.Presses).GetProperty("payload").GetRawText());
    }

    /// <summary>A show is the running Daoris's to read: alone it says so and exits 2, never from the record.</summary>
    [Fact]
    public async Task A_show_alone_is_said_not_read()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();

        Assert.Equal(2, await RunAsync(home, "--show", "s1"));
        Assert.Contains("running Daoris", home.Error.ToString());
        Assert.Equal(2, await RunAsync(home, "--show"));
    }

    /// <summary>Its usage line, in the driver's usage.</summary>
    [Fact]
    public void Its_usage_line_is_in_the_driver_s_usage() =>
        Assert.Contains("\n  notices [--json] [--show <subject> | --set-aside <kind>]\n", DriverCommand.Usage.ReplaceLineEndings("\n"));
}
