using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// USAGEREAD2a's spawn: Claude Code's native CLI, started as a harness's reporting command is (its PATH, its account's home in
/// <c>CLAUDE_CONFIG_DIR</c>, the explicit command before the pin before PATH), asked <c>get_usage</c> and stopped. The CLI is a
/// script that answers <c>control_request</c> lines on stdin as the evidence measured, does not exit by itself, and logs the
/// home and the working folder it was started under, so a test sees which account was asked, and where, without reading inside.
/// </summary>
/// <remarks>
/// A real process, so the <c>Process</c> half (MOD8). The protocol and the reading are <c>ClaudeUsageTests</c>, in process.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class ClaudeUsageProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-claudeuse-run-" + Guid.NewGuid().ToString("N")[..8]);

    private string Settings => DaorisHome.Entry(_home, HomeEntry.Harnesses);

    private string Script => Path.Combine(_home, "claude-stand-in.mjs");

    private string Asked => Path.Combine(_home, "asked.log");

    private string Command => Path.Combine(_home, "agent-here");

    public ClaudeUsageProcessTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
        // The account's directory name says how the CLI behaves: `hangs` never answers, `out` is not signed in.
        File.WriteAllText(Script, """
            import fs from 'node:fs';
            import path from 'node:path';
            import readline from 'node:readline';
            const home = process.env.CLAUDE_CONFIG_DIR ? path.basename(process.env.CLAUDE_CONFIG_DIR) : '(own)';
            fs.appendFileSync(new URL('./asked.log', import.meta.url), home + ' ' + path.basename(process.cwd()) + ' ' + process.argv.slice(2).join(' ') + '\n');
            const say = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const later = (hours) => new Date(Date.now() + hours * 3600000).toISOString();
            const reply = (id, response) => say({ type: 'control_response', response: { subtype: 'success', request_id: id, response } });
            // The CLI does not end by itself: the reader ends it.
            setInterval(() => {}, 1000);
            readline.createInterface({ input: process.stdin }).on('line', (line) => {
              const frame = JSON.parse(line);
              if (home === 'hangs' || frame.type !== 'control_request') return;
              if (frame.request.subtype === 'initialize') reply(frame.request_id, {});
              if (frame.request.subtype === 'get_usage') {
                say({ type: 'system', subtype: 'init' });
                if (home === 'out') { reply(frame.request_id, { subscription_type: null, rate_limits_available: false, rate_limits: null }); return; }
                reply(frame.request_id, {
                  subscription_type: 'team',
                  rate_limits_available: true,
                  rate_limits: {
                    five_hour: { utilization: 23, resets_at: later(3) },
                    seven_day: { utilization: 79, resets_at: later(100) },
                  },
                });
              }
            });
            """);
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private sealed class Adapter(string name, HarnessToolchain toolchain) : ISessionAdapter
    {
        public string Name => name;

        public SessionWire Wire => SessionWire.Acp;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new("unused");
    }

    private static readonly UsageQuestion Question = new(
        "claude-stand-in",
        ["claude-stand-in", "--input-format", "stream-json", "--output-format", "stream-json", "--verbose", "--strict-mcp-config"],
        ClaudeUsage.Question.Recorded,
        UsageKind.Claude);

    private const string Arguments = "--input-format stream-json --output-format stream-json --verbose --strict-mcp-config";

    private HarnessToolchain Toolchain => new(
        Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "CLAUDE_CONFIG_DIR", ProbeByPresence: true,
        ProfileMustExist: true, Usage: Question);

    private HarnessRoster Roster() =>
        new(new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["door"] = new Adapter("door", Toolchain),
        }), Settings);

    // The explicit command names the CLI's harness, as `driver.json`'s `commands` may name `claude-code`.
    private DriverConfig Config => DriverConfig.Empty with
    {
        Adapter = "door",
        Commands = new Dictionary<string, IReadOnlyList<string>> { ["claude-stand-in"] = ["node", Script] },
    };

    private string[] AskedLines() => File.Exists(Asked) ? File.ReadAllLines(Asked) : [];

    private static Func<TextReader, TextWriter, TimeSpan, CancellationToken, Task<IReadOnlyList<WindowReading>?>> Ask() =>
        (incoming, outgoing, patience, token) => ClaudeUsage.AskWithinAsync(incoming, outgoing, patience, DateTimeOffset.UtcNow, token);

    [Fact]
    public async Task An_account_s_press_starts_the_CLI_under_that_account_s_home_in_a_folder_of_its_own_and_keeps_a_read()
    {
        var account = HarnessSettings.ProfileHome(_home, "door", "account-1");
        Directory.CreateDirectory(account);
        var roster = Roster();

        await roster.ReportAsync("door", Config, refresh: true, account: "account-1");

        Assert.Equal([$"account-1 usage-read {Arguments}"], AskedLines());
        Assert.True(Directory.Exists(Path.Combine(account, "usage-read")));
        var said = AccountWindows.SaidOf(_home, "door", "account-1", DateTimeOffset.UtcNow)!;
        Assert.Equal(0.23, said.Of("session")!.Used);
        Assert.Equal(0.79, said.Of("weekly")!.Used);
        Assert.Equal(AccountWindows.SourceRead, said.Of("weekly")!.Source);
    }

    [Fact]
    public async Task A_CLI_that_hangs_is_unknown_once_its_patience_is_spent_and_its_process_is_ended()
    {
        var home = HarnessSettings.ProfileHome(_home, "door", "hangs");
        Directory.CreateDirectory(home);
        var timer = Stopwatch.StartNew();

        var readings = await CodexUsage.ReadAsync(
            CodexUsage.Prepare(["node", Script], Toolchain, home), TimeSpan.FromSeconds(2), CancellationToken.None, Ask());

        Assert.Null(readings);
        Assert.InRange(timer.Elapsed, TimeSpan.Zero, TimeSpan.FromSeconds(20));
    }

    [Fact]
    public async Task A_home_not_signed_in_and_a_binary_that_is_not_there_keep_nothing()
    {
        var signedOut = HarnessSettings.ProfileHome(_home, "door", "out");
        Directory.CreateDirectory(signedOut);

        Assert.Equal(
            [],
            await CodexUsage.ReadAsync(CodexUsage.Prepare(["node", Script], Toolchain, signedOut), TimeSpan.FromSeconds(15), CancellationToken.None, Ask()) ?? []);
        Assert.Null(await CodexUsage.ReadAsync(
            CodexUsage.Prepare(["daoris-no-such-agent-binary"], Toolchain, signedOut), TimeSpan.FromSeconds(15), CancellationToken.None, Ask()));

        await Roster().ReportAsync("door", Config, refresh: true, account: "out");
        Assert.False(File.Exists(AccountWindows.PathOf(_home)));
    }
}
