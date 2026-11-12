using System.Text.Json;

using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A plugin's declared command run round by round in its own process: its answers read by shape, the terminal saying its lines and
/// asking what it asks (hidden where it says so, the value never said back), a session's terminal refused, and a git identity's API
/// token adopted once by the command that declares it, the identity keeping none and the log naming no value.
/// </summary>
public sealed class PluginCommandRunTests : IDisposable
{
    private const string Token = "throwaway-adopted-token";
    private readonly string _home = ScratchFolders.New("daoris-plugin-command-");

    public void Dispose() => ScratchFolders.Remove(_home);

    private void Install(string interfaces = """{ "commands": [{ "name": "login", "about": "Keeps a token.", "interactive": true, "adopts": { "hosts": ["tickets.example"], "answer": "token" } }, { "name": "status", "about": "Says what is kept." }], "storage": { "for": "the token" } }""")
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, "acme.tickets");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName),
            $$"""{ "id": "acme.tickets", "hooks": { "command": ["node", "plugin.mjs"], "points": ["work/state"] }, "interfaces": {{interfaces}} }""");
    }

    /// <summary>A plugin's process that answers each round as <see cref="Answer"/> says, and keeps every frame sent to it.</summary>
    private sealed class Channel(Func<JsonElement, string> answer, List<string> frames) : IHookChannel
    {
        public IReadOnlyList<string> Points => [];

        public bool Alive => true;

        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) => Task.FromResult(HookDecision.Allow);

        public Task EndedAsync(object payload, CancellationToken ct) => Task.CompletedTask;

        public void Listen(Action<JsonElement?>? steps) { }

        public Task WatchAsync(WorkWatchFrame frame, CancellationToken ct) => Task.CompletedTask;

        public Task<JsonElement> CommandAsync(object frame, CancellationToken ct)
        {
            var sent = JsonSerializer.SerializeToElement(frame);
            frames.Add(sent.GetRawText());
            return Task.FromResult(JsonDocument.Parse(answer(sent)).RootElement.Clone());
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private readonly List<string> _frames = [];

    private PluginCommandRun.Starter Start(Func<JsonElement, string> answer) => (_, _) => Task.FromResult<IHookChannel>(new Channel(answer, _frames));

    /// <summary>A login that asks for its token, hidden, and ends once it has one.</summary>
    private static string Login(JsonElement frame) =>
        frame.GetProperty("answers").TryGetProperty("token", out _)
            ? """{ "said": ["The token is kept."], "exit": 0 }"""
            : """{ "said": ["Make a token at the site."], "open": "https://tickets.example/tokens", "ask": { "name": "token", "prompt": "Token", "masked": true } }""";

    [Theory]
    [InlineData("""{ "said": ["done"] }""", null)]
    [InlineData("""[]""", "it is not an object")]
    [InlineData("""{ "said": "done" }""", "`said` is not a list of at most 40 lines")]
    [InlineData("""{ "ask": { "prompt": "Token" } }""", "`ask` needs a `name` and a `prompt`")]
    [InlineData("""{ "open": "file:///etc/passwd" }""", "`open` is not a web address")]
    public void A_round_s_answer_is_read_by_its_shape(string answer, string? refused)
    {
        var read = () => CommandWire.Read("acme.tickets", JsonDocument.Parse(answer).RootElement);
        if (refused is null) Assert.Equal(["done"], read().Said);
        else Assert.Contains(refused, Assert.Throws<DriverException>(read).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_round_s_lines_are_bounded_in_all_under_every_frame_a_plugin_is_read_in()
    {
        static CommandRound Said(params string[] lines) =>
            CommandWire.Read("acme.tickets", JsonDocument.Parse(JsonSerializer.Serialize(new { said = lines })).RootElement);

        Assert.True(CommandWire.SaidBytes * 4 < WorkBounds.FrameBytes);
        Assert.Equal(8, Said([.. Enumerable.Repeat(new string('x', CommandWire.SaidBytes / 8), 8)]).Said.Count);
        Assert.Contains("longer than 8 KiB in all", Assert.Throws<DriverException>(() => Said([.. Enumerable.Repeat(new string('x', 1500), 6)])).Message, StringComparison.Ordinal);
        Assert.Throws<DriverException>(() => Said(new string('阶', 2000), new string('阶', 2000)));
    }

    [Fact]
    public async Task The_terminal_asks_what_the_command_asks_hidden_and_never_says_the_value_back()
    {
        Install();
        var (output, error) = (new StringWriter(), new StringWriter());
        var hidden = new List<bool>();
        var terminal = new PluginCommandConsole.Terminal(output, error, masked => { hidden.Add(masked); return Token; }, _ => null);

        var exit = await PluginCommandConsole.RunAsync(_home, ["acme.tickets", "login"], terminal, CancellationToken.None, Start(Login));

        Assert.Equal(0, exit);
        Assert.Equal([true], hidden);
        Assert.Equal(2, _frames.Count);
        Assert.Contains("\"from\":\"terminal\"", _frames[1], StringComparison.Ordinal);
        Assert.Contains("Open: https://tickets.example/tokens", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("The token is kept.", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Token, output + error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stdin_answers_each_ask_unhidden_and_a_session_s_terminal_is_refused()
    {
        Install();
        var hidden = new List<bool>();
        var piped = new PluginCommandConsole.Terminal(TextWriter.Null, TextWriter.Null, masked => { hidden.Add(masked); return Token; }, _ => null);
        Assert.Equal(0, await PluginCommandConsole.RunAsync(_home, ["acme.tickets", "login", "--stdin"], piped, CancellationToken.None, Start(Login)));
        Assert.Equal([false], hidden);

        var error = new StringWriter();
        var session = new PluginCommandConsole.Terminal(TextWriter.Null, error, _ => Token,
            name => name == DriverChoicesCommand.SessionVariable ? "s-1" : null);
        Assert.Equal(1, await PluginCommandConsole.RunAsync(_home, ["acme.tickets", "login"], session, CancellationToken.None, Start(Login)));
        Assert.Contains(DriverChoicesCommand.SessionsTerminal, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_command_not_declared_or_switched_off_is_refused_before_any_process_starts()
    {
        Install();
        var error = new StringWriter();
        var terminal = new PluginCommandConsole.Terminal(TextWriter.Null, error, _ => null, _ => null);

        Assert.Equal(2, await PluginCommandConsole.RunAsync(_home, ["acme.tickets", "deploy"], terminal, CancellationToken.None, Start(Login)));
        Assert.Contains("has no command `deploy`: one of login, status", error.ToString(), StringComparison.Ordinal);

        PluginSwitches.Set(_home, "acme.tickets", PluginSwitches.Commands, PluginPlace.MachineKey, on: false);
        Assert.Equal(2, await PluginCommandConsole.RunAsync(_home, ["acme.tickets", "status"], terminal, CancellationToken.None, Start(Login)));
        Assert.Contains("commands are switched off", error.ToString(), StringComparison.Ordinal);
        Assert.Empty(_frames);
        Assert.True(PluginCommandConsole.Names(_home, "acme.tickets"));
    }

    [Fact]
    public async Task An_identity_s_api_token_is_adopted_once_and_the_log_names_no_value()
    {
        Install();
        var identity = GitIdentityHttps.AddHttps(_home, "tickets.example", "Work", "alice", null, null, "throwaway-https-token");
        GitIdentityTokens.Set(_home, identity.Id, GitTokens.Api, Token);
        var handed = new List<IReadOnlyDictionary<string, string>>();
        using (var log = new MachineLog(_home, "desktop", () => DateTimeOffset.UtcNow))
        {
            PluginTokenAdoption.Round round = (_, command, answers, _) =>
            {
                handed.Add(answers);
                return Task.FromResult(new CommandRound(["kept"], null, null, null, 0));
            };
            await PluginTokenAdoption.RunAsync(_home, log, CancellationToken.None, round);
            await PluginTokenAdoption.RunAsync(_home, log, CancellationToken.None, round);
        }

        Assert.Equal(Token, Assert.Single(handed)["token"]);
        Assert.False(GitTokens.Has(_home, identity.Id, GitTokens.Api));
        var logged = string.Join("\n", Directory.EnumerateFiles(DaorisHome.Entry(_home, HomeEntry.Logs), "*", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.Contains("plugin.token.adopted", logged, StringComparison.Ordinal);
        Assert.DoesNotContain(Token, logged, StringComparison.Ordinal);
    }
}
