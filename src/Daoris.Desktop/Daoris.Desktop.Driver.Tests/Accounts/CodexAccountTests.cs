using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CODEXACCT1 (D125's CODEXACCT1 note): the window adds a Codex account as the terminal does. Codex reaches the driver only
/// through <c>codex-acp</c>, which runs <c>codex</c> and has no sign-in of its own, and no <c>codex</c> door exists, so the
/// agent whose accounts the door runs as is declared for it: <c>codex</c>'s own binary, its <c>login</c>, its
/// <c>login status</c> and its <c>CODEX_HOME</c>, the CLI's <c>codex</c> entry's twin.
/// </summary>
/// <remarks>
/// Nothing here starts a process. The press that asks through that declaration and the sign-in that runs it are the
/// <c>Process</c> half's (<c>CodexAccountProcessTests</c>).
/// </remarks>
public sealed class CodexAccountTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-codexacct-" + Guid.NewGuid().ToString("N")[..8]);

    public CodexAccountTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private HarnessRoster Roster() => new(AdapterSet.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses));

    /// <summary>
    /// The door's accounts are asked and signed in by <c>codex</c>, as <c>claude-code-acp</c>'s are by <c>claude-code</c>
    /// (AGT7): the account agent is <c>codex</c>, with its own binary, never the door's <c>codex-acp</c>, and the door keeps
    /// no sign-in of its own.
    /// </summary>
    [Fact]
    public void The_door_onto_codex_signs_in_and_asks_with_codex_s_own_binary()
    {
        var agent = Roster().AccountAgentOf("codex-acp")!.Value;

        Assert.Equal("codex", agent.Name);
        Assert.Equal(["codex"], agent.Toolchain.Binary);
        Assert.Equal("CODEX_HOME", agent.Toolchain.ProfileVariable);
        // CODEXLOGIN1: the browser sign-in first, as ChatGPT's desktop app signs in, on the ports 0.160.0 tries in order; the
        // device code beside it, since a workspace may refuse either.
        Assert.Equal(["login"], agent.Toolchain.LoginArguments!);
        Assert.Equal(["login", "--device-auth"], agent.Toolchain.DeviceLoginArguments!);
        Assert.Equal([1455, 1457], agent.Toolchain.LoginPorts!);
        Assert.Equal(["login", "status"], agent.Toolchain.LoginCheck!.Arguments);
        Assert.Equal(["codex"], Roster().AccountToolchain("codex-acp")!.Binary);
        // The door itself still has none: it runs `codex` and reads the home `codex` signed into (ACP3).
        Assert.Null(Roster().Toolchain("codex-acp")!.LoginArguments);
        // Measured with no key spent: `--with-api-key` stores a key in Codex's own home, which D67 §1 does not carry.
        Assert.Null(agent.Toolchain.KeyVariable);
    }

    /// <summary>An agent this build carries a door of answers for itself; the declaration is only for one it does not.</summary>
    [Fact]
    public void A_door_onto_an_agent_with_a_door_of_its_own_asks_that_door()
    {
        var roster = Roster();

        Assert.Equal("claude-code", roster.AccountAgentOf("claude-code-acp")!.Value.Name);
        Assert.Equal(["claude"], roster.AccountToolchain("claude-code-acp")!.Binary);
        Assert.Equal(["auth", "login"], roster.AccountToolchain("claude-code-acp")!.LoginArguments!);
        Assert.Equal("claude-code", roster.AccountAgentOf("claude-code")!.Value.Name);
        Assert.Equal("dsh", roster.AccountAgentOf("dsh")!.Value.Name);
        Assert.Null(AdapterSet.Built().Holder("claude-code"));
        Assert.NotNull(AdapterSet.Built().Holder("codex"));
    }

    /// <summary>
    /// What <c>codex login status</c> prints, measured with 0.160.0 on 2026-10-08 (exit 0 either way, so the words are the
    /// answer): the CLI's own rows (<c>toolchain.test.ts</c>'s login questions), each read as the CLI reads it. A sentence
    /// that contains "logged in" is not signed in, and a warning line before the answer moves nothing.
    /// </summary>
    [Theory]
    [InlineData("Logged in using ChatGPT", LoginState.In)]
    [InlineData("Not logged in", LoginState.Out)]
    [InlineData("WARNING: something\nNot logged in", LoginState.Out)]
    [InlineData("WARNING: something\nLogged in using ChatGPT", LoginState.In)]
    [InlineData("Logged in using ChatGPT\n", LoginState.In)]
    [InlineData("", LoginState.Unknown)]
    [InlineData("error: unexpected argument 'status' found", LoginState.Unknown)]
    public void Codex_s_status_words_are_read_as_the_cli_reads_them(string output, LoginState expected)
    {
        var said = Roster().AccountToolchain("codex-acp")!.LoginCheck!.Read(output);

        Assert.Equal(expected, said.Login);
        // Codex names nobody in its answer, so the account is named by the person (ACCT2), never guessed.
        Assert.Null(said.Account);
    }

    /// <summary>
    /// The twin (<c>.claude/knowledge/twins.md</c>, harnesses and accounts): the driver's declaration is the CLI's
    /// <c>codex</c> entry, read from its source, so <c>daoris agent login codex --new</c> and the window's *Add an
    /// account…* run one sign-in and ask one question. A JavaScript pattern's <c>m</c> is the driver's <c>(?m)</c>, and its
    /// <c>i</c> the driver's ignoring case, which every status question is matched with.
    /// </summary>
    [Fact]
    public void The_declaration_is_the_cli_s_codex_entry()
    {
        var source = File.ReadAllText(SourceFiles.Cli("toolchain.ts"));
        var start = source.IndexOf("\n  codex: {", StringComparison.Ordinal);
        Assert.True(start >= 0, "the CLI declares no `codex` entry");
        var entry = source[start..source.IndexOf("\n  },", start, StringComparison.Ordinal)];
        var declared = Roster().AccountToolchain("codex-acp")!;

        Assert.Equal(Words(Field(entry, @"\n    binary: \[(?<v>[^\]]*)\]")), declared.Binary);
        Assert.Equal(Field(entry, @"\n    profileVariable: '(?<v>[^']*)'"), declared.ProfileVariable);
        Assert.Equal(Words(Field(entry, @"\n    login: \[(?<v>[^\]]*)\]")), declared.LoginArguments!);
        // CODEXLOGIN1: the second way and the browser's ports, as the terminal declares them.
        Assert.Equal(Words(Field(entry, @"\n    deviceLogin: \[(?<v>[^\]]*)\]")), declared.DeviceLoginArguments!);
        Assert.Equal(
            Words(Field(entry, @"\n    loginPorts: \[(?<v>[^\]]*)\]")).Select(int.Parse), declared.LoginPorts!);

        var check = Regex.Match(
            entry, @"\n    loginCheck: \{ args: \[(?<args>[^\]]*)\], in: /(?<in>.+?)/(?<inFlags>[a-z]*), out: /(?<out>.+?)/(?<outFlags>[a-z]*) \}");
        Assert.True(check.Success, "the CLI's `codex` entry declares no login question this test reads");
        Assert.Equal(Words(check.Groups["args"].Value), declared.LoginCheck!.Arguments);
        Assert.Equal(Pattern(check.Groups["in"].Value, check.Groups["inFlags"].Value), declared.LoginCheck.LoggedIn);
        Assert.Equal(Pattern(check.Groups["out"].Value, check.Groups["outFlags"].Value), declared.LoginCheck.LoggedOut);
        // The CLI reads nobody's name from Codex's answer either.
        Assert.Null(declared.LoginCheck.Account);
    }

    /// <summary>
    /// CODEXACCT2: what <c>codex login --device-auth</c> prints reaches the window's console as plain lines, as Claude
    /// Code's sign-in does: its colours gone, the link a line of its own for the page to offer, and the one-time code the
    /// line after the sentence that asks for it. The words are those measured with 0.160.0 (D125's CODEXACCT2 note), shaped
    /// as Codex prints them, with a colour around the version, the link, the code and the notes; the code is invented.
    /// </summary>
    [Fact]
    public async Task Codex_s_device_code_reaches_the_console_as_plain_lines_with_its_link_and_code()
    {
        const string printed =
            "\nWelcome to Codex [v\u001b[90m0.160.0\u001b[0m]\n\u001b[90mOpenAI's command-line coding agent\u001b[0m\n"
            + "\nFollow these steps to sign in with ChatGPT using device code authorization:\n"
            + "\n1. Open this link in your browser and sign in to your account\n   \u001b[94mhttps://auth.openai.com/codex/device\u001b[0m\n"
            + "\n2. Enter this one-time code \u001b[90m(expires in 15 minutes)\u001b[0m\n   \u001b[94mABCD-12345\u001b[0m\n"
            + "\n\u001b[90mDevice codes are a common phishing target. Never share this code.\u001b[0m\n";
        var lines = new List<string>();

        await HarnessActions.PumpAsync(new StringReader(printed), lines.Add, CancellationToken.None);

        Assert.DoesNotContain(lines, line => line.Contains('\u001b'));
        Assert.Contains("Follow these steps to sign in with ChatGPT using device code authorization:", lines);
        Assert.Contains("   https://auth.openai.com/codex/device", lines);
        var asked = lines.IndexOf("2. Enter this one-time code (expires in 15 minutes)");
        Assert.True(asked >= 0, string.Join('\n', lines));
        Assert.Equal("   ABCD-12345", lines[asked + 1]);
    }

    // ——— Which `codex` a sign-in runs (D57 rule 4): the command named for it, then its pin, then PATH's, as its status
    // question asks. The owner's install pins Codex and has none on PATH, so a sign-in that ran PATH's could not start.

    private string Pin(string version)
    {
        var binary = Path.Combine(
            HarnessSettings.ManagedHome(_home, "codex", version), "bin", OperatingSystem.IsWindows() ? "codex.exe" : "codex");
        Directory.CreateDirectory(Path.GetDirectoryName(binary)!);
        File.WriteAllText(binary, "");
        return binary;
    }

    private void Pinned(string version) => new HarnessSettings().WithVersion("codex", version).Save(DaorisHome.Entry(_home, HomeEntry.Harnesses));

    private string Fresh => HarnessSettings.ProfileHome(_home, "codex", "acct-0a1b2c3d");

    /// <summary>
    /// Only a pin, no command named: the sign-in's start names the pinned <c>codex</c> and its browser sign-in first
    /// (CODEXLOGIN1), into its new folder; asked for, its device code (CODEXACCT2).
    /// </summary>
    [Fact]
    public void A_sign_in_with_only_a_pin_runs_the_pinned_codex_into_its_new_folder()
    {
        var binary = Pin("0.160.0");
        Pinned("0.160.0");
        var roster = Roster();

        var run = roster.SignInCommand("codex-acp", DriverConfig.Empty)!;
        var info = HarnessActions.PrepareLogin(roster.AccountToolchain("codex-acp")!, run, Fresh, fresh: true);
        var device = HarnessActions.PrepareLogin(roster.AccountToolchain("codex-acp")!, run, Fresh, fresh: true, SignInWay.DeviceCode);

        Assert.Equal(binary, run.Managed);
        Assert.Equal(binary, info.FileName);
        Assert.Equal(["login"], info.ArgumentList);
        Assert.Equal(["login", "--device-auth"], device.ArgumentList);
        Assert.Equal(Fresh, info.Environment["CODEX_HOME"]);
        Assert.True(Directory.Exists(Fresh));
    }

    /// <summary>
    /// A device code asked of an agent that has none (Claude Code's own flow is its one way) is refused before anything
    /// starts, with no folder made; the words name the two ways a press may send.
    /// </summary>
    [Fact]
    public void A_device_code_asked_of_an_agent_with_none_is_refused_and_makes_nothing()
    {
        var claude = Roster().AccountToolchain("claude-code")!;
        var run = new HarnessCommand("claude-code", ["claude"], null, null);

        var refusal = Assert.Throws<DriverException>(() =>
            HarnessActions.PrepareLogin(claude, run, Fresh, fresh: true, SignInWay.DeviceCode));

        Assert.Equal("`claude-code` signs in by no device code, so nothing was signed in — its own sign-in is its one way.", refusal.Message);
        Assert.False(Directory.Exists(Fresh));
        Assert.Equal(SignInWay.Browser, SignInWays.Of(null));
        Assert.Equal(SignInWay.DeviceCode, SignInWays.Of("device-code"));
        Assert.Equal("`by-phone` is no way to sign in — `browser` or `device-code`.", Assert.Throws<DriverException>(() => SignInWays.Of("by-phone")).Message);
    }

    // ——— CODEXLOGIN1: the browser sign-in's ports, tried for a moment, never read from Windows' table.

    /// <summary>
    /// A port another program holds reads as held, and one nobody holds as free, by taking it on the loopback address as
    /// <c>codex login</c> does; taking it leaves it free for the tool.
    /// </summary>
    [Fact]
    public void A_port_is_tried_for_a_moment_and_one_another_program_holds_reads_as_held()
    {
        var holder = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        holder.Start();
        var held = ((System.Net.IPEndPoint)holder.LocalEndpoint).Port;
        try
        {
            Assert.Equal(PortHold.InUse, SignInPorts.Hold(held));
        }
        finally
        {
            holder.Stop();
        }

        Assert.Equal(PortHold.Free, SignInPorts.Hold(held));
        // Let go at once: the tool that starts next takes it.
        Assert.Equal(PortHold.Free, SignInPorts.Hold(held));
    }

    /// <summary>
    /// Where 1455 is held and 1457 free, Codex 0.160.0 takes 1457 (measured), so the browser sign-in starts; only where both
    /// are held is it refused, in plain words naming each holder, with the device code offered. The CLI's
    /// <c>portsHeld</c> says these words to the byte (<c>toolchain.test.ts</c>).
    /// </summary>
    [Theory]
    [InlineData(PortHold.InUse, PortHold.Free, null)]
    [InlineData(PortHold.Free, PortHold.InUse, null)]
    [InlineData(PortHold.InUse, PortHold.Reserved,
        "`codex login` waits for your browser on port 1455, else 1457, and neither can be taken now: 1455 is held by another "
        + "program; 1457 is held by a range Windows reserves, which a restart may move. Nothing was signed in — sign in with a "
        + "device code instead.")]
    [InlineData(PortHold.Reserved, PortHold.Refused,
        "`codex login` waits for your browser on port 1455, else 1457, and neither can be taken now: 1455 is held by a range "
        + "Windows reserves, which a restart may move; 1457 is refused to Daoris. Nothing was signed in — sign in with a device "
        + "code instead.")]
    public void The_browser_sign_in_is_refused_only_where_none_of_its_ports_can_be_taken(PortHold first, PortHold second, string? said)
    {
        var asked = new List<int>();
        var codex = Roster().AccountToolchain("codex-acp")!;

        var refusal = SignInPorts.Refusal("codex", codex, port =>
        {
            asked.Add(port);
            return port == 1455 ? first : second;
        });

        Assert.Equal(said, refusal);
        Assert.Equal(first == PortHold.Free ? [1455] : [1455, 1457], asked);
        // An agent that waits on no port is never refused for one, and nothing is tried.
        Assert.Null(SignInPorts.Refusal("claude-code", Roster().AccountToolchain("claude-code")!, _ => throw new InvalidOperationException()));
    }

    /// <summary>
    /// What <c>codex login</c> 0.160.0 printed on its error stream, measured on 2026-10-21 under a throwaway home and stopped
    /// before any sign-in (D125's CODEXLOGIN1 note): where it listens, then the page to open, then the other way. The page's
    /// address is the measured one with every value of its query replaced. It reaches the console as plain lines, and the
    /// link to open is the one on <c>auth.openai.com</c>, never the local server's.
    /// </summary>
    [Fact]
    public async Task Codex_s_browser_sign_in_reaches_the_console_as_plain_lines_with_its_page()
    {
        const string page = "https://auth.openai.com/oauth/authorize?response_type=code&client_id=CLIENT"
            + "&redirect_uri=http%3A%2F%2F127.0.0.1%3A1455%2Fauth%2Fcallback&code_challenge=CHALLENGE&code_challenge_method=S256"
            + "&state=STATE&scope=openid+profile+email+offline_access&originator=codex_cli_rs";
        var printed = "Starting local login server on http://localhost:1455.\n"
            + "If your browser did not open, navigate to this URL to authenticate:\n\n" + page + "\n\n"
            + "On a remote or headless machine? Use `codex login --device-auth` instead.\n";
        var lines = new List<string>();

        await HarnessActions.PumpAsync(new StringReader(printed), lines.Add, CancellationToken.None);

        Assert.Equal(
            ["Starting local login server on http://localhost:1455.", "If your browser did not open, navigate to this URL to authenticate:",
             "", page, "", "On a remote or headless machine? Use `codex login --device-auth` instead."],
            lines);
    }

    /// <summary>A command named for <c>codex</c> has the last word over its pin, as at every spawn.</summary>
    [Fact]
    public void A_command_named_for_codex_signs_in_before_its_pin()
    {
        Pin("0.160.0");
        Pinned("0.160.0");
        var named = Path.Combine(_home, "named-codex.exe");
        File.WriteAllText(named, "");
        var config = DriverConfig.Empty with { Commands = new Dictionary<string, IReadOnlyList<string>> { ["codex"] = [named] } };

        var run = Roster().SignInCommand("codex-acp", config)!;

        Assert.Equal([named], run.Run);
        Assert.Null(run.Managed);
    }

    /// <summary>Pinned with nothing installed at the pin: refused, naming the pin, with no folder made and never PATH's.</summary>
    [Fact]
    public void A_sign_in_pinned_to_a_version_not_installed_is_refused_and_makes_nothing()
    {
        Pinned("0.160.0");
        var roster = Roster();

        var refusal = Assert.Throws<DriverException>(() => HarnessActions.PrepareLogin(
            roster.AccountToolchain("codex-acp")!, roster.SignInCommand("codex-acp", DriverConfig.Empty)!, Fresh, fresh: true));

        Assert.Contains("`codex` is pinned to 0.160.0 on this machine, and nothing is installed at that version", refusal.Message);
        Assert.Contains("`daoris agent pin codex 0.160.0` installs it", refusal.Message);
        Assert.False(Directory.Exists(Fresh));
    }

    // ——— A start on a door whose owner is declared, not carried: the walk reads the owner's accounts in the door's report,
    // and asks one through the owner's question. A stand-in answers in place of the agent, so no process starts.

    // The system's clock, which a sign-in's mark is read by: a word older than the backstop is asked again (TOOL6g).
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly List<string> _asked = [];

    private sealed class Door(string name, HarnessToolchain toolchain) : ISessionAdapter
    {
        public string Name => name;

        public SessionWire Wire => SessionWire.Acp;

        public HarnessToolchain? Toolchain => toolchain;

        public System.Diagnostics.ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new("unused");
    }

    /// <summary>`fake-door` runs as `fake`'s accounts, and no `fake` door exists: `fake` is declared, as `codex` is.</summary>
    private HarnessRoster HolderRoster()
    {
        var command = Path.Combine(_home, "door-here");
        File.WriteAllText(command, "");
        return new HarnessRoster(
            new AdapterSet(
                new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
                {
                    ["fake-door"] = new Door("fake-door", new HarnessToolchain(
                        Binary: [command], VersionArguments: [], ProfileVariable: "FAKE_HOME", AccountOf: "fake", ProbeByPresence: true)),
                },
                new Dictionary<string, HarnessToolchain>(StringComparer.OrdinalIgnoreCase)
                {
                    ["fake"] = new(
                        Binary: ["fake-agent-never-run"], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME",
                        LoginArguments: ["login"], LoginCheck: new LoginQuestion(["login", "status"], "^in", "^out")),
                }),
            DaorisHome.Entry(_home, HomeEntry.Harnesses))
        {
            Clock = () => Now,
            Asking = (owner, account, _) =>
            {
                lock (_asked) _asked.Add($"{owner}/{account}");
                return Task.FromResult(LoginState.In);
            },
        };
    }

    private static DriverConfig HolderConfig => DriverConfig.Empty with { Adapter = "fake-door" };

    private void HolderAccounts(params string[] names)
    {
        foreach (var name in names) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", name));
    }

    /// <summary>
    /// An account whose word that it is signed out is past the backstop is asked again at a start (TOOL6g), through the
    /// declared owner: the walk reads it in the door's report, where it asked a report of the owner, which is no adapter.
    /// </summary>
    [Fact]
    public async Task A_start_on_a_door_onto_a_declared_owner_asks_a_signed_out_account_again_through_the_owner()
    {
        HolderAccounts("account-1");
        new HarnessSettings().WithDefault("fake", "account-1").Save(DaorisHome.Entry(_home, HomeEntry.Harnesses));
        AccountReads.Keep(_home, "fake", "account-1", LoginState.Out, Now - HarnessRoster.SignedOutAskedAgain - TimeSpan.FromMinutes(5));

        var selection = await HolderRoster().SelectAsync("fake-door", HolderConfig, null, null);

        Assert.True(selection.Allowed, selection.Refusal);
        Assert.Equal("account-1", selection.Profile);
        Assert.Equal(["fake/account-1"], _asked);
        Assert.Equal(LoginState.In, AccountReads.Of(_home, "fake").Accounts["account-1"].Login);
    }

    /// <summary>A pick that is cooling is refused naming the owner's accounts that are ready (D125 §3.3), read in the door's report.</summary>
    [Fact]
    public async Task A_cooling_pick_on_a_door_onto_a_declared_owner_names_the_accounts_ready_instead()
    {
        HolderAccounts("account-1", "account-2");
        AccountCooling.Cool(_home, new CoolingEntry("fake", "account-1", Now.AddHours(3), true, "session", Now, "s-1"), Now);

        var selection = await HolderRoster().SelectAsync("fake-door", HolderConfig, null, "account-1");

        Assert.False(selection.Allowed);
        Assert.Contains("account-2", selection.Refusal);
        Assert.Empty(_asked);
    }

    private static string Field(string entry, string pattern)
    {
        var match = Regex.Match(entry, pattern);
        Assert.True(match.Success, $"the CLI's `codex` entry has nothing matching {pattern}");
        return match.Groups["v"].Value;
    }

    /// <summary>A TypeScript list of quoted words: <c>'login', 'status'</c>.</summary>
    private static string[] Words(string list) =>
        [.. list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(word => word.Trim('\''))];

    /// <summary>A JavaScript pattern as the driver writes it: case ignored where the CLI's ignores it, which the driver always does.</summary>
    private static string Pattern(string source, string flags)
    {
        Assert.Contains('i', flags);
        Assert.All(flags, flag => Assert.Contains(flag, "im"));
        return (flags.Contains('m') ? "(?m)" : "") + source;
    }
}
