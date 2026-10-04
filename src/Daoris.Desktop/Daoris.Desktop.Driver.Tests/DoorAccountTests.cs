using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 <b>A door runs as its owner's accounts</b> (AGT7).
/// </summary>
/// <remarks>
/// <para><c>accountOf</c> has said since ACP2 that the protocol door runs as the agent's account — and
/// the page groups a tool's doors on it — but the driver resolved a door's accounts, defaults and keys
/// under the DOOR's own name: <c>harnesses/claude-code-acp/…</c>. So an account made for Claude Code
/// was invisible to Claude Code over the protocol door, a Codex account never reached
/// <c>codex-acp</c>, and a key account (AGT3) worked on the direct door only. Found while designing
/// the key accounts, 2026-09-23.</para>
///
/// <para>What stays the door's own: its <b>pin</b>. A door is a different package at a different
/// version (ACP2), and one pin for both would install the wrong thing under a trusted name.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class DoorAccountTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-door-tests", Guid.NewGuid().ToString("N")[..8]);

    private string Settings => Path.Combine(_home, "harnesses.json");

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private sealed class Adapter(string name, HarnessToolchain toolchain, SessionWire wire) : ISessionAdapter
    {
        public string Name => name;

        public SessionWire Wire => wire;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new();

        public HarnessToolchain? Toolchain => toolchain;
    }

    /// <summary>A binary that is there, signed in when its home holds `credentials.json` or a key is set.</summary>
    private string Script()
    {
        Directory.CreateDirectory(_home);
        var script = Path.Combine(_home, "agent.mjs");
        File.WriteAllText(script, """
            const fs = await import('node:fs');
            if (process.argv[2] === '--version') { console.log('agent 1.0'); process.exit(0); }
            const home = process.env.OWNER_HOME;
            const signed = process.env.OWNER_KEY || (home && fs.existsSync(home + '/credentials.json'));
            console.log(signed ? 'logged-in' : 'logged-out');
            """);
        return script;
    }

    /// <summary>The owner (the pipe door, with a login question and a key variable) and a door onto it.</summary>
    private HarnessRoster Roster(bool withOwner = true)
    {
        var script = Script();
        var adapters = new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["owner-acp"] = new Adapter("owner-acp", new HarnessToolchain(
                Binary: ["node", script], VersionArguments: ["--version"],
                ProfileVariable: "OWNER_HOME", AccountOf: "owner"), SessionWire.Acp),
        };
        if (withOwner)
        {
            adapters["owner"] = new Adapter("owner", new HarnessToolchain(
                Binary: ["node", script], VersionArguments: ["--version"],
                ProfileVariable: "OWNER_HOME",
                LoginCheck: new LoginQuestion(["auth"], "logged-in", "logged-out"),
                KeyVariable: "OWNER_KEY"), SessionWire.Pipe);
        }

        return new HarnessRoster(new AdapterSet(adapters), Settings);
    }

    private static DriverConfig Config() => DriverConfig.Empty with { Adapter = "owner-acp" };

    [Fact]
    public async Task A_door_spawns_in_the_owner_s_account_chosen_by_the_owner_s_default()
    {
        var work = HarnessSettings.ProfileHome(_home, "owner", "work");
        Directory.CreateDirectory(work);
        File.WriteAllText(Path.Combine(work, "credentials.json"), "{}");
        new HarnessSettings().WithDefault("owner", "work").Save(Settings);

        var selection = await Roster().SelectAsync("owner-acp", Config(), null, null);

        Assert.True(selection.Allowed, selection.Refusal);
        Assert.Equal("work", selection.Profile);
        Assert.Equal(work, selection.ProfileHome);
        // Nothing was made under the door's own name.
        Assert.False(Directory.Exists(Path.Combine(_home, "harnesses", "owner-acp")));
    }

    /// <summary>A workspace's choice of account is the owner's too — one file, one answer.</summary>
    [Fact]
    public async Task A_workspace_s_account_for_the_owner_is_the_door_s()
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "owner", "office"));
        File.WriteAllText(Path.Combine(HarnessSettings.ProfileHome(_home, "owner", "office"), "credentials.json"), "{}");
        new HarnessSettings().WithWorkspaceDefault("aurora", "owner", "office").Save(Settings);

        var selection = await Roster().SelectAsync("owner-acp", Config(), "aurora", null);

        Assert.Equal("office", selection.Profile);
    }

    /// <summary>A key account (AGT3) reaches the door with the OWNER's key variable — the door declares none.</summary>
    [Fact]
    public async Task A_key_account_reaches_the_door_through_the_owner_s_key_variable()
    {
        var account = HarnessKeys.Add(_home, "owner", "sk-door-test-7777");
        new HarnessSettings().WithDefault("owner", account).Save(Settings);

        var selection = await Roster().SelectAsync("owner-acp", Config(), null, null);

        Assert.True(selection.Allowed, selection.Refusal);
        Assert.Equal("sk-door-test-7777", selection.Environment!["OWNER_KEY"]);
    }

    /// <summary>
    /// A signed-out owner account refuses the door's spawn by the owner's own login question, naming
    /// the OWNER's login — the door has no login flow, which is what <c>accountOf</c> declared. Read by
    /// a press through the door (ROSTER1: a look reads no account), which asks the way its owner asks.
    /// </summary>
    [Fact]
    public async Task A_signed_out_owner_account_refuses_the_door_naming_the_owner_s_login()
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "owner", "stale"));
        new HarnessSettings().WithDefault("owner", "stale").Save(Settings);
        var roster = Roster();
        await roster.ReportAsync("owner-acp", Config(), refresh: true, account: "stale");

        var selection = await roster.SelectAsync("owner-acp", Config(), null, null);

        Assert.False(selection.Allowed);
        Assert.Contains("daoris agent login owner --profile stale", selection.Refusal);
    }

    /// <summary>The door's roster rows ARE the owner's accounts — the same homes, so the page shows one list.</summary>
    [Fact]
    public async Task The_door_s_roster_lists_the_owner_s_accounts_and_default()
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "owner", "work"));
        new HarnessSettings().WithDefault("owner", "work").Save(Settings);

        var report = (await Roster().ReportAsync("owner-acp", Config()))!;

        Assert.Equal([HarnessSettings.ProfileHome(_home, "owner", "work")], report.Profiles.Select(p => p.Home));
        Assert.Equal("work", report.MachineDefault);
    }

    /// <summary>
    /// An owner this build carries no adapter for (`codex` behind `codex-acp`) still owns the
    /// accounts: they are directories under its name, and the door runs in them. Nothing to ask about
    /// the login, so it is unknown and permissive (SES3), and no key is handed (none was measured).
    /// </summary>
    [Fact]
    public async Task A_door_whose_owner_has_no_adapter_still_runs_in_the_owner_s_accounts()
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "owner", "work"));
        new HarnessSettings().WithDefault("owner", "work").Save(Settings);

        var selection = await Roster(withOwner: false).SelectAsync("owner-acp", Config(), null, null);

        Assert.True(selection.Allowed, selection.Refusal);
        Assert.Equal(HarnessSettings.ProfileHome(_home, "owner", "work"), selection.ProfileHome);
        Assert.Null(selection.Environment);
    }

    /// <summary>
    /// 🔴 An account its provider refused is not spent again (AGT3b): each further session would sit
    /// through the tool's own minutes of retries to fail the same way. The refusal holds the start,
    /// naming the fix, and it is the ACCOUNT's — on either door onto it — until a person looks again.
    /// </summary>
    [Fact]
    public async Task A_refused_account_holds_further_starts_until_a_person_looks_again()
    {
        var work = HarnessSettings.ProfileHome(_home, "owner", "work");
        Directory.CreateDirectory(work);
        File.WriteAllText(Path.Combine(work, "credentials.json"), "{}");
        new HarnessSettings().WithDefault("owner", "work").Save(Settings);
        var roster = Roster();

        roster.Refuse("owner", "work", "the provider refused `work` (401).");

        var direct = await roster.SelectAsync("owner", Config(), null, null);
        var door = await roster.SelectAsync("owner-acp", Config(), null, null);
        Assert.False(direct.Allowed);
        Assert.Contains("refused `work`", direct.Refusal);
        Assert.False(door.Allowed);

        await roster.RosterAsync(Config(), refresh: true);

        Assert.True((await roster.SelectAsync("owner", Config(), null, null)).Allowed);
    }

    /// <summary>The door's pin stays the door's: a different package, a different version (ACP2).</summary>
    [Fact]
    public async Task The_door_s_pin_is_still_its_own()
    {
        new HarnessSettings().WithVersion("owner", "1.0.0").Save(Settings);

        var selection = await Roster().SelectAsync("owner-acp", Config(), null, null);

        // The owner's pin names nothing for the door: it runs what its own resolution says (PATH here).
        Assert.True(selection.Allowed, selection.Refusal);
        Assert.Null(selection.Binary);
    }
}
