using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// RPC1e: <c>daoris-driver plugins approve|withdraw</c> through the running Daoris (the terminal RPC design §7.2 and §7.3): the person's
/// approval of the tools a plugin's server offers Ask Daoris, and its withdrawal, sent to the instance's <c>PLUGIN_ACTION</c>, which shows
/// it in its window; the terminal prints the lines the route answers (<c>said</c>, the alone path's own words). <c>new</c>, <c>try</c>,
/// <c>show</c>, <c>activity</c> and <c>install</c> stay this terminal's own: their words name this machine's folders.
/// </summary>
public sealed class PluginsThroughInstanceTests
{
    [Theory]
    [InlineData("approve acme.tickets tickets", """{"action":"help-approve","id":"acme.tickets","server":"tickets","said":true}""")]
    [InlineData("approve acme.tickets tickets --reads-only", """{"action":"help-reads","id":"acme.tickets","server":"tickets","said":true}""")]
    [InlineData("approve acme.tickets tickets --writes file_issue,close_issue",
        """{"action":"help-approve","id":"acme.tickets","server":"tickets","writes":["file_issue","close_issue"],"said":true}""")]
    [InlineData("withdraw acme.tickets tickets", """{"action":"help-withdraw","id":"acme.tickets","server":"tickets","said":true}""")]
    public async Task A_grant_through_the_instance_waits_for_the_window_and_prints_the_lines_it_answers(string words, string payload)
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Confirming = true;
        const string Line = "plugins: Ask Daoris may now read with list_issues on `tickets`.";
        standIn.Data = new { id = "acme.tickets", said = new TreesSaid([Line], [], 0).ToWire() };
        var output = new StringWriter();

        var exit = await PluginsCommand.RunAsync(words.Split(' '), output, home.Home, instance: home.World("plugins"));

        Assert.Equal(0, exit);
        Assert.Equal(Line + Environment.NewLine, output.ToString());
        Assert.Equal($"plugins: waiting for you to confirm this in Daoris's window…{Environment.NewLine}", home.Error.ToString());
        var press = Assert.Single(standIn.Presses);
        Assert.Equal("PLUGIN_ACTION", press.GetProperty("type").GetString());
        Assert.Equal(payload, press.GetProperty("payload").GetRawText());
        Assert.False(File.Exists(HelpGrants.PathIn(home.Home)));
    }

    [Theory]
    [InlineData("refused")]
    [InlineData("expired")]
    public async Task A_grant_the_person_refuses_or_leaves_unanswered_is_exit_1_and_grants_nothing_here(string code)
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Confirming = true;
        standIn.Press = StandInPress.Refuse;
        standIn.Code = code;
        var output = new StringWriter();

        var exit = await PluginsCommand.RunAsync(["withdraw", "acme.tickets", "tickets"], output, home.Home, instance: home.World("plugins"));

        Assert.Equal(1, exit);
        Assert.Equal("", output.ToString());
        Assert.Contains($"({code})", home.Error.ToString());
    }

    /// <summary>The verbs whose words name this machine's folders, and a grant's words the verb does not take, send nothing.</summary>
    [Theory]
    [InlineData("show acme.tickets")]
    [InlineData("activity acme.tickets")]
    [InlineData("try acme.tickets")]
    [InlineData("new")]
    [InlineData("approve acme.tickets tickets --sometimes")]
    public async Task The_other_plugins_verbs_stay_this_terminal_s_own_beside_a_running_Daoris(string words)
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        var output = new StringWriter();

        await PluginsCommand.RunAsync(words.Split(' '), output, home.Home, instance: home.World("plugins"));

        Assert.Empty(standIn.Presses);
        Assert.NotEqual("", output.ToString());
    }
}
