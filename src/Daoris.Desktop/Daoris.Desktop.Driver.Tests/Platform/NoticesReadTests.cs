using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris-driver notices</c> (NOTIFY2i, D196 point 8, the notification design §9): through the running Daoris, its <c>NOTIFICATIONS</c>
/// lines as it answers them, this process reading nothing; alone, the notices' record, the presses, what needs the person and the
/// messages said not read; as text and as <c>--json</c>; and nothing changed either way.
/// </summary>
public sealed class NoticesReadTests
{
    private static async Task<int> RunAsync(InstanceTestHome home, params string[] args) =>
        await NoticesRead.RunAsync(args, home.Home, home.World("notices"), home.Out, home.Error);

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
    /// <i>Done and failed</i> newest first, the unread marked; and the record left as it was, an unread notice still unread.
    /// </summary>
    [Fact]
    public async Task Alone_the_record_is_read_and_the_live_groups_said_not_read()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var now = DateTimeOffset.UtcNow;
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
        var now = DateTimeOffset.UtcNow;
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

    /// <summary>Its usage line, in the driver's usage.</summary>
    [Fact]
    public void Its_usage_line_is_in_the_driver_s_usage() =>
        Assert.Contains("\n  notices [--json]\n", DriverCommand.Usage.ReplaceLineEndings("\n"));
}
