using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The page's terminal twins (PLUGUI1d, D119 §4.4, D50): <c>daoris-driver plugins show &lt;id&gt; [--json]</c> and
/// <c>plugins activity &lt;id&gt; [--since &lt;span&gt;] [--json]</c>, run whole in-process, with their exits:
/// 0 shown, 2 could not do what was asked.
/// </summary>
public sealed class PluginsCommandTests : IDisposable
{
    private const string Secret = "s3cr3t-token-value";

    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-plugins-cmd-" + Guid.NewGuid().ToString("N")[..8]);

    public PluginsCommandTests()
    {
        Directory.CreateDirectory(_home);
        var folder = Path.Combine(_home, "plugins", "acme.gate");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), $$"""
            { "id": "acme.gate", "name": "Gate", "version": "1.0.0", "description": "Holds a quest whose title asks it to.",
              "hooks": { "command": ["node", "${plugin}/hooks.mjs"], "points": ["quest/consider", "session/ended"] },
              "servers": [ { "name": "tickets", "command": ["node", "${plugin}/tickets.mjs"], "env": { "TICKETS_TOKEN": "{{Secret}}" } } ] }
            """);
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task Show_prints_the_page_with_its_health_read_from_the_logs_last_word()
    {
        Log(DateTimeOffset.UtcNow, log => log.Started("acme.gate", ["quest/consider", "session/ended"], 40, PluginEvents.ByLoop));

        var (exit, said) = await Run("show", "acme.gate");

        Assert.Equal(0, exit);
        Assert.Contains("plugins: `acme.gate` — Gate 1.0.0", said);
        Assert.Contains("Holds a quest whose title asks it to.", said);
        Assert.Contains("running since", said);
        Assert.Contains("listening on quest/consider, session/ended", said);
        // The terminal says the state is the log's, and when and from where (D119 §2).
        Assert.Contains("the machine log's last word for it", said);
        Assert.Contains("(desktop)", said);
        Assert.Contains("quest/consider", said);
        Assert.Contains("a decision, waits 10 seconds, listening", said);
        Assert.Contains("TICKETS_TOKEN", said);
        Assert.DoesNotContain(Secret, said);
        Assert.Contains(Path.Combine(_home, "plugins", ".data", "acme.gate"), said);
    }

    [Fact]
    public async Task Show_with_json_is_the_page_as_data_and_still_no_value()
    {
        var (exit, said) = await Run("show", "acme.gate", "--json");

        Assert.Equal(0, exit);
        var page = JsonDocument.Parse(said).RootElement;
        Assert.Equal("acme.gate", page.GetProperty("id").GetString());
        Assert.Equal("ready", page.GetProperty("health").GetProperty("state").GetString());
        Assert.Equal("log", page.GetProperty("healthFrom").GetString());
        Assert.Equal("TICKETS_TOKEN", page.GetProperty("servers")[0].GetProperty("environment")[0].GetString());
        Assert.DoesNotContain(Secret, said);
    }

    [Fact]
    public async Task Activity_prints_the_summary_and_the_newest_events()
    {
        var now = DateTimeOffset.UtcNow;
        Log(now.AddMinutes(-3), log => log.Called("acme.gate", "quest/consider", PluginEvents.Allow, 12));
        Log(now.AddMinutes(-2), log => log.Called("acme.gate", "quest/consider", PluginEvents.Hold, 30));
        Log(now.AddMinutes(-1), log => log.Failed("acme.gate", "session/ended", PluginEvents.Late, null, 10_000, PluginEvents.ByLoop));

        var (exit, said) = await Run("activity", "acme.gate");

        Assert.Equal(0, exit);
        Assert.Contains("plugins: `acme.gate`'s activity since", said);
        Assert.Contains("2 answered (allow 1, hold 1)", said);
        Assert.Contains("median 21 ms, slowest 30 ms", said);
        Assert.Contains("1 failure (late 1)", said);
        Assert.Contains("tickets: handed to 0 session(s), withheld from 0", said);
        Assert.Contains("the newest 3 of 3", said);
        var recent = said[said.IndexOf("the newest", StringComparison.Ordinal)..];
        Assert.True(recent.IndexOf("late", StringComparison.Ordinal) < recent.IndexOf("allow", StringComparison.Ordinal), said);
    }

    [Fact]
    public async Task Activity_since_narrows_the_period_and_json_is_the_read()
    {
        var now = DateTimeOffset.UtcNow;
        Log(now.AddHours(-3), log => log.Called("acme.gate", "quest/consider", PluginEvents.Allow, 12));
        Log(now.AddMinutes(-5), log => log.Called("acme.gate", "quest/consider", PluginEvents.Hold, 30));

        var (exit, said) = await Run("activity", "acme.gate", "--since", "2h", "--json");

        Assert.Equal(0, exit);
        var read = JsonDocument.Parse(said).RootElement;
        var answers = read.GetProperty("points")[0].GetProperty("answers");
        Assert.Equal(1, answers.GetProperty("hold").GetInt32());
        Assert.False(answers.TryGetProperty("allow", out _));
        Assert.True(read.GetProperty("logged").GetBoolean());
    }

    [Fact]
    public async Task No_machine_log_is_said_and_is_no_failure()
    {
        var (exit, said) = await Run("activity", "acme.gate");

        Assert.Equal(0, exit);
        Assert.Contains("no machine log on this machine", said);
    }

    [Theory]
    [InlineData(new[] { "show" }, "`plugins show` needs a plugin's id")]
    [InlineData(new[] { "activity" }, "`plugins activity` needs a plugin's id")]
    [InlineData(new[] { "show", "acme.nobody" }, "no plugin `acme.nobody` on this machine")]
    [InlineData(new[] { "show", "acme.gate", "--loud" }, "`--loud` is not an option of `plugins show`")]
    [InlineData(new[] { "activity", "acme.gate", "--since", "soon" }, "`--since` takes a span such as 30m, 2h or 3d")]
    [InlineData(new[] { "explode" }, "usage: daoris-driver plugins new")]
    public async Task What_the_verbs_cannot_do_is_a_sentence_and_exit_2(string[] args, string sentence)
    {
        var (exit, said) = await Run(args);

        Assert.Equal(2, exit);
        Assert.Contains(sentence, said);
    }

    [Fact]
    public async Task With_no_home_there_is_no_plugin_to_show()
    {
        var output = new StringWriter();

        var exit = await PluginsCommand.RunAsync(["show", "acme.gate"], output, home: null);

        Assert.Equal(2, exit);
        Assert.Contains("no Daoris home", output.ToString());
    }

    /// <summary>The kit's verbs answer through the same door: one `plugins` verb on the host, as the usage says.</summary>
    [Fact]
    public async Task The_kits_verbs_answer_through_the_same_door()
    {
        var parent = Path.Combine(_home, "work");
        Directory.CreateDirectory(parent);

        var (exit, said) = await Run("new", "acme.made", "--point", "quest/consider", "--in", parent);

        Assert.Equal(0, exit);
        Assert.Contains("made `acme.made`", said);
        Assert.Contains("plugins show <id>", PluginsCommand.Usage);
        Assert.Contains("plugins activity <id>", PluginsCommand.Usage);
    }

    // ——— helpers

    private async Task<(int Exit, string Said)> Run(params string[] args)
    {
        var output = new StringWriter();
        var exit = await PluginsCommand.RunAsync(args, output, _home);
        return (exit, output.ToString());
    }

    private void Log(DateTimeOffset at, Action<PluginLog> write)
    {
        using var log = new MachineLog(_home, "desktop", () => at);
        write(new PluginLog(log));
    }
}
