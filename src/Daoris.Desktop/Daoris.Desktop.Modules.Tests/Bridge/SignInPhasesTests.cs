using System.Text.Json;
using Daoris.Desktop.Modules;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// SIGNIN1: a sign-in says where it is (the UI/UX design §3.2, R2). The driver writes each phase it performs or observes, with
/// its start, into the relay the tool's output takes (<c>Starting</c>, <c>Waiting on your browser</c>, <c>Checking who signed
/// in</c>, <c>Joining the lists</c>), keeps the run's lines so a panel mounted late reads them, and tells the action's life to
/// the machine log by its phases' times, never a link, a code, an email or an account.
/// </summary>
/// <remarks>
/// Codex's is a faithful stand-in for <c>codex login --device-auth</c> 0.160.0 as D125's CODEXACCT2 note measured it: one
/// write with the version, the link and the code in colour, then a wait in which it reads nothing on its input and polls until
/// the person has entered the code on the maker's page (here, a file the test writes), then <i>Successfully logged in</i> on
/// its error stream and exit 0. Real processes, so the <c>Process</c> half (MOD8).
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class SignInPhasesTests : Bridge
{
    private const string Link = "https://auth.openai.com/codex/device";

    private const string Code = "ABCD-12345";

    private const string Page = "https://auth.openai.com/oauth/authorize";

    private readonly MachineLog _log;

    public SignInPhasesTests() => _log = new MachineLog(Home, "desktop");

    // The browser's ports are tried by a stand-in that finds them free (CODEXLOGIN1), never this machine's 1455 and 1457.
    private DriverModule Module()
    {
        var loop = new DriverLoop(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:0", log: _log);
        loop.Harnesses.Ports = _ => PortHold.Free;
        return new DriverModule(Bus, loop);
    }

    private string Granted => Path.Combine(Home, "granted");

    // Each stand-in process writes its id here as it starts, so a test can say whether the one stopped is gone.
    private string Pids => Path.Combine(Home, "pids");

    private void Codex()
    {
        var script = Path.Combine(Home, "codex.mjs");
        File.WriteAllText(script, $$"""
            import fs from 'node:fs';
            import path from 'node:path';
            import { createServer } from 'node:http';
            const home = process.env.CODEX_HOME;
            const [verb, sub] = process.argv.slice(2);
            if (verb === '--version') { console.log('codex-cli 0.160.0'); process.exit(0); }
            if (verb === 'login' && sub === 'status') {
              console.log(home && fs.existsSync(path.join(home, 'auth.json')) ? 'Logged in using ChatGPT' : 'Not logged in');
              process.exit(0);
            }
            fs.appendFileSync(new URL('./pids', import.meta.url), `${process.argv.slice(2).join(' ')}=${process.pid}\n`);
            if (verb === 'login' && process.argv.length === 3 && home) {
              // CODEXLOGIN1, as 0.160.0 prints it (D125's note), on its error stream: where it listens, the page to open, the
              // other way; then it waits for the browser's answer on its port, here any free one, and the test is the browser.
              const server = createServer((req, res) => {
                const asked = new URL(req.url, 'http://127.0.0.1');
                if (asked.pathname !== '/auth/callback') { res.writeHead(404); res.end(); return; }
                res.writeHead(200);
                res.end('Signed in');
                fs.writeFileSync(path.join(home, 'auth.json'), '{}');
                console.error('Successfully logged in');
                process.exit(0);
              });
              server.listen(0, '127.0.0.1', () => {
                const port = server.address().port;
                process.stderr.write(
                  `Starting local login server on http://localhost:${port}.\nIf your browser did not open, navigate to this URL to authenticate:\n\n`
                  + `{{Page}}?response_type=code&redirect_uri=${encodeURIComponent(`http://127.0.0.1:${port}/auth/callback`)}&state=STATE\n\n`
                  + 'On a remote or headless machine? Use `codex login --device-auth` instead.\n');
              });
            } else if (verb === 'login' && sub === '--device-auth' && process.argv.length === 4 && home) {
              const gray = '\x1b[90m', blue = '\x1b[94m', reset = '\x1b[0m';
              process.stdout.write(
                `\nWelcome to Codex [v${gray}0.160.0${reset}]\n${gray}OpenAI's command-line coding agent${reset}\n\n`
                + 'Follow these steps to sign in with ChatGPT using device code authorization:\n\n'
                + `1. Open this link in your browser and sign in to your account\n   ${blue}{{Link}}${reset}\n\n`
                + `2. Enter this one-time code ${gray}(expires in 15 minutes)${reset}\n   ${blue}{{Code}}${reset}\n\n`
                + `${gray}Device codes are a common phishing target. Never share this code.${reset}\n\n`);
              const granted = new URL('./granted', import.meta.url);
              const began = Date.now();
              const poll = setInterval(() => {
                if (fs.existsSync(granted)) {
                  clearInterval(poll);
                  fs.writeFileSync(path.join(home, 'auth.json'), '{}');
                  console.error('Successfully logged in');
                  process.exit(0);
                }
                if (Date.now() - began > 60000) { console.error('Error logging in with device code: device auth timed out'); process.exit(1); }
              }, 50);
            } else {
              process.exit(1);
            }
            """);
        File.WriteAllText(DriverConfigPath, $$"""
            { "drivable": [], "holds": [], "cap": 1, "adapter": "codex-acp",
              "commands": { "codex": ["node", {{JsonSerializer.Serialize(script)}}], "codex-acp": ["node", {{JsonSerializer.Serialize(script)}}] } }
            """);
    }

    /// <summary>
    /// *Add an account…* on Codex through its faithful stand-in: the link and the code reach the relay while the tool waits,
    /// the phases so far are there for a panel mounted late, and once the person has entered the code the sign-in finishes
    /// and keeps the account, its four phases written in order.
    /// </summary>
    [Fact]
    public async Task A_codex_device_code_sign_in_shows_its_code_while_it_waits_and_finishes_once_the_code_is_entered()
    {
        Codex();
        var module = Module();

        var answer = await AnswerAsync(module, "HARNESS_ACTION", new { harness = "codex-acp", action = "login-new", way = "device-code" });
        Assert.True(answer.GetProperty("started").GetBoolean());
        var made = answer.GetProperty("profile").GetString()!;
        await UntilAsync(() => Texts().Any(text => text.Contains(Code, StringComparison.Ordinal)));

        // While it waits on the person: started and its process up, and the link and the code as plain lines.
        Assert.Equal(["start", "browser"], Phases());
        Assert.Contains($"   {Link}", Texts());
        Assert.Contains($"   {Code}", Texts());

        // A panel mounted now reads the run so far, its phases with it: an action has no session, and its relay keeps its lines.
        var tail = await AnswerAsync(module, "TAIL_SESSION", new { id = "codex-acp:login-new" });
        var held = tail.GetProperty("lines").EnumerateArray().Select(line => line.GetProperty("text").GetString()!).ToList();
        Assert.Contains($"   {Code}", held);
        Assert.Equal(["start", "browser"], held.Select(PhaseOf).OfType<string>());
        Assert.True(tail.GetProperty("live").GetBoolean());

        File.WriteAllText(Granted, "");
        await UntilAsync(() => Raised.Any(m => m.Type == "HARNESS_ENDED"));

        var ended = JsonSerializer.SerializeToElement(Raised.Single(m => m.Type == "HARNESS_ENDED").Payload);
        Assert.Equal(0, ended.GetProperty("ExitCode").GetInt32());
        Assert.True(ended.GetProperty("Kept").GetBoolean());
        Assert.False(ended.GetProperty("Cancelled").GetBoolean());
        Assert.True(File.Exists(Path.Combine(DaorisHome.Entry(Home, HomeEntry.AgentHomes, "codex", made), "auth.json")));
        Assert.Equal(["start", "browser", "who", "lists"], Phases());
        Assert.Contains("Successfully logged in", Texts());

        var life = LogLines().Where(line => line.GetProperty("event").GetString()!.StartsWith("harness.", StringComparison.Ordinal)).ToList();
        Assert.Equal(["harness.started", "harness.prompted", "harness.ended"], life.Select(line => line.GetProperty("event").GetString()));
        Assert.All(life, line => Assert.Equal(
            ("codex-acp", "login-new"),
            (line.GetProperty("data").GetProperty("harness").GetString(), line.GetProperty("data").GetProperty("action").GetString())));
        var end = life[2].GetProperty("data");
        Assert.Equal("done", end.GetProperty("outcome").GetString());
        Assert.Equal(0, end.GetProperty("code").GetInt32());
        foreach (var phase in new[] { "startMs", "toolMs", "whoMs", "listsMs", "ms" })
        {
            Assert.True(end.GetProperty(phase).GetInt64() >= 0, phase);
        }

        Assert.True(life[1].GetProperty("data").GetProperty("ms").GetInt64() >= 0);
        NothingOfThePersonIn(made);
    }

    /// <summary>
    /// A sign-in back in to an account that is here (ACCT1) checks who signed in before it joins the lists, so the phases run in
    /// the design's order; and a sign-in the tool fails ends at its second phase, said to the log as failed by its exit code.
    /// </summary>
    [Fact]
    public async Task A_sign_in_to_an_account_here_checks_who_then_joins_and_a_failed_one_stops_at_the_browser()
    {
        var script = Path.Combine(Home, "harness.mjs");
        File.WriteAllText(script, """
            import { existsSync, readFileSync, writeFileSync } from 'node:fs';
            const home = process.env.DAORIS_STUB_CONFIG_DIR;
            if (process.argv[2] === '--version') { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv[2] === '--login-state') {
              const signed = home && existsSync(home + '/credentials.json');
              console.log(signed ? 'logged-in as ' + readFileSync(home + '/credentials.json', 'utf8').trim() : 'logged-out');
              process.exit(0);
            }
            if (process.argv[2] === '--login') {
              if (existsSync(new URL('./fail', import.meta.url))) process.exit(3);
              writeFileSync(home + '/credentials.json', 'person@example.invalid');
              process.exit(0);
            }
            """);
        File.WriteAllText(DriverConfigPath, $$"""
            { "drivable": [], "holds": [], "cap": 1, "adapter": "stub",
              "commands": { "stub": ["node", {{JsonSerializer.Serialize(script)}}] } }
            """);
        var module = Module();
        await AnswerAsync(module, "HARNESS_ACTION", new { harness = "stub", action = "profile-add", profile = "work" });

        await AnswerAsync(module, "HARNESS_ACTION", new { harness = "stub", action = "login", profile = "work" });
        await UntilAsync(() => Raised.Count(m => m.Type == "HARNESS_ENDED") == 1);
        Assert.Equal(["start", "browser", "who", "lists"], Phases());

        File.WriteAllText(Path.Combine(Home, "fail"), "");
        await AnswerAsync(module, "HARNESS_ACTION", new { harness = "stub", action = "login", profile = "work" });
        await UntilAsync(() => Raised.Count(m => m.Type == "HARNESS_ENDED") == 2);

        // The second run's phases follow its own start: the relay's numbers go on, so a panel that stayed up merges them.
        Assert.Equal(["start", "browser", "who", "lists", "start", "browser"], Phases());
        var tail = await AnswerAsync(module, "TAIL_SESSION", new { id = "stub:login" });
        Assert.Equal(["start", "browser"], tail.GetProperty("lines").EnumerateArray()
            .Select(line => PhaseOf(line.GetProperty("text").GetString()!)).OfType<string>());
        var ends = LogLines().Where(line => line.GetProperty("event").GetString() == "harness.ended")
            .Select(line => line.GetProperty("data")).ToList();
        Assert.Equal(["done", "failed"], ends.Select(end => end.GetProperty("outcome").GetString()));
        Assert.Equal(3, ends[1].GetProperty("code").GetInt32());
        Assert.Equal(JsonValueKind.Null, ends[1].GetProperty("whoMs").ValueKind);
        Assert.Equal(JsonValueKind.Null, ends[1].GetProperty("listsMs").ValueKind);
        NothingOfThePersonIn("work");
    }

    /// <summary>A sign-in stopped by its *Cancel* is said to the log as cancelled, never as the tool's failure.</summary>
    [Fact]
    public async Task A_cancelled_sign_in_is_told_to_the_log_as_cancelled()
    {
        Codex();
        var module = Module();

        await AnswerAsync(module, "HARNESS_ACTION", new { harness = "codex-acp", action = "login-new", way = "device-code" });
        await UntilAsync(() => Texts().Any(text => text.Contains(Code, StringComparison.Ordinal)));
        await AnswerAsync(module, "HARNESS_CANCEL", new { harness = "codex-acp", action = "login-new" });
        await UntilAsync(() => Raised.Any(m => m.Type == "HARNESS_ENDED"));

        var end = LogLines().Single(line => line.GetProperty("event").GetString() == "harness.ended").GetProperty("data");
        Assert.Equal("cancelled", end.GetProperty("outcome").GetString());
        // The end's news says so too, so the page closes the panel saying nothing: the person pressed it (§3.2).
        var ended = JsonSerializer.SerializeToElement(Raised.Single(m => m.Type == "HARNESS_ENDED").Payload);
        Assert.True(ended.GetProperty("Cancelled").GetBoolean());
        Assert.False(ended.GetProperty("Switched").GetBoolean());
    }

    /// <summary>
    /// CODEXLOGIN1, the browser with its port free: *Add an account…* on Codex runs <c>codex login</c> first, the page to open
    /// reaches the relay while the tool waits on its port, and once the browser comes back there the sign-in finishes and keeps
    /// the account, its four phases written in order.
    /// </summary>
    [Fact]
    public async Task A_codex_browser_sign_in_says_its_page_while_it_waits_and_finishes_once_the_browser_answers()
    {
        Codex();
        var module = Module();

        var answer = await AnswerAsync(module, "HARNESS_ACTION", new { harness = "codex-acp", action = "login-new" });
        var made = answer.GetProperty("profile").GetString()!;
        var page = await PageAsync();

        Assert.Equal(["start", "browser"], Phases());
        Assert.Contains("If your browser did not open, navigate to this URL to authenticate:", Texts());
        await BrowserAnswersAsync(page);
        await UntilAsync(() => Raised.Any(m => m.Type == "HARNESS_ENDED"));

        var ended = JsonSerializer.SerializeToElement(Raised.Single(m => m.Type == "HARNESS_ENDED").Payload);
        Assert.Equal(0, ended.GetProperty("ExitCode").GetInt32());
        Assert.True(ended.GetProperty("Kept").GetBoolean());
        Assert.True(File.Exists(Path.Combine(DaorisHome.Entry(Home, HomeEntry.AgentHomes, "codex", made), "auth.json")));
        Assert.Equal(["start", "browser", "who", "lists"], Phases());
        Assert.Contains("Successfully logged in", Texts());
        NothingOfThePersonIn(made);
    }

    /// <summary>
    /// CODEXLOGIN1, the switch mid-sign-in: a device code waits, as one a workspace refused waits out its fifteen minutes with
    /// the refusal only on the maker's page, and the person switches to the browser. The device code's process, Daoris's own
    /// child, is stopped, its end told as switched with nothing kept; the browser's runs in the same relay after its own start,
    /// and finishes. The log says the first cancelled and the second done.
    /// </summary>
    [Fact]
    public async Task A_switch_mid_sign_in_stops_the_device_code_and_the_browser_signs_in()
    {
        Codex();
        var module = Module();

        var first = await AnswerAsync(module, "HARNESS_ACTION", new { harness = "codex-acp", action = "login-new", way = "device-code" });
        var abandoned = first.GetProperty("profile").GetString()!;
        await UntilAsync(() => Texts().Any(text => text.Contains(Code, StringComparison.Ordinal)));
        var device = StandIn("login --device-auth");

        var second = await AnswerAsync(module, "HARNESS_ACTION", new { harness = "codex-acp", action = "login-new", way = "browser", @switch = true });
        Assert.True(second.GetProperty("started").GetBoolean());
        var made = second.GetProperty("profile").GetString()!;

        // The first ended before the second started, told as switched, and its process is gone with nothing kept.
        var switched = JsonSerializer.SerializeToElement(Raised.First(m => m.Type == "HARNESS_ENDED").Payload);
        Assert.True(switched.GetProperty("Switched").GetBoolean());
        Assert.True(switched.GetProperty("Cancelled").GetBoolean());
        Assert.False(switched.GetProperty("Kept").GetBoolean());
        Assert.True(Gone(device), "the device code's process still runs");
        Assert.False(Directory.Exists(DaorisHome.Entry(Home, HomeEntry.AgentHomes, "codex", abandoned)));

        await BrowserAnswersAsync(await PageAsync());
        await UntilAsync(() => Raised.Count(m => m.Type == "HARNESS_ENDED") == 2);

        var ended = JsonSerializer.SerializeToElement(Raised.Where(m => m.Type == "HARNESS_ENDED").Last().Payload);
        Assert.Equal(0, ended.GetProperty("ExitCode").GetInt32());
        Assert.True(ended.GetProperty("Kept").GetBoolean());
        Assert.False(ended.GetProperty("Switched").GetBoolean());
        Assert.Equal(["start", "browser", "start", "browser", "who", "lists"], Phases());
        Assert.Equal([made], HarnessSettings.Profiles(Home, "codex"));
        var ends = LogLines().Where(line => line.GetProperty("event").GetString() == "harness.ended")
            .Select(line => line.GetProperty("data").GetProperty("outcome").GetString()).ToList();
        Assert.Equal(["cancelled", "done"], ends);
    }

    /// <summary>The page the browser sign-in printed for the person to open, once it has.</summary>
    private async Task<string> PageAsync()
    {
        string? page = null;
        await UntilAsync(() => (page = Texts().LastOrDefault(text => text.StartsWith(Page, StringComparison.Ordinal))) is not null);
        return page!;
    }

    /// <summary>The browser coming back to the address the page names, as the maker's page sends it once the person signed in.</summary>
    private static async Task BrowserAnswersAsync(string page)
    {
        var back = new Uri(System.Web.HttpUtility.ParseQueryString(new Uri(page).Query)["redirect_uri"]!);
        using var browser = new HttpClient();
        await browser.GetAsync(new Uri(back, back.AbsolutePath + "?code=CODE&state=STATE"));
    }

    /// <summary>The process id the stand-in started with for <paramref name="arguments"/>, the latest such.</summary>
    private int StandIn(string arguments) => int.Parse(
        File.ReadAllLines(Pids).Last(line => line.StartsWith(arguments + "=", StringComparison.Ordinal))[(arguments.Length + 1)..],
        System.Globalization.CultureInfo.InvariantCulture);

    private static bool Gone(int pid)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(pid);
            return process.HasExited;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    /// <summary>The relayed lines' texts, in their order, phases included.</summary>
    private List<string> Relayed() =>
    [
        .. Raised.Where(m => m.Type == "SESSION_OUTPUT")
            .Select(m => JsonSerializer.SerializeToElement(m.Payload))
            .SelectMany(json => json.GetProperty("Lines").EnumerateArray())
            .Select(line => (Sequence: line.GetProperty("Sequence").GetInt64(), Text: line.GetProperty("Text").GetString()!))
            .OrderBy(line => line.Sequence)
            .Select(line => line.Text),
    ];

    private List<string> Texts() => [.. Relayed().Where(text => PhaseOf(text) is null)];

    private List<string> Phases() => [.. Relayed().Select(PhaseOf).OfType<string>()];

    /// <summary>A phase line's phase: the record separator, then the driver's JSON naming the phase and its start.</summary>
    private static string? PhaseOf(string text)
    {
        if (!text.StartsWith('\u001e')) return null;
        using var json = JsonDocument.Parse(text[1..]);
        Assert.True(DateTimeOffset.TryParse(json.RootElement.GetProperty("at").GetString(), out _));
        return json.RootElement.GetProperty("phase").GetString();
    }

    private List<JsonElement> LogLines()
    {
        var folder = DaorisHome.Entry(Home, HomeEntry.Logs);
        if (!Directory.Exists(folder)) return [];
        return
        [
            .. Directory.GetFiles(folder).SelectMany(path =>
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            }).Select(line => JsonDocument.Parse(line).RootElement.Clone()),
        ];
    }

    /// <summary>The machine log holds no link, no code, no email and no account: the design's §5.</summary>
    private void NothingOfThePersonIn(string account)
    {
        var raw = string.Join("\n", LogLines().Select(line => line.GetRawText()));
        Assert.DoesNotContain("https://", raw);
        Assert.DoesNotContain(Code, raw);
        Assert.DoesNotContain("@example.invalid", raw);
        Assert.DoesNotContain(account, raw);
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var patience = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < patience, "the condition never held");
            await Task.Delay(25);
        }
    }
}
