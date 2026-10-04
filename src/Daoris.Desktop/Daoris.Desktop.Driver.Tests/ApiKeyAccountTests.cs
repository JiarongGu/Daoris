using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 <b>An account that is an API key</b> (AGT3, D67 §1: Daoris keeps the key).
/// </summary>
/// <remarks>
/// <para>Measured on Claude Code 2.1.280 with an invalid key and nothing spent: the key in
/// <c>ANTHROPIC_API_KEY</c> is enough for <c>auth status</c> (logged in, by <c>api_key</c>, no email)
/// and for a non-interactive run, with no prompt (<c>docs/2026-09-23-api-key-accounts.md</c>).</para>
///
/// <para>The key is kept <b>beside</b> the account in <c>keys.json</c> under the home, never inside the
/// directory that is the tool's (D49 §4), shown back only as its last four characters, and handed to
/// the agent at spawn through the variable its toolchain declares.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class ApiKeyAccountTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-key-tests", Guid.NewGuid().ToString("N")[..8]);

    private string Settings => Path.Combine(_home, "harnesses.json");

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private sealed class FakeAdapter(HarnessToolchain toolchain) : ISessionAdapter
    {
        public string Name => "fake";

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new();

        public HarnessToolchain? Toolchain => toolchain;
    }

    /// <summary>A harness that is signed in exactly when its key variable is set — as Claude Code answers.</summary>
    private HarnessToolchain Toolchain(string? keyVariable = "FAKE_KEY")
    {
        Directory.CreateDirectory(_home);
        var script = Path.Combine(_home, "keyed.mjs");
        File.WriteAllText(script, """
            if (process.argv[2] === '--version') { console.log('keyed 1.0'); process.exit(0); }
            console.log(process.env.FAKE_KEY ? 'logged-in' : 'logged-out');
            """);
        return new HarnessToolchain(
            Binary: ["node", script],
            VersionArguments: ["--version"],
            ProfileVariable: "FAKE_HOME",
            LoginCheck: new LoginQuestion(["auth"], "logged-in", "logged-out"),
            KeyVariable: keyVariable);
    }

    private HarnessRoster Roster(HarnessToolchain toolchain) => new(
        new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["fake"] = new FakeAdapter(toolchain),
        }),
        Settings);

    private static DriverConfig Config() => DriverConfig.Empty with { Adapter = "fake" };

    [Fact]
    public void A_key_is_kept_under_the_home_beside_the_account_and_never_inside_it()
    {
        var account = HarnessKeys.Add(_home, "fake", "sk-test-0000-wxyz");

        Assert.Equal("account-1", account);
        var directory = HarnessSettings.ProfileHome(_home, "fake", account);
        Assert.True(Directory.Exists(directory));
        // The directory is the TOOL's (D49 §4): Daoris made it and put nothing in it.
        Assert.Empty(Directory.EnumerateFileSystemEntries(directory));
        Assert.Equal("sk-test-0000-wxyz", HarnessKeys.Of(_home, "fake", account));
        Assert.True(File.Exists(Path.Combine(_home, "keys.json")));
    }

    /// <summary>
    /// Twin rule 6: the FILE is the contract, and this is the shape the CLI's <c>agent key</c> writes
    /// (its test asserts the same literal).
    /// </summary>
    [Fact]
    public void The_key_file_the_CLI_writes_is_the_file_the_driver_reads()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Path.Combine(_home, HarnessKeys.FileName), """
            { "claude-code": { "account-1": "sk-ant-api03-cli-test-wxyz" } }
            """);

        Assert.Equal("sk-ant-api03-cli-test-wxyz", HarnessKeys.Of(_home, "claude-code", "account-1"));
    }

    /// <summary>Every Anthropic key begins the same way, so the tail is what tells two apart.</summary>
    [Fact]
    public void A_key_is_shown_back_only_as_its_last_four_characters()
    {
        Assert.Equal("…wxyz", HarnessKeys.Handle("sk-ant-api03-0000-wxyz"));
        Assert.Equal("…", HarnessKeys.Handle("abc"));
    }

    [Fact]
    public void A_blank_key_is_refused_and_makes_no_account()
    {
        Assert.Throws<DriverException>(() => HarnessKeys.Add(_home, "fake", "   "));
        Assert.Empty(HarnessSettings.Profiles(_home, "fake"));
    }

    /// <summary>Removing an account removes its key (D66 §3), and another account's key survives the edit.</summary>
    [Fact]
    public void Removing_an_account_removes_its_key_and_only_its_key()
    {
        var first = HarnessKeys.Add(_home, "fake", "sk-first-1111");
        var second = HarnessKeys.Add(_home, "fake", "sk-second-2222");

        HarnessSettings.RemoveProfile(_home, "fake", first);

        Assert.Null(HarnessKeys.Of(_home, "fake", first));
        Assert.Equal("sk-second-2222", HarnessKeys.Of(_home, "fake", second));
        Assert.DoesNotContain("sk-first-1111", File.ReadAllText(Path.Combine(_home, "keys.json")));
    }

    /// <summary>The key reaches the agent through the tool's own variable, on the one line both doors take.</summary>
    [Fact]
    public async Task A_key_account_spawns_with_its_key_in_the_tool_s_own_variable()
    {
        var toolchain = Toolchain();
        var account = HarnessKeys.Add(_home, "fake", "sk-spawn-3333");
        new HarnessSettings().WithDefault("fake", account).Save(Settings);

        var selection = await Roster(toolchain).SelectAsync("fake", Config(), null, null);
        var info = new ProcessStartInfo("fake");
        HarnessProbe.Apply(info, toolchain, selection.ProfileHome, selection.Binary,
            selection.ClaudeExecutable, selection.Environment);

        Assert.True(selection.Allowed, selection.Refusal);
        Assert.Equal("sk-spawn-3333", info.Environment["FAKE_KEY"]);
    }

    /// <summary>A sign-in account, or the tool's own home, gains no key variable because keys exist.</summary>
    [Fact]
    public async Task A_sign_in_account_gains_no_key_variable()
    {
        var toolchain = Toolchain();
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", "signed"));
        HarnessKeys.Add(_home, "fake", "sk-someone-else-4444");
        new HarnessSettings().WithDefault("fake", "signed").Save(Settings);

        var selection = await Roster(toolchain).SelectAsync("fake", Config(), null, null);
        var info = new ProcessStartInfo("fake");
        info.Environment.Remove("FAKE_KEY");
        HarnessProbe.Apply(info, toolchain, selection.ProfileHome, selection.Binary,
            selection.ClaudeExecutable, selection.Environment);

        Assert.False(info.Environment.ContainsKey("FAKE_KEY"));
    }

    /// <summary>
    /// The press asks with the key set, so the roster shows what the tool says — signed in, by key —
    /// and names the account by its handle, never by the key.
    /// </summary>
    [Fact]
    public async Task The_roster_asks_a_key_account_with_its_key_and_shows_only_the_handle()
    {
        var account = HarnessKeys.Add(_home, "fake", "sk-roster-5555-abcd");

        var report = (await Roster(Toolchain()).ReportAsync("fake", Config(), refresh: true))!;
        var row = report.Profiles.Single(p => p.Name == account);

        Assert.Equal(LoginState.In, row.Login);
        Assert.Equal("…abcd", row.Key);
        Assert.DoesNotContain("sk-roster", $"{row.Name}|{row.Home}|{row.Account}|{row.Key}");
    }

    /// <summary>An agent whose toolchain declares no key variable takes no key: nothing to hand it.</summary>
    [Fact]
    public void An_agent_with_no_key_variable_takes_no_key()
    {
        Assert.Null(Toolchain(keyVariable: null).KeyVariable);
        Assert.Equal("ANTHROPIC_API_KEY", AdapterSet.Built().Resolve("claude-code").Toolchain!.KeyVariable);
    }
}
