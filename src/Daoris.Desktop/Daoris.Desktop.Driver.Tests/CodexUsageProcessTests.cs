using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CODEXUSE1's spawn: Codex's own app server, started as a harness's reporting command is (its PATH, its account's home in
/// the toolchain's variable, the explicit command before the pin before PATH), asked one question and stopped. The server is
/// a script speaking the evidence's lines (§1), which logs the home it was started under, so a test sees which account was
/// asked without reading inside it.
/// </summary>
/// <remarks>
/// A real process, so the <c>Process</c> half (MOD8). The protocol and the reading are <c>CodexUsageTests</c>, in process.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class CodexUsageProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-codexuse-run-" + Guid.NewGuid().ToString("N")[..8]);

    private string Settings => Path.Combine(_home, "harnesses.json");

    private string Script => Path.Combine(_home, "app-server.mjs");

    private string Asked => Path.Combine(_home, "asked.log");

    private string Command => Path.Combine(_home, "agent-here");

    public CodexUsageProcessTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
        // The account's directory name says how its server behaves: `hangs` never answers, `refuses` refuses the read.
        File.WriteAllText(Script, """
            import fs from 'node:fs';
            import path from 'node:path';
            import readline from 'node:readline';
            const home = process.env.CODEX_HOME ? path.basename(process.env.CODEX_HOME) : '(own)';
            fs.appendFileSync(new URL('./asked.log', import.meta.url), home + ' ' + process.argv.slice(2).join(' ') + '\n');
            const say = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const later = (hours) => Math.round(Date.now() / 1000 + hours * 3600);
            readline.createInterface({ input: process.stdin }).on('line', (line) => {
              const frame = JSON.parse(line);
              if (home === 'hangs') return;
              if (frame.method === 'initialize') say({ id: frame.id, result: { userAgent: 'stand-in' } });
              if (frame.method === 'account/rateLimits/read') {
                if (home === 'refuses') { say({ id: frame.id, error: { code: -32600, message: 'refused' } }); return; }
                say({ method: 'account/rateLimits/updated', params: {} });
                say({ id: frame.id, result: { rateLimits: {
                  limitId: 'codex',
                  primary: { usedPercent: 1, windowDurationMins: 300, resetsAt: later(3) },
                  secondary: { usedPercent: 15, windowDurationMins: 10080, resetsAt: later(100) },
                } } });
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

    private static readonly UsageQuestion Question = new("server-stand-in", ["server-stand-in", "app-server"], CodexUsage.Question.Recorded);

    private HarnessToolchain Toolchain => new(
        Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "CODEX_HOME", ProbeByPresence: true,
        ProfileMustExist: true, Usage: Question);

    private HarnessRoster Roster() =>
        new(new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["door"] = new Adapter("door", Toolchain),
        }), Settings);

    // The explicit command names the server's harness, as `driver.json`'s `commands` may name `codex`.
    private DriverConfig Config => DriverConfig.Empty with
    {
        Adapter = "door",
        Commands = new Dictionary<string, IReadOnlyList<string>> { ["server-stand-in"] = ["node", Script] },
    };

    private string[] AskedLines() => File.Exists(Asked) ? File.ReadAllLines(Asked) : [];

    [Fact]
    public async Task An_account_s_press_starts_the_server_under_that_account_s_home_and_keeps_what_it_answered()
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "door", "account-1"));
        var roster = Roster();

        await roster.ReportAsync("door", Config, refresh: true, account: "account-1");

        Assert.Equal(["account-1 app-server"], AskedLines());
        var said = AccountWindows.SaidOf(_home, "door", "account-1", DateTimeOffset.UtcNow)!;
        Assert.Equal(0.01, said.Of("session")!.Used);
        Assert.Equal(0.15, said.Of("weekly")!.Used);
    }

    [Fact]
    public async Task A_server_that_hangs_is_unknown_once_its_patience_is_spent()
    {
        var home = HarnessSettings.ProfileHome(_home, "door", "hangs");
        var timer = Stopwatch.StartNew();

        var readings = await CodexUsage.ReadAsync(
            CodexUsage.Prepare(["node", Script, "app-server"], Toolchain, home), TimeSpan.FromSeconds(2), CancellationToken.None);

        Assert.Null(readings);
        Assert.InRange(timer.Elapsed, TimeSpan.Zero, TimeSpan.FromSeconds(20));
        Assert.Equal(["hangs app-server"], AskedLines());
    }

    [Fact]
    public async Task A_refused_read_and_a_binary_that_is_not_there_are_unknown_and_keep_nothing()
    {
        var refuses = HarnessSettings.ProfileHome(_home, "door", "refuses");

        Assert.Null(await CodexUsage.ReadAsync(
            CodexUsage.Prepare(["node", Script, "app-server"], Toolchain, refuses), TimeSpan.FromSeconds(15), CancellationToken.None));
        Assert.Null(await CodexUsage.ReadAsync(
            CodexUsage.Prepare(["daoris-no-such-agent-binary", "app-server"], Toolchain, refuses), TimeSpan.FromSeconds(15), CancellationToken.None));
        Assert.False(File.Exists(AccountWindows.PathOf(_home)));
    }
}
